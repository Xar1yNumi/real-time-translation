using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Translation;

public sealed class NoAzureTranslationProvider : ITranslationProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Func<CancellationToken, Task<IWhisperTranscriber>> createTranscriber;
    private readonly IMyMemoryTranslator translator;
    private IWhisperTranscriber? transcriber;
    private bool disposed;

    public NoAzureTranslationProvider(
        IWhisperModelStore modelStore, IMyMemoryTranslator translator)
        : this(async cancellationToken => await WhisperTranscriber.CreateAsync(
            modelStore ?? throw new ArgumentNullException(nameof(modelStore)),
            cancellationToken).ConfigureAwait(false), translator)
    {
    }

    internal NoAzureTranslationProvider(
        Func<CancellationToken, Task<IWhisperTranscriber>> createTranscriber,
        IMyMemoryTranslator translator)
    {
        this.createTranscriber = createTranscriber ??
            throw new ArgumentNullException(nameof(createTranscriber));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (transcriber is not null)
            {
                return;
            }

            var created = await createTranscriber(cancellationToken).ConfigureAwait(false);
            try
            {
                // Prime the native inference engine before the audio source starts.
                // Vulkan's first inference compiles shaders; later utterances are fast.
                await created.TranscribeAsync(new byte[32_000], cancellationToken)
                    .ConfigureAwait(false);
                transcriber = created;
            }
            catch
            {
                await created.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<ITranslationSession> CreateSessionAsync(
        TranslationSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        await PrepareAsync(cancellationToken).ConfigureAwait(false);
        return new NoAzureTranslationSession(transcriber!, translator,
            options.TargetLanguage, Task.Delay, ownsTranscriber: false);
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (transcriber is not null)
            {
                await transcriber.DisposeAsync().ConfigureAwait(false);
                transcriber = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
