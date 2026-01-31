namespace ArcRaidersOverlay.Models;

public class GameEvent
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Timer { get; set; } = "";

    public bool IsActive => Timer.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);

    public TimeSpan? TimeRemaining => EventParser.ParseTimer(Timer);

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
