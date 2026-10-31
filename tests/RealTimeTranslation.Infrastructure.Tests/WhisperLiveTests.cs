using System.Diagnostics;
using System.Security.Cryptography;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class WhisperLiveTests
{
    // The upstream whisper.cpp sample is fetched separately and never redistributed here:
    // https://github.com/ggml-org/whisper.cpp/blob/master/samples/jfk.wav
    [Trait("Category", "WhisperLive")]
    [Fact]
    public async Task RecognizesEnglishFromRealWavOnCpu()
    {
        if (Environment.GetEnvironmentVariable("RUN_WHISPER_LIVE_TESTS") != "1")
        {
            Assert.Skip("Set RUN_WHISPER_LIVE_TESTS=1 after downloading the model and upstream JFK sample to artifacts.");
        }

        var root = FindRepositoryRoot();
        var modelDirectory = Path.Combine(root, "artifacts", "models");
        var modelPath = Path.Combine(modelDirectory, "ggml-small-q5_1.bin");
        var samplePath = Path.Combine(root, "artifacts", "samples", "jfk.wav");
        if (!File.Exists(modelPath) || !File.Exists(samplePath))
        {
            Assert.Skip("The validated model and JFK sample must be present under artifacts first.");
        }

        var wav = await File.ReadAllBytesAsync(samplePath, TestContext.Current.CancellationToken);
        var sampleHash = Convert.ToHexString(SHA256.HashData(wav));
        Assert.Equal("59DFB9A4ACB36FE2A2AFFC14BACBEE2920FF435CB13CC314A08C13F66BA7860E", sampleHash);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));

        using var client = new HttpClient();
        using var store = new WhisperModelStore(client, modelDirectory, new WhisperModelDescriptor(
            new Uri("https://huggingface.co/ggerganov/whisper.cpp/resolve/c521a4b02f422512d734391fdf08bb08c0862f68/ggml-small-q5_1.bin"),
            190_085_487,
            "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb"));
        await using var transcriber = await WhisperTranscriber.CreateAsync(
            store, TestContext.Current.CancellationToken);
        var timer = Stopwatch.StartNew();

        var result = await transcriber.TranscribeAsync(
            wav.AsMemory(44), TestContext.Current.CancellationToken);

        timer.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Whisper CPU: {timer.Elapsed.TotalSeconds:F2}s; language={result.LanguageCode}; characters={result.Text?.Length ?? 0}");
        Assert.Equal(TranscriptionStatus.Recognized, result.Status);
        Assert.Equal("en", result.LanguageCode);
        Assert.Contains("country", result.Text!, StringComparison.OrdinalIgnoreCase);
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
