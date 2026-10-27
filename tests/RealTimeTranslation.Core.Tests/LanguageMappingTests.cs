using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests;

public sealed class LanguageMappingTests
{
    [Theory]
    [InlineData(TargetLanguage.SimplifiedChinese, "zh-Hans")]
    [InlineData(TargetLanguage.English, "en")]
    [InlineData(TargetLanguage.Russian, "ru")]
    public void TargetCodeIsFixed(TargetLanguage language, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.GetTargetCode(language));
    }

    [Fact]
    public void CandidateLocalesAreExactlyTheMvpLanguages()
    {
        Assert.Equal(["zh-CN", "en-US", "ru-RU"], LanguageCatalog.SourceLocales);
    }
}
