using System.Buffers.Binary;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

internal interface ISpeechActivityDetector : IDisposable
{
    bool IsSpeech(ReadOnlySpan<byte> pcm16Mono);
    void Reset();
}

internal sealed class RmsSpeechActivityDetector : ISpeechActivityDetector
{
    private const int VoiceRmsThreshold = 400;

    public bool IsSpeech(ReadOnlySpan<byte> frame)
    {
        long power = 0;
        for (var index = 0; index + 1 < frame.Length; index += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(frame.Slice(index, 2));
            power += (long)sample * sample;
        }
        return power >= (long)VoiceRmsThreshold * VoiceRmsThreshold * (frame.Length / 2);
    }

    public void Reset() { }
    public void Dispose() { }
}
