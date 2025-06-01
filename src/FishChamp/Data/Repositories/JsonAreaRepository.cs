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
        var directory = Path.GetDirectoryName(_dataPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
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
                        Items = ItemRegistry.GetItems(
                            "basic_rod", "worm_bait", "spinner_lure", "basic_trap", "shallow_trap",
                            "bread_bait", "deep_trap", "rare_bait", "trap_material", "sturdy_rod",
                            "hook_master_rod", "corn_seeds", "tomato_seeds", "algae_spores", "herb_seeds"
                        ).ToList()
                    }
                },
                AvailablePlots = 
                [
                    new()
                    {
                        PlotId = "lakeside_cottage_1",
                        AreaId = "starter_lake",
                        Name = "Peaceful Lakeside Cottage",
                        Description = "A charming small plot overlooking the serene lake waters. Perfect for your first home.",
                        Price = 500,
                        Size = PlotSize.Small
                    },
                    new()
                    {
                        PlotId = "garden_plot_1",
                        AreaId = "starter_lake", 
                        Name = "Sunny Garden Plot",
                        Description = "A medium-sized plot with fertile soil, ideal for both housing and gardening.",
                        Price = 800,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "fishing_retreat_1",
                        AreaId = "starter_lake",
                        Name = "Angler's Retreat",
                        Description = "A small plot near the best fishing spots, perfect for the dedicated angler.",
                        Price = 450,
                        Size = PlotSize.Small
                    },
                    new()
                    {
                        PlotId = "dock_front_1",
                        AreaId = "starter_lake",
                        Name = "Dockfront Property", 
                        Description = "Premium medium plot with direct dock access. Wake up to the sound of gentle waves.",
                        Price = 1200,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "hillside_view_1",
                        AreaId = "starter_lake",
                        Name = "Hillside View",
                        Description = "A cozy small plot on elevated ground with panoramic lake views.",
                        Price = 600,
                        Size = PlotSize.Small
                    }
                ],
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
                        Items = ItemRegistry.GetItems("enchanted_rod", "glowing_lure", "skiff", "canoe").ToList()
                    }
                },
                AvailablePlots =
                [
                    new()
                    {
                        PlotId = "misty_shore_1",
                        AreaId = "mystic_lake",
                        Name = "Misty Shore Retreat",
                        Description = "A medium plot shrouded in mystical mist. The air here hums with magical energy.",
                        Price = 1500,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "enchanted_grove_1", 
                        AreaId = "mystic_lake",
                        Name = "Enchanted Grove Estate",
                        Description = "A large plot surrounded by ancient trees that glow softly at night.",
                        Price = 2500,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "crystal_waters_1",
                        AreaId = "mystic_lake",
                        Name = "Crystal Waters Plot",
                        Description = "A small plot near the crystal-clear waters, where rare fish are easily spotted.",
                        Price = 1000,
                        Size = PlotSize.Small
                    },
                    new()
                    {
                        PlotId = "magic_springs_1",
                        AreaId = "mystic_lake", 
                        Name = "Magic Springs Sanctuary",
                        Description = "A medium plot built around natural magic springs. The water here never freezes.",
                        Price = 1800,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "twilight_peninsula_1",
                        AreaId = "mystic_lake",
                        Name = "Twilight Peninsula",
                        Description = "An exclusive large plot extending into the lake, offering complete privacy.",
                        Price = 3000,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "mystic_overlook_1",
                        AreaId = "mystic_lake",
                        Name = "Mystic Overlook", 
                        Description = "A small elevated plot with commanding views of the entire mystical lake.",
                        Price = 1200,
                        Size = PlotSize.Small
                    }
                ],
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
                        Items = ItemRegistry.GetItems(
                            "precision_rod", "sharp_hook_rod", "fish_finder_rod",
                            "lure_master_rod", "ethereal_bait", "starlight_lure"
                        ).ToList()
                    }
                },
                AvailablePlots =
                [
                    new()
                    {
                        PlotId = "crystal_spring_1",
                        AreaId = "enchanted_forest",
                        Name = "Crystal Spring Manor",
                        Description = "A large estate built around the legendary crystal spring. The water grants eternal youth to plants.",
                        Price = 5000,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "fairy_circle_1",
                        AreaId = "enchanted_forest",
                        Name = "Fairy Circle Sanctuary",
                        Description = "A medium plot within an ancient fairy circle. Magical creatures visit nightly.",
                        Price = 3500,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "moonwell_1",
                        AreaId = "enchanted_forest",
                        Name = "Ancient Moonwell Estate",
                        Description = "A premium large plot surrounding the ancient moonwell. Lunar magic flows freely here.",
                        Price = 7500,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "starlight_grove_1",
                        AreaId = "enchanted_forest",
                        Name = "Starlight Grove",
                        Description = "A medium plot where starlight filters through ancient tree canopies all day long.",
                        Price = 4000,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "ethereal_garden_1",
                        AreaId = "enchanted_forest",
                        Name = "Ethereal Garden",
                        Description = "A medium plot where ethereal flowers bloom year-round, attracting mystical creatures.",
                        Price = 3800,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "woodland_throne_1",
                        AreaId = "enchanted_forest", 
                        Name = "Woodland Throne",
                        Description = "A magnificent large estate at the heart of the forest, fit for forest royalty.",
                        Price = 10000,
                        Size = PlotSize.Large
                    }
                ],
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
                        Items = ItemRegistry.GetItems("legendary_trident_rod", "kraken_bait", "pearl_lure", "speedboat").ToList()
                    }
                },
                AvailablePlots =
                [
                    new()
                    {
                        PlotId = "abyssal_palace_1",
                        AreaId = "deep_ocean",
                        Name = "Abyssal Palace",
                        Description = "A massive underwater palace plot in the deepest trenches. Home to legendary sea creatures.",
                        Price = 25000,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "coral_throne_1",
                        AreaId = "deep_ocean",
                        Name = "Coral Throne",
                        Description = "A large plot built into a living coral reef. The coral provides natural protection and beauty.",
                        Price = 20000,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "kraken_lair_1",
                        AreaId = "deep_ocean",
                        Name = "Kraken's Lair",
                        Description = "A legendary large plot in the abyssal depths. Only the bravest dare to claim this territory.",
                        Price = 50000,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "tsunami_peak_1",
                        AreaId = "deep_ocean",
                        Name = "Tsunami Peak",
                        Description = "A medium plot on a sea mount that rises above the waves. Commands the entire ocean view.",
                        Price = 15000,
                        Size = PlotSize.Medium
                    },
                    new()
                    {
                        PlotId = "neptune_domain_1",
                        AreaId = "deep_ocean",
                        Name = "Neptune's Domain",
                        Description = "The ultimate large estate, blessed by Neptune himself. Grants dominion over sea creatures.",
                        Price = 100000,
                        Size = PlotSize.Large
                    },
                    new()
                    {
                        PlotId = "whale_song_1",
                        AreaId = "deep_ocean",
                        Name = "Whale Song Sanctuary",
                        Description = "A large plot where ancient whales sing their eternal songs. A place of deep peace and power.",
                        Price = 30000,
                        Size = PlotSize.Large
                    }
                ],
                ConnectedAreas = ["enchanted_forest"],
                IsUnlocked = false,
                UnlockRequirement = "Catch 3 epic or legendary fish and own a rod with power 4+"
            }
        };

        await SaveAreasAsync(defaultAreas);
        return defaultAreas;
    }
}