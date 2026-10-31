using System.Buffers.Binary;
using System.Text;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class PcmWaveWriterTests
{
    [Fact]
    public void Wraps16kMonoPcmWithValidRiffHeader()
    {
        var pcm = new byte[] { 1, 2, 3, 4 };

        var wav = PcmWaveWriter.Wrap16kMono(pcm);

        Assert.Equal(48, wav.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4, 4)));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(wav, 8, 8));
        Assert.Equal(16, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(16, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22, 2)));
        Assert.Equal(16_000, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24, 4)));
        Assert.Equal(32_000, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(28, 4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(32, 2)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34, 2)));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40, 4)));
        Assert.Equal(pcm, wav.AsSpan(44).ToArray());
    }

    [Fact]
    public void RejectsPartialPcmSample()
    {
        Assert.Throws<ArgumentException>(() => PcmWaveWriter.Wrap16kMono(new byte[3]));
    }
}
