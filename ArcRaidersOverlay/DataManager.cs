using System.Linq;
using System.Text.RegularExpressions;
using ArcRaidersOverlay.Models;
using Newtonsoft.Json;

namespace ArcRaidersOverlay;

public class DataManager
{
    private readonly Dictionary<string, Item> _items;
    private readonly List<string> _itemNames;

    public DataManager()
    {
        _items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        _itemNames = new List<string>();

        LoadItems();
    }

    private void LoadItems()
    {
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "items.json");

        if (!File.Exists(dataPath))
        {
            System.Diagnostics.Debug.WriteLine($"Items database not found: {dataPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(dataPath);
            var items = JsonConvert.DeserializeObject<List<Item>>(json);

            if (items == null) return;

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Name)) continue;

                _items[item.Name] = item;
                _itemNames.Add(item.Name);

                // Also add normalized version for fuzzy matching
                var normalized = NormalizeName(item.Name);
                if (normalized != item.Name)
                {
                    _items[normalized] = item;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Loaded {_items.Count} items from database");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading items: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets an item by exact name match.
    /// </summary>
    public Item? GetItem(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Try exact match first
        if (_items.TryGetValue(name.Trim(), out var item))
        {
            return item;
        }

        // Try normalized match
        var normalized = NormalizeName(name);
        if (_items.TryGetValue(normalized, out item))
        {
            return item;
        }

        // Try fuzzy match
        return FuzzyMatch(name);
    }

    /// <summary>
    /// Gets the total number of items in the database.
    /// </summary>
    public int ItemCount => _itemNames.Count;

    /// <summary>
    /// Patterns that indicate a line is NOT an item name (stats, UI elements, etc.)
    /// </summary>
    private static readonly string[] NonItemPatterns = new[]
    {
        "durability", "ammo type", "magazine", "firing mode", "armor penetration",
        "damage", "accuracy", "recoil", "fire rate", "range", "weight",
        "category", "rarity", "common", "uncommon", "rare", "epic", "legendary",
        "special", "actions", "stash", "loadout", "equipment", "backpack",
        "quick use", "augmented", "slots", "ends in", "starts in"
    };

    /// <summary>
    /// Tries to find the best item match from multiple OCR text lines.
    /// Filters out non-item lines (stats, UI elements) and returns the best fuzzy match.
    /// </summary>
    public (Item? item, string matchedLine, double confidence) GetBestItemMatch(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return (null, string.Empty, 0);

        var lines = ocrText.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Where(l => l.Length >= 3)  // Skip very short lines
            .Where(IsLikelyItemName)    // Filter out stat lines
            .ToArray();

        if (lines.Length == 0)
            return (null, string.Empty, 0);

        Item? bestItem = null;
        string bestLine = string.Empty;
        double bestConfidence = 0;

        foreach (var line in lines)
        {
            // Try exact match first
            if (_items.TryGetValue(line, out var exactItem))
            {
                return (exactItem, line, 1.0);  // Perfect match
            }

            // Try normalized match
            var normalized = NormalizeName(line);
            if (_items.TryGetValue(normalized, out var normalizedItem))
            {
                return (normalizedItem, line, 0.95);  // Near-perfect match
            }

            // Try fuzzy match - find best score for this line
            foreach (var itemName in _itemNames)
            {
                var normalizedItemName = NormalizeName(itemName);
                var score = CalculateSimilarity(normalized, normalizedItemName);

                if (score > bestConfidence && score > 0.6)
                {
                    bestConfidence = score;
                    bestLine = line;
                    bestItem = _items[itemName];
                }
            }
        }

        if (bestItem != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Best match: '{bestLine}' → '{bestItem.Name}' (confidence: {bestConfidence:P0})");
        }

        return (bestItem, bestLine, bestConfidence);
    }

    /// <summary>
    /// Checks if a line is likely to be an item name (not a stat or UI element).
    /// </summary>
    private static bool IsLikelyItemName(string line)
    {
        var lower = line.ToLowerInvariant();

        // Skip lines that are just numbers or have stat-like patterns
        if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+[/x]\d+$"))  // "100/100", "5x"
            return false;

        if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+[\.,]?\d*\s*(kg|lb|%|m|s)?$"))  // "7.0", "100%"
            return false;

        // Skip known non-item patterns
        foreach (var pattern in NonItemPatterns)
        {
            if (lower.Contains(pattern))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Performs fuzzy matching to find the best item match.
    /// </summary>
    private Item? FuzzyMatch(string searchName)
    {
        var normalized = NormalizeName(searchName);
        var bestMatch = string.Empty;
        var bestScore = 0.0;

        foreach (var itemName in _itemNames)
        {
            var normalizedItemName = NormalizeName(itemName);
            var score = CalculateSimilarity(normalized, normalizedItemName);

            if (score > bestScore && score > 0.6) // Minimum 60% similarity
            {
                bestScore = score;
                bestMatch = itemName;
            }
        }

        if (!string.IsNullOrEmpty(bestMatch))
        {
            System.Diagnostics.Debug.WriteLine(
                $"Fuzzy matched '{searchName}' to '{bestMatch}' (score: {bestScore:P0})");
            return _items[bestMatch];
        }

        return null;
    }

    /// <summary>
    /// Normalizes a name for comparison (lowercase, remove special chars, etc.)
    /// </summary>
    private static string NormalizeName(string name)
    {
        // Remove special characters and extra spaces
        var normalized = Regex.Replace(name, @"[^\w\s]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Calculates the similarity between two strings using Levenshtein distance.
    /// Returns a value between 0 and 1, where 1 is an exact match.
    /// </summary>
    private static double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var distance = LevenshteinDistance(a, b);
        var maxLength = Math.Max(a.Length, b.Length);

        return 1.0 - (double)distance / maxLength;
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

}
