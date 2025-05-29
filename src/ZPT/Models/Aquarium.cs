namespace ZPT.Models;

public class Aquarium
{
    public string Name { get; set; } = string.Empty;
    public List<AquariumFish> Fish { get; set; } = new();
    public List<string> Decorations { get; set; } = new();
    public int Happiness { get; set; } = 0;
    public int WaterQuality { get; set; } = 100;
    public int Temperature { get; set; } = 75;
    public DateTime LastFed { get; set; } = DateTime.MinValue;
    public DateTime LastCleaned { get; set; } = DateTime.UtcNow;
    public DateTime LastMaintenance { get; set; } = DateTime.UtcNow;
}

public class AquariumFish
{
    public string Name { get; set; } = string.Empty;
    public string CustomName { get; set; } = string.Empty;
    public int Happiness { get; set; } = 50;
    public int Hunger { get; set; } = 0;
    public string Size { get; set; } = "Normal";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastFed { get; set; } = DateTime.MinValue;
    public bool IsBaby { get; set; } = false;
}