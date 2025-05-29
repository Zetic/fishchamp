namespace ZPT.Models;

public class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string Area { get; set; } = "Lake";
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public int Gold { get; set; } = 100;
    public string EquippedRod { get; set; } = "Basic Rod";
    public string EquippedBait { get; set; } = "Worms";
    public Dictionary<string, int> Inventory { get; set; } = new();
    public Dictionary<string, int> Equipment { get; set; } = new();
    public Dictionary<string, int> Fish { get; set; } = new();
    public List<Aquarium> Aquariums { get; set; } = new();
    public Dictionary<string, int> Traps { get; set; } = new();
    public DateTime LastFished { get; set; } = DateTime.MinValue;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; } = DateTime.UtcNow;
}