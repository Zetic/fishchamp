namespace FishChamp.Data.Models;

public class FishData
{
    public string Name { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public int MinSize { get; set; } = 10;
    public int MaxSize { get; set; } = 30;
    public int BaseValue { get; set; } = 1;
    public double CatchChance { get; set; } = 0.5;
}

public class Equipment
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Rod, Bait
    public int Power { get; set; } = 1;
    public int Durability { get; set; } = 100;
    public Dictionary<string, object> Properties { get; set; } = new();
}