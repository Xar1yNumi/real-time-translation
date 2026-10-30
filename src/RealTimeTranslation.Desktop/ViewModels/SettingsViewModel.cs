using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;

namespace RealTimeTranslation.Desktop.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore settingsStore;
    private readonly ICredentialStore credentialStore;
    private readonly ITranslationConnectionTester connectionTester;
    private readonly Func<StartTranslationOptions, CancellationToken, ValueTask> startTranslation;
    private readonly IWindowOrchestrator windowOrchestrator;
    private readonly Func<AudioSourceKind, string> deviceNameProvider;
    private readonly AsyncCommand testConnectionCommand;
    private readonly AsyncCommand startTranslationCommand;
    private readonly AsyncCommand deleteCredentialCommand;
    private string endpoint = string.Empty;
    private string apiKey = string.Empty;
    private string currentDeviceName = string.Empty;
    private string statusMessage = string.Empty;
    private AudioSourceKind selectedAudioSource = AudioSourceKind.SystemAudio;
    private TargetLanguage selectedTargetLanguage = TargetLanguage.SimplifiedChinese;
    private double subtitleLeft;
    private double subtitleTop;
    private bool hasSavedSubtitlePosition;
    private bool credentialVerified;
    private bool isBusy;
    private bool sessionStarted;

    public SettingsViewModel(
        IAppSettingsStore settingsStore,
        ICredentialStore credentialStore,
        ITranslationConnectionTester connectionTester,
        Func<StartTranslationOptions, CancellationToken, ValueTask> startTranslation,
        IWindowOrchestrator windowOrchestrator,
        Func<AudioSourceKind, string> deviceNameProvider)
    {
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.connectionTester = connectionTester ?? throw new ArgumentNullException(nameof(connectionTester));
        this.startTranslation = startTranslation ?? throw new ArgumentNullException(nameof(startTranslation));
        this.windowOrchestrator = windowOrchestrator ?? throw new ArgumentNullException(nameof(windowOrchestrator));
        this.deviceNameProvider = deviceNameProvider ?? throw new ArgumentNullException(nameof(deviceNameProvider));
        currentDeviceName = this.deviceNameProvider(selectedAudioSource);
        testConnectionCommand = new AsyncCommand(() => TestConnectionAsync(CancellationToken.None),
            () => !IsBusy && !sessionStarted && HasValidEndpoint
                && !string.IsNullOrWhiteSpace(ApiKey));
        startTranslationCommand = new AsyncCommand(() => StartTranslationAsync(CancellationToken.None),
            () => CanStart);
        deleteCredentialCommand = new AsyncCommand(() => DeleteCredentialAsync(CancellationToken.None),
            () => !IsBusy && !sessionStarted && !string.IsNullOrEmpty(ApiKey));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Endpoint
    {
        get => endpoint;
        set
        {
            if (SetField(ref endpoint, value ?? string.Empty))
            {
                credentialVerified = false;
                RefreshCommands();
            }
        }
    }

    public string ApiKey
    {
        get => apiKey;
        set
        {
            if (SetField(ref apiKey, value ?? string.Empty))
            {
                credentialVerified = false;
                RefreshCommands();
            }
        }
    }

    public AudioSourceKind SelectedAudioSource
    {
        get => selectedAudioSource;
        set
        {
            if (SetField(ref selectedAudioSource, value))
            {
                CurrentDeviceName = Enum.IsDefined(value)
                    ? deviceNameProvider(value) : string.Empty;
                OnPropertyChanged(nameof(IsSystemAudioSelected));
                OnPropertyChanged(nameof(IsMicrophoneSelected));
                RefreshCommands();
            }
        }
    }

    public bool IsSystemAudioSelected
    {
        get => SelectedAudioSource == AudioSourceKind.SystemAudio;
        set
        {
            if (value)
            {
                SelectedAudioSource = AudioSourceKind.SystemAudio;
            }
        }
    }

    public bool IsMicrophoneSelected
    {
        get => SelectedAudioSource == AudioSourceKind.Microphone;
        set
        {
            if (value)
            {
                SelectedAudioSource = AudioSourceKind.Microphone;
            }
        }
    }

    public TargetLanguage SelectedTargetLanguage
    {
        get => selectedTargetLanguage;
        set
        {
            if (SetField(ref selectedTargetLanguage, value))
            {
                RefreshCommands();
            }
        }
    }

    public string CurrentDeviceName
    {
        get => currentDeviceName;
        private set => SetField(ref currentDeviceName, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool CanStart => !IsBusy && !sessionStarted && credentialVerified
        && windowOrchestrator.CanShowSubtitles
        && HasValidEndpoint && !string.IsNullOrWhiteSpace(ApiKey)
        && Enum.IsDefined(SelectedAudioSource)
        && Enum.IsDefined(SelectedTargetLanguage);

    public ICommand TestConnectionCommand => testConnectionCommand;

    public ICommand StartTranslationCommand => startTranslationCommand;

    public ICommand DeleteCredentialCommand => deleteCredentialCommand;

    public StartTranslationOptions? ActiveOptions { get; private set; }

    public double SubtitleLeft => subtitleLeft;

    public double SubtitleTop => subtitleTop;

    public bool HasSavedSubtitlePosition => hasSavedSubtitlePosition;

    private bool HasValidEndpoint => Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsBusy = true;
            var settings = await settingsStore.LoadAsync(cancellationToken);
            if (settings is not null)
            {
                Endpoint = settings.Endpoint;
                SelectedAudioSource = settings.AudioSource;
                SelectedTargetLanguage = settings.TargetLanguage;
                subtitleLeft = settings.SubtitleLeft;
                subtitleTop = settings.SubtitleTop;
                hasSavedSubtitlePosition = settings.SubtitlePositionSaved
                    || subtitleLeft != 0 || subtitleTop != 0;
            }

            ApiKey = await credentialStore.ReadApiKeyAsync(cancellationToken) ?? string.Empty;
            credentialVerified = HasValidEndpoint && !string.IsNullOrWhiteSpace(ApiKey);
            StatusMessage = credentialVerified ? "配置已加载。"
                : "请填写 Azure Endpoint 和 API Key，并测试连接。";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "读取设置失败，请检查本机配置。";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || sessionStarted || !HasValidEndpoint || string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "请填写有效的 HTTPS Endpoint 和 API Key。";
            return;
        }

        IsBusy = true;
        credentialVerified = false;
        RefreshCommands();
        StatusMessage = "正在测试 Azure 连接…";
        var testedEndpoint = Endpoint;
        var testedKey = ApiKey;
        try
        {
            await connectionTester.TestAsync(testedEndpoint, testedKey, cancellationToken);
            if (Endpoint != testedEndpoint || ApiKey != testedKey)
            {
                StatusMessage = "配置已更改，请重新测试连接。";
                return;
            }

            await credentialStore.WriteApiKeyAsync(testedKey, cancellationToken);
            if (Endpoint != testedEndpoint || ApiKey != testedKey)
            {
                StatusMessage = "配置已更改，请重新测试连接。";
                return;
            }

            credentialVerified = true;
            StatusMessage = "连接测试通过，凭据已保存。";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "连接测试失败，请检查 Endpoint、密钥和网络。";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    public async Task StartTranslationAsync(CancellationToken cancellationToken)
    {
        if (!CanStart)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在连接翻译服务…";
        var selectedEndpoint = Endpoint;
        var selectedKey = ApiKey;
        var selectedSource = SelectedAudioSource;
        var selectedTarget = SelectedTargetLanguage;
        var subtitlesShown = false;
        try
        {
            await settingsStore.SaveAsync(new AppSettings(
                selectedEndpoint, selectedSource, selectedTarget, subtitleLeft, subtitleTop,
                hasSavedSubtitlePosition),
                cancellationToken);
            if (Endpoint != selectedEndpoint || ApiKey != selectedKey
                || SelectedAudioSource != selectedSource || SelectedTargetLanguage != selectedTarget
                || !credentialVerified || !windowOrchestrator.CanShowSubtitles)
            {
                StatusMessage = "配置已更改，请重新点击开始翻译。";
                return;
            }

            var options = new StartTranslationOptions(
                selectedEndpoint, selectedKey, selectedSource, selectedTarget);
            ActiveOptions = options;
            windowOrchestrator.ShowSubtitles();
            subtitlesShown = true;
            await startTranslation(options, cancellationToken);
            sessionStarted = true;
            StatusMessage = "翻译已启动。";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ActiveOptions = null;
            if (subtitlesShown)
            {
                windowOrchestrator.ShowSettings();
            }

            StatusMessage = "启动翻译失败，请检查连接和音频设备。";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    public async Task DeleteCredentialAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || sessionStarted)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await credentialStore.DeleteApiKeyAsync(cancellationToken);
            ApiKey = string.Empty;
            credentialVerified = false;
            StatusMessage = "已删除本机保存的 Azure 凭据。";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "删除凭据失败。";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    public async Task SaveSubtitlePositionAsync(
        double left,
        double top,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            throw new ArgumentOutOfRangeException(nameof(left));
        }

        await settingsStore.SaveAsync(new AppSettings(
            Endpoint, SelectedAudioSource, SelectedTargetLanguage, left, top, true),
            cancellationToken);
        subtitleLeft = left;
        subtitleTop = top;
        hasSavedSubtitlePosition = true;
    }

    public void MarkStopped()
    {
        sessionStarted = false;
        ActiveOptions = null;
        StatusMessage = "翻译已停止。点击开始翻译后才会再次采集音频。";
        RefreshCommands();
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

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanStart));
        testConnectionCommand.RaiseCanExecuteChanged();
        startTranslationCommand.RaiseCanExecuteChanged();
        deleteCredentialCommand.RaiseCanExecuteChanged();
    }

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
                // The view model displays operation failures without exposing credentials.
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
