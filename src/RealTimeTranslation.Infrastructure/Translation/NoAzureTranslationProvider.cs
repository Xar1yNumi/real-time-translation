using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Translation;

public sealed class NoAzureTranslationProvider : ITranslationProvider
{
    private readonly IWhisperModelStore modelStore;
    private readonly IMyMemoryTranslator translator;

    public NoAzureTranslationProvider(
        IWhisperModelStore modelStore, IMyMemoryTranslator translator)
    {
        this.modelStore = modelStore ?? throw new ArgumentNullException(nameof(modelStore));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public async ValueTask<ITranslationSession> CreateSessionAsync(
        TranslationSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var transcriber = await WhisperTranscriber.CreateAsync(
            modelStore, cancellationToken).ConfigureAwait(false);
        return new NoAzureTranslationSession(transcriber, translator, options.TargetLanguage);
    }
}
