using System.Windows;

namespace RealTimeTranslation.Desktop.Services;

public interface IWindowOrchestrator
{
    bool CanShowSubtitles { get; }

    void ShowSubtitles();

    void ShowSettings();

    void ShutdownApplication();
}

public sealed class WindowOrchestrator : IWindowOrchestrator
{
    private Window? settingsWindow;
    private Window? subtitleWindow;

    public bool CanShowSubtitles => settingsWindow is not null && subtitleWindow is not null;

    public void AttachWindows(Window settings, Window subtitles)
    {
        settingsWindow = settings ?? throw new ArgumentNullException(nameof(settings));
        subtitleWindow = subtitles ?? throw new ArgumentNullException(nameof(subtitles));
    }

    public void ShowSubtitles()
    {
        EnsureWindows();
        subtitleWindow!.Show();
        settingsWindow!.Hide();
    }

    public void ShowSettings()
    {
        EnsureWindows();
        settingsWindow!.Show();
        subtitleWindow!.Hide();
        settingsWindow.Activate();
    }

    public void ShutdownApplication() => Application.Current.Shutdown();

    private void EnsureWindows()
    {
        if (!CanShowSubtitles)
        {
            throw new InvalidOperationException("Subtitle window is not ready.");
        }
    }
}
