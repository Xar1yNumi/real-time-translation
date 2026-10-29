using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.Tests.TestSupport;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class Pcm16MonoConverterTests
{
    [Fact]
    public void Converts48kStereoFloatTo16kMonoPcm16()
    {
        var input = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
        using var converter = new Pcm16MonoConverter(input);
        var frames = new List<AudioFrame>();
        converter.FrameReady += (_, args) => frames.Add(args.Frame);

        converter.Write(SineWave.CreateFloatStereo(48_000, TimeSpan.FromSeconds(1), 440));

        Assert.Equal(AudioFormat.AzurePcm, converter.OutputFormat);
        Assert.NotEmpty(frames);
        Assert.All(frames, frame => Assert.InRange(frame.Data.Length, 1, 3_200));
        Assert.InRange(frames.Sum(frame => frame.Data.Length), 31_800, 32_200);
    }

    [Fact]
    public void DownmixAveragesChannels()
    {
        var input = new ConstantStereoSampleProvider(1.0f, -1.0f);
        var downmix = new DownmixToMonoSampleProvider(input);
        var samples = new float[4];

        var read = downmix.Read(samples, 0, samples.Length);

        Assert.Equal(4, read);
        Assert.All(samples, sample => Assert.Equal(0f, sample));
    }

    private sealed class ConstantStereoSampleProvider(float left, float right) : NAudio.Wave.ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            for (var index = 0; index < count; index++)
            {
                buffer[offset + index] = index % 2 == 0 ? left : right;
            }

            return count;
        }

        public int Read(Span<float> buffer)
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = index % 2 == 0 ? left : right;
            }

            return buffer.Length;
        }
    }
}
