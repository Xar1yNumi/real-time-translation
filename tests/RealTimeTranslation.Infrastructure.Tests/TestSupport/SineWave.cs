namespace RealTimeTranslation.Infrastructure.Tests.TestSupport;

internal static class SineWave
{
    public static byte[] CreateFloatStereo(int sampleRate, TimeSpan duration, double frequency)
    {
        var frameCount = checked((int)(sampleRate * duration.TotalSeconds));
        var samples = new float[frameCount * 2];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sample = (float)Math.Sin(2 * Math.PI * frequency * frame / sampleRate);
            samples[frame * 2] = sample;
            samples[frame * 2 + 1] = sample;
        }

        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
