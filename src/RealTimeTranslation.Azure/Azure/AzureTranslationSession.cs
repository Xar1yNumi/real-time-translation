using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Azure;

public sealed class AzureTranslationSession : ITranslationSession
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object disposeSync = new();
    private readonly ConcurrentDictionary<string, long> sequences = new(StringComparer.Ordinal);
    private readonly IAzureTranslationTransport transport;
    private readonly string targetLanguage;
    private Task? disposeTask;
    private long nextSequence;
    private int lifecycleState;

    public AzureTranslationSession(TranslationSessionOptions options)
        : this(new AzureTranslationTransport(options))
    {
    }

    internal AzureTranslationSession(IAzureTranslationTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        targetLanguage = transport.TargetLanguage;
        transport.Recognizing += OnRecognizing;
        transport.Recognized += OnRecognized;
        transport.Canceled += OnCanceled;
    }

    public event EventHandler<TranslationUpdateEventArgs>? UpdateReceived;

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref lifecycleState) == 3,
            this);
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (lifecycleState == 1)
            {
                return;
            }

            if (lifecycleState != 0)
            {
                throw new InvalidOperationException("A stopped translation session cannot be restarted.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await transport.StartAsync().ConfigureAwait(false);
            lifecycleState = 1;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public ValueTask WriteAudioAsync(
        ReadOnlyMemory<byte> audio,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref lifecycleState) != 1)
        {
            throw new InvalidOperationException("Audio can only be written while the session is started.");
        }

        if (audio.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        transport.Write(audio.ToArray());
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref lifecycleState) is 2 or 3)
        {
            return;
        }

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (lifecycleState is 2 or 3)
            {
                return;
            }

            if (lifecycleState == 1)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await transport.StopAsync().ConfigureAwait(false);
                }
                finally
                {
                    transport.CloseInput();
                    lifecycleState = 2;
                }

                return;
            }

            transport.CloseInput();
            lifecycleState = 2;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposeSync)
        {
            if (Volatile.Read(ref lifecycleState) == 3)
            {
                return ValueTask.CompletedTask;
            }

            disposeTask ??= DisposeCoreAsync();
            return new ValueTask(disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        List<Exception>? failures = null;
        await lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (lifecycleState == 3)
            {
                return;
            }

            if (lifecycleState == 1)
            {
                try
                {
                    await transport.StopAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures ??= [];
                    failures.Add(exception);
                }
            }

            TryCleanup(transport.CloseInput, ref failures);
            TryCleanup(Unsubscribe, ref failures);
            TryCleanup(transport.Dispose, ref failures);
            lifecycleState = 3;
            UpdateReceived = null;
        }
        finally
        {
            lifecycleGate.Release();
        }

        ThrowCleanupFailures(failures);
    }

    private void OnRecognizing(object? sender, TranslationRecognitionEventArgs args)
    {
        var resultId = args.Result.ResultId;
        if (string.IsNullOrWhiteSpace(resultId))
        {
            return;
        }

        Raise(new PartialTranscriptUpdate(
            resultId,
            SequenceFor(resultId),
            args.Result.Text));
    }

    private void OnRecognized(object? sender, TranslationRecognitionEventArgs args)
    {
        if (args.Result.Reason != ResultReason.TranslatedSpeech)
        {
            return;
        }

        var resultId = args.Result.ResultId;
        if (string.IsNullOrWhiteSpace(resultId))
        {
            return;
        }

        var sequence = SequenceFor(resultId);
        var detectedLanguage = args.Result.Properties.GetProperty(
            PropertyId.SpeechServiceConnection_AutoDetectSourceLanguageResult);
        args.Result.Translations.TryGetValue(targetLanguage, out var translatedText);
        try
        {
            Raise(new FinalUtteranceUpdate(
                resultId,
                sequence,
                args.Result.Text,
                translatedText ?? string.Empty,
                detectedLanguage ?? string.Empty));
        }
        finally
        {
            sequences.TryRemove(resultId, out _);
        }
    }

    private void OnCanceled(object? sender, TranslationRecognitionCanceledEventArgs args)
    {
        if (args.Reason == CancellationReason.EndOfStream)
        {
            return;
        }

        Raise(new TranslationCanceledUpdate(
            "session",
            Interlocked.Increment(ref nextSequence),
            AzureErrorMapper.Map(args.ErrorCode)));
    }

    private long SequenceFor(string resultId) =>
        sequences.GetOrAdd(resultId, _ => Interlocked.Increment(ref nextSequence));

    private void Raise(TranslationUpdate update) =>
        UpdateReceived?.Invoke(this, new TranslationUpdateEventArgs(update));

    private void Unsubscribe()
    {
        transport.Recognizing -= OnRecognizing;
        transport.Recognized -= OnRecognized;
        transport.Canceled -= OnCanceled;
    }

    private static void TryCleanup(Action cleanup, ref List<Exception>? failures)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failures ??= [];
            failures.Add(exception);
        }
    }

    private static void ThrowCleanupFailures(List<Exception>? failures)
    {
        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("Azure translation session failed to dispose.", failures);
    }
}
