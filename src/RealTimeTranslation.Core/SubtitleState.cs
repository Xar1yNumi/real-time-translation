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
                partial.Text,
                string.Empty,
                string.Empty,
                true),
            FinalUtteranceUpdate final => new(
                final.UtteranceId,
                final.Sequence,
                final.OriginalText,
                final.TranslatedText,
                final.DetectedLanguage,
                false),
            _ => this
        };
    }

}
