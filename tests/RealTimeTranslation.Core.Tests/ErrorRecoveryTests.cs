using RealTimeTranslation.Core;
using RealTimeTranslation.Core.Tests.Fakes;

namespace RealTimeTranslation.Core.Tests;

public sealed class ErrorRecoveryTests
{
    private static readonly StartTranslationOptions Options = new(
        "https://example.test", "fake-key", AudioSourceKind.SystemAudio,
        TargetLanguage.SimplifiedChinese);

    [Theory]
    [InlineData(AppErrorCode.AuthenticationFailed, false)]
    [InlineData(AppErrorCode.InvalidEndpoint, false)]
    [InlineData(AppErrorCode.NetworkUnavailable, true)]
    [InlineData(AppErrorCode.ServiceUnavailable, true)]
    [InlineData(AppErrorCode.RateLimited, false)]
    public void ErrorCategoryHasSafeMessageAndRetryDecision(
        AppErrorCode code, bool transient)
    {
        var error = AppError.For(code);

        Assert.Equal(transient, error.IsTransient);
        Assert.NotEmpty(error.UserMessage);
        Assert.DoesNotContain("secret-key", error.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticationCancellationFailsWithoutRetry()
    {
        var session = new FakeTranslationSession();
        var provider = new FakeTranslationProvider(session);
        var delays = new List<TimeSpan>();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new TranslationCoordinator(
            provider, _ => new FakeAudioSource(), (delay, _) =>
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

        session.Emit(new TranslationCanceledUpdate(
            "session", 1, AppErrorCode.AuthenticationFailed));
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Empty(delays);
        Assert.Equal(1, provider.CreateCount);
    }

    [Fact]
    public async Task CapturedCallbackFromPreviousSessionCannotUpdateReusedSession()
    {
        var session = new FakeTranslationSession();
        var provider = new FakeTranslationProvider(session, session);
        await using var coordinator = new TranslationCoordinator(
            provider, _ => new FakeAudioSource(), (_, _) => Task.CompletedTask);
        var subtitles = new List<SubtitleState>();
        coordinator.SubtitleChanged += (_, args) => subtitles.Add(args.State);
        await coordinator.StartAsync(Options, CancellationToken.None);
        var oldCallback = session.CaptureUpdateCallback();

        await coordinator.PauseAsync(CancellationToken.None);
        await coordinator.ResumeAsync(Options, CancellationToken.None);
        var before = subtitles.Count;
        oldCallback(new FinalUtteranceUpdate("stale", 1, "stale", "旧", "en-US"));
        oldCallback(new TranslationCanceledUpdate(
            "stale", 2, AppErrorCode.NetworkUnavailable));

        Assert.Equal(before, subtitles.Count);
        Assert.Equal(SessionState.Listening, coordinator.State);
        Assert.Equal(2, provider.CreateCount);
    }

    [Fact]
    public async Task AudioFaultStopsCaptureBeforeReportingFailure()
    {
        var audio = new FakeAudioSource();
        var provider = new FakeTranslationProvider();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new TranslationCoordinator(
            provider, _ => audio, (_, _) => Task.CompletedTask);
        coordinator.StateChanged += (_, args) =>
        {
            if (args.State == SessionState.Failed)
            {
                failed.TrySetResult();
            }
        };
        await coordinator.StartAsync(Options, CancellationToken.None);

        audio.EmitFault(new IOException("sensitive-original"));
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, audio.StopCount);
        Assert.Equal(1, provider.LastSession.StopCount);
    }

    [Fact]
    public async Task UnsupportedAudioFormatFailsImmediatelyWithSafeCategory()
    {
        await using var coordinator = new TranslationCoordinator(
            new FakeTranslationProvider(),
            _ => throw new NotSupportedException("sensitive-original"),
            (_, _) => Task.CompletedTask);
        AppError? reported = null;
        coordinator.StateChanged += (_, args) => reported = args.Error ?? reported;

        var failure = await Assert.ThrowsAsync<AppFailureException>(async () =>
            await coordinator.StartAsync(Options, CancellationToken.None));

        Assert.Equal(AppErrorCode.UnsupportedAudioFormat, failure.Error.Code);
        Assert.Equal(failure.Error, reported);
        Assert.Equal(SessionState.Failed, coordinator.State);
        Assert.DoesNotContain("sensitive-original", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupCleanupFailurePreservesOriginalErrorAndFailedState()
    {
        var session = new FakeTranslationSession
        {
            StartException = new IOException("network failed"),
            StopException = new InvalidOperationException("cleanup failed")
        };
        await using var coordinator = new TranslationCoordinator(
            new FakeTranslationProvider(session), _ => new FakeAudioSource(),
            (_, _) => Task.CompletedTask);

        var failure = await Assert.ThrowsAsync<AppFailureException>(async () =>
            await coordinator.StartAsync(Options, CancellationToken.None));

        Assert.Equal(AppErrorCode.NetworkUnavailable, failure.Error.Code);
        Assert.Equal(SessionState.Failed, coordinator.State);
        Assert.Equal(1, session.DisposeCount);
    }
}
