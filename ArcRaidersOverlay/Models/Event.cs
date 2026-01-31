namespace ArcRaidersOverlay.Models;

public class GameEvent
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Timer { get; set; } = "";

    public override string ToString()
    {
        return $"{Name} @ {Location} [{Timer}]";
    }
}
