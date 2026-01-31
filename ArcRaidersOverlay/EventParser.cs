using System.Text.RegularExpressions;
using ArcRaidersOverlay.Models;
using Newtonsoft.Json;

namespace ArcRaidersOverlay;

public static class EventParser
{
    // Maps loaded from configuration file (supports user customization)
    private static readonly List<MapInfo> KnownMaps;

    // Fallback maps if config file is missing
    private static readonly List<MapInfo> DefaultMaps = new()
    {
        new() { Name = "Dread Canyon", FileName = "dread_canyon.png", Aliases = new() { "dread", "canyon" } },
        new() { Name = "Blackstone Quarry", FileName = "blackstone_quarry.png", Aliases = new() { "blackstone", "quarry" } },
        new() { Name = "Wraith Basin", FileName = "wraith_basin.png", Aliases = new() { "wraith", "basin" } },
        new() { Name = "Thornback Ridge", FileName = "thornback_ridge.png", Aliases = new() { "thornback", "ridge" } },
        new() { Name = "Dustfall Expanse", FileName = "dustfall_expanse.png", Aliases = new() { "dustfall", "expanse" } },
        new() { Name = "Sunken Reach", FileName = "sunken_reach.png", Aliases = new() { "sunken", "reach" } },
        new() { Name = "Ironwood Forest", FileName = "ironwood_forest.png", Aliases = new() { "ironwood", "forest" } },
        new() { Name = "Ashland Dunes", FileName = "ashland_dunes.png", Aliases = new() { "ashland", "dunes" } }
    };

    static EventParser()
    {
        KnownMaps = LoadMapsFromConfig();
    }

    private static List<MapInfo> LoadMapsFromConfig()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "maps", "maps.json");

            if (!File.Exists(configPath))
            {
                System.Diagnostics.Debug.WriteLine($"Maps config not found at {configPath}, using defaults");
                return DefaultMaps;
            }

            var json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<MapsConfig>(json);

            if (config?.Maps == null || config.Maps.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Maps config empty, using defaults");
                return DefaultMaps;
            }

            System.Diagnostics.Debug.WriteLine($"Loaded {config.Maps.Count} maps from config");
            return config.Maps;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading maps config: {ex.Message}");
            return DefaultMaps;
        }
    }

    // Regex patterns for parsing event text
    // Pattern: Event Name - Location - Timer (e.g., "Supply Drop - Dread Canyon - 5:32")
    private static readonly Regex EventPattern = new(
        @"(?<name>[\w\s]+?)\s*[-–]\s*(?<location>[\w\s]+?)\s*[-–]\s*(?<timer>[\d:]+|ACTIVE)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Alternative pattern: "Event Name in Location (Timer)"
    private static readonly Regex EventPatternAlt = new(
        @"(?<name>[\w\s]+?)\s+(?:in|at|@)\s+(?<location>[\w\s]+?)\s*\((?<timer>[\d:]+|ACTIVE)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Timer pattern: "MM:SS" or just "M:SS"
    private static readonly Regex TimerPattern = new(
        @"\b(\d{1,2}:\d{2})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses OCR text to extract game events.
    /// </summary>
    public static List<GameEvent> Parse(string text)
    {
        var events = new List<GameEvent>();

        if (string.IsNullOrWhiteSpace(text))
            return events;

        // Split by newlines and process each line
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var evt = ParseLine(line.Trim());
            if (evt != null)
            {
                events.Add(evt);
            }
        }

        // If no structured events found, try to extract any timers
        if (events.Count == 0)
        {
            var timerMatches = TimerPattern.Matches(text);
            foreach (Match match in timerMatches)
            {
                events.Add(new GameEvent
                {
                    Name = "Unknown Event",
                    Location = "Unknown",
                    Timer = match.Groups[1].Value
                });
            }
        }

        return events;
    }

    private static GameEvent? ParseLine(string line)
    {
        // Try primary pattern
        var match = EventPattern.Match(line);
        if (match.Success)
        {
            return new GameEvent
            {
                Name = CleanText(match.Groups["name"].Value),
                Location = CleanText(match.Groups["location"].Value),
                Timer = match.Groups["timer"].Value.ToUpperInvariant()
            };
        }

        // Try alternative pattern
        match = EventPatternAlt.Match(line);
        if (match.Success)
        {
            return new GameEvent
            {
                Name = CleanText(match.Groups["name"].Value),
                Location = CleanText(match.Groups["location"].Value),
                Timer = match.Groups["timer"].Value.ToUpperInvariant()
            };
        }

        return null;
    }

    /// <summary>
    /// Detects the current map from text (location names, map markers, etc.)
    /// Returns the map info if found, null otherwise.
    /// </summary>
    public static MapInfo? DetectMapInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.ToLowerInvariant();

        foreach (var map in KnownMaps)
        {
            // Check for exact name match
            if (normalized.Contains(map.Name.ToLowerInvariant()))
            {
                return map;
            }

            // Check for alias match
            if (map.Aliases.Any(alias => normalized.Contains(alias.ToLowerInvariant())))
            {
                return map;
            }

            // Check for fuzzy match (all words in map name present)
            var mapWords = map.Name.ToLowerInvariant().Split(' ');
            if (mapWords.All(word => normalized.Contains(word)))
            {
                return map;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects the current map from text and returns just the map name.
    /// Convenience wrapper around DetectMapInfo.
    /// </summary>
    public static string? DetectMap(string text)
    {
        return DetectMapInfo(text)?.Name;
    }

    /// <summary>
    /// Cleans OCR text by fixing common recognition errors.
    /// </summary>
    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Remove extra whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Fix common OCR errors (only safe replacements - fuzzy matching handles the rest)
        text = text.Replace("|", "I");

        // Capitalize first letter of each word
        text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());

        return text;
    }

    /// <summary>
    /// Parses a timer string to TimeSpan.
    /// </summary>
    public static TimeSpan? ParseTimer(string timer)
    {
        if (string.IsNullOrWhiteSpace(timer))
            return null;

        if (timer.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            return TimeSpan.Zero;

        var parts = timer.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var minutes) &&
            int.TryParse(parts[1], out var seconds))
        {
            return new TimeSpan(0, minutes, seconds);
        }

        return null;
    }
}
