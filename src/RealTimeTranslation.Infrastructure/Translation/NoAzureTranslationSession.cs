using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Translation;

public sealed class NoAzureTranslationSession : ITranslationSession
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];
    private readonly object sync = new();
    private readonly Queue<QueuedUtterance> pending = new();
    private readonly SemaphoreSlim available = new(0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly PcmUtteranceSegmenter segmenter = new();
    private readonly IWhisperTranscriber transcriber;
    private readonly IMyMemoryTranslator translator;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly string targetCode;
    private Task? worker;
    private Task? disposeTask;
    private long nextSequence;
    private int state;

    public NoAzureTranslationSession(
        IWhisperTranscriber transcriber,
        IMyMemoryTranslator translator,
        TargetLanguage targetLanguage)
        : this(transcriber, translator, targetLanguage, Task.Delay)
    {
    }

    internal NoAzureTranslationSession(
        IWhisperTranscriber transcriber,
        IMyMemoryTranslator translator,
        TargetLanguage targetLanguage,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
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
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteAudioAsync(
        ReadOnlyMemory<byte> audio, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var warnings = new List<TranslationWarningUpdate>();
        lock (sync)
        {
            if (state != 1)
            {
                throw new InvalidOperationException("Audio can only be written while the session is started.");
            }

            foreach (var pcm in segmenter.Push(audio.Span))
            {
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

                var sequence = Interlocked.Increment(ref nextSequence);
                pending.Enqueue(new QueuedUtterance(
                    Guid.NewGuid().ToString("N"), sequence, pcm));
            }
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
            }

            running = worker;
        }

        if (running is not null)
        {
            await running.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        await transcriber.DisposeAsync().ConfigureAwait(false);
        lock (sync)
        {
            state = 3;
            segmenter.Dispose();
            UpdateReceived = null;
        }

        available.Dispose();
        shutdown.Dispose();
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
}
