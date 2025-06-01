namespace FishChamp.Data.Models;

public class Tournament
{
    public string TournamentId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TournamentType Type { get; set; } = TournamentType.HeaviestCatch;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<TournamentEntry> Entries { get; set; } = new();
    public List<TournamentReward> Rewards { get; set; } = new();
    public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;
    public string? AreaRestriction { get; set; } = null; // null means global
    public string? GuildId { get; set; } = null; // null means public tournament
    public int MaxParticipants { get; set; } = 0; // 0 means unlimited
}

public class TournamentEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public double Score { get; set; } = 0; // Weight for heaviest catch, count for most unique fish
    public string? FishType { get; set; } = null; // For heaviest catch tournaments
    public List<string> UniqueFishCaught { get; set; } = new(); // For most unique fish tournaments
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int Rank { get; set; } = 0;
}

public class TournamentReward
{
    public int Rank { get; set; } // 1st, 2nd, 3rd place, etc.
    public int FishCoins { get; set; } = 0;
    public List<RewardItem> Items { get; set; } = new();
    public string? Title { get; set; } = null; // Special title for profile
}

public class RewardItem
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum TournamentType
{
    HeaviestCatch,
    MostUniqueFish,
    MostFishCaught,
    SpecificFishType,
    BiggestCollectiveGuild
}

public enum TournamentStatus
{
    Upcoming,
    Active,
    Ended,
    Cancelled
}

public class Leaderboard
{
    public string LeaderboardId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LeaderboardType Type { get; set; }
    public List<LeaderboardEntry> Entries { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class LeaderboardEntry
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? AdditionalInfo { get; set; } = null; // Fish type, guild name, etc.
    public int Rank { get; set; }
}

public enum LeaderboardType
{
    RichestPlayers,
    HighestLevel,
    HeaviestSingleCatch,
    MostUniqueFish,
    BestGuilds
}