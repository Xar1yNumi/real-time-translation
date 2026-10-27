using System.Threading.Channels;

namespace RealTimeTranslation.Core;

public sealed class BoundedAudioFrameBuffer
{
    public const int DefaultCapacity = 20;

    private readonly Channel<AudioFrame> channel;

    public BoundedAudioFrameBuffer(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public bool TryWrite(AudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return channel.Writer.TryWrite(frame);
    }

    public IAsyncEnumerable<AudioFrame> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);

    public void Complete()
    {
        channel.Writer.TryComplete();
    }
}
