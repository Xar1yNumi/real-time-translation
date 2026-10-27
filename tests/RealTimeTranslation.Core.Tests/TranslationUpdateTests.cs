using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests;

public sealed class TranslationUpdateTests
{
    [Fact]
    public void BlankUtteranceIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new PartialTranscriptUpdate(" ", 1, "Hello"));
        Assert.Throws<ArgumentException>(() => new FinalUtteranceUpdate("", 1, "Hello", "你好", "en-US"));
        Assert.Throws<ArgumentException>(() => new TranslationCanceledUpdate("", 1, "Canceled"));
    }

    [Fact]
    public void FinalUpdateKeepsAssociatedSentenceFields()
    {
        var update = new FinalUtteranceUpdate("result-1", 7, "Hello", "你好", "en-US");

        Assert.Equal("result-1", update.UtteranceId);
        Assert.Equal(7, update.Sequence);
        Assert.Equal("Hello", update.OriginalText);
        Assert.Equal("你好", update.TranslatedText);
        Assert.Equal("en-US", update.DetectedLanguage);
    }
}
