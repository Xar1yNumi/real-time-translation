namespace RealTimeTranslation.Desktop.Services;

public interface IWindowOrchestrator
{
    bool CanShowSubtitles { get; }

    void ShowSubtitles();
}

public sealed class WindowOrchestrator : IWindowOrchestrator
{
    private Action? showSubtitles;

    public bool CanShowSubtitles => showSubtitles is not null;

    public void RegisterSubtitleWindow(Action show) =>
        showSubtitles = show ?? throw new ArgumentNullException(nameof(show));

    public void ShowSubtitles() =>
        (showSubtitles ?? throw new InvalidOperationException("Subtitle window is not ready."))();
}
