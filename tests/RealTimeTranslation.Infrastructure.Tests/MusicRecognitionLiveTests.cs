using System.Collections.Concurrent;
using NAudio.CoreAudioApi;
using Whisper.net;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class MusicRecognitionLiveTests
{
    [Trait("Category", "MusicRecognitionLive")]
    [Fact]
    public async Task CapturesDefaultSpeakerAndRecognizesShortMusicExcerptLocally()
    {
        if (Environment.GetEnvironmentVariable("RUN_MUSIC_RECOGNITION_LIVE_TEST") != "1")
        {
            Assert.Skip("Play music through the default speaker and set RUN_MUSIC_RECOGNITION_LIVE_TEST=1.");
        }

        var root = FindRepositoryRoot();
        using var devices = new MMDeviceEnumerator();
        using var defaultSpeaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        TestContext.Current.TestOutputHelper?.WriteLine($"Default speaker: {defaultSpeaker.FriendlyName}");
        foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Output: {device.FriendlyName}; peak={device.AudioMeterInformation.MasterPeakValue:F3}; " +
                $"volume={device.AudioEndpointVolume.MasterVolumeLevelScalar:F3}; mute={device.AudioEndpointVolume.Mute}");
        }
        var modelPath = Path.Combine(root, "artifacts", "models", "ggml-small-q5_1.bin");
        Assert.True(File.Exists(modelPath), $"Missing model: {modelPath}");
        var frames = new ConcurrentQueue<byte[]>();
        await using (var source = new SystemLoopbackAudioSource())
        {
            source.FrameAvailable += (_, args) => frames.Enqueue(args.Frame.Data.ToArray());
            await source.StartAsync(TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(12), TestContext.Current.CancellationToken);
            await source.StopAsync(TestContext.Current.CancellationToken);
        }

        var pcm = frames.SelectMany(frame => frame).ToArray();
        Assert.True(pcm.Length > 16_000, "No audio arrived from the default speaker.");
        var peak = pcm.Chunk(2).Max(sample => Math.Abs(BitConverter.ToInt16(sample)));
        TestContext.Current.TestOutputHelper?.WriteLine($"Capture: {pcm.Length / 32_000d:F2}s, peak={peak}");
        Assert.True(peak > 400, "Captured audio is effectively silent.");

        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, Path.GetDirectoryName(modelPath)!,
            new WhisperModelDescriptor(
                new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
                190_085_487,
                "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            store, TestContext.Current.CancellationToken);
        var full = await transcriber.TranscribeAsync(pcm, TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Full clip: status={full.Status}, lang={full.LanguageCode}, text={full.Text}");

        using (var factory = WhisperFactory.FromPath(modelPath,
            new WhisperFactoryOptions { UseGpu = true }))
        await using (var chineseProcessor = factory.CreateBuilder()
            .WithLanguage("zh").WithNoContext().Build())
        using (var wave = new MemoryStream(PcmWaveWriter.Wrap16kMono(pcm)))
        {
            var chineseText = new List<string>();
            await foreach (var result in chineseProcessor.ProcessAsync(
                wave, TestContext.Current.CancellationToken))
            {
                chineseText.Add(result.Text);
            }
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Forced Chinese: {string.Join(" ", chineseText)}");
        }

        using var segmenter = new PcmUtteranceSegmenter();
        var chunks = segmenter.PushProgress(pcm).Where(item => item.IsFinal).ToArray();
        foreach (var chunk in chunks)
        {
            var segmented = await transcriber.TranscribeAsync(
                chunk.Pcm, TestContext.Current.CancellationToken);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Segment: {chunk.Pcm.Length / 32_000d:F2}s, status={segmented.Status}, " +
                $"lang={segmented.LanguageCode}, text={segmented.Text}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var path = AppContext.BaseDirectory;
        while (path is not null && !File.Exists(Path.Combine(path, "RealTimeTranslation.slnx")))
        {
            path = Directory.GetParent(path)?.FullName;
        }

        return path ?? throw new InvalidOperationException("Repository root not found.");
    }
}
