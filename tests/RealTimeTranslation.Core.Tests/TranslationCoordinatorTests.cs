using RealTimeTranslation.Core;
using RealTimeTranslation.Core.Tests.Fakes;

namespace RealTimeTranslation.Core.Tests;

public sealed class TranslationCoordinatorTests
{
    private static readonly StartTranslationOptions Options = new(
        "https://example.test",
        "fake-key",
        AudioSourceKind.SystemAudio,
        TargetLanguage.SimplifiedChinese);

    [Fact]
    public async Task StartCreatesExactlyOneSourceAndSession()
    {
        var audio = new FakeAudioSource();
        var provider = new FakeTranslationProvider();
        await using var coordinator = CreateCoordinator(provider, audio);

        await coordinator.StartAsync(Options, CancellationToken.None);

        Assert.Equal(SessionState.Listening, coordinator.State);
        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(1, audio.StartCount);
    }

    [Fact]
    public async Task AudioFramesUseOneWriterAndKeepNewestBoundedFrames()
    {
        var audio = new FakeAudioSource();
        var firstSession = new FakeTranslationSession { BlockFirstWrite = true };
        var provider = new FakeTranslationProvider(firstSession);
        await using var coordinator = CreateCoordinator(provider, audio);
        await coordinator.StartAsync(Options, CancellationToken.None);

        audio.Emit(0);
        await firstSession.WaitForFirstWriteAsync().WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);
        for (byte value = 1; value <= 30; value++)
        {
            audio.Emit(value);
        }

        firstSession.ReleaseFirstWrite();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await firstSession.WaitForWriteCountAsync(21, timeout.Token);

        Assert.Equal(1, firstSession.MaximumConcurrentWrites);
        Assert.Equal([0, .. Enumerable.Range(11, 20).Select(value => (byte)value)], firstSession.WrittenValues);
    }

    [Fact]
    public async Task PauseStopsCaptureAndSession()
    {
        var audio = new FakeAudioSource();
        var provider = new FakeTranslationProvider();
        await using var coordinator = CreateCoordinator(provider, audio);
        await coordinator.StartAsync(Options, CancellationToken.None);

        await coordinator.PauseAsync(CancellationToken.None);

        Assert.Equal(SessionState.Paused, coordinator.State);
        Assert.Equal(1, audio.StopCount);
        Assert.Equal(1, provider.LastSession.StopCount);
    }

    [Fact]
    public async Task ThreeFailedReconnectsUseOneTwoFourDelaysThenFail()
    {
        var initial = new FakeTranslationSession();
        var provider = new FakeTranslationProvider(
            initial,
            FailedSession(),
            FailedSession(),
            FailedSession());
        var delays = new List<TimeSpan>();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new TranslationCoordinator(
            provider,
            _ => new FakeAudioSource(),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        coordinator.StateChanged += (_, args) =>
        {
            if (args.State == SessionState.Failed)
            {
                failed.TrySetResult();
            }
        };
        await coordinator.StartAsync(Options, CancellationToken.None);

        initial.Emit(new TranslationCanceledUpdate("session", 1, "Network"));
        await failed.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], delays);
        Assert.Equal(4, provider.CreateCount);
    }

    [Fact]
    public async Task PauseCancelsPendingReconnectDelay()
    {
        var initial = new FakeTranslationSession();
        var provider = new FakeTranslationProvider(initial);
        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new TranslationCoordinator(
            provider,
            _ => new FakeAudioSource(),
            async (_, cancellationToken) =>
            {
                delayEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        await coordinator.StartAsync(Options, CancellationToken.None);
        initial.Emit(new TranslationCanceledUpdate("session", 1, "Network"));
        await delayEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        await coordinator.PauseAsync(CancellationToken.None);

        Assert.Equal(SessionState.Paused, coordinator.State);
        Assert.Equal(1, provider.CreateCount);
    }

    [Fact]
    public async Task ReconnectRetriesWhenSdkStartThrowsIoException()
    {
        var initial = new FakeTranslationSession();
        var failedRetry = new FakeTranslationSession
        {
            StartException = new IOException("Simulated network failure.")
        };
        var recovered = new FakeTranslationSession();
        var provider = new FakeTranslationProvider(initial, failedRetry, recovered);
        var delays = new List<TimeSpan>();
        var recoveredState = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new TranslationCoordinator(
            provider,
            _ => new FakeAudioSource(),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        coordinator.StateChanged += (_, args) =>
        {
            if (args.State == SessionState.Listening && provider.CreateCount == 3)
            {
                recoveredState.TrySetResult();
            }
        };
        await coordinator.StartAsync(Options, CancellationToken.None);

        initial.Emit(new TranslationCanceledUpdate("session", 1, "Network"));
        await recoveredState.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], delays);
        Assert.Equal(SessionState.Listening, coordinator.State);
        Assert.Equal(3, provider.CreateCount);
        Assert.Equal(1, failedRetry.DisposeCount);
    }

    [Fact]
    public async Task ReconnectDisposesBrokenSessionWhenStopThrows()
    {
        var initial = new FakeTranslationSession
        {
            StopException = new IOException("Simulated broken connection.")
        };
        var recovered = new FakeTranslationSession();
        var provider = new FakeTranslationProvider(initial, recovered);
        var recoveredState = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new TranslationCoordinator(
            provider,
            _ => new FakeAudioSource(),
            (_, _) => Task.CompletedTask);
        coordinator.StateChanged += (_, args) =>
        {
            if (args.State == SessionState.Listening && provider.CreateCount == 2)
            {
                recoveredState.TrySetResult();
            }
        };
        await coordinator.StartAsync(Options, CancellationToken.None);

        initial.Emit(new TranslationCanceledUpdate("session", 1, "Network"));
        await recoveredState.Task.WaitAsync(
            TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, initial.StopCount);
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(SessionState.Listening, coordinator.State);
    }

    private static TranslationCoordinator CreateCoordinator(
        FakeTranslationProvider provider,
        FakeAudioSource audio) => new(
            provider,
            _ => audio,
            (_, _) => Task.CompletedTask);

    private static FakeTranslationSession FailedSession() => new()
    {
        StartException = new IOException("Simulated network failure.")
    };
}
