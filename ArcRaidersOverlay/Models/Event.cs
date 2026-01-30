namespace ArcRaidersOverlay.Models;

public class GameEvent
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Timer { get; set; } = "";

    public bool IsActive => Timer.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);

    public TimeSpan? TimeRemaining
    {
        get
        {
            if (IsActive) return TimeSpan.Zero;

            var parts = Timer.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var minutes) &&
                int.TryParse(parts[1], out var seconds))
            {
                return new TimeSpan(0, minutes, seconds);
            }

            return null;
        }
    }

    public EventType Type
    {
        get
        {
            var lower = Name.ToLowerInvariant();
            if (lower.Contains("drop") || lower.Contains("supply")) return EventType.SupplyDrop;
            if (lower.Contains("storm") || lower.Contains("anomaly")) return EventType.Storm;
            if (lower.Contains("convoy") || lower.Contains("transport")) return EventType.Convoy;
            if (lower.Contains("extract") || lower.Contains("evac")) return EventType.Extraction;
            if (lower.Contains("boss") || lower.Contains("titan")) return EventType.Boss;
            return EventType.Unknown;
        }
    }

    public override string ToString()
    {
        return $"{Name} @ {Location} [{Timer}]";
    }
}

public enum EventType
{
    Unknown,
    SupplyDrop,
    Storm,
    Convoy,
    Extraction,
    Boss
}
