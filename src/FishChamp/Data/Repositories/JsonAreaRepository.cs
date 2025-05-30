using FishChamp.Data.Models;
using Newtonsoft.Json;

namespace FishChamp.Data.Repositories;

public class JsonAreaRepository : IAreaRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "areas.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<AreaState?> GetAreaAsync(string areaId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var areas = await LoadAreasAsync();
            return areas.FirstOrDefault(a => a.AreaId == areaId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<AreaState>> GetAllAreasAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadAreasAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateAreaAsync(AreaState area)
    {
        await _semaphore.WaitAsync();
        try
        {
            var areas = await LoadAreasAsync();
            var existingArea = areas.FirstOrDefault(a => a.AreaId == area.AreaId);
            if (existingArea != null)
            {
                areas.Remove(existingArea);
                areas.Add(area);
                await SaveAreasAsync(areas);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<AreaState>> GetConnectedAreasAsync(string areaId)
    {
        var area = await GetAreaAsync(areaId);
        if (area == null) return new List<AreaState>();

        var allAreas = await GetAllAreasAsync();
        return allAreas.Where(a => area.ConnectedAreas.Contains(a.AreaId)).ToList();
    }

    private async Task<List<AreaState>> LoadAreasAsync()
    {
        if (!File.Exists(_dataPath))
        {
            return await InitializeDefaultAreasAsync();
        }

        var json = await File.ReadAllTextAsync(_dataPath);
        var areas = JsonConvert.DeserializeObject<List<AreaState>>(json) ?? new List<AreaState>();

        if (areas.Count == 0)
        {
            return await InitializeDefaultAreasAsync();
        }

        return areas;
    }

    private async Task SaveAreasAsync(List<AreaState> areas)
    {
        var json = JsonConvert.SerializeObject(areas, Formatting.Indented);
        await File.WriteAllTextAsync(_dataPath, json);
    }

    private async Task<List<AreaState>> InitializeDefaultAreasAsync()
    {
        var defaultAreas = new List<AreaState>
        {
            new()
            {
                AreaId = "starter_lake",
                Name = "Starter Lake",
                Description = "A peaceful lake perfect for beginner anglers. The water is clear and teeming with common fish.",
                FishingSpots = new List<FishingSpot>
                {
                    new()
                    {
                        SpotId = "dock",
                        Name = "Wooden Dock",
                        Type = "water",
                        AvailableFish = new List<string> { "common_carp", "bluegill", "bass" }
                    },
                    new()
                    {
                        SpotId = "shore",
                        Name = "Rocky Shore",
                        Type = "land",
                        AvailableFish = new List<string> { "minnow", "sunfish" }
                    }
                },
                ConnectedAreas = new List<string> { "mystic_lake" },
                IsUnlocked = true
            },
            new()
            {
                AreaId = "mystic_lake",
                Name = "Mystic Lake",
                Description = "A mysterious lake shrouded in mist. Rumors say rare fish dwell in its depths.",
                FishingSpots = new List<FishingSpot>
                {
                    new()
                    {
                        SpotId = "deep_waters",
                        Name = "Deep Waters",
                        Type = "water",
                        AvailableFish = new List<string> { "rainbow_trout", "pike", "mysterious_eel" }
                    }
                },
                ConnectedAreas = new List<string> { "starter_lake" },
                IsUnlocked = false,
                UnlockRequirement = "Catch 5 different fish species"
            }
        };

        await SaveAreasAsync(defaultAreas);
        return defaultAreas;
    }
}