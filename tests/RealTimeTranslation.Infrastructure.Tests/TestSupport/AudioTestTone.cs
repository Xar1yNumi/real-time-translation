using System.Buffers.Binary;

namespace RealTimeTranslation.Infrastructure.Tests.TestSupport;

internal static class AudioTestTone
{
    public static byte[] CreatePcm16Stereo(TimeSpan duration)
    {
        const int sampleRate = 48_000;
        var frameCount = checked((int)(duration.TotalSeconds * sampleRate));
        var bytes = new byte[frameCount * 2 * sizeof(short)];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / sampleRate)
                * short.MaxValue * 0.05);
            var offset = frame * 2 * sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), sample);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2, 2), sample);
        }

        return bytes;
    }
}
