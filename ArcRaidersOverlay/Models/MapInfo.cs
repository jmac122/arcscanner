namespace ArcRaidersOverlay.Models;

/// <summary>
/// Represents map information loaded from maps.json.
/// </summary>
public class MapInfo
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
}

/// <summary>
/// Root object for maps.json deserialization.
/// </summary>
public class MapsConfig
{
    public List<MapInfo> Maps { get; set; } = new();
}
