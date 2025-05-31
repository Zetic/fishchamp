namespace FishChamp.Data.Models;

public class Farm
{
    public ulong UserId { get; set; }
    public string AreaId { get; set; } = string.Empty;
    public string FarmSpotId { get; set; } = string.Empty;
    public List<Crop> Crops { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class Crop
{
    public string CropId { get; set; } = Guid.NewGuid().ToString();
    public string SeedType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CropStage Stage { get; set; } = CropStage.Planted;
    public DateTime PlantedAt { get; set; } = DateTime.UtcNow;
    public DateTime ReadyAt { get; set; }
    public int GrowthTimeHours { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum CropStage
{
    Planted,
    Growing,
    Ready,
    Harvested
}

public class SeedType
{
    public string SeedId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int GrowthTimeHours { get; set; }
    public List<string> PossibleHarvests { get; set; } = new();
    public string Rarity { get; set; } = "common";
    public Dictionary<string, object> Properties { get; set; } = new();
}