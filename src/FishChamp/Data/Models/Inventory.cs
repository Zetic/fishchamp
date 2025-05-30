namespace FishChamp.Data.Models;

public class Inventory
{
    public ulong UserId { get; set; }
    public List<InventoryItem> Items { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class InventoryItem
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // Fish, Rod, Bait, etc.
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
}