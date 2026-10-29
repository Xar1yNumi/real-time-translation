using System.Buffers;
using NAudio.Wave;

namespace RealTimeTranslation.Infrastructure.Audio;

public sealed class DownmixToMonoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly int channels;

    public DownmixToMonoSampleProvider(ISampleProvider source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        channels = source.WaveFormat.Channels;
        if (channels < 1)
        {
            throw new ArgumentException("Input must have at least one channel.", nameof(source));
        }

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public int Read(Span<float> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var rented = ArrayPool<float>.Shared.Rent(checked(buffer.Length * channels));
        try
        {
            var read = source.Read(rented.AsSpan(0, buffer.Length * channels));
            var frames = read / channels;
            for (var frame = 0; frame < frames; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sum += rented[frame * channels + channel];
                }

                buffer[frame] = sum / channels;
            }

            return frames;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(rented);
        }
    }
}
