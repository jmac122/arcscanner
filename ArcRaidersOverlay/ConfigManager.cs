using System.Windows.Input;
using Newtonsoft.Json;

namespace ArcRaidersOverlay;

public class ConfigManager
{
    private readonly string _configPath;
    public AppConfig Config { get; private set; }

    public ConfigManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArcRaidersOverlay");

        Directory.CreateDirectory(appDataPath);

        _configPath = Path.Combine(appDataPath, "config.json");
        Config = Load();
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                if (config != null)
                {
                    // Validate tessdata path - reset to default if invalid
                    config.TessdataPath = ValidateTessdataPath(config.TessdataPath);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }

        // Return default config
        return CreateDefaultConfig();
    }

    private static string ValidateTessdataPath(string? savedPath)
    {
        var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "tessdata");

        // If saved path is empty or doesn't exist, use default
        if (string.IsNullOrWhiteSpace(savedPath) || !Directory.Exists(savedPath))
        {
            return defaultPath;
        }

        return savedPath;
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    public void ResetToDefaults()
    {
        Config = CreateDefaultConfig();
        Save();
    }

    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            // Default to app's tessdata folder
            TessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "tessdata"),

            // Default regions (1920x1080 estimates - user should calibrate)
            EventsRegion = new RegionConfig
            {
                X = 10,
                Y = 100,
                Width = 300,
                Height = 200
            },

            TooltipRegion = new RegionConfig
            {
                X = 960,
                Y = 400,
                Width = 400,
                Height = 300
            },

            // Overlay position
            OverlayX = 10,
            OverlayY = 10,

            // General settings
            StartWithWindows = false,
            StartMinimized = false,
            EventPollIntervalSeconds = 15
        };
    }
}

public class AppConfig
{
    public string TessdataPath { get; set; } = "";
    public RegionConfig EventsRegion { get; set; } = new();
    public RegionConfig TooltipRegion { get; set; } = new();
    public int OverlayX { get; set; } = -1;
    public int OverlayY { get; set; } = -1;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public int EventPollIntervalSeconds { get; set; } = 15;

    // Events display settings
    /// <summary>
    /// When true, events panel is visible on the overlay.
    /// </summary>
    public bool ShowEvents { get; set; } = true;

    /// <summary>
    /// When true, shows compact event view (just count + next event).
    /// </summary>
    public bool EventsCompactMode { get; set; } = false;

    /// <summary>
    /// Modifier keys for the events toggle hotkey.
    /// </summary>
    public string EventsToggleHotkeyModifier { get; set; } = "";

    /// <summary>
    /// Key for the events toggle hotkey.
    /// </summary>
    public string EventsToggleHotkeyKey { get; set; } = "F8";

    /// <summary>
    /// Gets the events toggle hotkey modifier as ModifierKeys enum.
    /// </summary>
    [JsonIgnore]
    public ModifierKeys EventsToggleModifierKeys
    {
        get
        {
            if (string.IsNullOrEmpty(EventsToggleHotkeyModifier))
                return ModifierKeys.None;

            ModifierKeys result = ModifierKeys.None;
            foreach (var part in EventsToggleHotkeyModifier.Split(','))
            {
                if (Enum.TryParse<ModifierKeys>(part.Trim(), out var mod))
                    result |= mod;
            }
            return result;
        }
    }

    /// <summary>
    /// Gets the events toggle key as Key enum.
    /// </summary>
    [JsonIgnore]
    public Key EventsToggleKey
    {
        get
        {
            if (Enum.TryParse<Key>(EventsToggleHotkeyKey, out var key))
                return key;
            return Key.F8;
        }
    }

    /// <summary>
    /// Modifier keys for the overlay toggle hotkey.
    /// </summary>
    public string OverlayToggleHotkeyModifier { get; set; } = "";

    /// <summary>
    /// Key for the overlay toggle hotkey.
    /// </summary>
    public string OverlayToggleHotkeyKey { get; set; } = "F7";

    /// <summary>
    /// Gets the overlay toggle hotkey modifier as ModifierKeys enum.
    /// </summary>
    [JsonIgnore]
    public ModifierKeys OverlayToggleModifierKeys
    {
        get
        {
            if (string.IsNullOrEmpty(OverlayToggleHotkeyModifier))
                return ModifierKeys.None;

            ModifierKeys result = ModifierKeys.None;
            foreach (var part in OverlayToggleHotkeyModifier.Split(','))
            {
                if (Enum.TryParse<ModifierKeys>(part.Trim(), out var mod))
                    result |= mod;
            }
            return result;
        }
    }

    /// <summary>
    /// Gets the overlay toggle key as Key enum.
    /// </summary>
    [JsonIgnore]
    public Key OverlayToggleKey
    {
        get
        {
            if (Enum.TryParse<Key>(OverlayToggleHotkeyKey, out var key))
                return key;
            return Key.F7;
        }
    }

