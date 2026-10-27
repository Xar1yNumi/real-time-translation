using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests;

public sealed class AudioFrameTests
{
    [Fact]
    public void EmptyAudioFrameIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new AudioFrame([]));
    }

    [Fact]
    public void AzurePcmFormatIsCanonical()
    {
        Assert.Equal(new AudioFormat(16_000, 16, 1), AudioFormat.AzurePcm);
    }
}
