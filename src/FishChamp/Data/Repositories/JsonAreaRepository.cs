using FishChamp.Data.Models;
using System.Text.Json;

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
        if (area == null) return [];

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
        var areas = JsonSerializer.Deserialize<List<AreaState>>(json) ?? [];

        if (areas.Count == 0)
        {
            return await InitializeDefaultAreasAsync();
        }

        return areas;
    }

    private async Task SaveAreasAsync(List<AreaState> areas)
    {
        var json = JsonSerializer.Serialize(areas, options: new JsonSerializerOptions() { WriteIndented = true });
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
                FishingSpots =
                [
                    new()
                    {
                        SpotId = "dock",
                        Name = "Wooden Dock",
                        Type = FishingSpotType.Land,
                        AvailableFish = ["common_carp", "bluegill", "bass"]
                    },
                    new()
                    {
                        SpotId = "dock_end",
                        Name = "End of the Dock",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["bluegill", "bass", "catfish"]
                    },
                    new()
                    {
                        SpotId = "shore",
                        Name = "Rocky Shore",
                        Type = FishingSpotType.Land,
                        AvailableFish = ["minnow", "sunfish"]
                    }
                ],
                FarmSpots =
                [
                    new()
                    {
                        SpotId = "lakeside_garden",
                        Name = "Lakeside Garden",
                        AvailableCrops = ["worms", "corn", "wheat"],
                        CanDigForWorms = true
                    }
                ],
                Shops = new Dictionary<string, ShopInventory>
                {
                    ["tackle_shop"] = new()
                    {
                        ShopId = "tackle_shop",
                        Name = "Bob's Tackle Shop",
                        Items = 
                        [
                            new()
                            {
                                ItemId = "basic_rod",
                                Name = "Basic Fishing Rod",
                                ItemType = "Rod",
                                Price = 50,
                                Description = "A simple fishing rod for beginners.",
                                Properties = new() { ["power"] = 1, ["durability"] = 100 }
                            },
                            new()
                            {
                                ItemId = "worm_bait",
                                Name = "Worm Bait",
                                ItemType = "Bait",
                                Price = 5,
                                Description = "Common bait that attracts most fish.",
                                Properties = new() { ["attraction"] = 1.1 }
                            },
                            new()
                            {
                                ItemId = "spinner_lure",
                                Name = "Spinner Lure",
                                ItemType = "Bait",
                                Price = 15,
                                Description = "Attracts predatory fish like bass.",
                                Properties = new() { ["attraction"] = 1.2 }
                            },
                            new()
                            {
                                ItemId = "basic_trap",
                                Name = "Basic Fish Trap",
                                ItemType = "Trap",
                                Price = 100,
                                Description = "A simple fish trap for passive fishing. Lasts for several deployments.",
                                Properties = new() { ["durability"] = 100, ["efficiency"] = 1.0 }
                            },
                            new()
                            {
                                ItemId = "shallow_trap",
                                Name = "Shallow Water Trap",
                                ItemType = "Trap",
                                Price = 200,
                                Description = "Specialized trap for shallow waters. Better catch rate for shore fish.",
                                Properties = new() { ["durability"] = 120, ["efficiency"] = 1.3, ["water_type"] = "shallow" }
                            },
                            new()
                            {
                                ItemId = "bread_bait",
                                Name = "Bread Crumbs",
                                ItemType = "Bait",
                                Price = 3,
                                Description = "Simple bait made from bread. Works well for common fish.",
                                Properties = new() { ["attraction"] = 1.05 }
                            },
                            new()
                            {
                                ItemId = "deep_trap",
                                Name = "Deep Water Trap",
                                ItemType = "Trap",
                                Price = 350,
                                Description = "Specialized trap for deep waters. Better catch rate for large fish.",
                                Properties = new() { ["durability"] = 150, ["efficiency"] = 1.5, ["water_type"] = "deep" }
                            },
                            new()
                            {
                                ItemId = "rare_bait",
                                Name = "Golden Lure",
                                ItemType = "Bait",
                                Price = 50,
                                Description = "Premium bait that significantly attracts rare fish.",
                                Properties = new() { ["attraction"] = 1.8, ["rare_bonus"] = true }
                            },
                            new()
                            {
                                ItemId = "trap_material",
                                Name = "Trap Materials",
                                ItemType = "Material",
                                Price = 25,
                                Description = "Basic materials for crafting traps: rope, wire, and wooden planks.",
                                Properties = new() { ["stack_size"] = 10 }
                            }
                        ]
                    }
                },
                ConnectedAreas = ["mystic_lake"],
                IsUnlocked = true
            },
            new()
            {
                AreaId = "mystic_lake",
                Name = "Mystic Lake",
                Description = "A mysterious lake shrouded in mist. Rumors say rare fish dwell in its depths.",
                FishingSpots =
                [
                    new()
                    {
                        SpotId = "deep_waters",
                        Name = "Deep Waters",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["rainbow_trout", "pike", "mysterious_eel"]
                    },
                    new()
                    {
                        SpotId = "misty_shore",
                        Name = "Misty Shore",
                        Type = FishingSpotType.Land,
                        AvailableFish = ["ghost_carp", "silver_perch", "moonfish"]
                    }
                ],
                FarmSpots =
                [
                    new()
                    {
                        SpotId = "enchanted_soil",
                        Name = "Enchanted Soil Patch",
                        AvailableCrops = ["glowing_corn", "magic_beans", "starfruit"],
                        CanDigForWorms = true
                    }
                ],
                Shops = new Dictionary<string, ShopInventory>
                {
                    ["mystic_tackle"] = new()
                    {
                        ShopId = "mystic_tackle",
                        Name = "Mystic Fishing Supplies",
                        Items = 
                        [
                            new()
                            {
                                ItemId = "enchanted_rod",
                                Name = "Enchanted Fishing Rod",
                                ItemType = "Rod",
                                Price = 500,
                                Description = "A rod imbued with mysterious powers.",
                                Properties = new() { ["power"] = 3, ["durability"] = 200 }
                            },
                            new()
                            {
                                ItemId = "glowing_lure",
                                Name = "Glowing Lure",
                                ItemType = "Bait",
                                Price = 50,
                                Description = "Attracts rare fish from the depths.",
                                Properties = new() { ["attraction"] = 1.5 }
                            }
                        ]
                    }
                },
                ConnectedAreas = ["starter_lake"],
                IsUnlocked = false,
                UnlockRequirement = "Catch 5 different fish species"
            }
        };

        await SaveAreasAsync(defaultAreas);
        return defaultAreas;
    }
}