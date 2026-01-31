using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcRaidersOverlay.Models;

namespace ArcRaidersOverlay;

public partial class OverlayWindow : Window, IDisposable
{
    #region Win32 API

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    #endregion

    private readonly DispatcherTimer _eventPollTimer;
    private readonly DispatcherTimer _tooltipHideTimer;
    private OcrManager? _ocrManager;
    private ScreenCapture? _screenCapture;
    private DataManager? _dataManager;
    private HotkeyManager? _hotkeyManager;
    private ConfigManager? _configManager;
    private bool _isLocked;
    private bool _isClickThrough;
    private bool _disposed;

    public OverlayWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;

        // Event polling timer
        _eventPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _eventPollTimer.Tick += OnEventPollTick;

        // Tooltip auto-hide timer
        _tooltipHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _tooltipHideTimer.Tick += (s, e) =>
        {
            _tooltipHideTimer.Stop();
            HideTooltip();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make window click-through
        SetClickThrough(true);

        // Initialize managers
        try
        {
            _configManager = new ConfigManager();
            _dataManager = new DataManager();
            _screenCapture = new ScreenCapture();

            // Initialize OCR (may fail if tessdata not present)
            try
            {
                _ocrManager = new OcrManager(_configManager.Config.TessdataPath);
            }
            catch (Exception ex)
            {
                UpdateScanStatus($"OCR init failed: {ex.Message}");
            }

            // Initialize hotkeys
            var hwnd = new WindowInteropHelper(this).Handle;
            _hotkeyManager = new HotkeyManager(hwnd);
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
            _hotkeyManager.RegisterHotkey(HotkeyManager.SCAN_HOTKEY_ID,
                ModifierKeys.Shift, Key.S);

            // Hook into window messages for hotkeys
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            // Apply saved position
            if (_configManager.Config.OverlayX >= 0)
            {
                Left = _configManager.Config.OverlayX;
                Top = _configManager.Config.OverlayY;
            }

            // Start event polling
            _eventPollTimer.Start();
            OnEventPollTick(this, EventArgs.Empty);

            UpdateScanStatus("Press [Shift+S] to scan item");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Initialization error: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyManager.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            _hotkeyManager?.HandleHotkeyMessage(id);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void SetClickThrough(bool enable)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        uint exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (enable)
        {
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
        }

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        _isClickThrough = enable;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLocked && !_isClickThrough)
        {
            DragMove();
            SavePosition();
        }
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = !_isLocked;
        LockButton.Content = _isLocked ? "Unlock" : "Lock";

        // When locked, make click-through; when unlocked, allow interaction
        SetClickThrough(_isLocked);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void SavePosition()
    {
        if (_configManager != null)
        {
            _configManager.Config.OverlayX = (int)Left;
            _configManager.Config.OverlayY = (int)Top;
            _configManager.Save();
        }
    }

    #region Event Polling

    private void OnEventPollTick(object? sender, EventArgs e)
    {
        if (_ocrManager == null || _screenCapture == null || _configManager == null)
        {
            UpdateEventsPanel(new List<GameEvent>());
            return;
        }

        try
        {
            var config = _configManager.Config;
            if (config.EventsRegion.Width <= 0 || config.EventsRegion.Height <= 0)
            {
                // No region configured - show placeholder
                UpdateEventsPanel(new List<GameEvent>());
                return;
            }

            // Capture events region
            using var bitmap = _screenCapture.CaptureRegion(config.EventsRegion);
            var text = _ocrManager.Recognize(bitmap);

            // Parse events
            var events = EventParser.Parse(text);
            UpdateEventsPanel(events);

            // Detect current map
            var mapName = EventParser.DetectMap(text);
            if (!string.IsNullOrEmpty(mapName))
            {
                LoadMinimap(mapName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Event poll error: {ex.Message}");
        }
    }

    private void UpdateEventsPanel(List<GameEvent> events)
    {
        Dispatcher.Invoke(() =>
        {
            EventsPanel.Children.Clear();

            if (events.Count == 0)
            {
                EventsPanel.Children.Add(new TextBlock
                {
                    Text = "No events detected",
                    Style = (Style)FindResource("EventTextStyle"),
                    Foreground = Theme.BrushTextMuted,
                    FontStyle = FontStyles.Italic
                });
                ActiveEventBorder.Visibility = Visibility.Collapsed;
                return;
            }

            GameEvent? activeEvent = null;

            foreach (var evt in events)
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal };

                // Event icon based on type
                var icon = new TextBlock
                {
                    Text = GetEventIcon(evt.Name),
                    Foreground = GetEventBrush(evt.Name),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontSize = 11
                };

                var nameText = new TextBlock
                {
                    Text = $"{evt.Name}",
                    Style = (Style)FindResource("EventTextStyle")
                };

                var locationText = new TextBlock
                {
                    Text = $" @ {evt.Location}",
                    Style = (Style)FindResource("EventTextStyle"),
                    Foreground = Theme.BrushTextSecondary
                };

                var timerText = new TextBlock
                {
                    Text = $" [{evt.Timer}]",
                    Style = (Style)FindResource("EventTextStyle"),
                    Foreground = GetTimerBrush(evt.Timer)
                };

                panel.Children.Add(icon);
                panel.Children.Add(nameText);
                panel.Children.Add(locationText);
                panel.Children.Add(timerText);

                EventsPanel.Children.Add(panel);

                // Check if this event is active (timer shows "ACTIVE" or very low time)
                if (evt.Timer.Contains("ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                    evt.Timer.StartsWith("0:") || evt.Timer.StartsWith("1:"))
                {
                    activeEvent = evt;
                }
            }

            // Update active event display
            if (activeEvent != null)
            {
                ActiveEventBorder.Visibility = Visibility.Visible;
                ActiveEventText.Text = $"{activeEvent.Name} @ {activeEvent.Location}";
                ActiveEventTimer.Text = activeEvent.Timer;
            }
            else
            {
                ActiveEventBorder.Visibility = Visibility.Collapsed;
            }
        });
    }

    private static string GetEventIcon(string eventName)
    {
        var lower = eventName.ToLowerInvariant();
        if (lower.Contains("drop")) return "[D]";
        if (lower.Contains("storm")) return "[S]";
        if (lower.Contains("convoy")) return "[C]";
        if (lower.Contains("extraction")) return "[E]";
        return "[*]";
    }

    private static SolidColorBrush GetEventBrush(string eventName)
    {
        var lower = eventName.ToLowerInvariant();
        if (lower.Contains("drop")) return Theme.BrushEventDrop;
        if (lower.Contains("storm")) return Theme.BrushEventStorm;
        if (lower.Contains("convoy")) return Theme.BrushEventConvoy;
        if (lower.Contains("extraction")) return Theme.BrushEventExtraction;
        return Theme.BrushTextDefault;
    }

    private static SolidColorBrush GetTimerBrush(string timer)
    {
        if (timer.Contains("ACTIVE", StringComparison.OrdinalIgnoreCase))
            return Theme.BrushTimerActive;

        // Parse minutes and color code
        if (int.TryParse(timer.Split(':')[0], out int minutes))
        {
            if (minutes < 2) return Theme.BrushTimerUrgent;
            if (minutes < 5) return Theme.BrushTimerWarning;
        }

        return Theme.BrushTimerNormal;
    }

    #endregion

    #region Minimap

    private void LoadMinimap(string mapName)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Data", "maps", $"{mapName.ToLowerInvariant().Replace(" ", "_")}.png");

                if (File.Exists(mapPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(mapPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    MinimapImage.Source = bitmap;
                    CurrentMapText.Text = mapName;
                }
                else
                {
                    CurrentMapText.Text = $"{mapName} (no map)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Map load error: {ex.Message}");
            }
        });
    }

    #endregion

    #region Item Scanning

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId == HotkeyManager.SCAN_HOTKEY_ID)
        {
            ScanItem();
        }
    }

    private void ScanItem()
    {
        if (_ocrManager == null || _screenCapture == null ||
            _dataManager == null || _configManager == null)
        {
            UpdateScanStatus("Scanner not initialized");
            return;
        }

        try
        {
            UpdateScanStatus("Scanning...");

            var config = _configManager.Config;
            if (config.TooltipRegion.Width <= 0 || config.TooltipRegion.Height <= 0)
            {
                UpdateScanStatus("Tooltip region not configured");
                return;
            }

            // Capture tooltip region
            using var bitmap = _screenCapture.CaptureRegion(config.TooltipRegion);
            var text = _ocrManager.Recognize(bitmap);

            if (string.IsNullOrWhiteSpace(text))
            {
                UpdateScanStatus("No text detected");
                HideTooltip();
                return;
            }

            // Extract item name (first line typically)
            var itemName = text.Split('\n')[0].Trim();
            var item = _dataManager.GetItem(itemName);

            if (item != null)
            {
                ShowItemTooltip(item);
                UpdateScanStatus($"Found: {item.Name}");
                UpdateLastScannedItem(item.Name);
            }
            else
            {
                UpdateScanStatus($"Unknown item: {itemName}");
                HideTooltip();
            }
        }
        catch (Exception ex)
        {
            UpdateScanStatus($"Scan error: {ex.Message}");
        }
    }

    private void UpdateScanStatus(string status)
    {
        Dispatcher.Invoke(() =>
        {
            ScanStatusText.Text = status;
        });
    }

    private void UpdateLastScannedItem(string itemName)
    {
        Dispatcher.Invoke(() =>
        {
            LastScannedItem.Text = $"Last: {itemName}";
            LastScannedItem.Visibility = Visibility.Visible;
        });
    }

    private void ShowItemTooltip(Item item)
    {
        Dispatcher.Invoke(() =>
        {
            TooltipItemName.Text = item.Name;
            TooltipItemCategory.Text = $"{item.Category} - {item.Rarity}";
            TooltipValue.Text = $"{item.Value:N0} Credits";

            // Recycle outputs
            TooltipRecycleOutputs.Children.Clear();
            if (item.RecycleOutputs != null && item.RecycleOutputs.Count > 0)
            {
                foreach (var output in item.RecycleOutputs)
                {
                    TooltipRecycleOutputs.Children.Add(CreateTooltipTextBlock(
                        $"  {output.Key}: {output.Value}", Theme.BrushTextMuted));
                }
            }
            else
            {
                TooltipRecycleOutputs.Children.Add(CreateTooltipTextBlock(
                    "  Not recyclable", Theme.BrushTextDisabled));
            }

            // Project uses
            TooltipProjectUses.Children.Clear();
            if (item.ProjectUses != null && item.ProjectUses.Count > 0)
            {
                foreach (var project in item.ProjectUses)
                {
                    TooltipProjectUses.Children.Add(CreateTooltipTextBlock(
                        $"  {project}", Theme.BrushTooltipProject));
                }
            }
            else
            {
                TooltipProjectUses.Children.Add(CreateTooltipTextBlock(
                    "  Not used in projects", Theme.BrushTextDisabled));
            }

            TooltipPanel.Visibility = Visibility.Visible;

            // Auto-hide after delay
            _tooltipHideTimer.Stop();
            _tooltipHideTimer.Start();
        });
    }

    private void HideTooltip()
    {
        Dispatcher.Invoke(() =>
        {
            TooltipPanel.Visibility = Visibility.Collapsed;
        });
    }

    #endregion

    #region UI Helpers

    /// <summary>
    /// Creates a styled TextBlock for tooltip content. Reduces code duplication.
    /// </summary>
    private TextBlock CreateTooltipTextBlock(string text, SolidColorBrush foreground)
    {
        return new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("TooltipTextStyle"),
            Foreground = foreground
        };
    }

    #endregion

    #region Public Methods

    public void ToggleVisibility()
    {
        Visibility = Visibility == Visibility.Visible
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    public void ForceUnlock()
    {
        if (_isClickThrough)
        {
            SetClickThrough(false);
            _isLocked = false;
            LockButton.Content = "Lock";
        }
    }

    public void OpenSettings()
    {
        if (_configManager == null) return;

        // Temporarily make window interactive
        var wasClickThrough = _isClickThrough;
        if (wasClickThrough)
        {
            SetClickThrough(false);
        }

        var settingsWindow = new SettingsWindow(_configManager);
        settingsWindow.ShowDialog();

        // Restore click-through state
        if (wasClickThrough)
        {
            SetClickThrough(true);
        }

        // Refresh with new settings
        OnEventPollTick(this, EventArgs.Empty);
    }

    #endregion

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SavePosition();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _eventPollTimer.Stop();
        _tooltipHideTimer.Stop();
        _hotkeyManager?.Dispose();
        _ocrManager?.Dispose();

        GC.SuppressFinalize(this);
    }
}
