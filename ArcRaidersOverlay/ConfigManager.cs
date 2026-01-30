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
}

public class RegionConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public bool IsValid => Width > 0 && Height > 0;
}
