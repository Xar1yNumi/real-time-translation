using System.Buffers.Binary;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class PcmUtteranceSegmenterTests
{
    [Fact]
    public void SilenceDoesNotProduceAnUtterance()
    {
        var segmenter = new PcmUtteranceSegmenter();

        Assert.Empty(segmenter.Push(new byte[32_000]));
        Assert.Null(segmenter.Flush());
    }

    [Fact]
    public void SpeechThenTrailingSilenceProducesOneUtterance()
    {
        var segmenter = new PcmUtteranceSegmenter();
        Assert.Empty(segmenter.Push(new byte[3_200]));
        Assert.Empty(segmenter.Push(Signal(500, 5_000)));

        var utterances = segmenter.Push(new byte[19_200]);

        var utterance = Assert.Single(utterances);
        Assert.InRange(utterance.Length, 32_000, 45_000);
        Assert.Equal((short)5_000, BinaryPrimitives.ReadInt16LittleEndian(utterance.AsSpan(3_200, 2)));
        Assert.Null(segmenter.Flush());
    }

    [Fact]
    public void LongSpeechIsSplitAndStorageRemainsBounded()
    {
        var segmenter = new PcmUtteranceSegmenter();
        var emitted = new List<byte[]>();

        for (var index = 0; index < 12; index++)
        {
            emitted.AddRange(segmenter.Push(Signal(1_000, 5_000)));
        }

        Assert.NotEmpty(emitted);
        Assert.All(emitted, utterance => Assert.InRange(utterance.Length, 1, 256_000));
        Assert.InRange(segmenter.BufferedBytes, 0, 256_000);
    }

    [Fact]
    public void FlushEmitsFinalSpeechAndClearsPendingState()
    {
        var segmenter = new PcmUtteranceSegmenter();
        Assert.Empty(segmenter.Push(Signal(500, 5_000)));

        Assert.NotNull(segmenter.Flush());
        Assert.Null(segmenter.Flush());
        Assert.Empty(segmenter.Push(new byte[32_000]));
    }

    [Fact]
    public void ShortNoiseDoesNotProduceAnUtterance()
    {
        var segmenter = new PcmUtteranceSegmenter();

        Assert.Empty(segmenter.Push(Signal(200, 5_000)));
        Assert.Empty(segmenter.Push(new byte[19_200]));
        Assert.Null(segmenter.Flush());
    }

    [Fact]
    public void FlushPreservesUtteranceWhenFinalPartialFrameCompletesSilence()
    {
        using var segmenter = new PcmUtteranceSegmenter();
        Assert.Empty(segmenter.Push(Signal(500, 5_000)));
        Assert.Empty(segmenter.Push(new byte[29 * 640]));
        Assert.Empty(segmenter.Push(new byte[320]));

        var utterance = segmenter.Flush();

        Assert.NotNull(utterance);
        Assert.InRange(utterance.Length, 16_000, 40_000);
    }

    [Fact]
    public void ContinuousSpeechProducesUpdatedPreviewBeforeFinalWithStableIdentity()
    {
        using var segmenter = new PcmUtteranceSegmenter();

        var first = Assert.Single(segmenter.PushProgress(Signal(1_100, 5_000)));
        Assert.False(first.IsFinal);
        Assert.InRange(first.Pcm.Length, 25_600, 40_000);

        var second = Assert.Single(segmenter.PushProgress(Signal(1_100, 5_000)));
        Assert.False(second.IsFinal);
        Assert.Equal(first.UtteranceNumber, second.UtteranceNumber);
        Assert.True(second.Pcm.Length > first.Pcm.Length);

        var final = Assert.Single(segmenter.PushProgress(new byte[19_200]));
        Assert.True(final.IsFinal);
        Assert.Equal(first.UtteranceNumber, final.UtteranceNumber);
        Assert.True(final.Pcm.Length > second.Pcm.Length);
    }

    [Fact]
    public void ShortNoiseNeverProducesPreviewOrFinal()
    {
        using var segmenter = new PcmUtteranceSegmenter();

        Assert.Empty(segmenter.PushProgress(Signal(200, 5_000)));
        Assert.Empty(segmenter.PushProgress(new byte[19_200]));
    }

    private static byte[] Signal(int milliseconds, short amplitude)
    {
        var data = new byte[milliseconds * 32];
        for (var index = 0; index < data.Length; index += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index, 2), amplitude);
        }

        return data;
    }
}
