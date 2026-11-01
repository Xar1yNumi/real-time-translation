using System.Diagnostics;
using NAudio.Wave;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class MultilingualSpeechLiveTests
{
    [Trait("Category", "WhisperLive")]
    [Theory]
    [InlineData("chinese-weather.wav", "zh")]
    [InlineData("russian-intro-8s.wav", "ru")]
    public async Task RecognizesNonEnglishSpeech(string fileName, string languageCode)
    {
        if (Environment.GetEnvironmentVariable("RUN_MULTILINGUAL_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_MULTILINGUAL_LIVE_TESTS=1 after preparing the local Chinese and licensed Russian WAV samples.");
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
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            store, TestContext.Current.CancellationToken);
        var timer = Stopwatch.StartNew();

        var result = await transcriber.TranscribeAsync(
            pcm.ToArray(), TestContext.Current.CancellationToken);

        timer.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{fileName}: status={result.Status}; language={result.LanguageCode}; " +
            $"characters={result.Text?.Length ?? 0}; elapsed={timer.Elapsed.TotalSeconds:F2}s");
        Assert.Equal(TranscriptionStatus.Recognized, result.Status);
        Assert.Equal(languageCode, result.LanguageCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Text));
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
