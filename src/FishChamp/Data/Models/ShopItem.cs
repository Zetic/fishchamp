namespace FishChamp.Data.Models;

public class ShopItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // Rod, Bait, etc
    public int Price { get; set; } = 0;
    public string Description { get; set; } = string.Empty;
    public bool InStock { get; set; } = true;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public class FishingRod
{
    public string RodId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Power { get; set; } = 1; // Affects catch rate
    public int Durability { get; set; } = 100; // Decreases with use
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    public List<string> CompatibleBaits { get; set; } = new(); // Optional, if we want to restrict bait types
    public RodAbility Abilities { get; set; } = RodAbility.None; // Counter abilities for fish traits
}

public class Bait
{
    public string BaitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public ItemRarity Rarity { get; set; } = ItemRarity.Common;
    public List<string> AttractiveFish { get; set; } = new(); // Fish types this bait is more likely to catch
    public double AttractionMultiplier { get; set; } = 1.0; // Increases chances of catching fish
}