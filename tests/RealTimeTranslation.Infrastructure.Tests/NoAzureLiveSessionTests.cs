using System.Diagnostics;
using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class NoAzureLiveSessionTests
{
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
        var provider = new NoAzureTranslationProvider(store, new MyMemoryTranslator(client));
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
        var provider = new NoAzureTranslationProvider(store, new MyMemoryTranslator(client));
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
}
