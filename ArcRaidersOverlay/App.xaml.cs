using System.Windows;

namespace ArcRaidersOverlay;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private OverlayWindow? _overlayWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Create and show the overlay window
        _overlayWindow = new OverlayWindow();
        _overlayWindow.Show();

        // Create the main window (hidden, handles tray icon)
        _mainWindow = new MainWindow(_overlayWindow);

        // Don't show the main window - it just manages the tray icon
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        _overlayWindow?.Dispose();
        base.OnExit(e);
    }
}
