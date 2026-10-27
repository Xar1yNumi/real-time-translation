namespace RealTimeTranslation.Core;

public sealed record SubtitleState(
    string UtteranceId,
    long Sequence,
    string OriginalText,
    string TranslatedText,
    string DetectedLanguage,
    bool IsOriginalPartial)
{
    public static SubtitleState Empty { get; } = new(
        string.Empty,
        -1,
        string.Empty,
        string.Empty,
        string.Empty,
        false);

    public SubtitleState Apply(TranslationUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Sequence < Sequence)
        {
            return this;
        }

        return update switch
        {
            PartialTranscriptUpdate partial => new(
                partial.UtteranceId,
                partial.Sequence,
                LastTwoLines(partial.Text),
                string.Empty,
                string.Empty,
                true),
            FinalUtteranceUpdate final => new(
                final.UtteranceId,
                final.Sequence,
                LastTwoLines(final.OriginalText),
                LastTwoLines(final.TranslatedText),
                final.DetectedLanguage,
                false),
            _ => this
        };
    }

    private static string LastTwoLines(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(Environment.NewLine, lines.TakeLast(2));
    }
}
