using NAudio.Wave;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.LocalTranslation;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class BundledLocalTrialLiveTests
{
    [Trait("Category", "LocalLive")]
    [Fact]
    public async Task ExtractedPackageKeepsTranslatingForTenMinutes()
    {
        var package = Environment.GetEnvironmentVariable("BUNDLED_LOCAL_ROOT");
        if (Environment.GetEnvironmentVariable("RUN_BUNDLED_LOCAL_STABILITY_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(package))
        {
            Assert.Skip("Set RUN_BUNDLED_LOCAL_STABILITY_TESTS=1 and BUNDLED_LOCAL_ROOT to an extracted ZIP.");
        }

        var bundle = Path.Combine(package, "local-data");
        using var store = new BundledWhisperModelStore(bundle);
        var pythonPath = Path.Combine(bundle, "python", "python.exe");
        var existingPythonIds = GetPythonProcessIds();
        await using var translator = new LocalTranslatorHost(
            pythonPath,
            Path.Combine(bundle, "worker.py"),
            Path.Combine(bundle, "models"));
        await translator.PrepareAsync(TestContext.Current.CancellationToken);
        using var workerProcess = FindBundledWorker(pythonPath, existingPythonIds);
        Assert.NotNull(workerProcess);
        await using var provider = new NoAzureTranslationProvider(
            store, translator, Path.Combine(bundle, "models", "silero_vad.onnx"));
        await using var session = await provider.CreateSessionAsync(
            new TranslationSessionOptions("", "", TargetLanguage.SimplifiedChinese),
            TestContext.Current.CancellationToken);

        var completed = new ConcurrentQueue<FinalUtteranceUpdate>();
        var failures = 0;
        session.UpdateReceived += (_, args) =>
        {
            if (args.Update is FinalUtteranceUpdate final &&
                !string.IsNullOrWhiteSpace(final.TranslatedText))
                completed.Enqueue(final);
            if (args.Update is TranslationCanceledUpdate)
                Interlocked.Increment(ref failures);
        };

        var root = FindRepositoryRoot();
        using var wav = new WaveFileReader(Path.Combine(root, "artifacts", "samples", "jfk-first-5s.wav"));
        using var audio = new MemoryStream();
        await wav.CopyToAsync(audio, TestContext.Current.CancellationToken);
        var voice = audio.ToArray();
        Assert.True(voice.Length > 3_200);
        var silence = new byte[3_200];
        var timer = Stopwatch.StartNew();
        var chunkIndex = 0;
        var peakPending = 0;
        var peakBufferedBytes = 0;
        var memorySamples = new List<(long TestHostBytes, long WorkerBytes)>();
        var localSession = Assert.IsType<NoAzureTranslationSession>(session);
        using var testHostProcess = Process.GetCurrentProcess();
        await session.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            while (timer.Elapsed < TimeSpan.FromMinutes(10))
            {
                var withinMinute = chunkIndex % 600;
                if (withinMinute * 3_200 < voice.Length)
                {
                    var offset = withinMinute * 3_200;
                    var length = Math.Min(3_200, voice.Length - offset);
                    await session.WriteAudioAsync(voice.AsMemory(offset, length),
                        TestContext.Current.CancellationToken);
                }
                else
                {
                    await session.WriteAudioAsync(silence,
                        TestContext.Current.CancellationToken);
                }
                chunkIndex++;
                peakPending = Math.Max(peakPending, localSession.PendingUtterances);
                peakBufferedBytes = Math.Max(peakBufferedBytes, localSession.BufferedPcmBytes);
                if (chunkIndex % 600 == 0)
                {
                    testHostProcess.Refresh();
                    workerProcess.Refresh();
                    Assert.False(workerProcess.HasExited,
                        "Bundled Python worker disappeared during the run.");
                    memorySamples.Add((testHostProcess.PrivateMemorySize64,
                        workerProcess.PrivateMemorySize64));
                }
                var next = TimeSpan.FromMilliseconds(chunkIndex * 100L) - timer.Elapsed;
                if (next > TimeSpan.Zero)
                    await Task.Delay(next, TestContext.Current.CancellationToken);
            }
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        }
        finally
        {
            await session.StopAsync(CancellationToken.None);
        }

        var finals = completed.ToArray();
        Assert.True(timer.Elapsed >= TimeSpan.FromMinutes(10));
        Assert.True(finals.Length >= 8,
            $"Expected 8 or more completed translations, got {finals.Length}.");
        Assert.Equal(finals.Length, finals.Select(item => item.UtteranceId).Distinct().Count());
        Assert.True(finals.Zip(finals.Skip(1)).All(pair =>
            pair.First.Sequence < pair.Second.Sequence));
        Assert.Equal(0, failures);
        Assert.InRange(peakPending, 0, 2);
        Assert.InRange(peakBufferedBytes, 0, 768_000);
        Assert.True(memorySamples.Count >= 8);
        const long maxGrowthBytes = 512L * 1_048_576;
        var testHostGrowth = memorySamples.Max(sample => sample.TestHostBytes)
            - memorySamples[0].TestHostBytes;
        var workerGrowth = memorySamples.Max(sample => sample.WorkerBytes) - memorySamples[0].WorkerBytes;
        Assert.InRange(testHostGrowth, 0, maxGrowthBytes);
        Assert.InRange(workerGrowth, 0, maxGrowthBytes);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Duration={timer.Elapsed}; completed={finals.Length}; failures={failures}; " +
            $"chunks={chunkIndex}; peak-pending={peakPending}; peak-buffered-bytes={peakBufferedBytes}; " +
            $"testhost-growth-MiB={testHostGrowth / 1_048_576}; " +
            $"worker-growth-MiB={workerGrowth / 1_048_576}");
    }

    [Trait("Category", "LocalLive")]
    [Fact]
    public async Task ExtractedPackageTranslatesWithoutInstalledPythonOrOnlineService()
    {
        var package = Environment.GetEnvironmentVariable("BUNDLED_LOCAL_ROOT");
        if (Environment.GetEnvironmentVariable("RUN_BUNDLED_LOCAL_LIVE_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(package))
        {
            Assert.Skip("Set RUN_BUNDLED_LOCAL_LIVE_TESTS=1 and BUNDLED_LOCAL_ROOT to an extracted ZIP.");
        }

        var bundle = Path.Combine(package, "local-data");
        using var store = new BundledWhisperModelStore(bundle);
        Assert.NotNull(await store.VerifyCachedAsync(TestContext.Current.CancellationToken));
        var pythonPath = Path.Combine(bundle, "python", "python.exe");
        var existingPythonIds = GetPythonProcessIds();
        await using var translator = new LocalTranslatorHost(
            pythonPath,
            Path.Combine(bundle, "worker.py"),
            Path.Combine(bundle, "models"));
        await translator.PrepareAsync(TestContext.Current.CancellationToken);
        using var workerProcess = FindBundledWorker(pythonPath, existingPythonIds);
        Assert.NotNull(workerProcess);
        var chineseLanguage = await translator.TranslateAsync(
            "Chinese language", "en", TestContext.Current.CancellationToken);
        Assert.True(chineseLanguage.Contains('中') || chineseLanguage.Contains("汉语"), chineseLanguage);
        var greeting = (await translator.TranslateAsync(
            "你好", "zh", TestContext.Current.CancellationToken)).ToLowerInvariant();
        Assert.True(greeting.Contains("hello") || greeting.Contains("hi"), greeting);
        workerProcess.Refresh();
        Assert.True(workerProcess.PrivateMemorySize64 > 0);

        await using var provider = new NoAzureTranslationProvider(
            store, translator, Path.Combine(bundle, "models", "silero_vad.onnx"));
        foreach (var (file, target, source) in new[]
        {
            ("jfk-first-5s.wav", TargetLanguage.SimplifiedChinese, "en"),
            ("chinese-weather.wav", TargetLanguage.English, "zh")
        })
        {
            await using var session = await provider.CreateSessionAsync(
                new TranslationSessionOptions("", "", target),
                TestContext.Current.CancellationToken);
            var finalSeen = new TaskCompletionSource<FinalUtteranceUpdate>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            session.UpdateReceived += (_, args) =>
            {
                if (args.Update is FinalUtteranceUpdate final) finalSeen.TrySetResult(final);
                if (args.Update is TranslationCanceledUpdate canceled)
                    finalSeen.TrySetException(new InvalidOperationException(canceled.ErrorCode.ToString()));
            };
            var root = FindRepositoryRoot();
            using var wav = new WaveFileReader(Path.Combine(root, "artifacts", "samples", file));
            await session.StartAsync(TestContext.Current.CancellationToken);
            var chunk = new byte[3_200];
            int count;
            while ((count = await wav.ReadAsync(chunk, TestContext.Current.CancellationToken)) > 0)
            {
                await session.WriteAudioAsync(chunk.AsMemory(0, count), TestContext.Current.CancellationToken);
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            await session.WriteAudioAsync(new byte[19_200], TestContext.Current.CancellationToken);
            var result = await finalSeen.Task.WaitAsync(TimeSpan.FromSeconds(90),
                TestContext.Current.CancellationToken);
            Assert.Equal(source, result.DetectedLanguage);
            Assert.False(string.IsNullOrWhiteSpace(result.OriginalText));
            Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
            if (target == TargetLanguage.SimplifiedChinese)
                Assert.True(result.TranslatedText.Contains("美国") ||
                    result.TranslatedText.Contains("同胞"), result.TranslatedText);
            else
                Assert.Contains(result.TranslatedText, char.IsAsciiLetter);
            await session.StopAsync(TestContext.Current.CancellationToken);
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Extracted package {file}: {result.OriginalText} => {result.TranslatedText}");
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

    private static HashSet<int> GetPythonProcessIds()
    {
        var processes = Process.GetProcessesByName("python");
        try
        {
            return processes.Select(process => process.Id).ToHashSet();
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static Process? FindBundledWorker(string pythonPath, HashSet<int> existingIds)
    {
        foreach (var process in Process.GetProcessesByName("python"))
        {
            var matches = false;
            try
            {
                matches = !existingIds.Contains(process.Id) &&
                    string.Equals(process.MainModule?.FileName, pythonPath,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            if (matches) return process;
            process.Dispose();
        }
        return null;
    }
}
