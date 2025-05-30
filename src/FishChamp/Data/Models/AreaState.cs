namespace FishChamp.Data.Models;

public class AreaState
{
    public string AreaId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FishingSpot> FishingSpots { get; set; } = new();
    public List<FarmSpot> FarmSpots { get; set; } = new();
    public List<string> ConnectedAreas { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsUnlocked { get; set; } = true;
    public string UnlockRequirement { get; set; } = string.Empty;
    public Dictionary<string, ShopInventory> Shops { get; set; } = new();
}

public enum FishingSpotType
{
    Land, // For land-based fishing spots like ponds or streams
    Water, // For water bodies like lakes, rivers, seas
}

public class FishingSpot
{
    public string SpotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FishingSpotType Type { get; set; } = FishingSpotType.Land; // land, water
    public List<string> AvailableFish { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class FarmSpot
{
    public string SpotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> AvailableCrops { get; set; } = new();
    public bool CanDigForWorms { get; set; } = false;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class ShopInventory
{
    public string ShopId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ShopItem> Items { get; set; } = new();
}