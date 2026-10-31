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
    AppError? error = null) : EventArgs
{
    public SessionState State { get; } = state;

    public AppError? Error { get; } = error;

    public string? Message => Error?.UserMessage;
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
    private EventHandler<TranslationUpdateEventArgs>? translationUpdateHandler;
    private EventHandler<AudioFrameEventArgs>? audioFrameHandler;
    private EventHandler<AudioSourceFaultEventArgs>? audioFaultHandler;
    private Guid activeSessionId;
    private BoundedAudioFrameBuffer? audioBuffer;
    private Task? audioPump;
    private Task? reconnectTask;
    private StartTranslationOptions? currentOptions;
    private SubtitleState subtitleState = SubtitleState.Empty;
    private Guid reconnectSessionId;
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
        catch (Exception exception)
        {
            try
            {
                await StopResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // StopResourcesAsync attempts every cleanup; keep the original startup failure.
            }
            var error = AppError.FromException(exception);
            SetState(SessionState.Failed, error);
            if (exception is AppFailureException)
            {
                throw;
            }

            throw new AppFailureException(error, exception);
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
        var sessionId = Guid.NewGuid();
        lock (subtitleGate)
        {
            activeSessionId = sessionId;
            subtitleState = SubtitleState.Empty;
            SubtitleChanged?.Invoke(this, new SubtitleChangedEventArgs(subtitleState));
        }
        resourceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var resourceToken = resourceCancellation.Token;

        translationSession = await provider.CreateSessionAsync(
            new TranslationSessionOptions(options.Endpoint, options.ApiKey, options.TargetLanguage),
            resourceToken).ConfigureAwait(false);
        try
        {
            audioSource = audioFactory(options.Source);
        }
        catch (Exception exception)
        {
            throw new AppFailureException(AudioErrorFrom(exception), exception);
        }

        audioBuffer = new BoundedAudioFrameBuffer();

        translationUpdateHandler = (sender, args) => OnTranslationUpdate(sessionId, sender, args);
        audioFrameHandler = (sender, args) => OnAudioFrame(sessionId, sender, args);
        audioFaultHandler = (sender, args) => OnAudioFault(sessionId, sender, args);
        translationSession.UpdateReceived += translationUpdateHandler;
        audioSource.FrameAvailable += audioFrameHandler;
        audioSource.Faulted += audioFaultHandler;

        await translationSession.StartAsync(resourceToken).ConfigureAwait(false);
        audioPump = PumpAudioAsync(audioBuffer, translationSession, resourceToken);
        try
        {
            await audioSource.StartAsync(resourceToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (resourceToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AppFailureException(AudioErrorFrom(exception), exception);
        }
    }

    private static AppError AudioErrorFrom(Exception exception) => AppError.For(
        exception is NotSupportedException
            ? AppErrorCode.UnsupportedAudioFormat
            : AppErrorCode.AudioDeviceUnavailable);

    private async Task StopResourcesAsync()
    {
        var localAudio = audioSource;
        var localSession = translationSession;
        var localTranslationHandler = translationUpdateHandler;
        var localFrameHandler = audioFrameHandler;
        var localFaultHandler = audioFaultHandler;
        var localBuffer = audioBuffer;
        var localPump = audioPump;
        var localCancellation = resourceCancellation;

        audioSource = null;
        lock (subtitleGate)
        {
            translationSession = null;
            activeSessionId = Guid.Empty;
        }
        translationUpdateHandler = null;
        audioFrameHandler = null;
        audioFaultHandler = null;
        audioBuffer = null;
        audioPump = null;
        resourceCancellation = null;

        if (localAudio is not null)
        {
            if (localFrameHandler is not null)
            {
                localAudio.FrameAvailable -= localFrameHandler;
            }

            if (localFaultHandler is not null)
            {
                localAudio.Faulted -= localFaultHandler;
            }
        }

        if (localSession is not null)
        {
            if (localTranslationHandler is not null)
            {
                localSession.UpdateReceived -= localTranslationHandler;
            }
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

    private void OnAudioFrame(Guid sessionId, object? sender, AudioFrameEventArgs args)
    {
        lock (subtitleGate)
        {
            if (sessionId == activeSessionId && ReferenceEquals(sender, audioSource))
            {
                audioBuffer?.TryWrite(args.Frame);
            }
        }
    }

    private void OnAudioFault(Guid sessionId, object? sender, AudioSourceFaultEventArgs args)
    {
        lock (subtitleGate)
        {
            if (sessionId != activeSessionId || !ReferenceEquals(sender, audioSource))
            {
                return;
            }
        }

        _ = Task.Run(() => FailSessionAsync(sessionId, AudioErrorFrom(args.Exception)));
    }

    private void OnTranslationUpdate(
        Guid sessionId, object? sender, TranslationUpdateEventArgs args)
    {
        lock (subtitleGate)
        {
            if (sessionId != activeSessionId || !ReferenceEquals(sender, translationSession))
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

        var error = AppError.For(((TranslationCanceledUpdate)args.Update).ErrorCode);
        if (error.IsTransient)
        {
            ScheduleReconnect(sessionId, (ITranslationSession)sender!);
        }
        else
        {
            _ = Task.Run(() => FailSessionAsync(sessionId, error));
        }
    }

    private async Task FailSessionAsync(Guid sessionId, AppError error)
    {
        CancellationTokenSource? cancellation;
        lock (subtitleGate)
        {
            if (sessionId != activeSessionId)
            {
                return;
            }
            cancellation = runCancellation;
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent pause or stop has already disposed this session's token.
        }

        await lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (sessionId != activeSessionId)
            {
                return;
            }

            try
            {
                await StopResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // All available resources are still cleaned up before the error is shown.
            }

            DisposeRunCancellation();
            SetState(SessionState.Failed, error);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private void ScheduleReconnect(Guid sessionId, ITranslationSession failedSession)
    {
        lock (subtitleGate)
        {
            if (sessionId != activeSessionId || reconnectSessionId == sessionId)
            {
                return;
            }

            reconnectSessionId = sessionId;
        }

        reconnectTask = Task.Run(() => ReconnectAsync(sessionId, failedSession));
    }

    private async Task ReconnectAsync(Guid sessionId, ITranslationSession failedSession)
    {
        try
        {
            await lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (sessionId != activeSessionId
                    || !ReferenceEquals(failedSession, translationSession)
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
                    catch (Exception exception) when (!runToken.IsCancellationRequested)
                    {
                        try
                        {
                            await StopResourcesAsync().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // Cleanup proceeds through every resource before the next retry.
                        }

                        var error = AppError.FromException(exception);
                        if (!error.IsTransient)
                        {
                            SetState(SessionState.Failed, error);
                            return;
                        }
                    }
                }

                SetState(SessionState.Failed,
                    AppError.For(AppErrorCode.NetworkUnavailable));
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
                SetState(SessionState.Failed,
                    AppError.For(AppErrorCode.Unknown));
            }
        }
        finally
        {
            lock (subtitleGate)
            {
                if (reconnectSessionId == sessionId)
                {
                    reconnectSessionId = Guid.Empty;
                }
            }
        }
    }

    private void DisposeRunCancellation()
    {
        runCancellation?.Dispose();
        runCancellation = null;
        currentOptions = null;
        reconnectTask = null;
        lock (subtitleGate)
        {
            reconnectSessionId = Guid.Empty;
        }
    }

    private void SetState(SessionState state, AppError? error = null)
    {
        State = state;
        StateChanged?.Invoke(this, new CoordinatorStateChangedEventArgs(state, error));
    }
}
