using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using NAudio.CoreAudioApi;
using RealTimeTranslation.Core;
using RealTimeTranslation.Desktop.Services;
using RealTimeTranslation.Desktop.ViewModels;
using RealTimeTranslation.Infrastructure.Audio;
using RealTimeTranslation.Infrastructure.LocalSpeech;
using RealTimeTranslation.Infrastructure.LocalTranslation;
using RealTimeTranslation.Infrastructure.Logging;
using RealTimeTranslation.Infrastructure.Settings;
using RealTimeTranslation.Infrastructure.Translation;

namespace RealTimeTranslation.Desktop;

public partial class App : Application, IDisposable
{
    private Mutex? singleton;
    private TranslationCoordinator? coordinator;
    private SubtitleViewModel? subtitleViewModel;
    private BundledWhisperModelStore? modelStore;
    private LocalTranslatorHost? translator;
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
            var bundle = Path.Combine(AppContext.BaseDirectory, "local-data");
            modelStore = new BundledWhisperModelStore(bundle);
            translator = new LocalTranslatorHost(
                Path.Combine(bundle, "python", "python.exe"),
                Path.Combine(bundle, "worker.py"),
                Path.Combine(bundle, "models"));
            var noAzureProvider = new NoAzureTranslationProvider(
                modelStore, translator, Path.Combine(bundle, "models", "silero_vad.onnx"));
            coordinator = new TranslationCoordinator(
                noAzureProvider,
                kind => kind switch
                {
                    AudioSourceKind.SystemAudio => new SystemLoopbackAudioSource(),
                    AudioSourceKind.Microphone => new MicrophoneAudioSource(),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                },
                static (delay, token) => Task.Delay(delay, token));
            var logger = new SafeRollingFileLogger();
            coordinator.StateChanged += (_, args) => logger.Log(
                "session." + args.State.ToString().ToLowerInvariant(),
                args.Error?.Code ?? AppErrorCode.Unknown,
                DateTimeOffset.UtcNow);
            var orchestrator = new WindowOrchestrator();
            var model = new SettingsViewModel(
                new JsonAppSettingsStore(),
                modelStore,
                async (options, token) =>
                {
                    await translator.PrepareAsync(token);
                    await coordinator.StartAsync(options, token);
                },
                orchestrator,
                GetCurrentDeviceName,
                localTranslation: true);
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SettingsViewModel.ModelReady) && model.ModelReady)
                {
                    _ = Task.Run(() => PrepareRecognizerAsync(noAzureProvider));
                }
            };
            var window = new SettingsWindow { DataContext = model };
            subtitleViewModel = new SubtitleViewModel(
                new CoordinatorSessionController(coordinator),
                new WpfUiDispatcher(Dispatcher),
                orchestrator,
                () => model.ActiveOptions,
                model.MarkStopped,
                localTranslation: true);
            var subtitles = new SubtitleWindow(model) { DataContext = subtitleViewModel };
            orchestrator.AttachWindows(window, subtitles);
            MainWindow = window;
            window.Show();
            await model.InitializeAsync(CancellationToken.None);
            subtitles.RestorePosition();
        }
        catch (Exception exception)
        {
            Trace.TraceError("Desktop startup failed: {0}", exception.GetType().Name);
            MessageBox.Show("无法启动实时翻译设置窗口。", "实时翻译",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static async Task PrepareRecognizerAsync(NoAzureTranslationProvider provider)
    {
        try
        {
            await provider.PrepareAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Background recognizer preparation failed: {0}",
                exception.GetType().Name);
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
            modelStore?.Dispose();
            if (translator is not null)
                Task.Run(() => translator.DisposeAsync().AsTask()).GetAwaiter().GetResult();
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

