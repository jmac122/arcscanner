using Newtonsoft.Json;

namespace ArcRaidersOverlay.Models;

public class Item
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("category")]
    public string Category { get; set; } = "";

    [JsonProperty("rarity")]
    public string Rarity { get; set; } = "Common";

    [JsonProperty("value")]
    public int Value { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("weight")]
    public double Weight { get; set; }

    [JsonProperty("stackSize")]
    public int StackSize { get; set; } = 1;

    [JsonProperty("recycleOutputs")]
    public Dictionary<string, int>? RecycleOutputs { get; set; }

    [JsonProperty("projectUses")]
    public List<string>? ProjectUses { get; set; }

    [JsonProperty("foundIn")]
    public List<string>? FoundIn { get; set; }

    [JsonProperty("imageUrl")]
    public string? ImageUrl { get; set; }
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum ItemCategory
{
    Weapon,
    Armor,
    Consumable,
    Material,
    Component,
    Ammo,
    Tool,
    Quest,
    Misc
}
