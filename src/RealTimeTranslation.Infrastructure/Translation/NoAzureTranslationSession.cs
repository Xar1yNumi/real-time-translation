using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.LocalTranslation;

namespace RealTimeTranslation.Infrastructure.Translation;

public sealed class NoAzureTranslationSession : ITranslationSession
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];
    private readonly object sync = new();
    private readonly Queue<QueuedUtterance> pending = new();
    private readonly SemaphoreSlim available = new(0);
    private readonly SemaphoreSlim previewAvailable = new(0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly PcmUtteranceSegmenter segmenter;
    private readonly IWhisperTranscriber transcriber;
    private readonly bool ownsTranscriber;
    private readonly IMyMemoryTranslator translator;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly string targetCode;
    private readonly TargetLanguage targetLanguage;
    private readonly bool localOnly;
    private Task? worker;
    private Task? previewWorker;
    private Task? disposeTask;
    private QueuedPreview? pendingPreview;
    private bool previewSignalQueued;
    private CancellationTokenSource? previewInFlightCancellation;
    private long finalQueuedSequence;
    private long activeUtteranceNumber;
    private string? activeUtteranceId;
    private long activeSequence;
    private long nextSequence;
    private int state;

    public NoAzureTranslationSession(
        IWhisperTranscriber transcriber,
        IMyMemoryTranslator translator,
        TargetLanguage targetLanguage)
        : this(transcriber, translator, targetLanguage, Task.Delay, true)
    {
    }

    public NoAzureTranslationSession(
        IWhisperTranscriber transcriber,
        ILocalTextTranslator translator,
        TargetLanguage targetLanguage)
        : this(transcriber, new LocalTranslatorAdapter(translator), targetLanguage,
            Task.Delay, true, true)
    {
    }

    internal NoAzureTranslationSession(
        IWhisperTranscriber transcriber,
        IMyMemoryTranslator translator,
        TargetLanguage targetLanguage,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
        : this(transcriber, translator, targetLanguage, delayAsync, true)
    {
    }

    internal NoAzureTranslationSession(
        IWhisperTranscriber transcriber,
        IMyMemoryTranslator translator,
        TargetLanguage targetLanguage,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        bool ownsTranscriber,
        bool localOnly = false,
        ISpeechActivityDetector? detector = null)
    {
        this.transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        this.ownsTranscriber = ownsTranscriber;
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        this.targetLanguage = targetLanguage;
        this.localOnly = localOnly;
        segmenter = new PcmUtteranceSegmenter(detector);
        targetCode = targetLanguage switch
        {
            TargetLanguage.SimplifiedChinese => "zh",
            TargetLanguage.English => "en",
            TargetLanguage.Russian => "ru",
            _ => throw new ArgumentOutOfRangeException(nameof(targetLanguage))
        };
    }

    public event EventHandler<TranslationUpdateEventArgs>? UpdateReceived;

    public int PendingUtterances
    {
        get { lock (sync) { return pending.Count; } }
    }

    internal int BufferedPcmBytes
    {
        get { lock (sync) { return segmenter.BufferedBytes + pending.Sum(item => item.Pcm.Length); } }
    }

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (state == 1)
            {
                return ValueTask.CompletedTask;
            }

            if (state != 0)
            {
                throw new InvalidOperationException("A stopped session cannot be restarted.");
            }

            state = 1;
            worker = Task.Run(() => ProcessPendingAsync(shutdown.Token), CancellationToken.None);
            previewWorker = Task.Run(() => ProcessPreviewsAsync(shutdown.Token), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteAudioAsync(
        ReadOnlyMemory<byte> audio, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var warnings = new List<TranslationWarningUpdate>();
        var previewsToCancel = new List<CancellationTokenSource>();
        lock (sync)
        {
            if (state != 1)
            {
                throw new InvalidOperationException("Audio can only be written while the session is started.");
            }

            foreach (var progress in segmenter.PushProgress(audio.Span))
            {
                if (progress.UtteranceNumber != activeUtteranceNumber)
                {
                    activeUtteranceNumber = progress.UtteranceNumber;
                    activeUtteranceId = Guid.NewGuid().ToString("N");
                    activeSequence = Interlocked.Increment(ref nextSequence);
                }

                if (!progress.IsFinal)
                {
                    if (!previewSignalQueued)
                    {
                        previewSignalQueued = true;
                        previewAvailable.Release();
                    }

                    pendingPreview = new QueuedPreview(
                        activeUtteranceId!, activeSequence, progress.Pcm);

                    continue;
                }

                finalQueuedSequence = activeSequence;
                if (pendingPreview?.Sequence == activeSequence)
                {
                    pendingPreview = null;
                }
                if (previewInFlightCancellation is not null)
                {
                    previewsToCancel.Add(previewInFlightCancellation);
                }

                if (pending.Count == 2)
                {
                    var dropped = pending.Dequeue();
                    warnings.Add(new TranslationWarningUpdate(
                        dropped.Id, dropped.Sequence, AppErrorCode.AudioBacklog));
                }
                else
                {
                    available.Release();
                }

                pending.Enqueue(new QueuedUtterance(
                    activeUtteranceId!, activeSequence, progress.Pcm));
                activeUtteranceNumber = 0;
                activeUtteranceId = null;
                activeSequence = 0;
            }
        }

        foreach (var preview in previewsToCancel.Distinct())
        {
            try { preview.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        foreach (var warning in warnings)
        {
            Raise(warning);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        Task? running;
        Task? runningPreview;
        lock (sync)
        {
            if (state == 3)
            {
                return;
            }

            if (state != 2)
            {
                state = 2;
                shutdown.Cancel();
                pending.Clear();
                pendingPreview = null;
            }

            running = worker;
            runningPreview = previewWorker;
        }

        if (running is not null)
        {
            await running.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        if (runningPreview is not null)
        {
            await runningPreview.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            disposeTask ??= DisposeCoreAsync();
            return new ValueTask(disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (ownsTranscriber)
        {
            await transcriber.DisposeAsync().ConfigureAwait(false);
        }
        lock (sync)
        {
            state = 3;
            segmenter.Dispose();
            UpdateReceived = null;
        }

        available.Dispose();
        previewAvailable.Dispose();
        shutdown.Dispose();
    }

    private async Task ProcessPreviewsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await previewAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                QueuedPreview? preview;
                CancellationTokenSource? previewCancellation = null;
                lock (sync)
                {
                    preview = pendingPreview;
                    pendingPreview = null;
                    previewSignalQueued = false;
                    if (preview is not null)
                    {
                        previewCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        previewInFlightCancellation = previewCancellation;
                    }
                }

                if (preview is null || previewCancellation is null)
                {
                    continue;
                }

                try
                {
                    var result = await transcriber.TranscribeAsync(
                        preview.Pcm, previewCancellation.Token).ConfigureAwait(false);
                    if (result.Status == TranscriptionStatus.Recognized &&
                        !string.IsNullOrWhiteSpace(result.Text))
                    {
                        lock (sync)
                        {
                            if (state == 1 &&
                                preview.Sequence > finalQueuedSequence &&
                                !shutdown.IsCancellationRequested)
                            {
                                UpdateReceived?.Invoke(this, new TranslationUpdateEventArgs(
                                    new PartialTranscriptUpdate(
                                        preview.Id, preview.Sequence, result.Text)));
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (previewCancellation.IsCancellationRequested)
                {
                }
                finally
                {
                    lock (sync)
                    {
                        if (ReferenceEquals(previewInFlightCancellation, previewCancellation))
                        {
                            previewInFlightCancellation = null;
                        }
                    }
                    previewCancellation.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            RaiseFailure(AppErrorCode.Unknown);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await available.WaitAsync(cancellationToken).ConfigureAwait(false);
                QueuedUtterance utterance;
                lock (sync)
                {
                    utterance = pending.Dequeue();
                }

                var result = await transcriber.TranscribeAsync(
                    utterance.Pcm, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.Status != TranscriptionStatus.Recognized ||
                    string.IsNullOrWhiteSpace(result.Text) ||
                    string.IsNullOrWhiteSpace(result.LanguageCode))
                {
                    Raise(new TranslationWarningUpdate(utterance.Id, utterance.Sequence,
                        AppErrorCode.UnsupportedLanguage));
                    continue;
                }

                if (localOnly && BilingualRoute.Resolve(
                    result.LanguageCode, targetLanguage).Action == BilingualRouteAction.Unsupported)
                {
                    Raise(new TranslationWarningUpdate(utterance.Id, utterance.Sequence,
                        AppErrorCode.UnsupportedLanguage));
                    continue;
                }

                Raise(new PartialTranscriptUpdate(
                    utterance.Id, utterance.Sequence, result.Text));
                var translated = await TranslateWithRetriesAsync(
                    utterance, result, cancellationToken).ConfigureAwait(false);
                if (translated is null)
                {
                    continue;
                }
                cancellationToken.ThrowIfCancellationRequested();
                Raise(new FinalUtteranceUpdate(
                    utterance.Id, utterance.Sequence, result.Text,
                    translated, result.LanguageCode));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (AppFailureException failure)
        {
            RaiseFailure(failure.Error.Code);
        }
        catch (Exception)
        {
            RaiseFailure(AppErrorCode.Unknown);
        }
    }

    private async Task<string?> TranslateWithRetriesAsync(
        QueuedUtterance utterance,
        TranscriptionResult result,
        CancellationToken cancellationToken)
    {
        if (result.LanguageCode == targetCode)
        {
            return result.Text;
        }

        if (localOnly)
        {
            return await translator.TranslateAsync(result.Text!, result.LanguageCode!,
                targetCode, cancellationToken).ConfigureAwait(false);
        }

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                return await translator.TranslateAsync(
                    result.Text!, result.LanguageCode!, targetCode,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (AppFailureException failure) when (failure.Error.IsTransient)
            {
                if (attempt == 0)
                {
                    Raise(new TranslationWarningUpdate(
                        utterance.Id, utterance.Sequence, failure.Error.Code));
                }

                if (attempt == RetryDelays.Length)
                {
                    return null;
                }

                await delayAsync(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private void RaiseFailure(AppErrorCode code)
    {
        var sequence = Interlocked.Increment(ref nextSequence);
        Raise(new TranslationCanceledUpdate("session", sequence, code));
    }

    private void Raise(TranslationUpdate update)
    {
        if (!shutdown.IsCancellationRequested)
        {
            UpdateReceived?.Invoke(this, new TranslationUpdateEventArgs(update));
        }
    }

    private sealed record QueuedUtterance(string Id, long Sequence, byte[] Pcm);
    private sealed record QueuedPreview(string Id, long Sequence, byte[] Pcm);
}
