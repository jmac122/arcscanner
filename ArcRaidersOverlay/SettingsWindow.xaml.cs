using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace ArcRaidersOverlay;

public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;

    // Common keys for hotkey selection
    private static readonly Key[] AvailableKeys =
    {
        Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J, Key.K, Key.L, Key.M,
        Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
        Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9,
        Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
        Key.OemTilde, Key.OemMinus, Key.OemPlus, Key.OemOpenBrackets, Key.OemCloseBrackets,
        Key.OemSemicolon, Key.OemQuotes, Key.OemComma, Key.OemPeriod, Key.OemQuestion
    };

    public SettingsWindow(ConfigManager configManager)
    {
        InitializeComponent();
        _configManager = configManager;
        InitializeHotkeyComboBox();
        LoadSettings();
    }

    private void InitializeHotkeyComboBox()
    {
        foreach (var key in AvailableKeys)
        {
            var displayName = GetKeyDisplayName(key);
            HotkeyKey.Items.Add(new ComboBoxItem { Content = displayName, Tag = key });
        }
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            >= Key.D0 and <= Key.D9 => key.ToString()[1..], // "D0" -> "0"
            Key.OemTilde => "~",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            _ => key.ToString()
        };
    }

    #region Region Calibration Helpers

    /// <summary>
    /// Groups of TextBoxes for a calibration region (X, Y, Width, Height).
    /// Eliminates duplicate code in calibration handlers.
    /// </summary>
    private record RegionTextBoxes(TextBox X, TextBox Y, TextBox Width, TextBox Height);

    /// <summary>
    /// Opens the calibration window and applies the selected region to the TextBoxes.
    /// </summary>
    /// <param name="regionName">Display name for the region being calibrated</param>
    /// <param name="textBoxes">The TextBox group to update with the selected region</param>
    /// <param name="useGameRelative">Whether to use game-relative coordinates</param>
    private static void CalibrateRegion(string regionName, RegionTextBoxes textBoxes, bool useGameRelative = true)
    {
        var calibrationWindow = new CalibrationWindow(regionName, useGameRelative);
        if (calibrationWindow.ShowDialog() == true)
        {
            var region = calibrationWindow.SelectedRegion;
            textBoxes.X.Text = region.X.ToString();
            textBoxes.Y.Text = region.Y.ToString();
            textBoxes.Width.Text = region.Width.ToString();
            textBoxes.Height.Text = region.Height.ToString();
        }
    }

    /// <summary>
    /// Loads a RegionConfig into the corresponding TextBoxes.
    /// </summary>
    private static void LoadRegionToTextBoxes(RegionConfig region, RegionTextBoxes textBoxes)
    {
        textBoxes.X.Text = region.X.ToString();
        textBoxes.Y.Text = region.Y.ToString();
        textBoxes.Width.Text = region.Width.ToString();
        textBoxes.Height.Text = region.Height.ToString();
    }

    /// <summary>
    /// Parses TextBoxes into a RegionConfig.
    /// </summary>
    private static RegionConfig ParseRegionFromTextBoxes(RegionTextBoxes textBoxes)
    {
        return new RegionConfig
        {
            X = int.Parse(textBoxes.X.Text),
            Y = int.Parse(textBoxes.Y.Text),
            Width = int.Parse(textBoxes.Width.Text),
            Height = int.Parse(textBoxes.Height.Text)
        };
    }

    #endregion

    private RegionTextBoxes EventsRegionBoxes => new(EventsX, EventsY, EventsWidth, EventsHeight);
    private RegionTextBoxes TooltipRegionBoxes => new(TooltipX, TooltipY, TooltipWidth, TooltipHeight);

    private void LoadSettings()
    {
        var config = _configManager.Config;

        // Load regions using helper
        LoadRegionToTextBoxes(config.EventsRegion, EventsRegionBoxes);
        LoadRegionToTextBoxes(config.TooltipRegion, TooltipRegionBoxes);

        // Game detection settings
        UseGameRelative.IsChecked = config.UseGameRelativeCoordinates;
        FollowGameWindow.IsChecked = config.FollowGameWindow;
        OverlayOffsetX.Text = config.OverlayOffsetX.ToString();
        OverlayOffsetY.Text = config.OverlayOffsetY.ToString();

        // General settings
        StartWithWindows.IsChecked = config.StartWithWindows;
        StartMinimized.IsChecked = config.StartMinimized;
        PollInterval.Text = config.EventPollIntervalSeconds.ToString();

        // Hotkey settings
        LoadHotkeySettings(config);

        // Item scanner settings
        UseCursorScanning.IsChecked = config.UseCursorBasedScanning;
        ScanRegionWidth.Text = config.ScanRegionWidth.ToString();
        ScanRegionHeight.Text = config.ScanRegionHeight.ToString();
        UpdateScanPanelVisibility();

        // OCR settings
        TessdataPath.Text = config.TessdataPath;
    }

    private void UseCursorScanning_Changed(object sender, RoutedEventArgs e)
    {
        UpdateScanPanelVisibility();
    }

    private void UpdateScanPanelVisibility()
    {
        if (CursorScanPanel == null || FixedRegionPanel == null) return;

        var useCursor = UseCursorScanning.IsChecked ?? true;
        CursorScanPanel.Visibility = useCursor ? Visibility.Visible : Visibility.Collapsed;
        FixedRegionPanel.Visibility = useCursor ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LoadHotkeySettings(AppConfig config)
    {
        var modifiers = config.ScanModifierKeys;
        HotkeyCtrl.IsChecked = modifiers.HasFlag(ModifierKeys.Control);
        HotkeyShift.IsChecked = modifiers.HasFlag(ModifierKeys.Shift);
        HotkeyAlt.IsChecked = modifiers.HasFlag(ModifierKeys.Alt);

        var key = config.ScanKey;
        SelectHotkeyKey(key);

        // Default to S if not found
        if (HotkeyKey.SelectedIndex < 0)
        {
            SelectHotkeyKey(Key.S);
        }
    }

    private bool SelectHotkeyKey(Key targetKey)
    {
        for (int i = 0; i < HotkeyKey.Items.Count; i++)
        {
            if (HotkeyKey.Items[i] is ComboBoxItem item && item.Tag is Key itemKey && itemKey == targetKey)
            {
                HotkeyKey.SelectedIndex = i;
                return true;
            }
        }

        return false;
    }

    private (ModifierKeys modifiers, Key key) GetSelectedHotkey()
    {
        ModifierKeys modifiers = ModifierKeys.None;
        if (HotkeyCtrl.IsChecked == true) modifiers |= ModifierKeys.Control;
        if (HotkeyShift.IsChecked == true) modifiers |= ModifierKeys.Shift;
        if (HotkeyAlt.IsChecked == true) modifiers |= ModifierKeys.Alt;

        Key key = Key.S;
        if (HotkeyKey.SelectedItem is ComboBoxItem item && item.Tag is Key selectedKey)
        {
            key = selectedKey;
        }

        return (modifiers, key);
    }

    private static string ModifiersToString(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Control");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        return string.Join(",", parts);
    }

    private void CalibrateEvents_Click(object sender, RoutedEventArgs e)
    {
        var useGameRelative = UseGameRelative.IsChecked ?? true;
        CalibrateRegion("Events Region", EventsRegionBoxes, useGameRelative);
    }

    private void CalibrateTooltip_Click(object sender, RoutedEventArgs e)
    {
        var useGameRelative = UseGameRelative.IsChecked ?? true;
        CalibrateRegion("Tooltip Region", TooltipRegionBoxes, useGameRelative);
    }

    private void BrowseTessdata_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select Tessdata Folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            TessdataPath.Text = dialog.SelectedPath;
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to defaults?",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _configManager.ResetToDefaults();
            LoadSettings();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = _configManager.Config;

            // Parse regions using helper
            config.EventsRegion = ParseRegionFromTextBoxes(EventsRegionBoxes);
            config.TooltipRegion = ParseRegionFromTextBoxes(TooltipRegionBoxes);

            // Item scanner settings
            config.UseCursorBasedScanning = UseCursorScanning.IsChecked ?? true;
            var scanRegionWidth = int.Parse(ScanRegionWidth.Text);
            var scanRegionHeight = int.Parse(ScanRegionHeight.Text);
            if (scanRegionWidth <= 0 || scanRegionHeight <= 0)
            {
                MessageBox.Show("Scan region width and height must be positive values.",
                    "Invalid Scan Region", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            config.ScanRegionWidth = scanRegionWidth;
            config.ScanRegionHeight = scanRegionHeight;

            // Game detection settings
            config.UseGameRelativeCoordinates = UseGameRelative.IsChecked ?? true;
            config.FollowGameWindow = FollowGameWindow.IsChecked ?? true;
            config.OverlayOffsetX = int.Parse(OverlayOffsetX.Text);
            config.OverlayOffsetY = int.Parse(OverlayOffsetY.Text);

            // General settings
            var startWithWindows = StartWithWindows.IsChecked ?? false;
            config.StartWithWindows = startWithWindows;
            config.StartMinimized = StartMinimized.IsChecked ?? false;
            config.EventPollIntervalSeconds = int.Parse(PollInterval.Text);

            // Hotkey settings
            var (modifiers, key) = GetSelectedHotkey();
            if (modifiers == ModifierKeys.None)
            {
                MessageBox.Show("Please select at least one modifier key (Ctrl, Shift, or Alt) for the hotkey.",
                    "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            config.ScanHotkeyModifier = ModifiersToString(modifiers);
            config.ScanHotkeyKey = key.ToString();

            // OCR settings
            config.TessdataPath = TessdataPath.Text;

            // Handle startup with Windows
            if (!SetStartupWithWindows(startWithWindows) && startWithWindows)
            {
                config.StartWithWindows = false;
            }

            _configManager.Save();

            DialogResult = true;
            Close();
        }
        catch (FormatException)
        {
            MessageBox.Show("Please enter valid numbers for all fields.",
                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool SetStartupWithWindows(bool enable)
    {
        const string keyName = "ArcRaidersOverlay";
        var exePath = Environment.ProcessPath;

        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

        if (key == null) return false;

        if (enable)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                MessageBox.Show("Unable to determine the application path to add startup entry.",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            key.SetValue(keyName, $"\"{exePath}\"");
            return true;
        }

        key.DeleteValue(keyName, false);
        return true;
    }
}
