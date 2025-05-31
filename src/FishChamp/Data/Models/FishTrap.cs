namespace FishChamp.Data.Models;

public class FishTrap
{
    public string TrapId { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string TrapType { get; set; } = "basic_trap"; // basic_trap, shallow_trap, deep_trap
    public string CurrentArea { get; set; } = string.Empty;
    public string FishingSpot { get; set; } = string.Empty;
    public string? EquippedBait { get; set; } = null;
    public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletesAt { get; set; }
    public bool IsCompleted { get; set; } = false;
    public bool HasBeenChecked { get; set; } = false;
    public int Durability { get; set; } = 100; // Trap wear (0-100)
    public List<CaughtFish> CaughtFish { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class CaughtFish
{
    public string FishType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public int Size { get; set; }
    public double Weight { get; set; }
    public FishTrait Traits { get; set; } = FishTrait.None;
    public DateTime CaughtAt { get; set; } = DateTime.UtcNow;
}

public enum TrapType
{
    Basic,
    Shallow,
    Deep,
    Rare
}