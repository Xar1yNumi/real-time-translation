using System.Runtime.ExceptionServices;

namespace RealTimeTranslation.Core;

public enum SessionState
{
    Idle,
    Connecting,
    Listening,
    Paused,
    Reconnecting,
    Failed,
    Stopping
}

public sealed record StartTranslationOptions(
    string Endpoint,
    string ApiKey,
    AudioSourceKind Source,
    TargetLanguage TargetLanguage);

public sealed class CoordinatorStateChangedEventArgs(
    SessionState state,
    string? message = null) : EventArgs
{
    public SessionState State { get; } = state;

    public string? Message { get; } = message;
}

public sealed class SubtitleChangedEventArgs(SubtitleState state) : EventArgs
{
    public SubtitleState State { get; } = state;
}

public sealed class TranslationCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object subtitleGate = new();
    private readonly ITranslationProvider provider;
    private readonly Func<AudioSourceKind, IAudioCaptureSource> audioFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly RetryPolicy retryPolicy;
    private CancellationTokenSource? runCancellation;
    private CancellationTokenSource? resourceCancellation;
    private IAudioCaptureSource? audioSource;
    private ITranslationSession? translationSession;
    private BoundedAudioFrameBuffer? audioBuffer;
    private Task? audioPump;
    private Task? reconnectTask;
    private StartTranslationOptions? currentOptions;
    private SubtitleState subtitleState = SubtitleState.Empty;
    private int reconnectScheduled;
    private bool disposed;

    public TranslationCoordinator(
        ITranslationProvider provider,
        Func<AudioSourceKind, IAudioCaptureSource> audioFactory,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        RetryPolicy? retryPolicy = null)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.audioFactory = audioFactory ?? throw new ArgumentNullException(nameof(audioFactory));
        this.delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        this.retryPolicy = retryPolicy ?? RetryPolicy.NetworkDefault;
    }

    public SessionState State { get; private set; } = SessionState.Idle;

    public event EventHandler<CoordinatorStateChangedEventArgs>? StateChanged;

    public event EventHandler<SubtitleChangedEventArgs>? SubtitleChanged;

    public async ValueTask StartAsync(
        StartTranslationOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is not SessionState.Idle and not SessionState.Paused and not SessionState.Failed)
            {
                throw new InvalidOperationException($"Cannot start from {State}.");
            }

            await StopResourcesAsync().ConfigureAwait(false);
            runCancellation?.Dispose();
            runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            currentOptions = options;
            SetState(SessionState.Connecting);
            await StartResourcesAsync(options, runCancellation.Token).ConfigureAwait(false);
            SetState(SessionState.Listening);
        }
        catch
        {
            await StopResourcesAsync().ConfigureAwait(false);
            SetState(SessionState.Failed);
            throw;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        runCancellation?.Cancel();
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopResourcesAsync().ConfigureAwait(false);
            DisposeRunCancellation();
            SetState(SessionState.Paused);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public ValueTask ResumeAsync(
        StartTranslationOptions options,
        CancellationToken cancellationToken) => StartAsync(options, cancellationToken);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return;
        }

        runCancellation?.Cancel();
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SetState(SessionState.Stopping);
            await StopResourcesAsync().ConfigureAwait(false);
            DisposeRunCancellation();
            SetState(SessionState.Idle);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        disposed = true;
        lifecycleGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartResourcesAsync(
        StartTranslationOptions options,
        CancellationToken cancellationToken)
    {
        lock (subtitleGate)
        {
            subtitleState = SubtitleState.Empty;
            SubtitleChanged?.Invoke(this, new SubtitleChangedEventArgs(subtitleState));
        }
        resourceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var resourceToken = resourceCancellation.Token;

        translationSession = await provider.CreateSessionAsync(
            new TranslationSessionOptions(options.Endpoint, options.ApiKey, options.TargetLanguage),
            resourceToken).ConfigureAwait(false);
        audioSource = audioFactory(options.Source);
        audioBuffer = new BoundedAudioFrameBuffer();

        translationSession.UpdateReceived += OnTranslationUpdate;
        audioSource.FrameAvailable += OnAudioFrame;
        audioSource.Faulted += OnAudioFault;

        await translationSession.StartAsync(resourceToken).ConfigureAwait(false);
        audioPump = PumpAudioAsync(audioBuffer, translationSession, resourceToken);
        await audioSource.StartAsync(resourceToken).ConfigureAwait(false);
    }

    private async Task StopResourcesAsync()
    {
        var localAudio = audioSource;
        var localSession = translationSession;
        var localBuffer = audioBuffer;
        var localPump = audioPump;
        var localCancellation = resourceCancellation;

        audioSource = null;
        lock (subtitleGate)
        {
            translationSession = null;
        }
        audioBuffer = null;
        audioPump = null;
        resourceCancellation = null;

        if (localAudio is not null)
        {
            localAudio.FrameAvailable -= OnAudioFrame;
            localAudio.Faulted -= OnAudioFault;
        }

        if (localSession is not null)
        {
            localSession.UpdateReceived -= OnTranslationUpdate;
        }

        var failures = new List<Exception>();
        async Task TryCleanupAsync(Func<ValueTask> cleanup)
        {
            try
            {
                await cleanup().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        localCancellation?.Cancel();
        if (localAudio is not null)
        {
            await TryCleanupAsync(() => localAudio.StopAsync(CancellationToken.None))
                .ConfigureAwait(false);
        }

        localBuffer?.Complete();
        if (localPump is not null)
        {
            try
            {
                await localPump.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (localCancellation?.IsCancellationRequested == true)
            {
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (localSession is not null)
        {
            await TryCleanupAsync(() => localSession.StopAsync(CancellationToken.None))
                .ConfigureAwait(false);
        }

        if (localAudio is not null)
        {
            await TryCleanupAsync(localAudio.DisposeAsync).ConfigureAwait(false);
        }

        if (localSession is not null)
        {
            await TryCleanupAsync(localSession.DisposeAsync).ConfigureAwait(false);
        }

        localCancellation?.Dispose();
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Translation resources failed to stop cleanly.", failures);
        }
    }

    private static async Task PumpAudioAsync(
        BoundedAudioFrameBuffer buffer,
        ITranslationSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in buffer.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await session.WriteAudioAsync(frame.Data, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void OnAudioFrame(object? sender, AudioFrameEventArgs args)
    {
        if (ReferenceEquals(sender, audioSource))
        {
            audioBuffer?.TryWrite(args.Frame);
        }
    }

    private void OnAudioFault(object? sender, AudioSourceFaultEventArgs args)
    {
        if (ReferenceEquals(sender, audioSource))
        {
            SetState(SessionState.Failed, "Audio device unavailable.");
        }
    }

    private void OnTranslationUpdate(object? sender, TranslationUpdateEventArgs args)
    {
        lock (subtitleGate)
        {
            if (!ReferenceEquals(sender, translationSession))
            {
                return;
            }

            if (args.Update is not TranslationCanceledUpdate)
            {
                subtitleState = subtitleState.Apply(args.Update);
                SubtitleChanged?.Invoke(this, new SubtitleChangedEventArgs(subtitleState));
                return;
            }
        }

        ScheduleReconnect((ITranslationSession)sender!);
    }

    private void ScheduleReconnect(ITranslationSession failedSession)
    {
        if (Interlocked.CompareExchange(ref reconnectScheduled, 1, 0) != 0)
        {
            return;
        }

        reconnectTask = ReconnectAsync(failedSession);
    }

    private async Task ReconnectAsync(ITranslationSession failedSession)
    {
        try
        {
            await lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(failedSession, translationSession)
                    || currentOptions is null
                    || runCancellation is null
                    || runCancellation.IsCancellationRequested)
                {
                    return;
                }

                var options = currentOptions;
                var runToken = runCancellation.Token;
                SetState(SessionState.Reconnecting);
                try
                {
                    await StopResourcesAsync().ConfigureAwait(false);
                }
                catch (Exception) when (!runToken.IsCancellationRequested)
                {
                    // A broken transport can fail to stop after all resources are disposed.
                }

                foreach (var retryDelay in retryPolicy.Delays)
                {
                    await delayAsync(retryDelay, runToken).ConfigureAwait(false);
                    try
                    {
                        await StartResourcesAsync(options, runToken).ConfigureAwait(false);
                        SetState(SessionState.Listening);
                        return;
                    }
                    catch (OperationCanceledException) when (runToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception) when (!runToken.IsCancellationRequested)
                    {
                        try
                        {
                            await StopResourcesAsync().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // Cleanup proceeds through every resource before the next retry.
                        }
                    }
                }

                SetState(SessionState.Failed, "翻译连接中断");
            }
            finally
            {
                lifecycleGate.Release();
            }
        }
        catch (OperationCanceledException) when (runCancellation?.IsCancellationRequested != false)
        {
        }
        catch (Exception)
        {
            if (runCancellation?.IsCancellationRequested == false)
            {
                SetState(SessionState.Failed, "翻译连接中断");
            }
        }
        finally
        {
            Interlocked.Exchange(ref reconnectScheduled, 0);
        }
    }

    private void DisposeRunCancellation()
    {
        runCancellation?.Dispose();
        runCancellation = null;
        currentOptions = null;
        reconnectTask = null;
        Interlocked.Exchange(ref reconnectScheduled, 0);
    }

    private void SetState(SessionState state, string? message = null)
    {
        State = state;
        StateChanged?.Invoke(this, new CoordinatorStateChangedEventArgs(state, message));
    }
}
