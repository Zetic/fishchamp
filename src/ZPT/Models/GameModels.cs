namespace ZPT.Models;

public class FishingArea
{
    public string Name { get; set; } = string.Empty;
    public List<string> Fish { get; set; } = new();
    public int Difficulty { get; set; }
    public int Level { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Fish
{
    public string Name { get; set; } = string.Empty;
    public int BaseValue { get; set; }
    public string Rarity { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> Areas { get; set; } = new();
    public string? SpecialEffect { get; set; }
}

public class Rod
{
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public int Durability { get; set; }
    public double SuccessRate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int RequiredLevel { get; set; } = 1;
    public string? SpecialAbility { get; set; }
}

public class Bait
{
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public double Effectiveness { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> PreferredFish { get; set; } = new();
}

public class AquariumType
{
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public int Capacity { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Decoration
{
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public int HappinessBonus { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Trap
{
    public string Name { get; set; } = string.Empty;
    public int Price { get; set; }
    public int Duration { get; set; }
    public double SuccessRate { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> AvailableAreas { get; set; } = new();
}