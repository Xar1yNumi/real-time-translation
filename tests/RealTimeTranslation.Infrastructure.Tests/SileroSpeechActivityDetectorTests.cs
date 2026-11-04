using NAudio.Wave;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class SileroSpeechActivityDetectorTests
{
    [Trait("Category", "LocalLive")]
    [Fact]
    public void RecognizesSpeechWhileRejectingPureMusicTone()
    {
        var root = FindRepositoryRoot();
        var model = Path.Combine(root, "artifacts", "models", "silero_vad.onnx");
        var sample = Path.Combine(root, "artifacts", "samples", "jfk-first-5s.wav");
        if (!File.Exists(model) || !File.Exists(sample))
        {
            Assert.Skip("Silero model and speech sample must be cached under artifacts.");
        }

        using var detector = new SileroSpeechActivityDetector(model);
        var toneVoiced = 0;
        for (var frame = 0; frame < 150; frame++)
        {
            var pcm = new byte[1_024];
            for (var sampleIndex = 0; sampleIndex < 512; sampleIndex++)
            {
                var index = frame * 512 + sampleIndex;
                var value = (short)(5_000 * Math.Sin(2 * Math.PI * 440 * index / 16_000));
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(
                    pcm.AsSpan(sampleIndex * 2, 2), value);
            }
            if (detector.IsSpeech(pcm)) toneVoiced++;
        }

        detector.Reset();
        using var wav = new WaveFileReader(sample);
        var speechVoiced = 0;
        var speechFrames = 0;
        int? firstVoiceFrame = null;
        int? twentiethVoiceFrame = null;
        var chunk = new byte[1_024];
        while (wav.Read(chunk, 0, chunk.Length) == chunk.Length)
        {
            speechFrames++;
            if (detector.IsSpeech(chunk))
            {
                firstVoiceFrame ??= speechFrames;
                speechVoiced++;
                if (speechVoiced == 20) twentiethVoiceFrame = speechFrames;
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Silero: tone={toneVoiced}/150, speech={speechVoiced}/{speechFrames}, " +
            $"first={firstVoiceFrame * 0.032:F2}s, twentieth={twentiethVoiceFrame * 0.032:F2}s");
        Assert.InRange(toneVoiced, 0, 5);
        Assert.True(speechVoiced >= 15, $"Speech detected in only {speechVoiced} frames.");

        detector.Reset();
        wav.Position = 0;
        var mixedVoiced = 0;
        var mixedFrame = 0;
        while (wav.Read(chunk, 0, chunk.Length) == chunk.Length)
        {
            for (var sampleIndex = 0; sampleIndex < 512; sampleIndex++)
            {
                var sampleOffset = sampleIndex * 2;
                var speechSample = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(
                    chunk.AsSpan(sampleOffset, 2));
                var note = 1_500 * Math.Sin(2 * Math.PI * 440 *
                    (mixedFrame * 512 + sampleIndex) / 16_000);
                var mixed = (short)Math.Clamp((int)(speechSample + note), short.MinValue, short.MaxValue);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(
                    chunk.AsSpan(sampleOffset, 2), mixed);
            }
            if (detector.IsSpeech(chunk)) mixedVoiced++;
            mixedFrame++;
        }
        TestContext.Current.TestOutputHelper?.WriteLine($"Silero: speech+tone={mixedVoiced}/{mixedFrame}");
        Assert.True(mixedVoiced >= 15, $"Mixed speech detected in only {mixedVoiced} frames.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RealTimeTranslation.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
