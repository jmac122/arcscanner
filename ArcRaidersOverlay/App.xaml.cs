using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace ArcRaidersOverlay;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private OverlayWindow? _overlayWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Check elevation status and warn if needed
        CheckElevationStatus();

        // Create and show the overlay window
        _overlayWindow = new OverlayWindow();
        _overlayWindow.Show();

        // Create the main window (hidden, handles tray icon)
        _mainWindow = new MainWindow(_overlayWindow);

        // Don't show the main window - it just manages the tray icon
    }

    private static void CheckElevationStatus()
    {
        var isElevated = IsRunningAsAdmin();
        var gameElevated = IsGameRunningElevated();

        Debug.WriteLine($"Overlay elevated: {isElevated}, Game elevated: {gameElevated}");

        // Warn if game is elevated but overlay isn't
        if (gameElevated && !isElevated)
        {
            var result = MessageBox.Show(
                "ARC Raiders appears to be running with administrator privileges, " +
                "but the overlay is not.\n\n" +
                "This may cause issues with:\n" +
                "- Screen capture not working\n" +
                "- Overlay not staying on top\n" +
                "- Hotkeys not registering\n\n" +
                "Would you like to restart the overlay as administrator?",
                "Elevation Mismatch Detected",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
        }
    }

    /// <summary>
    /// Checks if the current process is running with admin privileges.
    /// </summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the game process is running elevated.
    /// </summary>
    private static bool IsGameRunningElevated()
    {
        var gameProcessNames = GameProcessNames.Known;

        foreach (var name in gameProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var process in processes)
                {
                    using (process)
                    {
                        try
                        {
                            // Try to access the process - if we can't, it's likely elevated
                            // and we're not (or we don't have permission)
                            _ = process.MainModule;
                            return false; // We could access it, so it's not elevated (or we are too)
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            // Access denied - likely elevated
                            return true;
                        }
                        catch (Exception ex)
                        {
                            // Log other errors for debugging, but continue
                            Debug.WriteLine(
                                $"Error checking process '{process.ProcessName}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log errors for debugging
                Debug.WriteLine($"Error getting processes by name '{name}': {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Restarts the application with admin privileges.
    /// </summary>
    private static void RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas" // This triggers UAC prompt
            };

            Process.Start(startInfo);
            Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC prompt
            MessageBox.Show(
                "The overlay will continue running without admin privileges.\n" +
                "Some features may not work correctly with the game.",
                "Continuing Without Admin",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        _overlayWindow?.Dispose();
        base.OnExit(e);
    }
}
