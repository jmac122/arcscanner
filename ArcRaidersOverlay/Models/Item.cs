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

    /// <summary>
    /// Sell value in Φ (phi/credits)
    /// </summary>
    [JsonProperty("value")]
    public int Value { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("weight")]
    public double Weight { get; set; }

    [JsonProperty("stackSize")]
    public int StackSize { get; set; } = 1;

    /// <summary>
    /// Recycling Value Index (percentage). Higher = better to recycle, lower = better to sell.
    /// Green (70%+) = recycle, Red (below 50%) = sell
    /// </summary>
    [JsonProperty("recycleValuePercent")]
    public int? RecycleValuePercent { get; set; }

    /// <summary>
    /// What components this item produces when recycled
    /// </summary>
    [JsonProperty("recycleOutputs")]
    public Dictionary<string, int>? RecycleOutputs { get; set; }

    /// <summary>
    /// Expedition projects that require this item
    /// </summary>
    [JsonProperty("projectUses")]
    public List<string>? ProjectUses { get; set; }

    /// <summary>
    /// Workshop upgrades that require this item (Gunsmith, Gear Bench, Medical Lab, Scrappy)
    /// </summary>
    [JsonProperty("workshopUses")]
    public List<string>? WorkshopUses { get; set; }

    /// <summary>
    /// If true, keep this item for quests - do not sell or recycle
    /// </summary>
    [JsonProperty("keepForQuests")]
    public bool KeepForQuests { get; set; }

    /// <summary>
    /// List of quest names that require this item
    /// </summary>
    [JsonProperty("questUses")]
    public List<string>? QuestUses { get; set; }

    /// <summary>
    /// Recommendation: "Recycle", "Sell", "Keep", or "Either"
    /// </summary>
    [JsonProperty("recommendation")]
    public string? Recommendation { get; set; }

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
