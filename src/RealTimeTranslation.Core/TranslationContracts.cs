namespace RealTimeTranslation.Core;

public abstract record TranslationUpdate
{
    protected TranslationUpdate(string utteranceId, long sequence)
    {
        if (string.IsNullOrWhiteSpace(utteranceId))
        {
            throw new ArgumentException("UtteranceId cannot be empty.", nameof(utteranceId));
        }

        UtteranceId = utteranceId;
        Sequence = sequence;
    }

    public string UtteranceId { get; }

    public long Sequence { get; }
}

public sealed record PartialTranscriptUpdate : TranslationUpdate
{
    public PartialTranscriptUpdate(string utteranceId, long sequence, string text)
        : base(utteranceId, sequence)
    {
        Text = text ?? string.Empty;
    }

    public string Text { get; }
}

public sealed record FinalUtteranceUpdate : TranslationUpdate
{
    public FinalUtteranceUpdate(
        string utteranceId,
        long sequence,
        string originalText,
        string translatedText,
        string detectedLanguage)
        : base(utteranceId, sequence)
    {
        OriginalText = originalText ?? string.Empty;
        TranslatedText = translatedText ?? string.Empty;
        DetectedLanguage = detectedLanguage ?? string.Empty;
    }

    public string OriginalText { get; }

    public string TranslatedText { get; }

    public string DetectedLanguage { get; }
}

public sealed record TranslationCanceledUpdate : TranslationUpdate
{
    public TranslationCanceledUpdate(string utteranceId, long sequence, AppErrorCode errorCode)
        : base(utteranceId, sequence)
    {
        ErrorCode = errorCode;
    }

    public AppErrorCode ErrorCode { get; }
}

public sealed class TranslationUpdateEventArgs(TranslationUpdate update) : EventArgs
{
    public TranslationUpdate Update { get; } = update;
}

public sealed record TranslationSessionOptions(
    string Endpoint,
    string ApiKey,
    TargetLanguage TargetLanguage);

public interface ITranslationProvider
{
    ValueTask<ITranslationSession> CreateSessionAsync(
        TranslationSessionOptions options,
        CancellationToken cancellationToken);
}

public interface ITranslationSession : IAsyncDisposable
{
    event EventHandler<TranslationUpdateEventArgs>? UpdateReceived;

    ValueTask StartAsync(CancellationToken cancellationToken);

    ValueTask WriteAudioAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
