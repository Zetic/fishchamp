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
}

public class FishingSpot
{
    public string SpotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "water"; // water, land
    public List<string> AvailableFish { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class FarmSpot
{
    public string SpotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "soil"; // soil, magical, etc.
    public List<string> AvailableCrops { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}