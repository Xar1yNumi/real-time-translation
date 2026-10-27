namespace RealTimeTranslation.Core;

public sealed record AudioFormat(int SampleRate, short BitsPerSample, short Channels)
{
    public static AudioFormat AzurePcm { get; } = new(16_000, 16, 1);
}

public sealed record AudioFrame
{
    public AudioFrame(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            throw new ArgumentException("Audio frame cannot be empty.", nameof(data));
        }

        Data = data;
    }

    public byte[] Data { get; }
}

public sealed class AudioFrameEventArgs(AudioFrame frame) : EventArgs
{
    public AudioFrame Frame { get; } = frame;
}

public sealed class AudioSourceFaultEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public interface IAudioCaptureSource : IAsyncDisposable
{
    AudioFormat OutputFormat { get; }

    event EventHandler<AudioFrameEventArgs>? FrameAvailable;

    event EventHandler<AudioSourceFaultEventArgs>? Faulted;

    ValueTask StartAsync(CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
