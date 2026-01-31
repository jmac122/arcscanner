using System.IO;
using System.Linq;
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
    private EventApiClient? _eventApiClient;
    private bool _isLocked;
    private bool _isClickThrough;
    private bool _disposed;
    private static readonly string LogFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "overlay.log");

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

        // Clear old log on startup
        try
        {
            File.WriteAllText(LogFilePath, $"=== ArcRaidersOverlay Log Started {DateTime.Now} ===\n");
        }
        catch { /* Ignore logging errors */ }
    }

    private static void LogMessage(string message)
    {
        try
        {
            var logLine = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            File.AppendAllText(LogFilePath, logLine);
            System.Diagnostics.Debug.WriteLine(message);
        }
        catch { /* Ignore logging errors */ }
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
                var tessdataPath = _configManager.Config.TessdataPath;
                LogMessage($"Initializing OCR with tessdata path: {tessdataPath}");
                _ocrManager = new OcrManager(tessdataPath);
                LogMessage("OCR initialized successfully");
            }
            catch (Exception ex)
            {
                var errorMsg = $"OCR init failed: {ex.Message}";
                LogMessage($"ERROR: {errorMsg}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogMessage($"Inner exception: {ex.InnerException.Message}");
                }
                UpdateScanStatus(errorMsg);
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

            // Initialize event API client (replaces OCR-based event detection)
            _eventApiClient = new EventApiClient();

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

            // Start event polling (only if events are enabled)
            if (_configManager.Config.ShowEvents)
            {
                EventsBorder.Visibility = Visibility.Visible;
                _eventPollTimer.Start();
                OnEventPollTick(this, EventArgs.Empty);
            }
            else
            {
                EventsBorder.Visibility = Visibility.Collapsed;
            }

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

        // Unregister existing hotkeys first
        _hotkeyManager.UnregisterHotkey(HotkeyManager.SCAN_HOTKEY_ID);
        _hotkeyManager.UnregisterHotkey(HotkeyManager.EVENTS_TOGGLE_HOTKEY_ID);
        _hotkeyManager.UnregisterHotkey(HotkeyManager.OVERLAY_TOGGLE_HOTKEY_ID);

        // Register scan hotkey
        var modifiers = _configManager.Config.ScanModifierKeys;
        var key = _configManager.Config.ScanKey;
        _hotkeyManager.RegisterHotkey(HotkeyManager.SCAN_HOTKEY_ID, modifiers, key);

        // Register events toggle hotkey (F8 by default)
        var eventsModifiers = _configManager.Config.EventsToggleModifierKeys;
        var eventsKey = _configManager.Config.EventsToggleKey;
        _hotkeyManager.RegisterHotkey(HotkeyManager.EVENTS_TOGGLE_HOTKEY_ID, eventsModifiers, eventsKey);

        // Register overlay toggle hotkey (F7 by default)
        var overlayModifiers = _configManager.Config.OverlayToggleModifierKeys;
        var overlayKey = _configManager.Config.OverlayToggleKey;
        _hotkeyManager.RegisterHotkey(HotkeyManager.OVERLAY_TOGGLE_HOTKEY_ID, overlayModifiers, overlayKey);
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

    private async void OnEventPollTick(object? sender, EventArgs e)
    {
        if (_eventApiClient == null)
        {
            UpdateEventsPanel(new List<GameEvent>());
            return;
        }

        try
        {
            // Fetch events from API
            var events = await _eventApiClient.GetEventsAsync();
            UpdateEventsPanel(events);

            // Load minimap for first active event's map
            var activeEvent = events.FirstOrDefault(ev => ev.Timer.StartsWith("ACTIVE"));
            if (activeEvent != null)
            {
                var mapInfo = EventParser.DetectMapInfo(activeEvent.Location);
                if (mapInfo != null)
                {
                    LoadMinimap(mapInfo);
                }
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
            var isCompactMode = _configManager?.Config.EventsCompactMode ?? false;

            if (events.Count == 0)
            {
                EventsPanel.Visibility = Visibility.Visible;
                EventsCompactSummary.Visibility = Visibility.Collapsed;
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

            // Compact mode: show summary only
            if (isCompactMode)
            {
                EventsPanel.Visibility = Visibility.Collapsed;
                EventsCompactSummary.Visibility = Visibility.Visible;

                var activeCount = events.Count(e => e.Timer.Contains("ACTIVE"));
                var nextEvent = events.FirstOrDefault(e => !e.Timer.Contains("ACTIVE"));

                if (activeCount > 0)
                {
                    EventsCompactSummary.Text = $"{activeCount} active, {events.Count} total";
                    EventsCompactSummary.Foreground = Theme.BrushTimerActive;
                }
                else if (nextEvent != null)
                {
                    EventsCompactSummary.Text = $"{events.Count} events - next: {nextEvent.Name} {nextEvent.Timer}";
                    EventsCompactSummary.Foreground = Theme.BrushTextSecondary;
                }
                else
                {
                    EventsCompactSummary.Text = $"{events.Count} events";
                    EventsCompactSummary.Foreground = Theme.BrushTextSecondary;
                }
            }
            else
            {
                EventsPanel.Visibility = Visibility.Visible;
                EventsCompactSummary.Visibility = Visibility.Collapsed;
            }

            // Find active event for the banner
            var activeEvent = events.FirstOrDefault(evt =>
                evt.Timer.Contains("ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                evt.Timer.StartsWith("0:") || evt.Timer.StartsWith("1:"));

            // Only build full event list if not in compact mode
            if (!isCompactMode)
            {
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

    private void LoadMinimap(MapInfo mapInfo)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var mapName = mapInfo.Name;
                var fileName = mapInfo.FileName;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = $"{mapName.ToLowerInvariant().Replace(" ", "_")}.png";
                }

                var mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Data", "maps", fileName);

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
        else if (hotkeyId == HotkeyManager.EVENTS_TOGGLE_HOTKEY_ID)
        {
            ToggleEventsVisibility();
        }
        else if (hotkeyId == HotkeyManager.OVERLAY_TOGGLE_HOTKEY_ID)
        {
            ToggleOverlayVisibility();
        }
    }

    private void ToggleOverlayVisibility()
    {
        Dispatcher.Invoke(() =>
        {
            if (Visibility == Visibility.Visible)
            {
                Hide();
                // Stop polling when hidden to save resources
                _eventPollTimer.Stop();
            }
            else
            {
                Show();
                // Resume polling if events are enabled
                if (_configManager?.Config.ShowEvents == true)
                {
                    _eventPollTimer.Start();
                    OnEventPollTick(this, EventArgs.Empty);
                }
            }
        });
    }

    private void ToggleEventsVisibility()
    {
        if (_configManager == null) return;

        Dispatcher.Invoke(() =>
        {
            // Toggle visibility
            _configManager.Config.ShowEvents = !_configManager.Config.ShowEvents;
            var isVisible = _configManager.Config.ShowEvents;

            // Update UI
            EventsBorder.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            ActiveEventBorder.Visibility = isVisible && ActiveEventBorder.Visibility == Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;

            // Start/stop API polling based on visibility
            if (isVisible)
            {
                _eventPollTimer.Start();
                OnEventPollTick(this, EventArgs.Empty); // Immediate refresh
            }
            else
            {
                _eventPollTimer.Stop();
            }

            // Save preference
            _configManager.Save();
        });
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

            // Get cursor position for logging
            var cursorPos = System.Windows.Forms.Cursor.Position;
            Log($"Scan triggered - Cursor at ({cursorPos.X}, {cursorPos.Y})");

            if (config.UseCursorBasedScanning)
            {
                Log($"Capturing {config.ScanRegionWidth}x{config.ScanRegionHeight} region centered on cursor");
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
                Log($"Captured bitmap: {bitmap.Width}x{bitmap.Height}");

                // Save debug image
                try
                {
                    var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Debug");
                    Directory.CreateDirectory(debugDir);
                    var debugPath = Path.Combine(debugDir, $"scan_{DateTime.Now:HHmmss}.png");
                    bitmap.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                    Log($"Debug image saved: {debugPath}");
                }
                catch (Exception saveEx)
                {
                    Log($"Failed to save debug image: {saveEx.Message}");
                }

                var text = _ocrManager.Recognize(bitmap);
                Log($"OCR result: '{text?.Replace("\n", "\\n") ?? "(null)"}'");

                if (string.IsNullOrWhiteSpace(text))
                {
                    UpdateScanStatus("No text detected");
                    HideTooltip();
                    return;
                }

                // Extract item name (first line typically)
                var itemName = text.Split('\n')[0].Trim();
                Log($"Extracted item name: '{itemName}'");
                var item = _dataManager.GetItem(itemName);

                if (item != null)
                {
                    ShowItemTooltip(item);
                    UpdateScanStatus($"Found: {item.Name}");
                    UpdateLastScannedItem(item.Name);
                    Log($"Item found in database: {item.Name}");
                }
                else
                {
                    UpdateScanStatus($"Unknown item: {itemName}");
                    HideTooltip();
                    Log($"Item not found in database");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateScanStatus($"Scan error: {ex.Message}");
            Log($"ERROR: Scan failed: {ex}");
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
        _eventApiClient?.Dispose();

        GC.SuppressFinalize(this);
    }
}
