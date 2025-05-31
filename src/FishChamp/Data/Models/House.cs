namespace FishChamp.Data.Models;

public class House
{
    public string HouseId { get; set; } = string.Empty;
    public ulong OwnerId { get; set; }
    public string PlotId { get; set; } = string.Empty;
    public string AreaId { get; set; } = string.Empty;
    public string Name { get; set; } = "My House";
    public HouseLayout Layout { get; set; } = HouseLayout.Cozy;
    public List<Room> Rooms { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class Room
{
    public string RoomId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public RoomType Type { get; set; } = RoomType.Living;
    public string Description { get; set; } = string.Empty;
    public List<Furniture> Furniture { get; set; } = new();
    public List<string> CraftingStations { get; set; } = new(); // Station IDs
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class Furniture
{
    public string FurnitureId { get; set; } = Guid.NewGuid().ToString();
    public string ItemId { get; set; } = string.Empty; // Reference to crafted item
    public string Name { get; set; } = string.Empty;
    public FurnitureType Type { get; set; } = FurnitureType.Decoration;
    public string Position { get; set; } = "center"; // Position in room layout
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<FurnitureBuff> Buffs { get; set; } = new(); // Buffs provided by this furniture
}

public class FurnitureBuff
{
    public string BuffId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Effects { get; set; } = new();
}

public enum HouseLayout
{
    Cozy,      // Small single room layout
    Spacious,  // Medium multi-room layout
    Mansion    // Large multi-story layout
}

public enum RoomType
{
    Living,    // Main living area
    Kitchen,   // Cooking area
    Bedroom,   // Rest area  
    Workshop,  // Crafting area
    Storage,   // Storage room
    Garden     // Indoor garden
}

public enum FurnitureType
{
    Decoration,  // Purely aesthetic
    Storage,     // Storage containers
    Seating,     // Chairs, sofas
    Table,       // Tables, desks
    Bed,         // Beds, sleeping furniture
    Appliance,   // Functional items (stove, etc.)
    CraftingStation // Crafting workbenches
}