using System.Drawing;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;

namespace ArcRaidersOverlay;

public partial class MainWindow : Window, IDisposable
{
    private readonly OverlayWindow _overlayWindow;
    private bool _disposed;

    public ICommand ToggleOverlayCommand { get; }

    public MainWindow(OverlayWindow overlayWindow)
    {
        InitializeComponent();

        _overlayWindow = overlayWindow;
        ToggleOverlayCommand = new DelegateCommand(() => _overlayWindow.ToggleVisibility());
        DataContext = this;

        // Set a default icon if app.ico doesn't exist
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                TrayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Use system default icon
                TrayIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            TrayIcon.Icon = SystemIcons.Application;
        }

        // Show balloon tip on startup
        TrayIcon.ShowBalloonTip("ARC Raiders Overlay",
            "Overlay is running. Press Shift+S to scan items.",
            BalloonIcon.Info);
    }

    private void ShowHide_Click(object sender, RoutedEventArgs e)
    {
        _overlayWindow.ToggleVisibility();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        _overlayWindow.ForceUnlock();
        TrayIcon.ShowBalloonTip("Overlay Unlocked",
            "You can now drag the overlay. Click Lock to re-enable click-through.",
            BalloonIcon.Info);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _overlayWindow.OpenSettings();
    }

    private void ScanItem_Click(object sender, RoutedEventArgs e)
    {
        // This is informational - hotkey handles actual scanning
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "ARC Raiders Overlay v1.0\n\n" +
            "A lightweight OCR-based overlay tool for ARC Raiders.\n\n" +
            "Features:\n" +
            "  - Event timers with countdowns\n" +
            "  - Interactive minimap\n" +
            "  - Item scanner with tooltips\n\n" +
            "Hotkeys:\n" +
            "  Shift+S - Scan item under cursor\n\n" +
            "Based on the RatScanner architecture.",
            "About ARC Raiders Overlay",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        TrayIcon.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;

        public DelegateCommand(Action execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
