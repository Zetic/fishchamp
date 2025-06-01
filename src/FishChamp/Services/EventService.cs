using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FishChamp.Services;

public class EventService : BackgroundService
{
    private readonly ILogger<EventService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(1); // Check every hour

    public EventService(ILogger<EventService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

                await ProcessEventsAsync(eventRepository);
                await CreateSeasonalEventsAsync(eventRepository);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing events");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    private async Task ProcessEventsAsync(IEventRepository eventRepository)
    {
        var allEvents = await eventRepository.GetAllEventsAsync();
        var now = DateTime.UtcNow;

        // Activate upcoming events
        var upcomingEvents = allEvents.Where(e => e.Status == EventStatus.Upcoming && e.StartDate <= now);
        foreach (var ev in upcomingEvents)
        {
            ev.Status = EventStatus.Active;
            // Initialize phase progression for multi-phase events
            if (ev.Phases.Any())
            {
                ev.CurrentPhase = 0;
                _logger.LogInformation($"Activated multi-phase event: {ev.Name} - Starting Phase {ev.CurrentPhase + 1}: {ev.Phases[ev.CurrentPhase].Name}");
            }
            else
            {
                _logger.LogInformation($"Activated event: {ev.Name}");
            }
            await eventRepository.UpdateEventAsync(ev);
        }

        // Process phase transitions for active events
        var activeEvents = allEvents.Where(e => e.Status == EventStatus.Active);
        foreach (var ev in activeEvents)
        {
            if (ev.Phases.Any() && await ProcessEventPhases(ev, now))
            {
                await eventRepository.UpdateEventAsync(ev);
            }
        }

        // End active events
        var expiredEvents = allEvents.Where(e => e.Status == EventStatus.Active && e.EndDate <= now);
        foreach (var ev in expiredEvents)
        {
            ev.Status = EventStatus.Ended;
            await eventRepository.UpdateEventAsync(ev);
            _logger.LogInformation($"Ended event: {ev.Name}");
        }

        // Mark events ending soon
        var endingSoonEvents = allEvents.Where(e => 
            e.Status == EventStatus.Active && 
            e.EndDate <= now.AddDays(1) && 
            e.EndDate > now);
        
        foreach (var ev in endingSoonEvents)
        {
            if (ev.Status != EventStatus.Ending)
            {
                ev.Status = EventStatus.Ending;
                await eventRepository.UpdateEventAsync(ev);
                _logger.LogInformation($"Event ending soon: {ev.Name}");
            }
        }

        // Process world boss events
        await ProcessWorldBossEventsAsync(eventRepository, now);
    }

    private async Task<bool> ProcessEventPhases(SeasonalEvent eventObj, DateTime now)
    {
        if (!eventObj.Phases.Any() || eventObj.CurrentPhase >= eventObj.Phases.Count)
            return false;

        var currentPhase = eventObj.Phases[eventObj.CurrentPhase];
        var phaseStartTime = eventObj.StartDate.Add(
            TimeSpan.FromTicks(eventObj.Phases.Take(eventObj.CurrentPhase)
                .Sum(p => p.Duration.Ticks)));

        // Check if current phase should end
        if (now >= phaseStartTime.Add(currentPhase.Duration))
        {
            // Move to next phase
            eventObj.CurrentPhase++;
            
            if (eventObj.CurrentPhase >= eventObj.Phases.Count)
            {
                // Event completed all phases
                _logger.LogInformation($"Event {eventObj.Name} completed all phases");
                return true;
            }

            var nextPhase = eventObj.Phases[eventObj.CurrentPhase];
            _logger.LogInformation($"Event {eventObj.Name} progressed to Phase {eventObj.CurrentPhase + 1}: {nextPhase.Name}");
            
            // TODO: Send phase transition notifications to Discord channels
            return true;
        }

        return false;
    }

    private async Task ProcessWorldBossEventsAsync(IEventRepository eventRepository, DateTime now)
    {
        var activeBosses = await eventRepository.GetActiveWorldBossesAsync();
        
        foreach (var boss in activeBosses)
        {
            // Check for timeout (30 minutes for boss encounters)
            if (boss.Status == BossStatus.Active && now >= boss.StartTime.AddMinutes(30))
            {
                boss.Status = BossStatus.Escaped;
                boss.EndTime = now;
                await eventRepository.UpdateWorldBossAsync(boss);
                _logger.LogInformation($"World boss {boss.Name} escaped due to timeout");
            }
            // Check if waiting too long for participants (10 minutes)
            else if (boss.Status == BossStatus.Waiting && now >= boss.StartTime.AddMinutes(10))
            {
                boss.Status = BossStatus.Failed;
                boss.EndTime = now;
                await eventRepository.UpdateWorldBossAsync(boss);
                _logger.LogInformation($"World boss {boss.Name} failed - not enough participants");
            }
        }
    }

    private async Task CreateSeasonalEventsAsync(IEventRepository eventRepository)
    {
        var now = DateTime.UtcNow;
        var allEvents = await eventRepository.GetAllEventsAsync();

        // Check if we need to create seasonal events
        await CreateSeasonalEventIfNeeded(eventRepository, allEvents, EventSeason.Spring, now);
        await CreateSeasonalEventIfNeeded(eventRepository, allEvents, EventSeason.Summer, now);
        await CreateSeasonalEventIfNeeded(eventRepository, allEvents, EventSeason.Autumn, now);
        await CreateSeasonalEventIfNeeded(eventRepository, allEvents, EventSeason.Winter, now);
        
        // Check for special holidays
        await CreateHolidayEventIfNeeded(eventRepository, allEvents, now);
        
        // Create dynamic events periodically (Phase 11 addition)
        await CreateDynamicEventsIfNeeded(eventRepository, allEvents, now);
    }

    private async Task CreateSeasonalEventIfNeeded(IEventRepository eventRepository, List<SeasonalEvent> allEvents, EventSeason season, DateTime now)
    {
        var seasonStartMonth = GetSeasonStartMonth(season);
        var seasonEndMonth = GetSeasonEndMonth(season);

        // Check if current month is in season
        if (now.Month < seasonStartMonth || now.Month > seasonEndMonth)
            return;

        // Check if there's already an active event for this season this year
        var existingEvent = allEvents.FirstOrDefault(e => 
            e.Season == season && 
            e.StartDate.Year == now.Year &&
            (e.Status == EventStatus.Active || e.Status == EventStatus.Upcoming));

        if (existingEvent != null)
            return;

        // Create new seasonal event
        var seasonEvent = CreateSeasonalEvent(season, now.Year);
        await eventRepository.CreateEventAsync(seasonEvent);
        _logger.LogInformation($"Created {season} event for {now.Year}");
    }

    private async Task CreateHolidayEventIfNeeded(IEventRepository eventRepository, List<SeasonalEvent> allEvents, DateTime now)
    {
        // Halloween event (October)
        if (now.Month == 10 && !allEvents.Any(e => 
            e.Season == EventSeason.Halloween && 
            e.StartDate.Year == now.Year &&
            (e.Status == EventStatus.Active || e.Status == EventStatus.Upcoming)))
        {
            var halloweenEvent = CreateHalloweenEvent(now.Year);
            await eventRepository.CreateEventAsync(halloweenEvent);
            _logger.LogInformation($"Created Halloween event for {now.Year}");
        }

        // Christmas event (December)
        if (now.Month == 12 && !allEvents.Any(e => 
            e.Season == EventSeason.Christmas && 
            e.StartDate.Year == now.Year &&
            (e.Status == EventStatus.Active || e.Status == EventStatus.Upcoming)))
        {
            var christmasEvent = CreateChristmasEvent(now.Year);
            await eventRepository.CreateEventAsync(christmasEvent);
            _logger.LogInformation($"Created Christmas event for {now.Year}");
        }

        // Easter event (April)
        if (now.Month == 4 && !allEvents.Any(e => 
            e.Season == EventSeason.Easter && 
            e.StartDate.Year == now.Year &&
            (e.Status == EventStatus.Active || e.Status == EventStatus.Upcoming)))
        {
            var easterEvent = CreateEasterEvent(now.Year);
            await eventRepository.CreateEventAsync(easterEvent);
            _logger.LogInformation($"Created Easter event for {now.Year}");
        }
    }

    private static int GetSeasonStartMonth(EventSeason season)
    {
        return season switch
        {
            EventSeason.Spring => 3,  // March
            EventSeason.Summer => 6,  // June
            EventSeason.Autumn => 9,  // September
            EventSeason.Winter => 12, // December
            _ => 1
        };
    }

    private static int GetSeasonEndMonth(EventSeason season)
    {
        return season switch
        {
            EventSeason.Spring => 5,  // May
            EventSeason.Summer => 8,  // August
            EventSeason.Autumn => 11, // November
            EventSeason.Winter => 2,  // February (next year)
            _ => 12
        };
    }

    private static SeasonalEvent CreateSeasonalEvent(EventSeason season, int year)
    {
        var startDate = new DateTime(year, GetSeasonStartMonth(season), 1);
        var endDate = new DateTime(year, GetSeasonEndMonth(season), DateTime.DaysInMonth(year, GetSeasonEndMonth(season)));

        return new SeasonalEvent
        {
            Name = $"{season} Festival {year}",
            Description = GetSeasonDescription(season),
            Season = season,
            StartDate = startDate,
            EndDate = endDate,
            Status = startDate <= DateTime.UtcNow ? EventStatus.Active : EventStatus.Upcoming,
            SpecialFish = GetSeasonalFish(season),
            SpecialItems = GetSeasonalItems(season),
            Rewards = GetSeasonalRewards(season)
        };
    }

    private static SeasonalEvent CreateHalloweenEvent(int year)
    {
        return new SeasonalEvent
        {
            Name = $"Spooky Halloween Fishing {year}",
            Description = "Mysterious fish have appeared in the dark waters! Catch ghostly fish and earn spooky rewards!",
            Season = EventSeason.Halloween,
            StartDate = new DateTime(year, 10, 15),
            EndDate = new DateTime(year, 11, 1),
            Status = EventStatus.Upcoming,
            SpecialFish = GetHalloweenFish(),
            SpecialItems = GetHalloweenItems(),
            Rewards = GetHalloweenRewards(),
            SpecialEmoji = "🎃"
        };
    }

    private static SeasonalEvent CreateChristmasEvent(int year)
    {
        return new SeasonalEvent
        {
            Name = $"Winter Wonderland Fishing {year}",
            Description = "Frozen lakes hide magical winter fish! Catch festive fish and spread holiday cheer!",
            Season = EventSeason.Christmas,
            StartDate = new DateTime(year, 12, 1),
            EndDate = new DateTime(year, 12, 31),
            Status = EventStatus.Upcoming,
            SpecialFish = GetChristmasFish(),
            SpecialItems = GetChristmasItems(),
            Rewards = GetChristmasRewards(),
            SpecialEmoji = "🎄"
        };
    }

    private static SeasonalEvent CreateEasterEvent(int year)
    {
        return new SeasonalEvent
        {
            Name = $"Spring Renewal Festival {year}",
            Description = "Spring has brought colorful new fish to the waters! Hunt for rainbow fish and Easter treasures!",
            Season = EventSeason.Easter,
            StartDate = new DateTime(year, 4, 1),
            EndDate = new DateTime(year, 4, 30),
            Status = EventStatus.Upcoming,
            SpecialFish = GetEasterFish(),
            SpecialItems = GetEasterItems(),
            Rewards = GetEasterRewards(),
            SpecialEmoji = "🐰"
        };
    }

    private static string GetSeasonDescription(EventSeason season)
    {
        return season switch
        {
            EventSeason.Spring => "Fresh spring waters teem with new life! Catch blooming fish and enjoy the season's bounty.",
            EventSeason.Summer => "The warm summer sun brings active fish to the surface! Perfect fishing weather awaits.",
            EventSeason.Autumn => "As leaves fall, mysterious fish emerge from the depths. Harvest the autumn bounty!",
            EventSeason.Winter => "Ice fishing reveals rare winter species. Brave the cold for exclusive catches!",
            _ => "A special seasonal event with unique fish and rewards!"
        };
    }

    private static List<EventFish> GetSeasonalFish(EventSeason season)
    {
        return season switch
        {
            EventSeason.Spring => [
                new() { FishId = "spring_blossom", Name = "Blossom Trout", Rarity = ItemRarity.Rare, SpecialEmoji = "🌸", SpawnRate = 0.1 },
                new() { FishId = "rainbow_bass", Name = "Rainbow Bass", Rarity = ItemRarity.Epic, SpecialEmoji = "🌈", SpawnRate = 0.05 }
            ],
            EventSeason.Summer => [
                new() { FishId = "sunfish_golden", Name = "Golden Sunfish", Rarity = ItemRarity.Rare, SpecialEmoji = "☀️", SpawnRate = 0.1 },
                new() { FishId = "tropical_angelfish", Name = "Tropical Angelfish", Rarity = ItemRarity.Epic, SpecialEmoji = "🐠", SpawnRate = 0.05 }
            ],
            EventSeason.Autumn => [
                new() { FishId = "amber_perch", Name = "Amber Perch", Rarity = ItemRarity.Rare, SpecialEmoji = "🍂", SpawnRate = 0.1 },
                new() { FishId = "harvest_carp", Name = "Harvest Carp", Rarity = ItemRarity.Epic, SpecialEmoji = "🎃", SpawnRate = 0.05 }
            ],
            EventSeason.Winter => [
                new() { FishId = "ice_salmon", Name = "Ice Salmon", Rarity = ItemRarity.Rare, SpecialEmoji = "❄️", SpawnRate = 0.1 },
                new() { FishId = "crystal_trout", Name = "Crystal Trout", Rarity = ItemRarity.Legendary, SpecialEmoji = "💎", SpawnRate = 0.02 }
            ],
            _ => []
        };
    }

    private static List<EventFish> GetHalloweenFish()
    {
        return [
            new() { FishId = "ghost_fish", Name = "Ghost Fish", Rarity = ItemRarity.Epic, SpecialEmoji = "👻", SpawnRate = 0.05 },
            new() { FishId = "vampire_eel", Name = "Vampire Eel", Rarity = ItemRarity.Legendary, SpecialEmoji = "🧛", SpawnRate = 0.02 },
            new() { FishId = "pumpkin_bass", Name = "Pumpkin Bass", Rarity = ItemRarity.Rare, SpecialEmoji = "🎃", SpawnRate = 0.08 }
        ];
    }

    private static List<EventFish> GetChristmasFish()
    {
        return [
            new() { FishId = "candy_cane_fish", Name = "Candy Cane Fish", Rarity = ItemRarity.Rare, SpecialEmoji = "🍭", SpawnRate = 0.08 },
            new() { FishId = "snow_angel_fish", Name = "Snow Angel Fish", Rarity = ItemRarity.Epic, SpecialEmoji = "👼", SpawnRate = 0.05 },
            new() { FishId = "christmas_star", Name = "Christmas Star Fish", Rarity = ItemRarity.Legendary, SpecialEmoji = "⭐", SpawnRate = 0.02 }
        ];
    }

    private static List<EventFish> GetEasterFish()
    {
        return [
            new() { FishId = "easter_egg_fish", Name = "Easter Egg Fish", Rarity = ItemRarity.Rare, SpecialEmoji = "🥚", SpawnRate = 0.08 },
            new() { FishId = "bunny_fish", Name = "Bunny Fish", Rarity = ItemRarity.Epic, SpecialEmoji = "🐰", SpawnRate = 0.05 },
            new() { FishId = "spring_flower_fish", Name = "Spring Flower Fish", Rarity = ItemRarity.Rare, SpecialEmoji = "🌷", SpawnRate = 0.08 }
        ];
    }

    private static List<EventItem> GetSeasonalItems(EventSeason season)
    {
        return [
            new() { ItemId = $"{season.ToString().ToLower()}_rod", Name = $"{season} Rod", ItemType = "Rod", Rarity = ItemRarity.Epic }
        ];
    }

    private static List<EventItem> GetHalloweenItems()
    {
        return [
            new() { ItemId = "spooky_rod", Name = "Spooky Rod", ItemType = "Rod", Rarity = ItemRarity.Epic, SpecialEmoji = "🎃" },
            new() { ItemId = "ghost_bait", Name = "Ghost Bait", ItemType = "Bait", Rarity = ItemRarity.Rare, SpecialEmoji = "👻" }
        ];
    }

    private static List<EventItem> GetChristmasItems()
    {
        return [
            new() { ItemId = "festive_rod", Name = "Festive Rod", ItemType = "Rod", Rarity = ItemRarity.Epic, SpecialEmoji = "🎄" },
            new() { ItemId = "christmas_decoration", Name = "Christmas Decoration", ItemType = "Decoration", Rarity = ItemRarity.Rare, SpecialEmoji = "🎁" }
        ];
    }

    private static List<EventItem> GetEasterItems()
    {
        return [
            new() { ItemId = "easter_rod", Name = "Easter Rod", ItemType = "Rod", Rarity = ItemRarity.Epic, SpecialEmoji = "🐰" },
            new() { ItemId = "flower_decoration", Name = "Flower Decoration", ItemType = "Decoration", Rarity = ItemRarity.Rare, SpecialEmoji = "🌷" }
        ];
    }

    private static List<EventReward> GetSeasonalRewards(EventSeason season)
    {
        return [
            new() { Name = "Participation Reward", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 5, FishCoins = 100 },
            new() { Name = "Seasonal Explorer", Type = RewardType.Milestone, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 10, FishCoins = 500 },
            new() { Name = $"{season} Master", Type = RewardType.Completion, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 25, FishCoins = 1000, SpecialTitle = $"{season} Master" }
        ];
    }

    private static List<EventReward> GetHalloweenRewards()
    {
        return [
            new() { Name = "Spooky Participation", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 5, FishCoins = 150 },
            new() { Name = "Ghost Hunter", Type = RewardType.Milestone, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 13, FishCoins = 666, SpecialTitle = "Ghost Hunter" },
            new() { Name = "Halloween Master", Type = RewardType.Completion, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 31, FishCoins = 1313, SpecialTitle = "Spooky Master" }
        ];
    }

    private static List<EventReward> GetChristmasRewards()
    {
        return [
            new() { Name = "Holiday Spirit", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 5, FishCoins = 200 },
            new() { Name = "Christmas Helper", Type = RewardType.Milestone, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 12, FishCoins = 777, SpecialTitle = "Christmas Helper" },
            new() { Name = "Winter Wondermaster", Type = RewardType.Completion, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 25, FishCoins = 1225, SpecialTitle = "Winter Wondermaster" }
        ];
    }

    private static List<EventReward> GetEasterRewards()
    {
        return [
            new() { Name = "Spring Awakening", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 5, FishCoins = 150 },
            new() { Name = "Easter Egg Hunter", Type = RewardType.Milestone, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 15, FishCoins = 555, SpecialTitle = "Easter Egg Hunter" },
            new() { Name = "Spring Renewal Master", Type = RewardType.Completion, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 30, FishCoins = 888, SpecialTitle = "Spring Renewal Master" }
        ];
    }

    // Phase 11 additions - Dynamic Event Creation
    private async Task CreateDynamicEventsIfNeeded(IEventRepository eventRepository, List<SeasonalEvent> allEvents, DateTime now)
    {
        // Check if we should create a random dynamic event (5% chance each hour when processing)
        if (Random.Shared.NextDouble() < 0.05)
        {
            var eventTypes = Enum.GetValues<EventType>().Where(t => t == EventType.Dynamic).ToArray();
            if (eventTypes.Length > 0)
            {
                var recentDynamicEvents = allEvents.Where(e => 
                    e.Type == EventType.Dynamic && 
                    e.StartDate > now.AddDays(-7)).ToList(); // No dynamic events in last 7 days

                if (!recentDynamicEvents.Any())
                {
                    var dynamicEvent = CreateRandomDynamicEvent(now);
                    await eventRepository.CreateEventAsync(dynamicEvent);
                    _logger.LogInformation($"Created dynamic event: {dynamicEvent.Name}");
                }
            }
        }
    }

    private static SeasonalEvent CreateRandomDynamicEvent(DateTime now)
    {
        var eventTemplates = new[]
        {
            CreateFishingFrenzyEvent(now),
            CreateDarkSkiesEvent(now),
            CreateFloodSeasonEvent(now),
            CreateVolcanicUnrestEvent(now),
            CreateCelestialDriftEvent(now)
        };

        return eventTemplates[Random.Shared.Next(eventTemplates.Length)];
    }

    private static SeasonalEvent CreateFishingFrenzyEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Fishing Frenzy",
            Description = "The fish are biting like crazy! All fishing spots are active with increased catch rates!",
            Season = EventSeason.Special,
            Type = EventType.Dynamic,
            StartDate = now,
            EndDate = now.AddHours(6), // 6-hour event
            Status = EventStatus.Upcoming,
            SpecialEmoji = "🎣",
            EffectModifiers = new Dictionary<string, double>
            {
                ["bite_rate_bonus"] = 0.25, // +25% bite rate
                ["all_rods_anywhere"] = 1.0 // Any rod works anywhere
            },
            SpecialFish = [],
            SpecialItems = [],
            Rewards = [
                new() { Name = "Frenzy Participant", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 3, FishCoins = 200 },
                new() { Name = "Frenzy Master", Type = RewardType.Milestone, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 15, FishCoins = 750 }
            ]
        };
    }

    private static SeasonalEvent CreateDarkSkiesEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Dark Skies",
            Description = "Ominous clouds gather as mysterious forces stir the waters...",
            Season = EventSeason.Special,
            Type = EventType.Dynamic,
            StartDate = now,
            EndDate = now.AddHours(12), // 12-hour multi-phase event
            Status = EventStatus.Upcoming,
            SpecialEmoji = "⚡",
            Phases = [
                new EventPhase
                {
                    Name = "Storm Gathering",
                    Description = "Dark clouds form overhead, eerie fish begin to appear",
                    Duration = TimeSpan.FromHours(4),
                    PhaseModifiers = new Dictionary<string, double> { ["eerie_fish_spawn"] = 0.15 },
                    PhaseFish = [
                        new() { FishId = "shadow_fish", Name = "Shadow Fish", Rarity = ItemRarity.Rare, SpecialEmoji = "🌑", SpawnRate = 0.15 }
                    ]
                },
                new EventPhase
                {
                    Name = "Storm Peak",
                    Description = "Lightning strikes the water, storm fish emerge from the depths",
                    Duration = TimeSpan.FromHours(4),
                    PhaseModifiers = new Dictionary<string, double> { ["storm_fish_spawn"] = 0.2, ["lightning_bonus"] = 0.1 },
                    PhaseFish = [
                        new() { FishId = "storm_bass", Name = "Storm Bass", Rarity = ItemRarity.Epic, SpecialEmoji = "⚡", SpawnRate = 0.1 },
                        new() { FishId = "thunder_eel", Name = "Thunder Eel", Rarity = ItemRarity.Legendary, SpecialEmoji = "🌩️", SpawnRate = 0.05 }
                    ]
                },
                new EventPhase
                {
                    Name = "Calm After Storm",
                    Description = "The storm passes, leaving behind enriched waters with bonus rewards",
                    Duration = TimeSpan.FromHours(4),
                    PhaseModifiers = new Dictionary<string, double> { ["reward_bonus"] = 0.3, ["peaceful_waters"] = 1.0 }
                }
            ],
            EffectModifiers = new Dictionary<string, double>
            {
                ["weather_effects"] = 1.0,
                ["rare_fish_bonus"] = 0.1
            },
            Rewards = [
                new() { Name = "Storm Survivor", Type = RewardType.Participation, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 5, FishCoins = 300 },
                new() { Name = "Lightning Catcher", Type = RewardType.Milestone, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 20, FishCoins = 1000, SpecialTitle = "Storm Chaser" }
            ]
        };
    }

    private static SeasonalEvent CreateFloodSeasonEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Flood Season",
            Description = "Rising waters have flooded the docks! Only boat fishing is possible during this time.",
            Season = EventSeason.Special,
            Type = EventType.Dynamic,
            StartDate = now,
            EndDate = now.AddDays(2), // 2-day event
            Status = EventStatus.Upcoming,
            SpecialEmoji = "🌊",
            EffectModifiers = new Dictionary<string, double>
            {
                ["dock_fishing_disabled"] = 1.0,
                ["boat_fishing_required"] = 1.0,
                ["flood_fish_bonus"] = 0.2
            },
            SpecialFish = [
                new() { FishId = "flood_carp", Name = "Flood Carp", Rarity = ItemRarity.Rare, SpecialEmoji = "🌊", SpawnRate = 0.12 },
                new() { FishId = "torrent_trout", Name = "Torrent Trout", Rarity = ItemRarity.Epic, SpecialEmoji = "💧", SpawnRate = 0.08 }
            ],
            Rewards = [
                new() { Name = "Flood Navigator", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 8, FishCoins = 400 },
                new() { Name = "Master of the Deluge", Type = RewardType.Completion, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 25, FishCoins = 1200, SpecialTitle = "Flood Master" }
            ]
        };
    }

    private static SeasonalEvent CreateVolcanicUnrestEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Volcanic Unrest",
            Description = "Underwater volcanic activity has opened new lava zones! Heat-proof rods required for extreme fishing.",
            Season = EventSeason.Special,
            Type = EventType.Dynamic,
            StartDate = now,
            EndDate = now.AddDays(3), // 3-day event
            Status = EventStatus.Upcoming,
            SpecialEmoji = "🌋",
            EffectModifiers = new Dictionary<string, double>
            {
                ["lava_zone_opened"] = 1.0,
                ["heat_proof_required"] = 1.0,
                ["volcanic_fish_bonus"] = 0.25
            },
            SpecialFish = [
                new() { FishId = "magma_fish", Name = "Magma Fish", Rarity = ItemRarity.Epic, SpecialEmoji = "🔥", SpawnRate = 0.1 },
                new() { FishId = "obsidian_bass", Name = "Obsidian Bass", Rarity = ItemRarity.Legendary, SpecialEmoji = "🌋", SpawnRate = 0.03 }
            ],
            SpecialItems = [
                new() { ItemId = "heat_proof_rod", Name = "Heat-Proof Rod", ItemType = "Rod", Rarity = ItemRarity.Epic, SpecialEmoji = "🔥" }
            ],
            Rewards = [
                new() { Name = "Volcano Explorer", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 5, FishCoins = 350 },
                new() { Name = "Lava Fisher", Type = RewardType.Milestone, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 15, FishCoins = 900, SpecialTitle = "Volcano Diver" }
            ]
        };
    }

    private static SeasonalEvent CreateCelestialDriftEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Celestial Drift",
            Description = "Cosmic energies flow through all fishing zones, bringing otherworldly fish to every location!",
            Season = EventSeason.Special,
            Type = EventType.Dynamic,
            StartDate = now,
            EndDate = now.AddHours(18), // 18-hour event
            Status = EventStatus.Upcoming,
            SpecialEmoji = "✨",
            EffectModifiers = new Dictionary<string, double>
            {
                ["cosmic_fish_everywhere"] = 1.0,
                ["stellar_bonus"] = 0.15,
                ["zone_restrictions_removed"] = 1.0
            },
            SpecialFish = [
                new() { FishId = "star_fish", Name = "Star Fish", Rarity = ItemRarity.Rare, SpecialEmoji = "⭐", SpawnRate = 0.15 },
                new() { FishId = "nebula_eel", Name = "Nebula Eel", Rarity = ItemRarity.Epic, SpecialEmoji = "🌌", SpawnRate = 0.08 },
                new() { FishId = "cosmic_leviathan", Name = "Cosmic Leviathan", Rarity = ItemRarity.Legendary, SpecialEmoji = "🌠", SpawnRate = 0.02 }
            ],
            Rewards = [
                new() { Name = "Stargazer", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 6, FishCoins = 450 },
                new() { Name = "Cosmic Navigator", Type = RewardType.Completion, RequirementType = RequirementType.CatchEventFish, RequiredAmount = 20, FishCoins = 1100, SpecialTitle = "Celestial Fisher" }
            ]
        };
    }
}