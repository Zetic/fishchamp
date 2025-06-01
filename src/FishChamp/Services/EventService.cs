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
            await eventRepository.UpdateEventAsync(ev);
            _logger.LogInformation($"Activated event: {ev.Name}");
        }

        // End active events
        var activeEvents = allEvents.Where(e => e.Status == EventStatus.Active && e.EndDate <= now);
        foreach (var ev in activeEvents)
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
}