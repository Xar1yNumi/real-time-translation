namespace RealTimeTranslation.Infrastructure.LocalSpeech;

internal sealed record PcmSpeechProgress(long UtteranceNumber, byte[] Pcm, bool IsFinal);

internal sealed class PcmUtteranceSegmenter : IDisposable
{
    private const int FrameBytes = 1_024; // 32 ms at 16 kHz, PCM16 mono.
    private const int PreRollFrames = 5;
    private const int MinimumVoicedFrames = 10;
    private const int PreviewStartFrames = 20;
    private const int PreviewIntervalFrames = 25;
    private const int TrailingSilenceFrames = 15;
    private const int MaximumUtteranceBytes = 256_000;

    private readonly byte[] pendingFrame = new byte[FrameBytes];
    private readonly Queue<byte[]> preRoll = new();
    private readonly ISpeechActivityDetector detector;
    private MemoryStream? current;
    private int pendingCount;
    private int voicedFrames;
    private int silentFrames;
    private int lastPreviewVoicedFrames;
    private long nextUtteranceNumber;
    private long currentUtteranceNumber;
    private bool disposed;

    public PcmUtteranceSegmenter(ISpeechActivityDetector? detector = null)
    {
        this.detector = detector ?? new RmsSpeechActivityDetector();
    }

    internal int BufferedBytes => pendingCount + (preRoll.Count * FrameBytes) + (int)(current?.Length ?? 0);

    public IReadOnlyList<byte[]> Push(ReadOnlySpan<byte> pcm16Mono) =>
        PushProgress(pcm16Mono).Where(progress => progress.IsFinal)
            .Select(progress => progress.Pcm).ToArray();

    public IReadOnlyList<PcmSpeechProgress> PushProgress(ReadOnlySpan<byte> pcm16Mono)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var progress = new List<PcmSpeechProgress>();
        while (!pcm16Mono.IsEmpty)
        {
            var count = Math.Min(FrameBytes - pendingCount, pcm16Mono.Length);
            pcm16Mono[..count].CopyTo(pendingFrame.AsSpan(pendingCount));
            pendingCount += count;
            pcm16Mono = pcm16Mono[count..];
            if (pendingCount == FrameBytes)
            {
                ProcessFrame(pendingFrame, progress);
                pendingCount = 0;
            }
        }

        return progress;
    }

    public byte[]? Flush()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var completed = new List<PcmSpeechProgress>(1);
        if (pendingCount >= 2)
        {
            Array.Clear(pendingFrame, pendingCount, FrameBytes - pendingCount);
            ProcessFrame(pendingFrame, completed);
        }

        var utterance = completed.FirstOrDefault(progress => progress.IsFinal)?.Pcm
            ?? (voicedFrames >= MinimumVoicedFrames ? current?.ToArray() : null);
        Reset();
        preRoll.Clear();
        pendingCount = 0;
        detector.Reset();
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
        detector.Dispose();
    }

    private void ProcessFrame(ReadOnlySpan<byte> frame, List<PcmSpeechProgress>? progress)
    {
        var isVoice = detector.IsSpeech(frame);
        if (current is null)
        {
            if (!isVoice)
            {
                RememberPreRoll(frame);
                return;
            }

            current = new MemoryStream(MaximumUtteranceBytes);
            currentUtteranceNumber = ++nextUtteranceNumber;
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
            if (progress is not null && voicedFrames >= PreviewStartFrames &&
                (lastPreviewVoicedFrames == 0 ||
                 voicedFrames - lastPreviewVoicedFrames >= PreviewIntervalFrames))
            {
                progress.Add(new PcmSpeechProgress(
                    currentUtteranceNumber, current.ToArray(), false));
                lastPreviewVoicedFrames = voicedFrames;
            }
        }
        else
        {
            silentFrames++;
        }

        if (silentFrames >= TrailingSilenceFrames || current.Length >= MaximumUtteranceBytes)
        {
            if (voicedFrames >= MinimumVoicedFrames)
            {
                progress?.Add(new PcmSpeechProgress(
                    currentUtteranceNumber, current.ToArray(), true));
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

    private void Reset()
    {
        current?.Dispose();
        current = null;
        voicedFrames = 0;
        silentFrames = 0;
        lastPreviewVoicedFrames = 0;
        currentUtteranceNumber = 0;
    }
}
