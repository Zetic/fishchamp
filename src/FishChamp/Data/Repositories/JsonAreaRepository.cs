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
                        AvailableFish = ["azure_finling", "crystal_perch", "ember_bass"]
                    },
                    new()
                    {
                        SpotId = "dock_end",
                        Name = "End of the Dock",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["crystal_perch", "ember_bass", "shadow_whiskers"]
                    },
                    new()
                    {
                        SpotId = "shore",
                        Name = "Rocky Shore",
                        Type = FishingSpotType.Land,
                        AvailableFish = ["glimmer_minnow", "golden_sunfish"]
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
                                Description = "Attracts predatory fish like ember bass.",
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
                            },
                            new()
                            {
                                ItemId = "sturdy_rod",
                                Name = "Sturdy Fishing Rod",
                                ItemType = "Rod",
                                Price = 150,
                                Description = "A more durable rod with basic fish-finding capabilities.",
                                Properties = new() { ["power"] = 2, ["durability"] = 150, ["abilities"] = (int)RodAbility.FishFinder }
                            },
                            new()
                            {
                                ItemId = "hook_master_rod",
                                Name = "Hook Master Rod",  
                                ItemType = "Rod",
                                Price = 200,
                                Description = "Features sharp hooks that prevent fish from slipping away.",
                                Properties = new() { ["power"] = 2, ["durability"] = 120, ["abilities"] = (int)RodAbility.SharpHook }
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
                    ["mystic_fishing_supplies"] = new()
                    {
                        ShopId = "mystic_fishing_supplies",
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
                                Properties = new() { ["power"] = 3, ["durability"] = 200, ["abilities"] = (int)RodAbility.Precision }
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
                ConnectedAreas = ["starter_lake", "enchanted_forest"],
                IsUnlocked = false,
                UnlockRequirement = "Catch 5 different fish species"
            },
            new()
            {
                AreaId = "enchanted_forest",
                Name = "Enchanted Forest Springs",
                Description = "Ancient springs hidden deep within a magical forest. The water here glows with ethereal light, home to the rarest mystical fish.",
                FishingSpots =
                [
                    new()
                    {
                        SpotId = "crystal_spring",
                        Name = "Crystal Spring",
                        Type = FishingSpotType.Land,
                        AvailableFish = ["prism_trout", "ethereal_guppy", "starlight_salmon"]
                    },
                    new()
                    {
                        SpotId = "moonwell",
                        Name = "Ancient Moonwell",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["lunar_bass", "void_eel", "phoenix_koi"]
                    },
                    new()
                    {
                        SpotId = "fairy_pond",
                        Name = "Fairy Pond",
                        Type = FishingSpotType.Land,
                        AvailableFish = ["fairy_fin", "dream_carp", "celestial_perch"]
                    }
                ],
                FarmSpots =
                [
                    new()
                    {
                        SpotId = "mystic_grove",
                        Name = "Mystic Grove",
                        AvailableCrops = ["moonberries", "starflower_seeds", "enchanted_moss"],
                        CanDigForWorms = true
                    }
                ],
                Shops = new Dictionary<string, ShopInventory>
                {
                    ["arcane_angler"] = new()
                    {
                        ShopId = "arcane_angler",
                        Name = "The Arcane Angler",
                        Items = 
                        [
                            new()
                            {
                                ItemId = "precision_rod",
                                Name = "Rod of Precision",
                                ItemType = "Rod",
                                Price = 750,
                                Description = "A masterwork rod that helps target evasive fish.",
                                Properties = new() { ["power"] = 4, ["durability"] = 250, ["abilities"] = (int)RodAbility.Precision }
                            },
                            new()
                            {
                                ItemId = "sharp_hook_rod",
                                Name = "Sharp Hook Rod",
                                ItemType = "Rod",
                                Price = 650,
                                Description = "Prevents slippery fish from escaping your grasp.",
                                Properties = new() { ["power"] = 3, ["durability"] = 200, ["abilities"] = (int)RodAbility.SharpHook }
                            },
                            new()
                            {
                                ItemId = "fish_finder_rod",
                                Name = "Fish Finder Rod",
                                ItemType = "Rod",
                                Price = 800,
                                Description = "Reveals camouflaged fish hiding in the depths.",
                                Properties = new() { ["power"] = 4, ["durability"] = 280, ["abilities"] = (int)RodAbility.FishFinder }
                            },
                            new()
                            {
                                ItemId = "lure_master_rod",
                                Name = "Lure Master Rod",
                                ItemType = "Rod",
                                Price = 900,
                                Description = "Enhances magnetic fish to attract schools.",
                                Properties = new() { ["power"] = 5, ["durability"] = 300, ["abilities"] = (int)RodAbility.Lure }
                            },
                            new()
                            {
                                ItemId = "ethereal_bait",
                                Name = "Ethereal Bait",
                                ItemType = "Bait",
                                Price = 75,
                                Description = "Mystical bait that draws rare ethereal fish.",
                                Properties = new() { ["attraction"] = 2.0, ["rare_bonus"] = true }
                            },
                            new()
                            {
                                ItemId = "starlight_lure",
                                Name = "Starlight Lure",
                                ItemType = "Bait",
                                Price = 100,
                                Description = "Captures the essence of starlight to attract celestial fish.",
                                Properties = new() { ["attraction"] = 2.5, ["rare_bonus"] = true }
                            }
                        ]
                    }
                },
                ConnectedAreas = ["mystic_lake", "deep_ocean"],
                IsUnlocked = false,
                UnlockRequirement = "Catch a rare or higher rarity fish in Mystic Lake"
            },
            new()
            {
                AreaId = "deep_ocean",
                Name = "Abyssal Deep Ocean",
                Description = "The vast, dark depths of the ocean where legendary sea creatures dwell. Only the most skilled anglers dare venture here.",
                FishingSpots =
                [
                    new()
                    {
                        SpotId = "ocean_surface",
                        Name = "Ocean Surface",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["titan_tuna", "storm_marlin", "kraken_spawn"]
                    },
                    new()
                    {
                        SpotId = "abyssal_trench",
                        Name = "Abyssal Trench",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["void_leviathan", "ancient_angler", "deep_dragon"]
                    },
                    new()
                    {
                        SpotId = "coral_reef",
                        Name = "Enchanted Coral Reef",
                        Type = FishingSpotType.Water,
                        AvailableFish = ["coral_emperor", "reef_spirit", "rainbow_ray"]
                    }
                ],
                FarmSpots =
                [
                    new()
                    {
                        SpotId = "kelp_forest",
                        Name = "Giant Kelp Forest",
                        AvailableCrops = ["sea_grapes", "ocean_kelp", "pearl_algae"],
                        CanDigForWorms = false
                    }
                ],
                Shops = new Dictionary<string, ShopInventory>
                {
                    ["neptunes_arsenal"] = new()
                    {
                        ShopId = "neptunes_arsenal",
                        Name = "Neptune's Arsenal",
                        Items = 
                        [
                            new()
                            {
                                ItemId = "legendary_trident_rod",
                                Name = "Legendary Trident Rod",
                                ItemType = "Rod",
                                Price = 2000,
                                Description = "A divine rod forged by Neptune himself. Combines all rod abilities.",
                                Properties = new() { ["power"] = 7, ["durability"] = 500, ["abilities"] = (int)(RodAbility.Precision | RodAbility.SharpHook | RodAbility.FishFinder | RodAbility.Lure) }
                            },
                            new()
                            {
                                ItemId = "kraken_bait",
                                Name = "Kraken Bait",
                                ItemType = "Bait",
                                Price = 200,
                                Description = "Legendary bait that can attract even the mightiest sea creatures.",
                                Properties = new() { ["attraction"] = 3.0, ["legendary_bonus"] = true }
                            },
                            new()
                            {
                                ItemId = "pearl_lure",
                                Name = "Black Pearl Lure",
                                ItemType = "Bait",
                                Price = 150,
                                Description = "A rare black pearl that mesmerizes deep sea creatures.",
                                Properties = new() { ["attraction"] = 2.2, ["deep_water_bonus"] = true }
                            }
                        ]
                    }
                },
                ConnectedAreas = ["enchanted_forest"],
                IsUnlocked = false,
                UnlockRequirement = "Catch 3 epic or legendary fish and own a rod with power 4+"
            }
        };

        await SaveAreasAsync(defaultAreas);
        return defaultAreas;
    }
}