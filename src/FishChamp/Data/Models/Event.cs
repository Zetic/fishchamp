namespace FishChamp.Data.Models;

public class SeasonalEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EventSeason Season { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Upcoming;
    public List<EventFish> SpecialFish { get; set; } = new();
    public List<EventItem> SpecialItems { get; set; } = new();
    public List<EventReward> Rewards { get; set; } = new();
    public Dictionary<string, object> EventProperties { get; set; } = new(); // Weather effects, spawn rates, etc.
    public string? BackgroundMusic { get; set; } = null; // Special music/ambiance
    public string? SpecialEmoji { get; set; } = null; // Event-specific emoji
    
    // Phase 11 additions
    public EventType Type { get; set; } = EventType.Seasonal; // Event type for dynamic mechanics
    public List<EventPhase> Phases { get; set; } = new(); // Multi-phase progression
    public int CurrentPhase { get; set; } = 0; // Current active phase index
    public bool IsTriggerable { get; set; } = false; // Can be triggered by admins/players
    public Dictionary<string, double> EffectModifiers { get; set; } = new(); // Fishing bonuses, spawn rate changes, etc.
}

public class EventFish
{
    public string FishId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double MinWeight { get; set; } = 0.1;
    public double MaxWeight { get; set; } = 10.0;
    public ItemRarity Rarity { get; set; } = ItemRarity.Rare;
    public List<string> AvailableAreas { get; set; } = new();
    public double SpawnRate { get; set; } = 0.05; // 5% chance
    public bool IsLimited { get; set; } = true; // Only available during event
    public string? SpecialEmoji { get; set; } = null;
    public List<FishTrait> Traits { get; set; } = new();
}

public class EventItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // Rod, Bait, Decoration, Cosmetic
    public string Description { get; set; } = string.Empty;
    public ItemRarity Rarity { get; set; } = ItemRarity.Epic;
    public bool IsLimited { get; set; } = true;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? SpecialEmoji { get; set; } = null;
}

public class EventReward
{
    public string RewardId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public RewardType Type { get; set; }
    public List<RewardItem> Items { get; set; } = new();
    public int FishCoins { get; set; } = 0;
    public string? SpecialTitle { get; set; } = null;
    public RequirementType RequirementType { get; set; }
    public int RequiredAmount { get; set; } = 1;
    public string? RequiredFishType { get; set; } = null;
    public bool IsClaimed { get; set; } = false;
}

public class EventParticipation
{
    public string ParticipationId { get; set; } = Guid.NewGuid().ToString();
    public string EventId { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Dictionary<string, int> EventProgress { get; set; } = new(); // Fish caught, items collected, etc.
    public List<string> ClaimedRewards { get; set; } = new();
    public DateTime FirstParticipation { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public int EventPoints { get; set; } = 0;
}

public enum EventSeason
{
    Spring,
    Summer,
    Autumn,
    Winter,
    Halloween,
    Christmas,
    Easter,
    Special // For one-off events
}

public enum EventStatus
{
    Upcoming,
    Active,
    Ending, // Last day/hours
    Ended,
    Cancelled
}

public enum RewardType
{
    Participation, // Just for participating
    Milestone, // Reach X fish caught/points
    Completion, // Complete all event objectives
    Leaderboard, // Top X players
    Daily, // Daily login/activity
    Secret // Hidden achievements
}

public enum RequirementType
{
    CatchEventFish, // Catch X event fish
    CatchAnyFish, // Catch X fish during event
    EarnEventPoints, // Earn X event points
    LoginDaily, // Login X days during event
    UseEventItem, // Use event items X times
    CompleteObjective // Complete specific objective
}

public class EventPhase
{
    public string PhaseId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(24); // Default 24 hours
    public Dictionary<string, double> PhaseModifiers { get; set; } = new(); // Phase-specific modifiers
    public List<EventFish> PhaseFish { get; set; } = new(); // Fish only available in this phase
    public string? PhaseEmoji { get; set; } = null;
    public bool RequiresProgression { get; set; } = false; // Requires completion of objectives to advance
    public List<PhaseObjective> Objectives { get; set; } = new(); // Objectives for phase progression
}

public class PhaseObjective
{
    public string ObjectiveId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RequirementType Type { get; set; }
    public int RequiredAmount { get; set; } = 1;
    public int CurrentProgress { get; set; } = 0; // Global progress for community objectives
    public bool IsComplete { get; set; } = false;
}

public class WorldBossEvent
{
    public string BossId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredPlayers { get; set; } = 3;
    public int MaxHealth { get; set; } = 1000;
    public int CurrentHealth { get; set; } = 1000;
    public List<ulong> Participants { get; set; } = new();
    public Dictionary<ulong, int> PlayerContributions { get; set; } = new(); // Damage dealt by each player
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; } = null;
    public BossStatus Status { get; set; } = BossStatus.Waiting;
    public List<EventReward> BossRewards { get; set; } = new();
    public string? BossEmoji { get; set; } = null;
}

public enum EventType
{
    Seasonal, // Traditional seasonal events
    Dynamic, // Dynamic events that alter mechanics (Flood Season, Volcanic Unrest)
    WorldBoss, // Co-op legendary hunts
    Community, // Community-wide objectives
    Triggered // Admin or player triggered events
}

public enum BossStatus
{
    Waiting, // Waiting for enough players
    Active, // Battle in progress
    Defeated, // Boss defeated
    Escaped, // Boss escaped (time ran out)
    Failed // Not enough participants
}