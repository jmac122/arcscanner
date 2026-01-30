using System.Windows;
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

    private void LoadSettings()
    {
        var config = _configManager.Config;

        // Events region
        EventsX.Text = config.EventsRegion.X.ToString();
        EventsY.Text = config.EventsRegion.Y.ToString();
        EventsWidth.Text = config.EventsRegion.Width.ToString();
        EventsHeight.Text = config.EventsRegion.Height.ToString();

        // Tooltip region
        TooltipX.Text = config.TooltipRegion.X.ToString();
        TooltipY.Text = config.TooltipRegion.Y.ToString();
        TooltipWidth.Text = config.TooltipRegion.Width.ToString();
        TooltipHeight.Text = config.TooltipRegion.Height.ToString();

        // General settings
        StartWithWindows.IsChecked = config.StartWithWindows;
        StartMinimized.IsChecked = config.StartMinimized;
        PollInterval.Text = config.EventPollIntervalSeconds.ToString();

        // OCR settings
        TessdataPath.Text = config.TessdataPath;
    }

    private void CalibrateEvents_Click(object sender, RoutedEventArgs e)
    {
        var calibrationWindow = new CalibrationWindow("Events Region");
        if (calibrationWindow.ShowDialog() == true)
        {
            var region = calibrationWindow.SelectedRegion;
            EventsX.Text = region.X.ToString();
            EventsY.Text = region.Y.ToString();
            EventsWidth.Text = region.Width.ToString();
            EventsHeight.Text = region.Height.ToString();
        }
    }

    private void CalibrateTooltip_Click(object sender, RoutedEventArgs e)
    {
        var calibrationWindow = new CalibrationWindow("Tooltip Region");
        if (calibrationWindow.ShowDialog() == true)
        {
            var region = calibrationWindow.SelectedRegion;
            TooltipX.Text = region.X.ToString();
            TooltipY.Text = region.Y.ToString();
            TooltipWidth.Text = region.Width.ToString();
            TooltipHeight.Text = region.Height.ToString();
        }
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

            // Events region
            config.EventsRegion = new RegionConfig
            {
                X = int.Parse(EventsX.Text),
                Y = int.Parse(EventsY.Text),
                Width = int.Parse(EventsWidth.Text),
                Height = int.Parse(EventsHeight.Text)
            };

            // Tooltip region
            config.TooltipRegion = new RegionConfig
            {
                X = int.Parse(TooltipX.Text),
                Y = int.Parse(TooltipY.Text),
                Width = int.Parse(TooltipWidth.Text),
                Height = int.Parse(TooltipHeight.Text)
            };

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
