using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ArcRaidersOverlay;

public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;

    public SettingsWindow(ConfigManager configManager)
    {
        InitializeComponent();
        _configManager = configManager;
        LoadSettings();
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
    private static void CalibrateRegion(string regionName, RegionTextBoxes textBoxes)
    {
        var calibrationWindow = new CalibrationWindow(regionName);
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

        // General settings
        StartWithWindows.IsChecked = config.StartWithWindows;
        StartMinimized.IsChecked = config.StartMinimized;
        PollInterval.Text = config.EventPollIntervalSeconds.ToString();

        // OCR settings
        TessdataPath.Text = config.TessdataPath;
    }

    private void CalibrateEvents_Click(object sender, RoutedEventArgs e)
    {
        CalibrateRegion("Events Region", EventsRegionBoxes);
    }

    private void CalibrateTooltip_Click(object sender, RoutedEventArgs e)
    {
        CalibrateRegion("Tooltip Region", TooltipRegionBoxes);
    }

    private void BrowseTessdata_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Tessdata Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            TessdataPath.Text = dialog.FolderName;
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

            // General settings
            config.StartWithWindows = StartWithWindows.IsChecked ?? false;
            config.StartMinimized = StartMinimized.IsChecked ?? false;
            config.EventPollIntervalSeconds = int.Parse(PollInterval.Text);

            // OCR settings
            config.TessdataPath = TessdataPath.Text;

            // Handle startup with Windows
            SetStartupWithWindows(config.StartWithWindows);

            _configManager.Save();

            DialogResult = true;
            Close();
        }
        catch (FormatException)
        {
            MessageBox.Show("Please enter valid numbers for all region coordinates.",
                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SetStartupWithWindows(bool enable)
    {
        const string keyName = "ArcRaidersOverlay";
        var exePath = Environment.ProcessPath ?? "";

        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

        if (key == null) return;

        if (enable)
        {
            key.SetValue(keyName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(keyName, false);
        }
    }
}
