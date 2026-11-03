using System.Collections.Concurrent;
using System.Diagnostics;
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
        var diagnostics = new List<string>();
        void Note(string message)
        {
            diagnostics.Add(message);
            TestContext.Current.TestOutputHelper?.WriteLine(message);
        }

        using var devices = new MMDeviceEnumerator();
        using var defaultSpeaker = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        Note($"Default speaker: {defaultSpeaker.FriendlyName}");
        foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            Note(
                $"Output: {device.FriendlyName}; peak={device.AudioMeterInformation.MasterPeakValue:F3}; " +
                $"volume={device.AudioEndpointVolume.MasterVolumeLevelScalar:F3}; mute={device.AudioEndpointVolume.Mute}");
        }
        var modelPath = Path.Combine(root, "artifacts", "models", "ggml-small-q5_1.bin");
        Assert.True(File.Exists(modelPath), $"Missing model: {modelPath}");
        var frames = new ConcurrentQueue<byte[]>();
        var wasMuted = defaultSpeaker.AudioEndpointVolume.Mute;
        try
        {
            await using var source = new SystemLoopbackAudioSource();
            source.FrameAvailable += (_, args) => frames.Enqueue(args.Frame.Data.ToArray());
            await source.StartAsync(TestContext.Current.CancellationToken);
            defaultSpeaker.AudioEndpointVolume.Mute = false;
            await Task.Delay(TimeSpan.FromSeconds(12), TestContext.Current.CancellationToken);
            await source.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            defaultSpeaker.AudioEndpointVolume.Mute = wasMuted;
            Note(
                $"Speaker mute restored: {defaultSpeaker.AudioEndpointVolume.Mute}");
        }
        Assert.Equal(wasMuted, defaultSpeaker.AudioEndpointVolume.Mute);

        var pcm = frames.SelectMany(frame => frame).ToArray();
        Assert.True(pcm.Length > 16_000, "No audio arrived from the default speaker.");
        var peak = pcm.Chunk(2).Max(sample => Math.Abs(BitConverter.ToInt16(sample)));
        Note($"Capture: {pcm.Length / 32_000d:F2}s, peak={peak}");
        Assert.True(peak > 400, "Captured audio is effectively silent.");

        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, Path.GetDirectoryName(modelPath)!,
            new WhisperModelDescriptor(
                new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
                190_085_487,
                "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            store, TestContext.Current.CancellationToken);
        var timer = Stopwatch.StartNew();
        var full = await transcriber.TranscribeAsync(pcm, TestContext.Current.CancellationToken);
        Note($"Full clip: {timer.Elapsed.TotalSeconds:F2}s, status={full.Status}, " +
            $"lang={full.LanguageCode}, text={full.Text}");

        timer.Restart();
        using (var factory = WhisperFactory.FromPath(modelPath,
            new WhisperFactoryOptions { UseGpu = true }))
        await using (var chineseProcessor = factory.CreateBuilder()
            .WithLanguage("zh").WithNoContext().WithProbabilities().Build())
        using (var wave = new MemoryStream(PcmWaveWriter.Wrap16kMono(pcm)))
        {
            var chineseText = new List<string>();
            await foreach (var result in chineseProcessor.ProcessAsync(
                wave, TestContext.Current.CancellationToken))
            {
                chineseText.Add($"{result.Text} [p={result.Probability:F2}, silence={result.NoSpeechProbability:F2}]");
            }
            Note($"Forced Chinese: {timer.Elapsed.TotalSeconds:F2}s, " +
                $"text={string.Join(" ", chineseText)}");

            var speechSamplePath = Path.Combine(root, "artifacts", "samples", "chinese-weather.wav");
            if (File.Exists(speechSamplePath))
            {
                timer.Restart();
                using var speechWave = File.OpenRead(speechSamplePath);
                await foreach (var result in chineseProcessor.ProcessAsync(
                    speechWave, TestContext.Current.CancellationToken))
                {
                    Note($"Speech control: {timer.Elapsed.TotalSeconds:F2}s, " +
                        $"text={result.Text} [p={result.Probability:F2}, silence={result.NoSpeechProbability:F2}]");
                }
            }
        }

        var mediumModelPath = Path.Combine(root, "artifacts", "models", "ggml-medium-q5_0.bin");
        if (File.Exists(mediumModelPath))
        {
            timer.Restart();
            using var mediumFactory = WhisperFactory.FromPath(mediumModelPath,
                new WhisperFactoryOptions { UseGpu = true });
            await using var mediumProcessor = mediumFactory.CreateBuilder()
                .WithLanguage("zh").WithNoContext().WithProbabilities().Build();
            using var mediumWave = new MemoryStream(PcmWaveWriter.Wrap16kMono(pcm));
            var mediumText = new List<string>();
            await foreach (var result in mediumProcessor.ProcessAsync(
                mediumWave, TestContext.Current.CancellationToken))
            {
                mediumText.Add($"{result.Text} [p={result.Probability:F2}, silence={result.NoSpeechProbability:F2}]");
            }
            Note($"Medium forced Chinese: {timer.Elapsed.TotalSeconds:F2}s, " +
                $"text={string.Join(" ", mediumText)}");
        }

        var turboModelPath = Path.Combine(root, "artifacts", "models", "ggml-large-v3-turbo-q5_0.bin");
        if (File.Exists(turboModelPath))
        {
            timer.Restart();
            using var turboFactory = WhisperFactory.FromPath(turboModelPath,
                new WhisperFactoryOptions { UseGpu = true });
            await using var turboProcessor = turboFactory.CreateBuilder()
                .WithLanguage("zh").WithNoContext().WithProbabilities().Build();
            using var turboWave = new MemoryStream(PcmWaveWriter.Wrap16kMono(pcm));
            var turboText = new List<string>();
            await foreach (var result in turboProcessor.ProcessAsync(
                turboWave, TestContext.Current.CancellationToken))
            {
                turboText.Add($"{result.Text} [p={result.Probability:F2}, silence={result.NoSpeechProbability:F2}]");
            }
            Note($"Turbo forced Chinese: {timer.Elapsed.TotalSeconds:F2}s, " +
                $"text={string.Join(" ", turboText)}");
        }

        using var segmenter = new PcmUtteranceSegmenter();
        var chunks = segmenter.PushProgress(pcm).Where(item => item.IsFinal).ToArray();
        foreach (var chunk in chunks)
        {
            timer.Restart();
            var segmented = await transcriber.TranscribeAsync(
                chunk.Pcm, TestContext.Current.CancellationToken);
            Note($"Segment: audio={chunk.Pcm.Length / 32_000d:F2}s, " +
                $"inference={timer.Elapsed.TotalSeconds:F2}s, status={segmented.Status}, " +
                $"lang={segmented.LanguageCode}, text={segmented.Text}");
        }

        await File.WriteAllLinesAsync(Path.Combine(root, "artifacts", "music-recognition-diagnostic.txt"),
            diagnostics, TestContext.Current.CancellationToken);
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
