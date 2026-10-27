using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests;

public sealed class SubtitleStateTests
{
    [Fact]
    public void FinalOriginalAndTranslationAreAppliedAtomically()
    {
        var state = SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "result-1", 1, "Hello", "你好", "en-US"));

        Assert.Equal("result-1", state.UtteranceId);
        Assert.Equal("Hello", state.OriginalText);
        Assert.Equal("你好", state.TranslatedText);
        Assert.False(state.IsOriginalPartial);
    }

    [Fact]
    public void NewPartialClearsTranslationInsteadOfPairingWrongSentence()
    {
        var state = SubtitleState.Empty
            .Apply(new FinalUtteranceUpdate("result-1", 1, "Hello", "你好", "en-US"))
            .Apply(new PartialTranscriptUpdate("result-2", 2, "How are"));

        Assert.Equal("result-2", state.UtteranceId);
        Assert.Equal("How are", state.OriginalText);
        Assert.Equal(string.Empty, state.TranslatedText);
        Assert.True(state.IsOriginalPartial);
    }

    [Fact]
    public void LateFinalFromOlderSequenceIsIgnored()
    {
        var state = SubtitleState.Empty
            .Apply(new PartialTranscriptUpdate("result-2", 2, "How are"))
            .Apply(new FinalUtteranceUpdate("result-1", 1, "Hello", "你好", "en-US"));

        Assert.Equal("result-2", state.UtteranceId);
        Assert.Equal("How are", state.OriginalText);
        Assert.Equal(string.Empty, state.TranslatedText);
    }

    [Fact]
    public void FinalTextIsLimitedToTwoDisplayLines()
    {
        var state = SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "result-1", 1, "one\ntwo\nthree", "一\n二\n三", "en-US"));

        Assert.Equal("two\nthree", state.OriginalText.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("二\n三", state.TranslatedText.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void CancellationDoesNotReplaceVisibleSubtitle()
    {
        var state = SubtitleState.Empty
            .Apply(new FinalUtteranceUpdate("result-1", 1, "Hello", "你好", "en-US"))
            .Apply(new TranslationCanceledUpdate("session", 2, "Network"));

        Assert.Equal("Hello", state.OriginalText);
        Assert.Equal("你好", state.TranslatedText);
    }
}
