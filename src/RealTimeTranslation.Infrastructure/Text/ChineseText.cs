using OpenccNetLib;

namespace RealTimeTranslation.Infrastructure.Text;

internal static class ChineseText
{
    private static readonly Opencc Converter = new(OpenccConfig.Tw2Sp, isFrozen: true);

    public static string ToSimplified(string text) => Converter.Convert(text);
}
