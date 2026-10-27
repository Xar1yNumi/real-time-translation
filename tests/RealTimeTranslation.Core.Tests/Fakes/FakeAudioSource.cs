using RealTimeTranslation.Core;

namespace RealTimeTranslation.Core.Tests.Fakes;

internal sealed class FakeAudioSource : IAudioCaptureSource
{
    public AudioFormat OutputFormat => AudioFormat.AzurePcm;

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public event EventHandler<AudioFrameEventArgs>? FrameAvailable;

    public event EventHandler<AudioSourceFaultEventArgs>? Faulted;

    public ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StartCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return ValueTask.CompletedTask;
    }

    public void Emit(byte value)
    {
        FrameAvailable?.Invoke(this, new AudioFrameEventArgs(new AudioFrame([value])));
    }

    public void EmitFault(Exception exception)
    {
        Faulted?.Invoke(this, new AudioSourceFaultEventArgs(exception));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
