using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class NoAzureLiveSessionTests
{
    [Trait("Category", "NoAzureLive")]
    [Fact]
    public async Task SystemLoopbackPlaybackProducesProgressiveOnlineSubtitle()
    {
        if (Environment.GetEnvironmentVariable("RUN_PROGRESSIVE_LOOPBACK_TESTS") != "1")
        {
            Assert.Skip("Set RUN_PROGRESSIVE_LOOPBACK_TESTS=1 to play a short English sample through the default speaker.");
        }

        var root = FindRepositoryRoot();
        var modelDirectory = Path.Combine(root, "artifacts", "models");
        var samplePath = Path.Combine(root, "artifacts", "samples", "jfk-first-5s.wav");
        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, modelDirectory, new WhisperModelDescriptor(
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        var timer = new Stopwatch();
        var timedTranslator = new TimedTranslator(new MyMemoryTranslator(client), timer);
        await using var provider = new NoAzureTranslationProvider(store, timedTranslator);
        await using var coordinator = new TranslationCoordinator(provider,
            _ => new SystemLoopbackAudioSource(), Task.Delay);
        var firstPreview = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var warnings = new ConcurrentQueue<AppErrorCode>();
        var partialTimes = new ConcurrentQueue<TimeSpan>();
        TimeSpan? finalTime = null;
        TimeSpan? playbackStoppedAt = null;
        coordinator.WarningReceived += (_, args) =>
        {
            if (args.Update is TranslationWarningUpdate warning)
            {
                warnings.Enqueue(warning.ErrorCode);
            }
        };
        coordinator.SubtitleChanged += (_, args) =>
        {
            if (args.State.IsOriginalPartial && !string.IsNullOrWhiteSpace(args.State.OriginalText))
            {
                firstPreview.TrySetResult(timer.Elapsed);
                partialTimes.Enqueue(timer.Elapsed);
            }
            if (args.Completed is { } completed)
            {
                finalTime = timer.Elapsed;
                finalSeen.TrySetResult(completed);
            }
        };

        using var reader = new WaveFileReader(samplePath);
        using var pcm = new MemoryStream();
        await reader.CopyToAsync(pcm, TestContext.Current.CancellationToken);
        pcm.Write(new byte[32_000]);
        pcm.Position = 0;
        using var stream = new RawSourceWaveStream(pcm, new WaveFormat(16_000, 16, 1));
        using var playback = new WaveOut();
        playback.PlaybackStopped += (_, _) => playbackStoppedAt = timer.Elapsed;
        playback.Init(stream);

        await coordinator.StartAsync(new StartTranslationOptions("", "",
            AudioSourceKind.SystemAudio, TargetLanguage.SimplifiedChinese),
            TestContext.Current.CancellationToken);
        timer.Start();
        playback.Play();
        var previewTime = await firstPreview.Task.WaitAsync(
            TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        var final = await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        playback.Stop();
        await coordinator.StopAsync(TestContext.Current.CancellationToken);
        timer.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"System loopback: preview={previewTime.TotalSeconds:F2}s; " +
            $"partials=[{string.Join(",", partialTimes.Select(time => time.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)))}]s; " +
            $"playback-stop={playbackStoppedAt?.TotalSeconds:F2}s; " +
            $"translation-start={timedTranslator.Start?.TotalSeconds:F2}s; " +
            $"translation-end={timedTranslator.End?.TotalSeconds:F2}s; " +
            $"final={finalTime?.TotalSeconds:F2}s; audio={reader.TotalTime.TotalSeconds:F2}s; " +
            $"warnings=[{string.Join(",", warnings)}]");

        Assert.Equal("en", final.DetectedLanguage);
        Assert.False(string.IsNullOrWhiteSpace(final.TranslatedText));
        Assert.True(previewTime < finalTime);
    }

    [Trait("Category", "NoAzureLive")]
    [Theory]
    [InlineData("jfk-first-5s.wav", TargetLanguage.SimplifiedChinese, "en")]
    [InlineData("chinese-weather.wav", TargetLanguage.English, "zh")]
    public async Task ProgressiveRealWavShowsPreviewBeforeOnlineFinal(
        string fileName, TargetLanguage targetLanguage, string expectedSource)
    {
        if (Environment.GetEnvironmentVariable("RUN_PROGRESSIVE_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_PROGRESSIVE_LIVE_TESTS=1 after preparing the model and samples.");
        }

        var root = FindRepositoryRoot();
        var modelDirectory = Path.Combine(root, "artifacts", "models");
        var samplePath = Path.Combine(root, "artifacts", "samples", fileName);
        Assert.True(File.Exists(samplePath), $"Missing audio sample: {samplePath}");
        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, modelDirectory, new WhisperModelDescriptor(
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var provider = new NoAzureTranslationProvider(store, new MyMemoryTranslator(client));
        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions("", "", targetLanguage),
            TestContext.Current.CancellationToken);
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new Stopwatch();
        TimeSpan? firstPreview = null;
        TimeSpan? finalTime = null;
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is PartialTranscriptUpdate && firstPreview is null)
            {
                firstPreview = timer.Elapsed;
            }
            if (args.Update is FinalUtteranceUpdate final)
            {
                finalTime = timer.Elapsed;
                finalSeen.TrySetResult(final);
            }
            if (args.Update is TranslationCanceledUpdate canceled)
            {
                finalSeen.TrySetException(new InvalidOperationException(
                    $"Session failed: {canceled.ErrorCode}"));
            }
        };

        using var wav = new WaveFileReader(samplePath);
        Assert.Equal(16_000, wav.WaveFormat.SampleRate);
        Assert.Equal(16, wav.WaveFormat.BitsPerSample);
        Assert.Equal(1, wav.WaveFormat.Channels);
        await session.StartAsync(TestContext.Current.CancellationToken);
        timer.Start();
        var chunk = new byte[3_200];
        int count;
        while ((count = await wav.ReadAsync(chunk, TestContext.Current.CancellationToken)) > 0)
        {
            await session.WriteAudioAsync(chunk.AsMemory(0, count),
                TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        await session.WriteAudioAsync(new byte[19_200], TestContext.Current.CancellationToken);
        var result = await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
        timer.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{fileName}: preview={firstPreview?.TotalSeconds:F2}s; final={finalTime?.TotalSeconds:F2}s; audio={wav.TotalTime.TotalSeconds:F2}s");

        Assert.Equal(expectedSource, result.DetectedLanguage);
        Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
        Assert.NotNull(firstPreview);
        Assert.True(firstPreview < finalTime);
    }

    [Trait("Category", "NoAzureLive")]
    [Fact]
    public async Task RealEnglishWavProducesOnlineChineseSubtitle()
    {
        if (Environment.GetEnvironmentVariable("RUN_NOAZURE_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_NOAZURE_LIVE_TESTS=1 after downloading the model and public JFK WAV sample.");
        }

        var root = FindRepositoryRoot();
        var modelDirectory = Path.Combine(root, "artifacts", "models");
        var samplePath = Path.Combine(root, "artifacts", "samples", "jfk.wav");
        if (!File.Exists(Path.Combine(modelDirectory, "ggml-small-q5_1.bin")) ||
            !File.Exists(samplePath))
        {
            Assert.Skip("The model and public JFK WAV sample must be present under artifacts.");
        }

        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, modelDirectory, new WhisperModelDescriptor(
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var provider = new NoAzureTranslationProvider(store, new MyMemoryTranslator(client));
        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions("", "", TargetLanguage.SimplifiedChinese),
            TestContext.Current.CancellationToken);
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var partialBytes = 0;
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is FinalUtteranceUpdate final)
            {
                finalSeen.TrySetResult(final);
            }
            else if (args.Update is PartialTranscriptUpdate partial)
            {
                partialBytes = System.Text.Encoding.UTF8.GetByteCount(partial.Text);
            }
            else if (args.Update is TranslationCanceledUpdate canceled)
            {
                finalSeen.TrySetException(new InvalidOperationException(
                    $"Session failed: {canceled.ErrorCode}; transcript bytes={partialBytes}"));
            }
        };
        var wav = await File.ReadAllBytesAsync(samplePath, TestContext.Current.CancellationToken);

        await session.StartAsync(TestContext.Current.CancellationToken);
        var timer = Stopwatch.StartNew();
        await session.WriteAudioAsync(wav.AsMemory(44), TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(new byte[19_200], TestContext.Current.CancellationToken);
        var finalUpdate = await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
        timer.Stop();

        Assert.Equal("en", finalUpdate.DetectedLanguage);
        Assert.Matches("[A-Za-z]{3}", finalUpdate.OriginalText);
        Assert.False(string.IsNullOrWhiteSpace(finalUpdate.TranslatedText));
        Assert.NotEqual(finalUpdate.OriginalText, finalUpdate.TranslatedText);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Real no-Azure path: language={finalUpdate.DetectedLanguage}; original chars={finalUpdate.OriginalText.Length}; translated chars={finalUpdate.TranslatedText.Length}; elapsed={timer.Elapsed.TotalSeconds:F2}s");
    }

    [Trait("Category", "NoAzureLive")]
    [Theory]
    [InlineData("chinese-weather.wav", TargetLanguage.English, "zh")]
    [InlineData("russian-intro-8s.wav", TargetLanguage.SimplifiedChinese, "ru")]
    public async Task RealMultilingualWavProducesOnlineSubtitle(
        string fileName, TargetLanguage targetLanguage, string expectedSource)
    {
        if (Environment.GetEnvironmentVariable("RUN_NOAZURE_MULTILINGUAL_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_NOAZURE_MULTILINGUAL_LIVE_TESTS=1 after preparing local Chinese and licensed Russian WAV samples.");
        }

        var root = FindRepositoryRoot();
        var modelDirectory = Path.Combine(root, "artifacts", "models");
        var samplePath = Path.Combine(root, "artifacts", "samples", fileName);
        if (!File.Exists(Path.Combine(modelDirectory, "ggml-small-q5_1.bin")) ||
            !File.Exists(samplePath))
        {
            Assert.Skip("The model and local acceptance samples must be present under artifacts.");
        }

        using var wav = new WaveFileReader(samplePath);
        Assert.Equal(16_000, wav.WaveFormat.SampleRate);
        Assert.Equal(16, wav.WaveFormat.BitsPerSample);
        Assert.Equal(1, wav.WaveFormat.Channels);
        using var pcm = new MemoryStream();
        await wav.CopyToAsync(pcm, TestContext.Current.CancellationToken);

        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, modelDirectory, new WhisperModelDescriptor(
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var provider = new NoAzureTranslationProvider(store, new MyMemoryTranslator(client));
        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions("", "", targetLanguage),
            TestContext.Current.CancellationToken);
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is FinalUtteranceUpdate final)
            {
                finalSeen.TrySetResult(final);
            }
            else if (args.Update is TranslationCanceledUpdate canceled)
            {
                finalSeen.TrySetException(new InvalidOperationException(
                    $"Session failed: {canceled.ErrorCode}"));
            }
        };

        await session.StartAsync(TestContext.Current.CancellationToken);
        var timer = Stopwatch.StartNew();
        await session.WriteAudioAsync(pcm.ToArray(), TestContext.Current.CancellationToken);
        await session.WriteAudioAsync(new byte[19_200], TestContext.Current.CancellationToken);
        var finalUpdate = await finalSeen.Task.WaitAsync(
            TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
        timer.Stop();

        Assert.Equal(expectedSource, finalUpdate.DetectedLanguage);
        Assert.False(string.IsNullOrWhiteSpace(finalUpdate.OriginalText));
        Assert.False(string.IsNullOrWhiteSpace(finalUpdate.TranslatedText));
        Assert.NotEqual(finalUpdate.OriginalText, finalUpdate.TranslatedText);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{fileName}->{targetLanguage}: original chars={finalUpdate.OriginalText.Length}; " +
            $"translated chars={finalUpdate.TranslatedText.Length}; elapsed={timer.Elapsed.TotalSeconds:F2}s");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RealTimeTranslation.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class TimedTranslator(IMyMemoryTranslator inner, Stopwatch timer)
        : IMyMemoryTranslator
    {
        public TimeSpan? Start { get; private set; }
        public TimeSpan? End { get; private set; }

        public async Task<string> TranslateAsync(string text, string source,
            string target, CancellationToken cancellationToken)
        {
            Start ??= timer.Elapsed;
            try
            {
                return await inner.TranslateAsync(text, source, target, cancellationToken);
            }
            finally
            {
                End = timer.Elapsed;
            }
        }
    }
}
