using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;
using RealTimeTranslation.Desktop.ViewModels;

namespace RealTimeTranslation.Desktop.Tests;

public sealed class SubtitleViewModelTests
{
    private static readonly StartTranslationOptions Options = new(
        "https://speech.example.com", "test-key", AudioSourceKind.SystemAudio,
        TargetLanguage.SimplifiedChinese);

    [Fact]
    public void PartialOriginalUsesPartialVisualState()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        var partial = SubtitleState.Empty.Apply(
            new PartialTranscriptUpdate("utterance-1", 1, "Hello"));

        fixture.Controller.EmitSubtitle(partial);

        Assert.Equal("Hello", model.OriginalText);
        Assert.True(model.IsOriginalPartial);
        Assert.Equal(string.Empty, model.TranslatedText);
        Assert.Equal(1, fixture.Dispatcher.CallCount);
    }

    [Fact]
    public void FailedStateShowsSafeCategorizedMessage()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();

        fixture.Controller.SetState(SessionState.Failed,
            AppError.For(AppErrorCode.AuthenticationFailed));

        Assert.Equal(AppError.For(AppErrorCode.AuthenticationFailed).UserMessage,
            model.ConnectionStatus);
    }

    [Fact]
    public void SameDetectedAndTargetLanguageShowsNotice()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        var final = SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "utterance-1", 1, "你好", "你好", "zh-CN"));

        fixture.Controller.EmitSubtitle(final);

        Assert.Equal("你好", model.OriginalText);
        Assert.Equal("与目标语言相同", model.TranslatedText);
        Assert.False(model.IsOriginalPartial);
    }

    [Fact]
    public void ShortLanguageCodeAlsoShowsSameLanguageNotice()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "utterance-1", 1, "你好", "你好", "zh")));

        Assert.Equal("与目标语言相同", model.TranslatedText);
    }

    [Fact]
    public void BacklogWarningIsVisibleWithoutClearingSubtitle()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "utterance-1", 1, "Hello", "你好", "en")));

        fixture.Controller.EmitWarning(new TranslationWarningUpdate(
            "dropped", 2, AppErrorCode.AudioBacklog));

        Assert.Equal(AppError.For(AppErrorCode.AudioBacklog).UserMessage, model.ConnectionStatus);
        Assert.Equal("你好", model.TranslatedText);
    }

    [Theory]
    [InlineData(AppErrorCode.NetworkUnavailable)]
    [InlineData(AppErrorCode.RateLimited)]
    public void OnlineTranslationFailureIsVisible(AppErrorCode code)
    {
        var fixture = new Fixture();
        using var model = fixture.Create();

        fixture.Controller.EmitWarning(new TranslationWarningUpdate("failed", 1, code));

        Assert.Equal(AppError.For(code).UserMessage, model.ConnectionStatus);
    }

    [Fact]
    public void SuccessfulTranslationClearsPreviousNetworkWarning()
    {
        var fixture = new Fixture();
        fixture.ActiveOptions = new StartTranslationOptions(
            "", "", AudioSourceKind.SystemAudio, TargetLanguage.SimplifiedChinese);
        fixture.Controller.SetState(SessionState.Listening);
        using var model = fixture.Create();
        fixture.Controller.EmitWarning(new TranslationWarningUpdate(
            "failed", 1, AppErrorCode.NetworkUnavailable));

        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(
            new FinalUtteranceUpdate("recovered", 2, "Hello", "你好", "en")));

        Assert.Equal("本机识别／联网翻译中", model.ConnectionStatus);
        Assert.Equal("你好", model.TranslatedText);
    }

    [Fact]
    public void SameLanguageUtteranceDoesNotClearNetworkWarning()
    {
        var fixture = new Fixture();
        fixture.ActiveOptions = new StartTranslationOptions(
            "", "", AudioSourceKind.SystemAudio, TargetLanguage.SimplifiedChinese);
        fixture.Controller.SetState(SessionState.Listening);
        using var model = fixture.Create();
        fixture.Controller.EmitWarning(new TranslationWarningUpdate(
            "failed", 1, AppErrorCode.NetworkUnavailable));

        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(
            new FinalUtteranceUpdate("same-language", 2, "你好", "你好", "zh")));

        Assert.Equal(AppError.For(AppErrorCode.NetworkUnavailable).UserMessage,
            model.ConnectionStatus);
        Assert.Equal("与目标语言相同", model.TranslatedText);
    }

    [Fact]
    public void NoAzureListeningStateNamesLocalRecognitionAndOnlineTranslation()
    {
        var fixture = new Fixture();
        fixture.ActiveOptions = new StartTranslationOptions(
            "", "", AudioSourceKind.SystemAudio, TargetLanguage.SimplifiedChinese);
        fixture.Controller.SetState(SessionState.Listening);

        using var model = fixture.Create();

        Assert.Contains("本机识别", model.ConnectionStatus);
        Assert.Contains("联网翻译", model.ConnectionStatus);
    }

    [Fact]
    public async Task PauseCallsCoordinatorOnceAndChangesLabelToResume()
    {
        var fixture = new Fixture();
        fixture.Controller.SetState(SessionState.Listening);
        using var model = fixture.Create();

        await model.PauseResumeAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Controller.PauseCount);
        Assert.Equal("恢复", model.PauseButtonText);
        Assert.Equal("已暂停", model.ConnectionStatus);
    }

    [Fact]
    public async Task PauseDuringReconnectCancelsRetry()
    {
        var fixture = new Fixture();
        fixture.Controller.SetState(SessionState.Reconnecting);
        using var model = fixture.Create();

        Assert.True(model.PauseResumeCommand.CanExecute(null));
        await model.PauseResumeAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Controller.PauseCount);
    }

    [Fact]
    public void NewSessionClearsOldSubtitleText()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(
            new FinalUtteranceUpdate("old", 4, "old", "旧", "en-US")));

        fixture.Controller.EmitSubtitle(SubtitleState.Empty);

        Assert.Equal(string.Empty, model.OriginalText);
        Assert.Equal(string.Empty, model.TranslatedText);
    }

    [Fact]
    public void LateSubtitleAfterStopDoesNotReadClearedOptions()
    {
        var fixture = new Fixture();
        using var model = fixture.Create();
        fixture.ActiveOptions = null;

        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(
            new FinalUtteranceUpdate("late", 1, "late", "迟到", "en-US")));

        Assert.Equal(string.Empty, model.OriginalText);
    }

    [Fact]
    public async Task ResumeKeepsReconnectingStatusDuringConnect()
    {
        var fixture = new Fixture();
        fixture.Controller.SetState(SessionState.Paused);
        fixture.Controller.ResumeGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = fixture.Create();

        var resuming = model.PauseResumeAsync(CancellationToken.None);
        Assert.Equal("重新连接中", model.ConnectionStatus);

        fixture.Controller.ResumeGate.TrySetResult();
        await resuming;
        Assert.Equal("正在聆听", model.ConnectionStatus);
    }

    [Fact]
    public async Task ResumeUsesActiveSessionOptions()
    {
        var fixture = new Fixture();
        fixture.Controller.SetState(SessionState.Paused);
        using var model = fixture.Create();

        await model.PauseResumeAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Controller.ResumeCount);
        Assert.Equal(Options, fixture.Controller.ResumedOptions);
    }

    [Fact]
    public async Task ReturnToSettingsStopsBeforeSwitchingWindows()
    {
        var fixture = new Fixture();
        fixture.Controller.StopGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = fixture.Create();

        var returning = model.ReturnToSettingsAsync(CancellationToken.None);
        Assert.Equal(1, fixture.Controller.StopCount);
        Assert.Equal(0, fixture.Windows.SettingsCount);

        fixture.Controller.StopGate.TrySetResult();
        await returning;
        Assert.Equal(1, fixture.Windows.SettingsCount);
        Assert.Equal(1, fixture.StoppedCount);
    }

    [Fact]
    public async Task CloseStopsBeforeRequestingApplicationShutdown()
    {
        var fixture = new Fixture();
        fixture.Controller.StopGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = fixture.Create();

        var closing = model.CloseAsync(CancellationToken.None);
        Assert.Equal(1, fixture.Controller.StopCount);
        Assert.Equal(0, fixture.Windows.ShutdownCount);

        fixture.Controller.StopGate.TrySetResult();
        await closing;
        Assert.Equal(1, fixture.Windows.ShutdownCount);
    }

    [Fact]
    public void DisposeUnsubscribesFromCoordinatorEvents()
    {
        var fixture = new Fixture();
        var model = fixture.Create();
        model.Dispose();

        fixture.Controller.EmitSubtitle(SubtitleState.Empty.Apply(
            new PartialTranscriptUpdate("utterance-1", 1, "Later")));

        Assert.Equal(string.Empty, model.OriginalText);
        Assert.Equal(0, fixture.Dispatcher.CallCount);
    }

    private sealed class Fixture
    {
        public FakeController Controller { get; } = new();
        public FakeDispatcher Dispatcher { get; } = new();
        public FakeWindows Windows { get; } = new();
        public StartTranslationOptions? ActiveOptions { get; set; } = Options;
        public int StoppedCount { get; private set; }

        public SubtitleViewModel Create() => new(
            Controller, Dispatcher, Windows, () => ActiveOptions, () => StoppedCount++);
    }

    private sealed class FakeController : ITranslationSessionController
    {
        public SessionState State { get; private set; } = SessionState.Idle;
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int StopCount { get; private set; }
        public StartTranslationOptions? ResumedOptions { get; private set; }
        public TaskCompletionSource? StopGate { get; set; }
        public TaskCompletionSource? ResumeGate { get; set; }
        public event EventHandler<CoordinatorStateChangedEventArgs>? StateChanged;
        public event EventHandler<SubtitleChangedEventArgs>? SubtitleChanged;
        public event EventHandler<TranslationUpdateEventArgs>? WarningReceived;

        public void SetState(SessionState state, AppError? error = null)
        {
            State = state;
            StateChanged?.Invoke(this, new CoordinatorStateChangedEventArgs(state, error));
        }

        public void EmitSubtitle(SubtitleState state) =>
            SubtitleChanged?.Invoke(this, new SubtitleChangedEventArgs(state));

        public void EmitWarning(TranslationWarningUpdate warning) =>
            WarningReceived?.Invoke(this, new TranslationUpdateEventArgs(warning));

        public ValueTask PauseAsync(CancellationToken cancellationToken)
        {
            PauseCount++;
            SetState(SessionState.Paused);
            return ValueTask.CompletedTask;
        }

        public async ValueTask ResumeAsync(StartTranslationOptions options, CancellationToken cancellationToken)
        {
            ResumeCount++;
            ResumedOptions = options;
            SetState(SessionState.Connecting);
            if (ResumeGate is not null)
            {
                await ResumeGate.Task.WaitAsync(cancellationToken);
            }

            SetState(SessionState.Listening);
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            if (StopGate is not null)
            {
                await StopGate.Task.WaitAsync(cancellationToken);
            }

            SetState(SessionState.Idle);
        }
    }

    private sealed class FakeDispatcher : IUiDispatcher
    {
        public int CallCount { get; private set; }
        public void Invoke(Action action)
        {
            CallCount++;
            action();
        }
    }

    private sealed class FakeWindows : IWindowOrchestrator
    {
        public bool CanShowSubtitles => true;
        public int SettingsCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public void ShowSubtitles() { }
        public void ShowSettings() => SettingsCount++;
        public void ShutdownApplication() => ShutdownCount++;
    }
}
