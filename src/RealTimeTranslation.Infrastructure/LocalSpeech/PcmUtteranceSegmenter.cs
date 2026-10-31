using System.Buffers.Binary;

namespace RealTimeTranslation.Infrastructure.LocalSpeech;

internal sealed class PcmUtteranceSegmenter : IDisposable
{
    private const int FrameBytes = 640; // 20 ms at 16 kHz, PCM16 mono.
    private const int PreRollFrames = 10;
    private const int MinimumVoicedFrames = 20;
    private const int TrailingSilenceFrames = 30;
    private const int MaximumUtteranceBytes = 256_000;
    private const int VoiceRmsThreshold = 400;

    private readonly byte[] pendingFrame = new byte[FrameBytes];
    private readonly Queue<byte[]> preRoll = new();
    private MemoryStream? current;
    private int pendingCount;
    private int voicedFrames;
    private int silentFrames;
    private bool disposed;

    internal int BufferedBytes => pendingCount + (preRoll.Count * FrameBytes) + (int)(current?.Length ?? 0);

    public IReadOnlyList<byte[]> Push(ReadOnlySpan<byte> pcm16Mono)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var utterances = new List<byte[]>();
        while (!pcm16Mono.IsEmpty)
        {
            var count = Math.Min(FrameBytes - pendingCount, pcm16Mono.Length);
            pcm16Mono[..count].CopyTo(pendingFrame.AsSpan(pendingCount));
            pendingCount += count;
            pcm16Mono = pcm16Mono[count..];
            if (pendingCount == FrameBytes)
            {
                ProcessFrame(pendingFrame, utterances);
                pendingCount = 0;
            }
        }

        return utterances;
    }

    public byte[]? Flush()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var completed = new List<byte[]>(1);
        if (pendingCount >= 2)
        {
            var evenCount = pendingCount & ~1;
            ProcessFrame(pendingFrame.AsSpan(0, evenCount), completed);
        }

        var utterance = completed.Count > 0
            ? completed[0]
            : voicedFrames >= MinimumVoicedFrames ? current?.ToArray() : null;
        Reset();
        preRoll.Clear();
        pendingCount = 0;
        return utterance;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Reset();
        preRoll.Clear();
        pendingCount = 0;
    }

    private void ProcessFrame(ReadOnlySpan<byte> frame, List<byte[]>? utterances)
    {
        var isVoice = HasVoice(frame);
        if (current is null)
        {
            if (!isVoice)
            {
                RememberPreRoll(frame);
                return;
            }

            current = new MemoryStream(MaximumUtteranceBytes);
            foreach (var preceding in preRoll)
            {
                current.Write(preceding);
            }

            preRoll.Clear();
        }

        current.Write(frame);
        if (isVoice)
        {
            voicedFrames++;
            silentFrames = 0;
        }
        else
        {
            silentFrames++;
        }

        if (silentFrames >= TrailingSilenceFrames || current.Length >= MaximumUtteranceBytes)
        {
            if (voicedFrames >= MinimumVoicedFrames)
            {
                utterances?.Add(current.ToArray());
            }

            Reset();
        }
    }

    private void RememberPreRoll(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != FrameBytes)
        {
            return;
        }

        preRoll.Enqueue(frame.ToArray());
        if (preRoll.Count > PreRollFrames)
        {
            preRoll.Dequeue();
        }
    }

    private static bool HasVoice(ReadOnlySpan<byte> frame)
    {
        long power = 0;
        for (var index = 0; index + 1 < frame.Length; index += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(frame.Slice(index, 2));
            power += (long)sample * sample;
        }

        return power >= (long)VoiceRmsThreshold * VoiceRmsThreshold * (frame.Length / 2);
    }

    private void Reset()
    {
        current?.Dispose();
        current = null;
        voicedFrames = 0;
        silentFrames = 0;
    }
}
