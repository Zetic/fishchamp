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
    public string CurrentFishingSpot { get; set; } = string.Empty;
    public Dictionary<string, double> BiggestCatch { get; set; } = new(); // Fish type -> weight
    public List<string> UnlockedAreas { get; set; } = ["starter_lake"]; // Areas unlocked by this player
    public List<ActiveBuff> ActiveBuffs { get; set; } = new(); // Active meal buffs
    public int CookingLevel { get; set; } = 1; // Cooking skill level
    public int CookingExperience { get; set; } = 0; // Cooking XP
    public int CraftingLevel { get; set; } = 1; // General crafting skill level
    public int CraftingExperience { get; set; } = 0; // General crafting XP
    public List<string> UnlockedBlueprints { get; set; } = new(); // Unlocked blueprint IDs
    public Dictionary<string, DateTime> ActiveCraftingJobs { get; set; } = new(); // Item ID -> completion time
    public string? EquippedBoat { get; set; } = null; // Currently equipped boat ID
    public List<OwnedPlot> OwnedPlots { get; set; } = new(); // Player-owned land plots
    
    // Social Systems
    public string? GuildId { get; set; } = null; // Current guild membership
    public List<string> TournamentTitles { get; set; } = new(); // Earned tournament titles
    public Dictionary<string, int> TournamentStats { get; set; } = new(); // Tournament wins, participations, etc.
    public List<string> EventParticipations { get; set; } = new(); // Event IDs participated in
    public Dictionary<string, object> SocialStats { get; set; } = new(); // Trading stats, guild contributions, etc.
}

public class ActiveBuff
{
    public string BuffId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Dictionary<string, object> Effects { get; set; } = new();
}