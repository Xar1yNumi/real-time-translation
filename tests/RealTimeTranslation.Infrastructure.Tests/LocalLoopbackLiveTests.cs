using System.Diagnostics;
using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.LocalTranslation;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class LocalLoopbackLiveTests
{
    [Trait("Category", "LocalLive")]
    [Fact]
    public async Task SpeakerPlaybackProducesOfflineChineseSubtitle()
    {
        if (Environment.GetEnvironmentVariable("RUN_LOCAL_LOOPBACK_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_LOCAL_LOOPBACK_LIVE_TESTS=1 with cached models and speakers.");
        }

        var root = FindRepositoryRoot();
        var models = Path.Combine(root, "artifacts", "models");
        var python = Path.Combine(root, "artifacts", "localtranslation-venv", "Scripts", "python.exe");
        var script = Path.Combine(root, "tools", "LocalTranslation.Worker", "worker.py");
        await using var translator = await LocalTranslationClient.StartAsync(
            python, script, models, TestContext.Current.CancellationToken);
        using var http = new HttpClient(new DenyNetworkHandler());
        using var store = new WhisperModelStore(http, models, new WhisperModelDescriptor(
            new Uri("https://example.invalid/never-download"), 190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var provider = new NoAzureTranslationProvider(
            store, translator, Path.Combine(models, "silero_vad.onnx"));
        await using var coordinator = new TranslationCoordinator(provider,
            _ => new SystemLoopbackAudioSource(), Task.Delay);

        var timer = new Stopwatch();
        TimeSpan? previewAt = null;
        TimeSpan? finalAt = null;
        var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.SubtitleChanged += (_, args) =>
        {
            if (args.State.IsOriginalPartial &&
                !string.IsNullOrWhiteSpace(args.State.OriginalText))
                previewAt ??= timer.Elapsed;
            if (args.Completed is { } completed)
            {
                finalAt = timer.Elapsed;
                finalSeen.TrySetResult(completed);
            }
        };
        coordinator.WarningReceived += (_, args) =>
        {
            if (args.Update is TranslationCanceledUpdate canceled)
                finalSeen.TrySetException(new InvalidOperationException(
                    $"Local loopback session canceled: {canceled.ErrorCode}"));
        };

        using var reader = new WaveFileReader(Path.Combine(root, "artifacts", "samples", "jfk-first-5s.wav"));
        using var pcm = new MemoryStream();
        await reader.CopyToAsync(pcm, TestContext.Current.CancellationToken);
        pcm.Write(new byte[32_000]);
        pcm.Position = 0;
        using var stream = new RawSourceWaveStream(pcm, new WaveFormat(16_000, 16, 1));
        using var playback = new WasapiPlayerBuilder().Build();
        playback.Init(stream);

        await coordinator.StartAsync(new StartTranslationOptions("", "",
            AudioSourceKind.SystemAudio, TargetLanguage.SimplifiedChinese),
            TestContext.Current.CancellationToken);
        timer.Start();
        playback.Play();
        try
        {
            var result = await finalSeen.Task.WaitAsync(
                TimeSpan.FromSeconds(90), TestContext.Current.CancellationToken);
            Assert.Equal("en", result.DetectedLanguage);
            Assert.Contains(result.TranslatedText,
                character => character is >= '\u4e00' and <= '\u9fff');
            Assert.NotNull(previewAt);
            Assert.NotNull(finalAt);
            Assert.True(previewAt < finalAt);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"system-loopback: preview={previewAt?.TotalSeconds:F2}s " +
                $"final={finalAt?.TotalSeconds:F2}s " +
                $"audio={reader.TotalTime.TotalSeconds:F2}s; " +
                $"{result.OriginalText} => {result.TranslatedText}");
        }
        finally
        {
            playback.Stop();
            await coordinator.StopAsync(CancellationToken.None);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RealTimeTranslation.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class DenyNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Network access is forbidden in the local live test.");
    }
}
