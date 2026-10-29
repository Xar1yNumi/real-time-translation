using System.Buffers.Binary;
using System.Diagnostics;
using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.Tests.TestSupport;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class WindowsAudioStabilityTests
{
    private const long ExpectedPcmBytes = 30L * 60 * 16_000 * sizeof(short);

    [Trait("Category", "WindowsAudioStabilityManual")]
    [Fact]
    public async Task SystemLoopbackRunsForThirtyMinutesWithoutFaultsOrMemoryGrowth()
    {
        if (Environment.GetEnvironmentVariable("RUN_WINDOWS_AUDIO_STABILITY_TESTS") != "1")
        {
            Assert.Skip("Set RUN_WINDOWS_AUDIO_STABILITY_TESTS=1 for a 30-minute real loopback capture test.");
        }

        var token = TestContext.Current.CancellationToken;
        var output = TestContext.Current.TestOutputHelper;
        var tone = AudioTestTone.CreatePcm16Stereo(TimeSpan.FromSeconds(4));
        var playbackBuffer = new BufferedWaveProvider(
            new WaveFormat(48_000, 16, 2), TimeSpan.FromSeconds(5));
        using var playback = new WaveOut();
        playback.Init(playbackBuffer);
        using var process = Process.GetCurrentProcess();
        await using var source = new SystemLoopbackAudioSource();

        long frames = 0;
        long bytes = 0;
        long nonZeroSamples = 0;
        long invalidFrames = 0;
        var faults = 0;
        source.FrameAvailable += (_, args) =>
        {
            var data = args.Frame.Data;
            if (data.Length is 0 or > 3_200 || data.Length % 2 != 0)
            {
                Interlocked.Increment(ref invalidFrames);
            }

            var nonZeroInFrame = 0;
            for (var offset = 0; offset + 1 < data.Length; offset += 2)
            {
                if (BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)) != 0)
                {
                    nonZeroInFrame++;
                }
            }

            Interlocked.Increment(ref frames);
            Interlocked.Add(ref bytes, data.Length);
            Interlocked.Add(ref nonZeroSamples, nonZeroInFrame);
        };
        source.Faulted += (_, _) => Interlocked.Increment(ref faults);

        Assert.Equal(AudioFormat.AzurePcm, source.OutputFormat);
        await source.StartAsync(token);
        playback.Play();
        playbackBuffer.AddSamples(tone);

        var timer = Stopwatch.StartNew();
        long firstMinutePrivateBytes = 0;
        long twentiethMinutePrivateBytes = 0;
        long peakPrivateBytes = 0;
        long nonZeroBeforeFinalTone = 0;
        for (var minute = 1; minute <= 30; minute++)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), token);
            process.Refresh();
            var privateBytes = process.PrivateMemorySize64;
            peakPrivateBytes = Math.Max(peakPrivateBytes, privateBytes);
            if (minute == 1)
            {
                firstMinutePrivateBytes = privateBytes;
                Assert.True(Volatile.Read(ref frames) > 0, "No audio frames arrived in the first minute.");
                Assert.True(Volatile.Read(ref nonZeroSamples) > 0,
                    "The initial test tone was not captured by system loopback.");
            }

            if (minute == 20)
            {
                twentiethMinutePrivateBytes = privateBytes;
            }

            output?.WriteLine(
                $"minute={minute} elapsed-seconds={timer.Elapsed.TotalSeconds:F1} "
                + $"frames={Volatile.Read(ref frames)} bytes={Volatile.Read(ref bytes)} "
                + $"nonzero-samples={Volatile.Read(ref nonZeroSamples)} "
                + $"private-mib={privateBytes / 1_048_576.0:F1} "
                + $"invalid-frames={Volatile.Read(ref invalidFrames)} "
                + $"faults={Volatile.Read(ref faults)}");

            if (minute == 29)
            {
                nonZeroBeforeFinalTone = Volatile.Read(ref nonZeroSamples);
                playbackBuffer.AddSamples(tone);
            }
        }

        playback.Stop();
        await source.StopAsync(CancellationToken.None);
        var framesAtStop = Volatile.Read(ref frames);
        await Task.Delay(TimeSpan.FromMilliseconds(250), token);
        process.Refresh();
        var finalPrivateBytes = process.PrivateMemorySize64;
        output?.WriteLine(
            $"result elapsed-seconds={timer.Elapsed.TotalSeconds:F1} frames={framesAtStop} "
            + $"bytes={Volatile.Read(ref bytes)} peak-private-mib={peakPrivateBytes / 1_048_576.0:F1} "
            + $"final-private-mib={finalPrivateBytes / 1_048_576.0:F1} "
            + $"private-growth-from-minute-1-mib={(finalPrivateBytes - firstMinutePrivateBytes) / 1_048_576.0:F1} "
            + $"private-growth-from-minute-20-mib={(finalPrivateBytes - twentiethMinutePrivateBytes) / 1_048_576.0:F1}");

        Assert.True(timer.Elapsed >= TimeSpan.FromMinutes(30));
        Assert.True(Volatile.Read(ref bytes) >= ExpectedPcmBytes * 9 / 10,
            "Captured PCM volume fell below 90% of the expected 30-minute stream.");
        Assert.True(Volatile.Read(ref nonZeroSamples) > nonZeroBeforeFinalTone,
            "The final test tone was not captured.");
        Assert.Equal(0, Volatile.Read(ref invalidFrames));
        Assert.Equal(0, Volatile.Read(ref faults));
        Assert.Equal(framesAtStop, Volatile.Read(ref frames));
        Assert.True(finalPrivateBytes - firstMinutePrivateBytes <= 64L * 1_048_576,
            "Private memory grew by more than 64 MiB from the first minute.");
        Assert.True(finalPrivateBytes - twentiethMinutePrivateBytes <= 32L * 1_048_576,
            "Private memory grew by more than 32 MiB in the final ten minutes.");
    }
}
