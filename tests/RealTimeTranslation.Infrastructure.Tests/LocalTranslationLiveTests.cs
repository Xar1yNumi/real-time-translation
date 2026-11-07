using NAudio.Wave;
using System.Diagnostics;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.LocalTranslation;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class LocalTranslationLiveTests
{
    [Trait("Category", "LocalLive")]
    [Fact]
    public async Task RealEnglishAndChineseWavProduceOfflineLocalSubtitles()
    {
        if (Environment.GetEnvironmentVariable("RUN_LOCAL_TRANSLATION_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_LOCAL_TRANSLATION_LIVE_TESTS=1 with cached models and samples.");
        }
        var root = FindRepositoryRoot();
        var python = Path.Combine(root, "artifacts", "localtranslation-venv", "Scripts", "python.exe");
        var script = Path.Combine(root, "tools", "LocalTranslation.Worker", "worker.py");
        var models = Path.Combine(root, "artifacts", "models");
        await using var local = await LocalTranslationClient.StartAsync(
            python, script, models, TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(await local.TranslateAsync(
            "Hello", "en", TestContext.Current.CancellationToken)));
        Assert.False(string.IsNullOrWhiteSpace(await local.TranslateAsync(
            "你好", "zh", TestContext.Current.CancellationToken)));

        using var http = new HttpClient(new DenyNetworkHandler());
        using var store = new WhisperModelStore(http, models, new WhisperModelDescriptor(
            new Uri("https://example.invalid/never-download"), 190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var provider = new NoAzureTranslationProvider(
            store, local, Path.Combine(models, "silero_vad.onnx"));
        foreach (var (file, target, source) in new[]
        {
            ("jfk-first-5s.wav", TargetLanguage.SimplifiedChinese, "en"),
            ("chinese-weather.wav", TargetLanguage.English, "zh")
        })
        {
            await using var session = await provider.CreateSessionAsync(
                new TranslationSessionOptions("", "", target), TestContext.Current.CancellationToken);
            var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TimeSpan? firstPreview = null;
            TimeSpan? finalAt = null;
            var timer = new Stopwatch();
            session.UpdateReceived += (_, args) =>
            {
                if (args.Update is PartialTranscriptUpdate && firstPreview is null)
                    firstPreview = timer.Elapsed;
                if (args.Update is FinalUtteranceUpdate final)
                {
                    finalAt = timer.Elapsed;
                    finalSeen.TrySetResult(final);
                }
                if (args.Update is TranslationCanceledUpdate canceled)
                    finalSeen.TrySetException(new InvalidOperationException(canceled.ErrorCode.ToString()));
            };
            using var wav = new WaveFileReader(Path.Combine(root, "artifacts", "samples", file));
            using var segmentation = new PcmUtteranceSegmenter(
                new SileroSpeechActivityDetector(Path.Combine(models, "silero_vad.onnx")));
            TimeSpan? segmentedAt = null;
            await session.StartAsync(TestContext.Current.CancellationToken);
            timer.Start();
            var chunk = new byte[3_200];
            int count;
            while ((count = await wav.ReadAsync(chunk, TestContext.Current.CancellationToken)) > 0)
            {
                if (segmentedAt is null && segmentation.PushProgress(chunk.AsSpan(0, count))
                    .Any(progress => progress.IsFinal)) segmentedAt = timer.Elapsed;
                await session.WriteAudioAsync(chunk.AsMemory(0, count), TestContext.Current.CancellationToken);
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            var trailing = new byte[19_200];
            if (segmentedAt is null && segmentation.PushProgress(trailing)
                .Any(progress => progress.IsFinal)) segmentedAt = timer.Elapsed;
            await session.WriteAudioAsync(trailing, TestContext.Current.CancellationToken);
            var result = await finalSeen.Task.WaitAsync(
                TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
            Assert.Equal(source, result.DetectedLanguage);
            Assert.False(string.IsNullOrWhiteSpace(result.OriginalText));
            Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
            Assert.NotNull(firstPreview);
            Assert.NotNull(finalAt);
            Assert.NotNull(segmentedAt);
            Assert.True(firstPreview < finalAt);
            if (target == TargetLanguage.SimplifiedChinese)
            {
                Assert.Contains(result.TranslatedText,
                    character => character is >= '\u4e00' and <= '\u9fff');
                Assert.DoesNotContain('\uFFFD', result.TranslatedText);
                Assert.Contains("美国同胞", result.TranslatedText);
            }
            await session.StopAsync(TestContext.Current.CancellationToken);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"{file}: preview={firstPreview?.TotalSeconds:F2}s final={finalAt?.TotalSeconds:F2}s " +
                $"post-segment={(finalAt - segmentedAt)?.TotalSeconds:F2}s " +
                $"audio={wav.TotalTime.TotalSeconds:F2}s; {result.OriginalText} => {result.TranslatedText}");
        }
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

    private sealed class DenyNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Network access is forbidden in the local live test.");
    }
}
