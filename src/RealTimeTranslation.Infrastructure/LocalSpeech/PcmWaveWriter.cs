using System.Buffers.Binary;
using System.Text;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

internal static class PcmWaveWriter
{
    public static byte[] Wrap16kMono(ReadOnlySpan<byte> pcm16Mono)
    {
        if ((pcm16Mono.Length & 1) != 0)
        {
            throw new ArgumentException("PCM16 data must contain complete samples.", nameof(pcm16Mono));
        }

        var wav = new byte[checked(44 + pcm16Mono.Length)];
        WriteTag(wav, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4, 4), wav.Length - 8);
        WriteTag(wav, 8, "WAVE");
        WriteTag(wav, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22, 2), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24, 4), 16_000);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28, 4), 32_000);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32, 2), 2);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34, 2), 16);
        WriteTag(wav, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40, 4), pcm16Mono.Length);
        pcm16Mono.CopyTo(wav.AsSpan(44));
        return wav;
    }

    private static void WriteTag(Span<byte> buffer, int offset, string tag)
    {
        Encoding.ASCII.GetBytes(tag, buffer.Slice(offset, 4));
    }
}
