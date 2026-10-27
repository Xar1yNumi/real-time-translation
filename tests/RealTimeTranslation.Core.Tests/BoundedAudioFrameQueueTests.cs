using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests;

public sealed class BoundedAudioFrameQueueTests
{
    [Fact]
    public async Task FullQueueDropsOldestFramesAndPreservesOrder()
    {
        var queue = new BoundedAudioFrameBuffer(3);
        for (byte value = 0; value < 5; value++)
        {
            Assert.True(queue.TryWrite(new AudioFrame([value])));
        }

        queue.Complete();

        var values = new List<byte>();
        await foreach (var frame in queue.ReadAllAsync(CancellationToken.None))
        {
            values.Add(frame.Data[0]);
        }

        Assert.Equal([2, 3, 4], values);
    }

    [Fact]
    public void CompletedQueueRejectsNewFrames()
    {
        var queue = new BoundedAudioFrameBuffer(3);
        queue.Complete();

        Assert.False(queue.TryWrite(new AudioFrame([1])));
    }
}
