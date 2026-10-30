using System.ComponentModel;
using System.Windows;
using RealTimeTranslation.Desktop.ViewModels;

namespace RealTimeTranslation.Desktop;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? observedViewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        observedViewModel = args.NewValue as SettingsViewModel;
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncPassword();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SettingsViewModel.ApiKey))
        {
            SyncPassword();
        }
    }

    private void SyncPassword()
    {
        if (observedViewModel is not null && ApiKeyBox.Password != observedViewModel.ApiKey)
        {
            ApiKeyBox.Password = observedViewModel.ApiKey;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs args)
    {
        if (observedViewModel is not null && observedViewModel.ApiKey != ApiKeyBox.Password)
        {
            observedViewModel.ApiKey = ApiKeyBox.Password;
        }
    }
}
