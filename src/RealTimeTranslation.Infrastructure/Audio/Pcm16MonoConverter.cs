using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Audio;

public sealed class Pcm16MonoConverter : IDisposable
{
    private const int FrameBytes = 3_200;
    private readonly AudioFormat outputFormat = AudioFormat.AzurePcm;
    private readonly BufferedWaveProvider buffered;
    private readonly SampleToWaveProvider16 output;
    private bool disposed;

    public Pcm16MonoConverter(WaveFormat inputFormat)
    {
        ArgumentNullException.ThrowIfNull(inputFormat);
        buffered = new BufferedWaveProvider(inputFormat, TimeSpan.FromSeconds(2))
        {
            DiscardOnBufferOverflow = false,
            ReadFully = false
        };

        ISampleProvider samples = buffered.ToSampleProvider();
        samples = new DownmixToMonoSampleProvider(samples);
        samples = new WdlResamplingSampleProvider(samples, AudioFormat.AzurePcm.SampleRate);
        output = new SampleToWaveProvider16(samples);
    }

    public AudioFormat OutputFormat => outputFormat;

    public event EventHandler<AudioFrameEventArgs>? FrameReady;

    public void Write(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Write(data.AsSpan());
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (data.IsEmpty)
        {
            return;
        }

        buffered.AddSamples(data);
        while (true)
        {
            var frame = new byte[FrameBytes];
            var read = output.Read(frame.AsSpan());
            if (read == 0)
            {
                return;
            }

            if (read != frame.Length)
            {
                Array.Resize(ref frame, read);
            }

            FrameReady?.Invoke(this, new AudioFrameEventArgs(new AudioFrame(frame)));
        }
    }

    public void Dispose()
    {
        disposed = true;
        FrameReady = null;
    }
}
