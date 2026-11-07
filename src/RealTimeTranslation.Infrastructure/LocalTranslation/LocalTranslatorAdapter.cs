using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.LocalTranslation;

internal sealed class LocalTranslatorAdapter(ILocalTextTranslator local) : IMyMemoryTranslator
{
    public Task<string> TranslateAsync(
        string text, string source, string target, CancellationToken cancellationToken)
    {
        if (source is not ("zh" or "en") || target is not ("zh" or "en") || source == target)
        {
            throw new AppFailureException(AppError.For(AppErrorCode.InvalidTranslationRequest));
        }
        return local.TranslateAsync(text, source, cancellationToken);
    }
}
