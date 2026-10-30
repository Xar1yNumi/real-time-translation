namespace RealTimeTranslation.Desktop.Services;

public interface IUiDispatcher
{
    void Invoke(Action action);
}
