using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Azure;

public sealed class AzureTranslationProvider : ITranslationProvider
{
    public ValueTask<ITranslationSession> CreateSessionAsync(
        TranslationSessionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ITranslationSession>(new AzureTranslationSession(options));
    }
}
