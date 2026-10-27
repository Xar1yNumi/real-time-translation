namespace RealTimeTranslation.Core;

public enum TargetLanguage
{
    SimplifiedChinese,
    English,
    Russian
}

public enum AudioSourceKind
{
    SystemAudio,
    Microphone
}

public static class LanguageCatalog
{
    public static IReadOnlyList<string> SourceLocales { get; } = ["zh-CN", "en-US", "ru-RU"];

    public static string GetTargetCode(TargetLanguage language) => language switch
    {
        TargetLanguage.SimplifiedChinese => "zh-Hans",
        TargetLanguage.English => "en",
        TargetLanguage.Russian => "ru",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
}
