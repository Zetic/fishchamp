using FishChamp.Data.Models;

namespace FishChamp.Data.Models;

public class Blueprint
{
    public string BlueprintId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BlueprintType Type { get; set; } = BlueprintType.Tool;
    public BlueprintTier Tier { get; set; } = BlueprintTier.Basic;
    public Dictionary<string, int> Materials { get; set; } = new();
    public CraftResult Result { get; set; } = new();
    public CraftingRequirements Requirements { get; set; } = new();
    public CraftingDifficulty Difficulty { get; set; } = new();
}

public class CraftResult
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class CraftingRequirements
{
    public int CraftingLevel { get; set; } = 1;
    public string? RequiredStation { get; set; } = null;
    public List<string> UnlockedBy { get; set; } = new(); // Prerequisites (other blueprints, achievements, etc.)
}

public class CraftingDifficulty
{
    public double BaseSuccessRate { get; set; } = 1.0; // 0.0 to 1.0
    public int CraftingTimeMinutes { get; set; } = 0; // 0 for instant
    public int ExperienceGained { get; set; } = 10;
}

public enum BlueprintType
{
    Tool,      // Fishing rods, nets, etc.
    Furniture, // House furniture, decorations
    Equipment, // Traps, tools, etc.
    Station    // Crafting stations
}

public enum BlueprintTier
{
    Basic = 1,
    Advanced = 2,
    Expert = 3,
    Master = 4,
    Legendary = 5
}