using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;
using RealTimeTranslation.Infrastructure.LocalSpeech;

namespace RealTimeTranslation.Desktop.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore settingsStore;
    private readonly ICredentialStore? credentialStore;
    private readonly ITranslationConnectionTester? connectionTester;
    private readonly IWhisperModelStore? modelStore;
    private readonly Func<StartTranslationOptions, CancellationToken, ValueTask> startTranslation;
    private readonly IWindowOrchestrator windowOrchestrator;
    private readonly Func<AudioSourceKind, string> deviceNameProvider;
    private readonly AsyncCommand testConnectionCommand;
    private readonly AsyncCommand startTranslationCommand;
    private readonly AsyncCommand deleteCredentialCommand;
    private readonly AsyncCommand downloadModelCommand;
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
    private bool modelReady;
    private bool hasTextSharingConsent;
    private double modelProgress;

    public SettingsViewModel(
        IAppSettingsStore settingsStore,
        ICredentialStore credentialStore,
        ITranslationConnectionTester connectionTester,
        Func<StartTranslationOptions, CancellationToken, ValueTask> startTranslation,
        IWindowOrchestrator windowOrchestrator,
        Func<AudioSourceKind, string> deviceNameProvider)
        : this(settingsStore, credentialStore, connectionTester, null,
            startTranslation, windowOrchestrator, deviceNameProvider)
    {
    }

    public SettingsViewModel(
        IAppSettingsStore settingsStore,
        IWhisperModelStore modelStore,
        Func<StartTranslationOptions, CancellationToken, ValueTask> startTranslation,
        IWindowOrchestrator windowOrchestrator,
        Func<AudioSourceKind, string> deviceNameProvider)
        : this(settingsStore, null, null, modelStore,
            startTranslation, windowOrchestrator, deviceNameProvider)
    {
    }

    private SettingsViewModel(
        IAppSettingsStore settingsStore,
        ICredentialStore? credentialStore,
        ITranslationConnectionTester? connectionTester,
        IWhisperModelStore? modelStore,
        Func<StartTranslationOptions, CancellationToken, ValueTask> startTranslation,
        IWindowOrchestrator windowOrchestrator,
        Func<AudioSourceKind, string> deviceNameProvider)
    {
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.credentialStore = credentialStore;
        this.connectionTester = connectionTester;
        this.modelStore = modelStore;
        this.startTranslation = startTranslation ?? throw new ArgumentNullException(nameof(startTranslation));
        this.windowOrchestrator = windowOrchestrator ?? throw new ArgumentNullException(nameof(windowOrchestrator));
        this.deviceNameProvider = deviceNameProvider ?? throw new ArgumentNullException(nameof(deviceNameProvider));
        currentDeviceName = this.deviceNameProvider(selectedAudioSource);
        testConnectionCommand = new AsyncCommand(() => TestConnectionAsync(CancellationToken.None),
            () => !IsNoAzureMode && !IsBusy && !sessionStarted && HasValidEndpoint
                && !string.IsNullOrWhiteSpace(ApiKey));
        startTranslationCommand = new AsyncCommand(() => StartTranslationAsync(CancellationToken.None),
            () => CanStart);
        deleteCredentialCommand = new AsyncCommand(() => DeleteCredentialAsync(CancellationToken.None),
            () => !IsNoAzureMode && !IsBusy && !sessionStarted && !string.IsNullOrEmpty(ApiKey));
        downloadModelCommand = new AsyncCommand(() => DownloadModelAsync(CancellationToken.None),
            () => IsNoAzureMode && !IsBusy && !sessionStarted && !ModelReady);
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

    public bool IsNoAzureMode => modelStore is not null;

    public bool ModelReady
    {
        get => modelReady;
        private set
        {
            if (SetField(ref modelReady, value))
            {
                RefreshCommands();
            }
        }
    }

    public double ModelProgress
    {
        get => modelProgress;
        private set => SetField(ref modelProgress, value);
    }

    public bool HasTextSharingConsent
    {
        get => hasTextSharingConsent;
        set
        {
            if (SetField(ref hasTextSharingConsent, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool CanStart => !IsBusy && !sessionStarted
        && windowOrchestrator.CanShowSubtitles
        && Enum.IsDefined(SelectedAudioSource)
        && Enum.IsDefined(SelectedTargetLanguage)
        && (IsNoAzureMode
            ? ModelReady && HasTextSharingConsent
            : credentialVerified && HasValidEndpoint && !string.IsNullOrWhiteSpace(ApiKey));

    public ICommand TestConnectionCommand => testConnectionCommand;

    public ICommand StartTranslationCommand => startTranslationCommand;

    public ICommand DeleteCredentialCommand => deleteCredentialCommand;
    public ICommand DownloadModelCommand => downloadModelCommand;

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
                Endpoint = IsNoAzureMode ? string.Empty : settings.Endpoint;
                SelectedAudioSource = settings.AudioSource;
                SelectedTargetLanguage = settings.TargetLanguage;
                subtitleLeft = settings.SubtitleLeft;
                subtitleTop = settings.SubtitleTop;
                hasSavedSubtitlePosition = settings.SubtitlePositionSaved
                    || subtitleLeft != 0 || subtitleTop != 0;
            }

            if (modelStore is not null)
            {
                ModelReady = await modelStore.VerifyCachedAsync(cancellationToken) is not null;
                ModelProgress = ModelReady ? 100 : 0;
                StatusMessage = ModelReady ? "本机语音模型已就绪。请确认文字发送说明。"
                    : "请先下载语音模型，再确认文字发送说明。";
            }
            else
            {
                ApiKey = await credentialStore!.ReadApiKeyAsync(cancellationToken) ?? string.Empty;
                credentialVerified = HasValidEndpoint && !string.IsNullOrWhiteSpace(ApiKey);
                StatusMessage = credentialVerified ? "配置已加载。"
                    : "请填写 Azure Endpoint 和 API Key，并测试连接。";
            }
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

    public async Task DownloadModelAsync(CancellationToken cancellationToken)
    {
        if (modelStore is null || IsBusy || sessionStarted || ModelReady)
        {
            return;
        }

        IsBusy = true;
        ModelProgress = 0;
        StatusMessage = "正在下载并校验本机语音模型…";
        try
        {
            var progress = new Progress<double>(value => ModelProgress = Math.Clamp(value * 100, 0, 100));
            await modelStore.EnsureAvailableAsync(progress, cancellationToken);
            ModelReady = true;
            ModelProgress = 100;
            StatusMessage = "语音模型已就绪。请确认文字发送说明。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "语音模型下载已取消。";
        }
        catch (Exception)
        {
            StatusMessage = "语音模型下载或校验失败，请检查网络和磁盘空间后重试。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        if (connectionTester is null || credentialStore is null)
        {
            return;
        }
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
        catch (AppFailureException exception) when (!cancellationToken.IsCancellationRequested)
        {
            StatusMessage = exception.Error.UserMessage;
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
        StatusMessage = IsNoAzureMode ? "正在准备识别引擎并启动翻译（首次约需 20 秒）…" : "正在连接翻译服务…";
        var selectedEndpoint = IsNoAzureMode ? string.Empty : Endpoint;
        var selectedKey = IsNoAzureMode ? string.Empty : ApiKey;
        var selectedSource = SelectedAudioSource;
        var selectedTarget = SelectedTargetLanguage;
        var subtitlesShown = false;
        try
        {
            await settingsStore.SaveAsync(new AppSettings(
                selectedEndpoint, selectedSource, selectedTarget, subtitleLeft, subtitleTop,
                hasSavedSubtitlePosition),
                cancellationToken);
            if ((!IsNoAzureMode && (Endpoint != selectedEndpoint || ApiKey != selectedKey
                    || !credentialVerified))
                || (IsNoAzureMode && (!ModelReady || !HasTextSharingConsent))
                || SelectedAudioSource != selectedSource || SelectedTargetLanguage != selectedTarget
                || !windowOrchestrator.CanShowSubtitles)
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
        catch (AppFailureException exception) when (!cancellationToken.IsCancellationRequested)
        {
            ActiveOptions = null;
            if (subtitlesShown)
            {
                windowOrchestrator.ShowSettings();
            }

            StatusMessage = exception.Error.UserMessage;
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
        if (credentialStore is null || IsBusy || sessionStarted)
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
            IsNoAzureMode ? string.Empty : Endpoint,
            SelectedAudioSource, SelectedTargetLanguage, left, top, true),
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
        downloadModelCommand.RaiseCanExecuteChanged();
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
