namespace FishChamp.Data.Models;

public class PlayerProfile
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string CurrentArea { get; set; } = "starter_lake";
    public int FishCoins { get; set; } = 100;
    public DateTime LastActive { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public string EquippedRod { get; set; } = "basic_rod";
    public string EquippedBait { get; set; } = string.Empty;
}