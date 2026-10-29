using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Audio;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class WindowsAudioManualTests
{
    [Trait("Category", "WindowsAudioManual")]
    [Fact]
    public async Task SystemLoopbackCapturesPlayback()
    {
        RequireManualRun();
        await CaptureForFiveSeconds(new SystemLoopbackAudioSource());
    }

    [Trait("Category", "WindowsAudioManual")]
    [Fact]
    public async Task MicrophoneCapturesSpeech()
    {
        RequireManualRun();
        await CaptureForFiveSeconds(new MicrophoneAudioSource());
    }

    private static void RequireManualRun()
    {
        if (Environment.GetEnvironmentVariable("RUN_WINDOWS_AUDIO_TESTS") != "1")
        {
            Assert.Skip("Set RUN_WINDOWS_AUDIO_TESTS=1 and play audio or speak for five seconds.");
        }
    }

    private static async Task CaptureForFiveSeconds(IAudioCaptureSource source)
    {
        await using (source)
        {
            var frames = 0;
            source.FrameAvailable += (_, args) =>
            {
                if (args.Frame.Data.Length > 0)
                {
                    Interlocked.Increment(ref frames);
                }
            };

            await source.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await source.StopAsync(CancellationToken.None);
            Assert.True(frames > 0, "No PCM frames captured. Play audio for loopback or speak for microphone.");
        }
    }
}
