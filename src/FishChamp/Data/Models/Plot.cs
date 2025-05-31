namespace FishChamp.Data.Models;

public class Plot
{
    public string PlotId { get; set; } = string.Empty;
    public string AreaId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public ulong? OwnerId { get; set; } = null; // null means available for purchase
    public DateTime? PurchasedAt { get; set; } = null;
    public PlotSize Size { get; set; } = PlotSize.Small;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum PlotSize
{
    Small,   // 1x1 plot
    Medium,  // 2x2 plot  
    Large    // 3x3 plot
}

public class OwnedPlot
{
    public string PlotId { get; set; } = string.Empty;
    public string AreaId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public PlotSize Size { get; set; } = PlotSize.Small;
    public string? HouseId { get; set; } = null; // Reference to house built on this plot
    public Dictionary<string, object> Properties { get; set; } = new();
}