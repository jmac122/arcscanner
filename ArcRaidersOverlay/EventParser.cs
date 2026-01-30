using System.Text.RegularExpressions;
using ArcRaidersOverlay.Models;

namespace ArcRaidersOverlay;

public static class EventParser
{
    // Known map names in ARC Raiders
    private static readonly string[] KnownMaps =
    {
        "Dread Canyon",
        "Blackstone Quarry",
        "Wraith Basin",
        "Thornback Ridge",
        "Dustfall Expanse",
        "Sunken Reach",
        "Ironwood Forest",
        "Ashland Dunes"
    };

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
    /// </summary>
    public static string? DetectMap(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.ToLowerInvariant();

        foreach (var map in KnownMaps)
        {
            // Check for exact or partial match
            if (normalized.Contains(map.ToLowerInvariant()))
            {
                return map;
            }

            // Check for fuzzy match (map name words)
            var mapWords = map.ToLowerInvariant().Split(' ');
            if (mapWords.All(word => normalized.Contains(word)))
            {
                return map;
            }
        }

        return null;
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

        // Fix common OCR errors
        text = text
            .Replace("0", "O") // Only in names, not numbers
            .Replace("1", "l") // Only in names, context-dependent
            .Replace("|", "I");

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
