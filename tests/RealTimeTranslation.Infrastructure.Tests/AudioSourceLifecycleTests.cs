using NAudio.Wave;
using RealTimeTranslation.Core;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.Tests.TestSupport;

namespace RealTimeTranslation.Infrastructure.Tests;

public sealed class AudioSourceLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceStartsStopsAndRelaysOwnedPcmFrames(bool microphone)
    {
        var device = new FakeWaveIn();
        await using var source = CreateSource(microphone, device);
        var frames = new List<AudioFrame>();
        source.FrameAvailable += (_, args) => frames.Add(args.Frame);

        await source.StartAsync(CancellationToken.None);
        await source.StartAsync(CancellationToken.None);
        device.EmitAudio(SineWave.CreateFloatStereo(48_000, TimeSpan.FromSeconds(1), 440));
        await source.StopAsync(CancellationToken.None);
        await source.StopAsync(CancellationToken.None);

        Assert.Equal(AudioFormat.AzurePcm, source.OutputFormat);
        Assert.Equal(1, device.StartCount);
        Assert.Equal(1, device.StopCount);
        Assert.NotEmpty(frames);
        Assert.All(frames, frame => Assert.InRange(frame.Data.Length, 1, 3_200));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await source.StartAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceReportsRecordingFaultAndDisposesOnce(bool microphone)
    {
        var device = new FakeWaveIn();
        var source = CreateSource(microphone, device);
        var faults = new List<Exception>();
        source.Faulted += (_, args) => faults.Add(args.Exception);

        await source.StartAsync(CancellationToken.None);
        device.StopException = new InvalidOperationException("Device disconnected.");
        await source.StopAsync(CancellationToken.None);
        await source.DisposeAsync();
        await source.DisposeAsync();

        Assert.Single(faults);
        Assert.Same(device.StopException, faults[0]);
        Assert.Equal(1, device.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalWaitsForPendingRecordingStop(bool microphone)
    {
        var device = new FakeWaveIn { DelayStop = true };
        var source = CreateSource(microphone, device);
        await source.StartAsync(CancellationToken.None);

        var stopping = source.StopAsync(CancellationToken.None).AsTask();
        var disposing = source.DisposeAsync().AsTask();
        Assert.False(stopping.IsCompleted);
        Assert.False(disposing.IsCompleted);

        device.CompleteStop();
        await Task.WhenAll(stopping, disposing).WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(1, device.StopCount);
        Assert.Equal(1, device.DisposeCount);
    }

    private static IAudioCaptureSource CreateSource(bool microphone, IWaveIn device) =>
        microphone ? new MicrophoneAudioSource(device) : new SystemLoopbackAudioSource(device);

    private sealed class FakeWaveIn : IWaveIn
    {
        public WaveFormat WaveFormat { get; set; } = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Exception? StopException { get; set; }

        public bool DelayStop { get; set; }

        public event EventHandler<WaveInEventArgs>? DataAvailable;

        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public void StartRecording() => StartCount++;

        public void StopRecording()
        {
            StopCount++;
            if (!DelayStop)
            {
                CompleteStop();
            }
        }

        public void CompleteStop() =>
            RecordingStopped?.Invoke(this, new StoppedEventArgs(StopException));

        public void EmitAudio(byte[] audio) =>
            DataAvailable?.Invoke(this, new WaveInEventArgs(audio, audio.Length));

        public void Dispose() => DisposeCount++;
    }
}
