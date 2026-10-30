using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using NAudio.CoreAudioApi;
using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;
using RealTimeTranslation.Desktop.ViewModels;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.Azure;
using RealTimeTranslation.Infrastructure.Credentials;
using RealTimeTranslation.Infrastructure.Settings;

namespace RealTimeTranslation.Desktop;

public partial class App : Application, IDisposable
{
    private Mutex? singleton;
    private TranslationCoordinator? coordinator;
    private SubtitleViewModel? subtitleViewModel;
    private bool ownsSingleton;
    private bool disposed;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        singleton = new Mutex(true, "RealTimeTranslation.MVP.Singleton", out ownsSingleton);
        if (!ownsSingleton)
        {
            MessageBox.Show("实时翻译已经在运行", "实时翻译", MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            coordinator = new TranslationCoordinator(
                new AzureTranslationProvider(),
                kind => kind switch
                {
                    AudioSourceKind.SystemAudio => new SystemLoopbackAudioSource(),
                    AudioSourceKind.Microphone => new MicrophoneAudioSource(),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                },
                static (delay, token) => Task.Delay(delay, token));
            var orchestrator = new WindowOrchestrator();
            var model = new SettingsViewModel(
                new JsonAppSettingsStore(),
                new WindowsCredentialStore(),
                new AzureSpeechConnectionTester(),
                coordinator.StartAsync,
                orchestrator,
                GetCurrentDeviceName);
            var window = new SettingsWindow { DataContext = model };
            subtitleViewModel = new SubtitleViewModel(
                new CoordinatorSessionController(coordinator),
                new WpfUiDispatcher(Dispatcher),
                orchestrator,
                () => model.ActiveOptions,
                model.MarkStopped);
            var subtitles = new SubtitleWindow(model) { DataContext = subtitleViewModel };
            orchestrator.AttachWindows(window, subtitles);
            MainWindow = window;
            window.Show();
            await model.InitializeAsync(CancellationToken.None);
            subtitles.RestorePosition();
        }
        catch (Exception)
        {
            MessageBox.Show("无法启动实时翻译设置窗口。", "实时翻译",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Dispose();
        }
        catch (Exception exception)
        {
            Trace.TraceError("Translation shutdown cleanup failed: {0}",
                exception.GetType().Name);
        }
        finally
        {
            base.OnExit(e);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            subtitleViewModel?.Dispose();
            if (coordinator is not null)
            {
                Task.Run(() => coordinator.DisposeAsync().AsTask())
                    .GetAwaiter().GetResult();
            }
        }
        finally
        {
            if (ownsSingleton)
            {
                singleton?.ReleaseMutex();
            }

            singleton?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private static string GetCurrentDeviceName(AudioSourceKind kind)
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            var direction = kind == AudioSourceKind.SystemAudio
                ? DataFlow.Render : DataFlow.Capture;
            using var device = devices.GetDefaultAudioEndpoint(direction, Role.Multimedia);
            return device.FriendlyName;
        }
        catch (Exception)
        {
            return "默认音频设备不可用";
        }
    }
}

