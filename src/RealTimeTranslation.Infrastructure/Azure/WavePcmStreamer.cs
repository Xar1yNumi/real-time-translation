using NAudio.Wave;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Azure;

public static class WavePcmStreamer
{
    public static async Task<long> StreamAsync(
        string path,
        ITranslationSession session,
        TimeSpan frameInterval,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThan(frameInterval, TimeSpan.Zero);

        using var reader = new WaveFileReader(path);
        ValidateFormat(reader.WaveFormat);
        var buffer = new byte[3_200];
        long bytesWritten = 0;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await session.WriteAudioAsync(
                buffer.AsMemory(0, read),
                cancellationToken).ConfigureAwait(false);
            bytesWritten += read;
            if (frameInterval > TimeSpan.Zero)
            {
                await Task.Delay(frameInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return bytesWritten;
    }

    private static void ValidateFormat(WaveFormat format)
    {
        if (format.Encoding != WaveFormatEncoding.Pcm
            || format.SampleRate != AudioFormat.AzurePcm.SampleRate
            || format.BitsPerSample != AudioFormat.AzurePcm.BitsPerSample
            || format.Channels != AudioFormat.AzurePcm.Channels)
        {
            throw new InvalidDataException(
                $"Expected 16 kHz, 16-bit, mono PCM WAV; found {format}.");
        }
    }
}
