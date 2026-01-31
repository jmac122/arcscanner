using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ArcRaidersOverlay;

/// <summary>
/// Detects the ARC Raiders game window and provides monitor/position information.
/// Supports multi-monitor setups and various resolutions.
/// </summary>
public class GameWindowDetector : IDisposable
{
    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    #endregion

    // Known window titles for ARC Raiders (may need updating)
    private static readonly string[] GameWindowTitles =
    {
        "ARC Raiders",
        "Arc Raiders",
        "ArcRaiders",
        "PioneerGame"
    };

    private IntPtr _gameWindowHandle;
    private readonly System.Windows.Threading.DispatcherTimer _pollTimer;
    private bool _disposed;

    /// <summary>
    /// Fired when the game window is detected or lost.
    /// </summary>
    public event EventHandler<GameWindowEventArgs>? GameWindowChanged;

    /// <summary>
    /// Current game window information, or null if not detected.
    /// </summary>
    public GameWindowInfo? CurrentWindow { get; private set; }

    /// <summary>
    /// Whether the game window is currently detected and visible.
    /// </summary>
    public bool IsGameRunning => CurrentWindow != null;

    public GameWindowDetector()
    {
        _pollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _pollTimer.Tick += OnPollTick;
    }

    /// <summary>
    /// Starts monitoring for the game window.
    /// </summary>
    public void StartMonitoring()
    {
        // Do an immediate check
        DetectGameWindow();
        _pollTimer.Start();
    }

    /// <summary>
    /// Stops monitoring for the game window.
    /// </summary>
    public void StopMonitoring()
    {
        _pollTimer.Stop();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        DetectGameWindow();
    }

    /// <summary>
    /// Manually triggers game window detection.
    /// </summary>
    public void DetectGameWindow()
    {
        var hwnd = FindGameWindow();

        if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
        {
            if (hwnd != _gameWindowHandle)
            {
                _gameWindowHandle = hwnd;
                UpdateWindowInfo();
                GameWindowChanged?.Invoke(this, new GameWindowEventArgs(CurrentWindow, true));
            }
            else
            {
                // Same window, just update position info
                UpdateWindowInfo();
            }
        }
        else if (_gameWindowHandle != IntPtr.Zero)
        {
            // Game window was lost
            _gameWindowHandle = IntPtr.Zero;
            CurrentWindow = null;
            GameWindowChanged?.Invoke(this, new GameWindowEventArgs(null, false));
        }
    }

    private IntPtr FindGameWindow()
    {
        // Try finding by window title first
        foreach (var title in GameWindowTitles)
        {
            var hwnd = FindWindow(null, title);
            if (hwnd != IntPtr.Zero)
            {
                return hwnd;
            }
        }

        // Try finding by process name
        IntPtr foundHwnd = IntPtr.Zero;

        foreach (var processName in GameProcessNames.Known)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName.Replace(" ", ""));
                foreach (var process in processes)
                {
                    using (process)
                    {
                        if (foundHwnd == IntPtr.Zero && process.MainWindowHandle != IntPtr.Zero)
                        {
                            foundHwnd = process.MainWindowHandle;
                        }
                    }
                }

                if (foundHwnd != IntPtr.Zero)
                    break;
            }
            catch
            {
                // Process access denied or other error
            }
        }

        if (foundHwnd != IntPtr.Zero)
            return foundHwnd;

        // Last resort: enumerate all windows looking for a match
        EnumWindows((hwnd, _) =>
        {
            var length = GetWindowTextLength(hwnd);
            if (length == 0)
                return true;

            var sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();

            foreach (var gameTitle in GameWindowTitles)
            {
                if (title.Contains(gameTitle, StringComparison.OrdinalIgnoreCase))
                {
                    foundHwnd = hwnd;
                    return false; // Stop enumeration
                }
            }

            return true;
        }, IntPtr.Zero);

        return foundHwnd;
    }

    private void UpdateWindowInfo()
    {
        if (_gameWindowHandle == IntPtr.Zero)
        {
            CurrentWindow = null;
            return;
        }

        if (!GetWindowRect(_gameWindowHandle, out var rect))
        {
            CurrentWindow = null;
            return;
        }

        var monitor = MonitorFromWindow(_gameWindowHandle, MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref monitorInfo);

        // Get DPI for the monitor
        uint dpiX = 96, dpiY = 96;
        try
        {
            GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
        }
        catch
        {
            // Fallback to 96 DPI if not supported
        }

        CurrentWindow = new GameWindowInfo
        {
            Handle = _gameWindowHandle,
            Bounds = new System.Drawing.Rectangle(rect.Left, rect.Top, rect.Width, rect.Height),
            MonitorHandle = monitor,
            MonitorBounds = new System.Drawing.Rectangle(
                monitorInfo.rcMonitor.Left,
                monitorInfo.rcMonitor.Top,
                monitorInfo.rcMonitor.Width,
                monitorInfo.rcMonitor.Height),
            WorkArea = new System.Drawing.Rectangle(
                monitorInfo.rcWork.Left,
                monitorInfo.rcWork.Top,
                monitorInfo.rcWork.Width,
                monitorInfo.rcWork.Height),
            DpiX = dpiX,
            DpiY = dpiY,
            ScaleFactor = dpiX / 96.0
        };
    }

    /// <summary>
    /// Converts a region from game-relative to screen coordinates.
    /// </summary>
    public RegionConfig GameRelativeToScreen(RegionConfig relativeRegion)
    {
        if (CurrentWindow == null)
            return relativeRegion;

        return new RegionConfig
        {
            X = relativeRegion.X + CurrentWindow.Bounds.X,
            Y = relativeRegion.Y + CurrentWindow.Bounds.Y,
            Width = relativeRegion.Width,
            Height = relativeRegion.Height
        };
    }

    /// <summary>
    /// Gets the recommended overlay position (top-left of game window + offset).
    /// </summary>
    public System.Drawing.Point GetRecommendedOverlayPosition(int offsetX = 10, int offsetY = 10)
    {
        if (CurrentWindow == null)
            return new System.Drawing.Point(offsetX, offsetY);

        return new System.Drawing.Point(
            CurrentWindow.Bounds.X + offsetX,
            CurrentWindow.Bounds.Y + offsetY);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer.Stop();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Information about the detected game window.
/// </summary>
public class GameWindowInfo
{
    /// <summary>Window handle.</summary>
    public IntPtr Handle { get; init; }

    /// <summary>Window bounds in screen coordinates.</summary>
    public System.Drawing.Rectangle Bounds { get; init; }

    /// <summary>Monitor handle.</summary>
    public IntPtr MonitorHandle { get; init; }

    /// <summary>Full monitor bounds.</summary>
    public System.Drawing.Rectangle MonitorBounds { get; init; }

    /// <summary>Monitor work area (excluding taskbar).</summary>
    public System.Drawing.Rectangle WorkArea { get; init; }

    /// <summary>Horizontal DPI.</summary>
    public uint DpiX { get; init; }

    /// <summary>Vertical DPI.</summary>
    public uint DpiY { get; init; }

    /// <summary>Scale factor (1.0 = 100%, 1.5 = 150%, 2.0 = 200%).</summary>
    public double ScaleFactor { get; init; }

}

/// <summary>
/// Event args for game window detection events.
/// </summary>
public class GameWindowEventArgs : EventArgs
{
    public GameWindowInfo? WindowInfo { get; }
    public bool IsDetected { get; }

    public GameWindowEventArgs(GameWindowInfo? windowInfo, bool isDetected)
    {
        WindowInfo = windowInfo;
        IsDetected = isDetected;
    }
}
