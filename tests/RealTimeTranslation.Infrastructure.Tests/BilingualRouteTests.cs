using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalTranslation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class BilingualRouteTests
{
    [Theory]
    [InlineData("zh", TargetLanguage.English, "zh", "en")]
    [InlineData("en", TargetLanguage.SimplifiedChinese, "en", "zh")]
    [InlineData(" ZH ", TargetLanguage.English, "zh", "en")]
    public void OppositeLanguageRequiresTranslation(
        string detected, TargetLanguage target, string sourceCode, string targetCode)
    {
        var route = BilingualRoute.Resolve(detected, target);

        Assert.Equal(BilingualRouteAction.Translate, route.Action);
        Assert.Equal(sourceCode, route.SourceCode);
        Assert.Equal(targetCode, route.TargetCode);
    }

    [Theory]
    [InlineData("zh", TargetLanguage.SimplifiedChinese)]
    [InlineData("en", TargetLanguage.English)]
    public void SameLanguageKeepsOriginal(string detected, TargetLanguage target)
    {
        var route = BilingualRoute.Resolve(detected, target);

        Assert.Equal(BilingualRouteAction.ShowOriginal, route.Action);
    }

    [Theory]
    [InlineData("ru", TargetLanguage.English)]
    [InlineData("", TargetLanguage.English)]
    [InlineData(null, TargetLanguage.English)]
    [InlineData("zh", TargetLanguage.Russian)]
    public void OtherLanguagesCannotBeRouted(string? detected, TargetLanguage target)
    {
        var route = BilingualRoute.Resolve(detected, target);

        Assert.Equal(BilingualRouteAction.Unsupported, route.Action);
        Assert.Null(route.SourceCode);
        Assert.Null(route.TargetCode);
    }
}
