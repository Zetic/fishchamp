using FishChamp.Helpers;

namespace FishChamp.Data.Models;

public class Aquarium
{
    public ulong UserId { get; set; }
    public string Name { get; set; } = "My Aquarium";
    public int Capacity { get; set; } = 10; // Maximum fish that can be stored
    public List<AquariumFish> Fish { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Future maintenance properties (for iteration 4.2)
    public double Cleanliness { get; set; } = 100.0; // 0-100%
    public double Temperature { get; set; } = 22.0; // Celsius
    public DateTime LastFed { get; set; } = DateTime.UtcNow;
    public DateTime LastCleaned { get; set; } = DateTime.UtcNow;
    
    // Future decoration properties (for iteration 4.4)
    public List<string> Decorations { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class AquariumFish
{
    public string FishId { get; set; } = string.Empty;
    public string FishType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public int Size { get; set; }
    public double Weight { get; set; }
    public FishTrait Traits { get; set; } = FishTrait.None;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    // Aquarium-specific properties
    public double Happiness { get; set; } = 100.0; // 0-100%
    public double Health { get; set; } = 100.0; // 0-100%
    public bool IsAlive { get; set; } = true;
    
    // Properties for breeding (iteration 4.3)
    public bool CanBreed { get; set; } = true;
    public DateTime LastBred { get; set; } = DateTime.MinValue;
    
    public Dictionary<string, object> Properties { get; set; } = new();
    
    // Create AquariumFish from InventoryItem
    public static AquariumFish FromInventoryItem(InventoryItem item)
    {
        return new AquariumFish
        {
            FishId = Guid.NewGuid().ToString(),
            FishType = item.ItemId,
            Name = item.Name,
            Rarity = item.Properties.GetString("rarity", "common"),
            Size = item.Properties.GetInt("size", 10),
            Weight = item.Properties.GetDouble("weight", 0.5),
            Traits = (FishTrait)item.Properties.GetInt("traits", 0),
            Properties = new Dictionary<string, object>(item.Properties)
        };
    }
    
    // Convert back to InventoryItem
    public InventoryItem ToInventoryItem()
    {
        var properties = new Dictionary<string, object>(Properties)
        {
            ["rarity"] = Rarity,
            ["size"] = Size,
            ["weight"] = Weight,
            ["traits"] = (int)Traits
        };
        
        return new InventoryItem
        {
            ItemId = FishType,
            ItemType = "Fish",
            Name = Name,
            Quantity = 1,
            Properties = properties,
            AcquiredAt = AddedAt
        };
    }
}