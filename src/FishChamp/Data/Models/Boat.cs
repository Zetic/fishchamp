namespace FishChamp.Data.Models;

public class Boat
{
    public ulong UserId { get; set; }
    public string BoatId { get; set; } = string.Empty;
    public string BoatType { get; set; } = string.Empty; // skiff, canoe, speedboat
    public string Name { get; set; } = string.Empty;
    public int Durability { get; set; } = 100;
    public int MaxDurability { get; set; } = 100;
    public int StorageCapacity { get; set; } = 5; // Number of storage slots
    public List<InventoryItem> Storage { get; set; } = new();
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public static class BoatTypes
{
    public const string Skiff = "skiff";
    public const string Canoe = "canoe";
    public const string Speedboat = "speedboat";
    
    public static readonly Dictionary<string, BoatInfo> BoatData = new()
    {
        [Skiff] = new BoatInfo 
        { 
            Name = "Small Skiff", 
            MaxDurability = 80, 
            StorageCapacity = 3,
            Description = "A simple wooden boat perfect for shallow waters."
        },
        [Canoe] = new BoatInfo 
        { 
            Name = "Sturdy Canoe", 
            MaxDurability = 120, 
            StorageCapacity = 5,
            Description = "A reliable canoe that can handle rougher waters."
        },
        [Speedboat] = new BoatInfo 
        { 
            Name = "Fast Speedboat", 
            MaxDurability = 200, 
            StorageCapacity = 8,
            Description = "A modern speedboat for deep sea exploration."
        }
    };
}

public class BoatInfo
{
    public string Name { get; set; } = string.Empty;
    public int MaxDurability { get; set; }
    public int StorageCapacity { get; set; }
    public string Description { get; set; } = string.Empty;
}