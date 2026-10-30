using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using RealTimeTranslation.Desktop.ViewModels;

namespace RealTimeTranslation.Desktop;

public partial class SubtitleWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const long NoActivateStyle = 0x08000000;
    private const long ToolWindowStyle = 0x00000080;
    private readonly SettingsViewModel settings;

    public SubtitleWindow(SettingsViewModel settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
    }

    public void RestorePosition()
    {
        if (settings.HasSavedSubtitlePosition)
        {
            Left = settings.SubtitleLeft;
            Top = settings.SubtitleTop;
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Bottom - Height - 32;
    }

    private void OnSourceInitialized(object? sender, EventArgs args)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        SetWindowLongPtr(handle, ExtendedStyleIndex,
            new IntPtr(styles | NoActivateStyle | ToolWindowStyle));
    }

    private void OnMouseEnter(object sender, MouseEventArgs args)
    {
        if (DataContext is SubtitleViewModel model)
        {
            model.AreControlsVisible = true;
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs args)
    {
        if (DataContext is SubtitleViewModel model)
        {
            model.AreControlsVisible = false;
        }
    }

    private async void OnDragAreaMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (args.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            await settings.SaveSubtitlePositionAsync(Left, Top, CancellationToken.None);
        }
        catch (Exception)
        {
            // A failed drag or settings write must not interrupt translation.
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
}
