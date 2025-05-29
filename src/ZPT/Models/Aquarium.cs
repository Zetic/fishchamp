namespace ZPT.Models;

public class Aquarium
{
    public string Name { get; set; } = string.Empty;
    public List<AquariumFish> Fish { get; set; } = new();
    public List<string> Decorations { get; set; } = new();
    public int Happiness { get; set; } = 0;
    public DateTime LastFed { get; set; } = DateTime.MinValue;
}

public class AquariumFish
{
    public string Name { get; set; } = string.Empty;
    public int Happiness { get; set; } = 50;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}