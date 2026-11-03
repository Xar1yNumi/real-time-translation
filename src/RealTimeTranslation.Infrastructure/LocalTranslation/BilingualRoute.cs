using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.LocalTranslation;

internal enum BilingualRouteAction
{
    Translate,
    ShowOriginal,
    Unsupported
}

internal readonly record struct BilingualRoute(
    BilingualRouteAction Action,
    string? SourceCode = null,
    string? TargetCode = null)
{
    public static BilingualRoute Resolve(string? detectedSource, TargetLanguage target)
    {
        var sourceCode = detectedSource?.Trim().ToLowerInvariant();
        var targetCode = target switch
        {
            TargetLanguage.SimplifiedChinese => "zh",
            TargetLanguage.English => "en",
            _ => null
        };

        if (sourceCode is not ("zh" or "en") || targetCode is null)
        {
            return new(BilingualRouteAction.Unsupported);
        }

        return sourceCode == targetCode
            ? new(BilingualRouteAction.ShowOriginal)
            : new(BilingualRouteAction.Translate, sourceCode, targetCode);
    }
}
