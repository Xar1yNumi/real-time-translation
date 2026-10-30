using System.Windows.Threading;

namespace RealTimeTranslation.Desktop.Services;

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Invoke(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
