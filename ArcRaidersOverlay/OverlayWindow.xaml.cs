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
    private GameWindowDetector? _gameWindowDetector;
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
        // Start UNLOCKED so user can interact with the overlay
        // They can lock it (click-through) once they're ready
        _isLocked = false;
        LockButton.Content = "Lock";
        SetClickThrough(false);

        // Initialize managers
        try
        {
            _configManager = new ConfigManager();
            ApplyEventPollInterval();
            ApplyStartMinimized();
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
            RegisterConfiguredHotkey();

            // Hook into window messages for hotkeys
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            // Initialize game window detector
            _gameWindowDetector = new GameWindowDetector();
            _gameWindowDetector.GameWindowChanged += OnGameWindowChanged;
            _gameWindowDetector.StartMonitoring();

            // Apply saved position or follow game
            if (_configManager.Config.FollowGameWindow && _gameWindowDetector.IsGameRunning)
            {
                FollowGameWindow();
            }
            else if (_configManager.Config.OverlayX >= 0)
            {
                Left = _configManager.Config.OverlayX;
                Top = _configManager.Config.OverlayY;
            }

            // Start event polling
            _eventPollTimer.Start();
            OnEventPollTick(this, EventArgs.Empty);

            UpdateGameStatus();
            UpdateScanStatusWithHotkey();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Initialization error: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RegisterConfiguredHotkey()
    {
        if (_hotkeyManager == null || _configManager == null) return;

        // Unregister existing hotkey first
        _hotkeyManager.UnregisterHotkey(HotkeyManager.SCAN_HOTKEY_ID);

        // Register with configured values
        var modifiers = _configManager.Config.ScanModifierKeys;
        var key = _configManager.Config.ScanKey;
        _hotkeyManager.RegisterHotkey(HotkeyManager.SCAN_HOTKEY_ID, modifiers, key);
    }

    public string GetScanHotkeyDisplayString()
    {
        if (_configManager == null) return "Ctrl+Shift+S";

        var parts = new List<string>();
        var modifiers = _configManager.Config.ScanModifierKeys;

        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");

        var key = _configManager.Config.ScanKey;
        var keyStr = key switch
        {
            >= Key.D0 and <= Key.D9 => key.ToString()[1..],
            Key.OemTilde => "~",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            _ => key.ToString()
        };
        parts.Add(keyStr);

        return string.Join("+", parts);
    }

    private void UpdateScanStatusWithHotkey()
    {
        UpdateScanStatus($"Press [{GetScanHotkeyDisplayString()}] to scan item");
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

    private void ApplyEventPollInterval()
    {
        if (_configManager == null) return;

        var intervalSeconds = _configManager.Config.EventPollIntervalSeconds;
        if (intervalSeconds > 0)
        {
            _eventPollTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
        }
    }

    private void ApplyStartMinimized()
    {
        if (_configManager?.Config.StartMinimized == true)
        {
            Hide();
        }
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

    #region Game Window Detection

    private void OnGameWindowChanged(object? sender, GameWindowEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.IsDetected && e.WindowInfo != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Game detected: {e.WindowInfo.Bounds.Width}x{e.WindowInfo.Bounds.Height} " +
                    $"@ ({e.WindowInfo.Bounds.X}, {e.WindowInfo.Bounds.Y}) " +
                    $"DPI: {e.WindowInfo.ScaleFactor:P0}");

                // Check for resolution change
                var config = _configManager?.Config;
                if (config != null)
                {
                    if (config.LastGameWidth > 0 &&
                        (config.LastGameWidth != e.WindowInfo.Bounds.Width ||
                         config.LastGameHeight != e.WindowInfo.Bounds.Height))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Resolution changed from {config.LastGameWidth}x{config.LastGameHeight}");
                        // Could trigger recalibration prompt here
                    }

                    config.LastGameWidth = e.WindowInfo.Bounds.Width;
                    config.LastGameHeight = e.WindowInfo.Bounds.Height;
                    _configManager?.Save();
                }

                if (_configManager?.Config.FollowGameWindow == true)
                {
                    FollowGameWindow();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Game window lost");
            }

            UpdateGameStatus();
        });
    }

    private void FollowGameWindow()
    {
        if (_gameWindowDetector?.CurrentWindow == null || _configManager == null)
            return;

        var config = _configManager.Config;
        var pos = _gameWindowDetector.GetRecommendedOverlayPosition(
            config.OverlayOffsetX, config.OverlayOffsetY);

        Left = pos.X;
        Top = pos.Y;
    }

    private void UpdateGameStatus()
    {
        // Could update a status indicator in the UI
        var isRunning = _gameWindowDetector?.IsGameRunning ?? false;
        System.Diagnostics.Debug.WriteLine($"Game status: {(isRunning ? "Running" : "Not detected")}");
    }

    /// <summary>
    /// Gets the actual screen region for capture, converting from game-relative if needed.
    /// Returns false when conversion is required but no game window is detected.
    /// </summary>
    private bool TryGetScreenRegion(RegionConfig configRegion, out RegionConfig screenRegion)
    {
        screenRegion = configRegion;
        if (_configManager?.Config.UseGameRelativeCoordinates == true)
        {
            if (_gameWindowDetector?.CurrentWindow == null)
            {
                return false;
            }

            screenRegion = _gameWindowDetector.GameRelativeToScreen(configRegion);
        }

        return true;
    }

    #endregion

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
            if (!config.EventsRegion.IsValid)
            {
                // No region configured - show placeholder
                UpdateEventsPanel(new List<GameEvent>());
                return;
            }

            // Capture events region (convert to screen coords if using game-relative)
            if (!TryGetScreenRegion(config.EventsRegion, out var eventsRegion))
            {
                UpdateEventsPanel(new List<GameEvent>());
                return;
            }
            using var bitmap = _screenCapture.CaptureRegion(eventsRegion);
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
            System.Drawing.Bitmap bitmap;

            if (config.UseCursorBasedScanning)
            {
                // Capture region centered on current cursor position
                bitmap = _screenCapture.CaptureAtCursor(
                    config.ScanRegionWidth,
                    config.ScanRegionHeight);
            }
            else
            {
                // Legacy: Use fixed tooltip region (requires calibration)
                if (!config.TooltipRegion.IsValid)
                {
                    UpdateScanStatus("Tooltip region not configured");
                    return;
                }

                if (!TryGetScreenRegion(config.TooltipRegion, out var tooltipRegion))
                {
                    UpdateScanStatus("Game not detected");
                    return;
                }
                bitmap = _screenCapture.CaptureRegion(tooltipRegion);
            }

            using (bitmap)
            {
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
            // Recommendation banner
            SetRecommendationBanner(item.Recommendation, item.KeepForQuests);

            TooltipItemName.Text = item.Name;
            TooltipItemCategory.Text = $"{item.Category} - {item.Rarity}";
            TooltipValue.Text = $"{item.Value:N0} Φ";

            // Quest item warning
            if (item.KeepForQuests)
            {
                TooltipQuestWarning.Visibility = Visibility.Visible;
                var questText = item.QuestUses?.Count > 0
                    ? $"QUEST ITEM: {string.Join(", ", item.QuestUses)}"
                    : "KEEP FOR QUESTS!";
                TooltipQuestText.Text = questText;
            }
            else
            {
                TooltipQuestWarning.Visibility = Visibility.Collapsed;
            }

            // Recycle efficiency percentage
            if (item.RecycleValuePercent.HasValue)
            {
                TooltipRecyclePercentRow.Visibility = Visibility.Visible;
                var percent = item.RecycleValuePercent.Value;
                TooltipRecyclePercent.Text = $"{percent}%";
                TooltipRecyclePercent.Foreground = percent >= 70
                    ? Theme.BrushRecycleGood
                    : percent >= 50 ? Theme.BrushRecycleMedium : Theme.BrushRecyclePoor;
            }
            else
            {
                TooltipRecyclePercentRow.Visibility = Visibility.Collapsed;
            }

            // Workshop uses
            TooltipWorkshopUses.Children.Clear();
            if (item.WorkshopUses != null && item.WorkshopUses.Count > 0)
            {
                TooltipWorkshopSection.Visibility = Visibility.Visible;
                foreach (var workshop in item.WorkshopUses)
                {
                    TooltipWorkshopUses.Children.Add(CreateTooltipTextBlock(
                        $"  {workshop}", Theme.BrushWorkshop));
                }
            }
            else
            {
                TooltipWorkshopSection.Visibility = Visibility.Collapsed;
            }

            // Recycle outputs
            TooltipRecycleOutputs.Children.Clear();
            if (item.RecycleOutputs != null && item.RecycleOutputs.Count > 0)
            {
                TooltipRecycleSection.Visibility = Visibility.Visible;
                foreach (var output in item.RecycleOutputs)
                {
                    TooltipRecycleOutputs.Children.Add(CreateTooltipTextBlock(
                        $"  {output.Key}: {output.Value}", Theme.BrushTextMuted));
                }
            }
            else
            {
                TooltipRecycleSection.Visibility = Visibility.Collapsed;
            }

            // Project uses
            TooltipProjectUses.Children.Clear();
            if (item.ProjectUses != null && item.ProjectUses.Count > 0)
            {
                TooltipProjectSection.Visibility = Visibility.Visible;
                foreach (var project in item.ProjectUses)
                {
                    TooltipProjectUses.Children.Add(CreateTooltipTextBlock(
                        $"  {project}", Theme.BrushTooltipProject));
                }
            }
            else
            {
                TooltipProjectSection.Visibility = Visibility.Collapsed;
            }

            TooltipPanel.Visibility = Visibility.Visible;

            // Auto-hide after delay
            _tooltipHideTimer.Stop();
            _tooltipHideTimer.Start();
        });
    }

    private void SetRecommendationBanner(string? recommendation, bool keepForQuests)
    {
        if (keepForQuests)
        {
            TooltipRecommendation.Text = "KEEP FOR QUESTS";
            TooltipRecommendationBorder.Background = Theme.BrushRecommendQuest;
            TooltipRecommendation.Foreground = Theme.BrushTextDefault;
            TooltipRecommendationBorder.Visibility = Visibility.Visible;
            return;
        }

        var (text, background, foreground) = recommendation?.ToUpperInvariant() switch
        {
            "KEEP" => ("KEEP", Theme.BrushRecommendKeep, Theme.BrushTextDefault),
            "SELL" => ("SELL", Theme.BrushRecommendSell, Theme.BrushTextDefault),
            "RECYCLE" => ("RECYCLE", Theme.BrushRecommendRecycle, Theme.BrushTextDefault),
            "EITHER" => ("SELL OR RECYCLE", Theme.BrushRecommendEither, Theme.BrushTextDefault),
            _ => ("", Theme.BrushTransparent, Theme.BrushTextMuted)
        };

        TooltipRecommendation.Text = text;
        TooltipRecommendationBorder.Background = background;
        TooltipRecommendation.Foreground = foreground;
        TooltipRecommendationBorder.Visibility = string.IsNullOrEmpty(text)
            ? Visibility.Collapsed : Visibility.Visible;
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
        ApplyEventPollInterval();
        RegisterConfiguredHotkey();
        UpdateScanStatusWithHotkey();
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
        _gameWindowDetector?.Dispose();
        _hotkeyManager?.Dispose();
        _ocrManager?.Dispose();

        GC.SuppressFinalize(this);
    }
}
