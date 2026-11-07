using RealTimeTranslation.Core;

namespace RealTimeTranslation.Desktop.Services;

public interface ITranslationSessionController
{
    SessionState State { get; }

    event EventHandler<CoordinatorStateChangedEventArgs>? StateChanged;

    event EventHandler<SubtitleChangedEventArgs>? SubtitleChanged;

    event EventHandler<TranslationUpdateEventArgs>? WarningReceived;

    ValueTask PauseAsync(CancellationToken cancellationToken);

    ValueTask ResumeAsync(StartTranslationOptions options, CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}

public sealed class CoordinatorSessionController(TranslationCoordinator coordinator)
    : ITranslationSessionController
{
    public SessionState State => coordinator.State;

    public event EventHandler<CoordinatorStateChangedEventArgs>? StateChanged
    {
        add => coordinator.StateChanged += value;
        remove => coordinator.StateChanged -= value;
    }

    public event EventHandler<SubtitleChangedEventArgs>? SubtitleChanged
    {
        add => coordinator.SubtitleChanged += value;
        remove => coordinator.SubtitleChanged -= value;
    }

    public event EventHandler<TranslationUpdateEventArgs>? WarningReceived
    {
        add => coordinator.WarningReceived += value;
        remove => coordinator.WarningReceived -= value;
    }

    public ValueTask PauseAsync(CancellationToken cancellationToken) =>
        coordinator.PauseAsync(cancellationToken);

    public ValueTask ResumeAsync(StartTranslationOptions options, CancellationToken cancellationToken) =>
        coordinator.ResumeAsync(options, cancellationToken);

    public ValueTask StopAsync(CancellationToken cancellationToken) =>
        coordinator.StopAsync(cancellationToken);
}
