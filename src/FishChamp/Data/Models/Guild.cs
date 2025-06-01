namespace FishChamp.Data.Models;

public class Guild
{
    public string GuildId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ulong OwnerId { get; set; }
    public List<GuildMember> Members { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public string? Tag { get; set; } = null; // Guild tag, e.g., [FISH]
    public bool IsPublic { get; set; } = true;
    public int MaxMembers { get; set; } = 20;
    public List<GuildGoal> Goals { get; set; } = new();
    public Dictionary<string, object> Stats { get; set; } = new(); // Total fish caught, coins earned, etc.
    public List<string> SharedHouses { get; set; } = new(); // House IDs accessible to all members
    public string? Banner { get; set; } = null; // Custom emoji or banner
}

public class GuildMember
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public GuildRole Role { get; set; } = GuildRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public int ContributionPoints { get; set; } = 0;
    public Dictionary<string, int> Contributions { get; set; } = new(); // Fish caught, coins donated, etc.
    public DateTime LastActive { get; set; } = DateTime.UtcNow;
}

public class GuildGoal
{
    public string GoalId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public GoalType Type { get; set; }
    public int TargetAmount { get; set; }
    public int CurrentAmount { get; set; } = 0;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    public List<RewardItem> Rewards { get; set; } = new();
    public string? TargetFishType { get; set; } = null; // For specific fish goals
}

public class GuildInvitation
{
    public string InvitationId { get; set; } = Guid.NewGuid().ToString();
    public string GuildId { get; set; } = string.Empty;
    public string GuildName { get; set; } = string.Empty;
    public ulong InviterId { get; set; }
    public string InviterUsername { get; set; } = string.Empty;
    public ulong TargetUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
}

public enum GuildRole
{
    Member,
    Officer,
    Leader,
    Owner
}

public enum GoalType
{
    CatchFish, // Catch X total fish
    CatchSpecificFish, // Catch X of a specific fish type
    EarnCoins, // Earn X fish coins collectively
    ReachLevel, // Have X members reach level Y
    CompleteTournament, // Win/place in X tournaments
    BuildHouses // Build X houses for the guild
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired,
    Cancelled
}