    // Game window detection settings
    /// <summary>
    /// When true, regions are stored relative to game window position.
    /// This allows calibration to work across different monitors/positions.
    /// </summary>
    public bool UseGameRelativeCoordinates { get; set; } = true;

    /// <summary>
    /// When true, the overlay will automatically follow the game window.
    /// </summary>
    public bool FollowGameWindow { get; set; } = true;

    /// <summary>
    /// Offset from game window edge when following (X).
    /// </summary>
    public int OverlayOffsetX { get; set; } = 10;

    /// <summary>
    /// Offset from game window edge when following (Y).
    /// </summary>
    public int OverlayOffsetY { get; set; } = 10;

    /// <summary>
    /// Last known game window resolution (for detecting resolution changes).
    /// </summary>
    public int LastGameWidth { get; set; }
    public int LastGameHeight { get; set; }

    /// <summary>
    /// Width of the scan capture region.
    /// </summary>
    public int ScanRegionWidth { get; set; } = 400;

    /// <summary>
    /// Height of the scan capture region.
    /// </summary>
    public int ScanRegionHeight { get; set; } = 350;

    /// <summary>
    /// When true, scan captures at cursor position. When false, uses fixed TooltipRegion.
    /// </summary>
    public bool UseCursorBasedScanning { get; set; } = true;

    /// <summary>
    /// Horizontal offset from cursor for tooltip capture (positive = right).
    /// Arc Raiders tooltips appear to the right of the cursor.
    /// </summary>
    public int ScanOffsetX { get; set; } = 100;

    /// <summary>
    /// Vertical offset from cursor for tooltip capture (negative = up).
    /// Arc Raiders tooltips appear above the cursor, so this should be negative.
    /// </summary>
    public int ScanOffsetY { get; set; } = -200;

    /// <summary>
    /// Game resolution preset. Affects scan region size and icon matching.
    /// Values: "1080p", "1440p", "4K", "Custom"
    /// </summary>
    public string GameResolution { get; set; } = "1080p";

    /// <summary>
    /// Applies resolution preset values. Call this when resolution changes.
    /// The Y offset is negative to start ABOVE the cursor since Arc Raiders
    /// tooltips appear above and to the right of the hovered item.
    /// </summary>
    public void ApplyResolutionPreset(string resolution)
    {
        GameResolution = resolution;

        switch (resolution)
        {
            case "1080p":
                ScanRegionWidth = 400;
                ScanRegionHeight = 350;
                ScanOffsetX = 100;
                ScanOffsetY = -200;  // Start 200px above cursor to capture title
                break;

            case "1440p":
                ScanRegionWidth = 500;
                ScanRegionHeight = 450;
                ScanOffsetX = 130;
                ScanOffsetY = -270;  // Start 270px above cursor to capture title
                break;

            case "4K":
            case "2160p":
                ScanRegionWidth = 700;
                ScanRegionHeight = 650;
                ScanOffsetX = 200;
                ScanOffsetY = -400;  // Start 400px above cursor to capture title
                break;

            case "Custom":
                // Don't modify values - user has customized them
                break;
        }
    }

    /// <summary>
    /// Modifier key for the scan hotkey (e.g., Ctrl, Alt, Shift).
    /// Stored as string for JSON serialization.
    /// </summary>
    public string ScanHotkeyModifier { get; set; } = "Control,Shift";

    /// <summary>
    /// Key for the scan hotkey (e.g., S, F9).
    /// Stored as string for JSON serialization.
    /// </summary>
    public string ScanHotkeyKey { get; set; } = "S";

    /// <summary>
    /// Gets the scan hotkey modifier as ModifierKeys enum.
    /// </summary>
    [JsonIgnore]
    public ModifierKeys ScanModifierKeys
    {
        get
        {
            if (string.IsNullOrEmpty(ScanHotkeyModifier))
                return ModifierKeys.Control | ModifierKeys.Shift;

            ModifierKeys result = ModifierKeys.None;
            foreach (var part in ScanHotkeyModifier.Split(','))
            {
                if (Enum.TryParse<ModifierKeys>(part.Trim(), out var mod))
                    result |= mod;
            }
            return result == ModifierKeys.None ? ModifierKeys.Control | ModifierKeys.Shift : result;
        }
    }

    /// <summary>
    /// Gets the scan hotkey key as Key enum.
    /// </summary>
    [JsonIgnore]
    public Key ScanKey
    {
        get
        {
            if (Enum.TryParse<Key>(ScanHotkeyKey, out var key))
                return key;
            return Key.S;
        }
    }
}

public class RegionConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public bool IsValid => Width > 0 && Height > 0;
}
