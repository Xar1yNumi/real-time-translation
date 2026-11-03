using Whisper.net;
using RealTimeTranslation.Infrastructure.Text;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

public enum TranscriptionStatus
{
    Recognized,
    Empty,
    UnsupportedLanguage
}

public sealed record TranscriptionResult(
    TranscriptionStatus Status, string? Text, string? LanguageCode);

public interface IWhisperTranscriber : IAsyncDisposable
{
    Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<byte> pcm16Mono, CancellationToken cancellationToken);
}

internal sealed record InferenceSegment(string Text, string LanguageCode);

internal interface IWhisperInference : IAsyncDisposable
{
    Task<IReadOnlyList<InferenceSegment>> InferAsync(
        string modelPath, ReadOnlyMemory<byte> wav, CancellationToken cancellationToken);
}

public sealed class WhisperTranscriber : IWhisperTranscriber
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IWhisperInference inference;
    private readonly string modelPath;
    private bool disposed;

    private WhisperTranscriber(string modelPath, IWhisperInference inference)
    {
        this.modelPath = modelPath;
        this.inference = inference;
    }

    public static Task<WhisperTranscriber> CreateAsync(
        IWhisperModelStore modelStore, CancellationToken cancellationToken) =>
        CreateAsync(modelStore, new NativeWhisperInference(), cancellationToken);

    internal static async Task<WhisperTranscriber> CreateAsync(
        IWhisperModelStore modelStore,
        IWhisperInference inference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modelStore);
        ArgumentNullException.ThrowIfNull(inference);
        try
        {
            var path = await modelStore.EnsureAvailableAsync(
                null, cancellationToken).ConfigureAwait(false);
            return new WhisperTranscriber(path, inference);
        }
        catch
        {
            await inference.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<byte> pcm16Mono, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (pcm16Mono.IsEmpty)
        {
            return new TranscriptionResult(TranscriptionStatus.Empty, null, null);
        }

        if ((pcm16Mono.Length & 1) != 0)
        {
            throw new ArgumentException("PCM16 data must contain complete samples.", nameof(pcm16Mono));
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var wav = PcmWaveWriter.Wrap16kMono(pcm16Mono.Span);
            var segments = await inference.InferAsync(
                modelPath, wav, cancellationToken).ConfigureAwait(false);
            var nonempty = segments.Where(segment => !string.IsNullOrWhiteSpace(segment.Text)).ToArray();
            if (nonempty.Length == 0)
            {
                return new TranscriptionResult(TranscriptionStatus.Empty, null, null);
            }

            var language = nonempty[0].LanguageCode?.Trim().ToLowerInvariant();
            if (language is not ("zh" or "en" or "ru") ||
                nonempty.Any(segment => !string.Equals(
                    segment.LanguageCode?.Trim(), language, StringComparison.OrdinalIgnoreCase)))
            {
                return new TranscriptionResult(
                    TranscriptionStatus.UnsupportedLanguage, null, language);
            }

            var text = string.Join(" ", nonempty.Select(segment => segment.Text.Trim()));
            if (language == "zh")
            {
                text = ChineseText.ToSimplified(text);
            }
            return new TranscriptionResult(TranscriptionStatus.Recognized, text, language);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            await inference.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

    }
}

internal sealed class NativeWhisperInference : IWhisperInference
{
    private WhisperFactory? factory;
    private WhisperProcessor? processor;

    public async Task<IReadOnlyList<InferenceSegment>> InferAsync(
        string modelPath, ReadOnlyMemory<byte> wav, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        factory ??= WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = true });
        processor ??= factory.CreateBuilder()
            .WithLanguageDetection()
            .WithNoContext()
            .Build();

        using var stream = new MemoryStream(wav.ToArray(), writable: false);
        var segments = new List<InferenceSegment>();
        await foreach (var segment in processor.ProcessAsync(stream, cancellationToken))
        {
            segments.Add(new InferenceSegment(segment.Text, segment.Language));
        }

        return segments;
    }

    public async ValueTask DisposeAsync()
    {
        if (processor is not null)
        {
            await processor.DisposeAsync().ConfigureAwait(false);
        }

        factory?.Dispose();
    }
}
