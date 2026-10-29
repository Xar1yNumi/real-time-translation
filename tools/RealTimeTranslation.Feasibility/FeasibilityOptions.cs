using System.Globalization;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Feasibility;

internal enum FeasibilitySource
{
    Wave,
    System,
    Microphone
}

internal sealed record FeasibilityOptions(
    FeasibilitySource Source,
    AudioSourceKind? AudioSource,
    string? WavePath,
    TargetLanguage TargetLanguage,
    TimeSpan Duration,
    bool ShowText)
{
    public static bool TryParse(string[] arguments, out FeasibilityOptions options)
    {
        options = null!;
        string? source = null;
        string? wavePath = null;
        string? targetCode = null;
        string? durationCode = null;
        var showText = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (!seen.Add(argument))
            {
                return false;
            }

            if (argument.Equals("--show-text", StringComparison.OrdinalIgnoreCase))
            {
                showText = true;
                continue;
            }

            if (index + 1 >= arguments.Length)
            {
                return false;
            }

            switch (argument.ToLowerInvariant())
            {
                case "--wav":
                    wavePath = arguments[++index];
                    break;
                case "--source":
                    source = arguments[++index];
                    break;
                case "--target":
                    targetCode = arguments[++index];
                    break;
                case "--duration":
                    durationCode = arguments[++index];
                    break;
                default:
                    return false;
            }
        }

        if (!TryTarget(targetCode, out var targetLanguage))
        {
            return false;
        }

        if (source is null && !string.IsNullOrWhiteSpace(wavePath) && durationCode is null)
        {
            options = new(FeasibilitySource.Wave, null, wavePath,
                targetLanguage, TimeSpan.Zero, showText);
            return true;
        }

        if (wavePath is not null || source is null
            || !TimeSpan.TryParseExact(durationCode, "c", CultureInfo.InvariantCulture,
                out var duration)
            || duration <= TimeSpan.Zero)
        {
            return false;
        }

        var sourceKind = source.ToLowerInvariant() switch
        {
            "system" => AudioSourceKind.SystemAudio,
            "microphone" => AudioSourceKind.Microphone,
            _ => (AudioSourceKind?)null
        };
        if (sourceKind is null)
        {
            return false;
        }

        options = new(sourceKind == AudioSourceKind.SystemAudio
                ? FeasibilitySource.System : FeasibilitySource.Microphone,
            sourceKind, null, targetLanguage, duration, showText);
        return true;
    }

    private static bool TryTarget(string? code, out TargetLanguage target)
    {
        target = code?.ToLowerInvariant() switch
        {
            "zh-hans" => TargetLanguage.SimplifiedChinese,
            "en" => TargetLanguage.English,
            "ru" => TargetLanguage.Russian,
            _ => default
        };
        return code is not null
            && (code.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
                || code.Equals("en", StringComparison.OrdinalIgnoreCase)
                || code.Equals("ru", StringComparison.OrdinalIgnoreCase));
    }
}
