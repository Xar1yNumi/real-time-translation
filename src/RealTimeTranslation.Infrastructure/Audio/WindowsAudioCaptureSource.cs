using NAudio.Wave;
using RealTimeTranslation.Core;

namespace RealTimeTranslation.Infrastructure.Audio;

public abstract class WindowsAudioCaptureSource : IAudioCaptureSource
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object disposeSync = new();
    private readonly IWaveIn capture;
    private readonly Pcm16MonoConverter converter;
    private TaskCompletionSource? recordingStopped;
    private Task? disposeTask;
    private int state;

    protected WindowsAudioCaptureSource(IWaveIn capture)
    {
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        try
        {
            converter = new Pcm16MonoConverter(capture.WaveFormat);
        }
        catch
        {
            capture.Dispose();
            throw;
        }

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        converter.FrameReady += OnFrameReady;
    }

    public AudioFormat OutputFormat => converter.OutputFormat;

    public event EventHandler<AudioFrameEventArgs>? FrameAvailable;

    public event EventHandler<AudioSourceFaultEventArgs>? Faulted;

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(state == 3, this);
            if (state == 1)
            {
                return;
            }

            if (state != 0)
            {
                throw new InvalidOperationException("A stopped capture source cannot be restarted.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            recordingStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref state, 1);
            try
            {
                capture.StartRecording();
            }
            catch
            {
                Volatile.Write(ref state, 0);
                recordingStopped = null;
                throw;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        Task? waitForStop = null;
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state == 3)
            {
                return;
            }

            if (state == 2)
            {
                waitForStop = recordingStopped?.Task;
            }
            else if (state == 1)
            {
                Volatile.Write(ref state, 2);
                waitForStop = recordingStopped?.Task;
                try
                {
                    capture.StopRecording();
                }
                catch (Exception exception)
                {
                    recordingStopped?.TrySetException(exception);
                    throw;
                }
            }
            else
            {
                Volatile.Write(ref state, 2);
            }
        }
        finally
        {
            lifecycleGate.Release();
        }

        if (waitForStop is not null)
        {
            await waitForStop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        lock (disposeSync)
        {
            disposeTask ??= DisposeCoreAsync();
            return new ValueTask(disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                Volatile.Write(ref state, 3);
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnRecordingStopped;
                converter.FrameReady -= OnFrameReady;
                converter.Dispose();
                capture.Dispose();
                FrameAvailable = null;
                Faulted = null;
            }
            finally
            {
                lifecycleGate.Release();
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref state) != 1)
        {
            return;
        }

        try
        {
            converter.Write(args.BufferSpan);
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(this, new AudioSourceFaultEventArgs(exception));
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        Volatile.Write(ref state, 2);
        if (args.Exception is not null)
        {
            Faulted?.Invoke(this, new AudioSourceFaultEventArgs(args.Exception));
        }

        recordingStopped?.TrySetResult();
    }

    private void OnFrameReady(object? sender, AudioFrameEventArgs args) =>
        FrameAvailable?.Invoke(this, args);
}
