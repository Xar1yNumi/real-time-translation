using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;

namespace RealTimeTranslation.Desktop.ViewModels;

public sealed class SubtitleViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ObservableCollection<SubtitleLine> history = [];
    private readonly Dictionary<string, int> historyIndex = new(StringComparer.Ordinal);
    private readonly ITranslationSessionController coordinator;
    private readonly IUiDispatcher dispatcher;
    private readonly IWindowOrchestrator windows;
    private readonly Func<StartTranslationOptions?> activeOptions;
    private readonly Action onStopped;
    private readonly AsyncCommand pauseResumeCommand;
    private readonly AsyncCommand returnToSettingsCommand;
    private readonly AsyncCommand closeCommand;
    private string originalText = string.Empty;
    private string translatedText = string.Empty;
    private string connectionStatus = string.Empty;
    private bool isOriginalPartial;
    private bool areControlsVisible;
    private bool isBusy;
    private bool disposed;
    private long lastSequence = -1;

    public SubtitleViewModel(
        ITranslationSessionController coordinator,
        IUiDispatcher dispatcher,
        IWindowOrchestrator windows,
        Func<StartTranslationOptions?> activeOptions,
        Action onStopped)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
        this.activeOptions = activeOptions ?? throw new ArgumentNullException(nameof(activeOptions));
        this.onStopped = onStopped ?? throw new ArgumentNullException(nameof(onStopped));
        History = new ReadOnlyObservableCollection<SubtitleLine>(history);
        connectionStatus = GetStatus(coordinator.State);
        pauseResumeCommand = new AsyncCommand(() => PauseResumeAsync(CancellationToken.None),
            () => !isBusy && coordinator.State is SessionState.Listening
                or SessionState.Reconnecting or SessionState.Paused);
        returnToSettingsCommand = new AsyncCommand(
            () => ReturnToSettingsAsync(CancellationToken.None), () => !isBusy);
        closeCommand = new AsyncCommand(() => CloseAsync(CancellationToken.None), () => !isBusy);
        coordinator.StateChanged += OnStateChanged;
        coordinator.SubtitleChanged += OnSubtitleChanged;
        coordinator.WarningReceived += OnWarningReceived;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<SubtitleLine> History { get; }

    public string OriginalText
    {
        get => originalText;
        private set => SetField(ref originalText, value);
    }

    public string TranslatedText
    {
        get => translatedText;
        private set => SetField(ref translatedText, value);
    }

    public bool IsOriginalPartial
    {
        get => isOriginalPartial;
        private set => SetField(ref isOriginalPartial, value);
    }

    public string ConnectionStatus
    {
        get => connectionStatus;
        private set => SetField(ref connectionStatus, value);
    }

    public bool AreControlsVisible
    {
        get => areControlsVisible;
        set => SetField(ref areControlsVisible, value);
    }

    public string PauseButtonText => coordinator.State == SessionState.Paused ? "恢复" : "暂停";

    public ICommand PauseResumeCommand => pauseResumeCommand;

    public ICommand ReturnToSettingsCommand => returnToSettingsCommand;

    public ICommand CloseCommand => closeCommand;

    public async Task PauseResumeAsync(CancellationToken cancellationToken)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        RefreshCommands();
        try
        {
            if (coordinator.State == SessionState.Paused)
            {
                ConnectionStatus = "重新连接中";
                await coordinator.ResumeAsync(activeOptions() ??
                    throw new InvalidOperationException("No active translation options."),
                    cancellationToken);
            }
            else if (coordinator.State is SessionState.Listening or SessionState.Reconnecting)
            {
                await coordinator.PauseAsync(cancellationToken);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ConnectionStatus = "操作失败，请返回设置后重试";
        }
        finally
        {
            isBusy = false;
            RefreshCommands();
        }
    }

    public async Task ReturnToSettingsAsync(CancellationToken cancellationToken)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        RefreshCommands();
        try
        {
            await coordinator.StopAsync(cancellationToken);
            onStopped();
            windows.ShowSettings();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ConnectionStatus = "停止翻译失败，请重试";
        }
        finally
        {
            isBusy = false;
            RefreshCommands();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        RefreshCommands();
        try
        {
            await coordinator.StopAsync(cancellationToken);
            onStopped();
            windows.ShutdownApplication();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ConnectionStatus = "停止翻译失败，请重试";
        }
        finally
        {
            isBusy = false;
            RefreshCommands();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        coordinator.StateChanged -= OnStateChanged;
        coordinator.SubtitleChanged -= OnSubtitleChanged;
        coordinator.WarningReceived -= OnWarningReceived;
        GC.SuppressFinalize(this);
    }

    private void OnStateChanged(object? sender, CoordinatorStateChangedEventArgs args) =>
        dispatcher.Invoke(() =>
        {
            if (disposed)
            {
                return;
            }

            ConnectionStatus = args.State == SessionState.Failed && args.Error is not null
                ? args.Error.UserMessage
                : args.State == SessionState.Connecting && isBusy
                    ? "重新连接中" : GetStatus(args.State);
            OnPropertyChanged(nameof(PauseButtonText));
            RefreshCommands();
        });

    private void OnSubtitleChanged(object? sender, SubtitleChangedEventArgs args) =>
        dispatcher.Invoke(() =>
        {
            if (disposed)
            {
                return;
            }

            if (args.State == SubtitleState.Empty)
            {
                history.Clear();
                historyIndex.Clear();
                lastSequence = -1;
                OriginalText = string.Empty;
                TranslatedText = string.Empty;
                IsOriginalPartial = false;
                return;
            }

            var options = activeOptions();
            if (options is null)
            {
                return;
            }

            if (args.State.Sequence < lastSequence)
            {
                return;
            }

            lastSequence = args.State.Sequence;

            OriginalText = args.State.OriginalText;
            TranslatedText = !args.State.IsOriginalPartial
                && IsSameLanguage(args.State.DetectedLanguage, options.TargetLanguage)
                ? "与目标语言相同" : args.State.TranslatedText;
            IsOriginalPartial = args.State.IsOriginalPartial;
            if (!args.State.IsOriginalPartial
                && !string.IsNullOrWhiteSpace(args.State.OriginalText)
                && !string.IsNullOrWhiteSpace(args.State.UtteranceId))
            {
                var line = new SubtitleLine(args.State.OriginalText, TranslatedText);
                if (historyIndex.TryGetValue(args.State.UtteranceId, out var index))
                {
                    if (history[index] != line)
                    {
                        history[index] = line;
                    }
                }
                else
                {
                    historyIndex.Add(args.State.UtteranceId, history.Count);
                    history.Add(line);
                }
            }
            if (!args.State.IsOriginalPartial
                && !IsSameLanguage(args.State.DetectedLanguage, options.TargetLanguage)
                && !string.IsNullOrWhiteSpace(args.State.TranslatedText))
            {
                ConnectionStatus = GetStatus(coordinator.State);
            }
        });

    private void OnWarningReceived(object? sender, TranslationUpdateEventArgs args) =>
        dispatcher.Invoke(() =>
        {
            if (!disposed && args.Update is TranslationWarningUpdate warning)
            {
                ConnectionStatus = AppError.For(warning.ErrorCode).UserMessage;
            }
        });

    private static bool IsSameLanguage(string detected, TargetLanguage target) => target switch
    {
        TargetLanguage.SimplifiedChinese =>
            string.Equals(detected, "zh-CN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(detected, "zh", StringComparison.OrdinalIgnoreCase),
        TargetLanguage.English =>
            string.Equals(detected, "en-US", StringComparison.OrdinalIgnoreCase)
            || string.Equals(detected, "en", StringComparison.OrdinalIgnoreCase),
        TargetLanguage.Russian =>
            string.Equals(detected, "ru-RU", StringComparison.OrdinalIgnoreCase)
            || string.Equals(detected, "ru", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private string GetStatus(SessionState state) => state switch
    {
        SessionState.Connecting => "连接中",
        SessionState.Listening when activeOptions()?.Endpoint == string.Empty =>
            "本机识别／联网翻译中",
        SessionState.Listening => "正在聆听",
        SessionState.Paused => "已暂停",
        SessionState.Reconnecting => "重新连接中",
        SessionState.Failed => "连接中断，请返回设置后重试",
        SessionState.Stopping => "正在停止",
        _ => "等待开始"
    };

    private void RefreshCommands()
    {
        pauseResumeCommand.RaiseCanExecuteChanged();
        returnToSettingsCommand.RaiseCanExecuteChanged();
        closeCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute();
        public async void Execute(object? parameter)
        {
            try
            {
                await execute();
            }
            catch (Exception)
            {
                // Command methods show user-safe status messages.
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record SubtitleLine(string OriginalText, string TranslatedText);
