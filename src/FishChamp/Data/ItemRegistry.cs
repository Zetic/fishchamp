using FishChamp.Data.Models;

namespace FishChamp.Data;

/// <summary>
/// Centralized registry for all item definitions in the game.
/// Provides item definitions that can be referenced by shops and other systems.
/// </summary>
public static class ItemRegistry
{
    private static readonly Dictionary<string, ShopItem> _items = new();

    static ItemRegistry()
    {
        InitializeItems();
    }

    /// <summary>
    /// Gets an item by its ID. Returns null if the item is not found.
    /// </summary>
    public static ShopItem? GetItem(string itemId)
    {
        return _items.GetValueOrDefault(itemId);
    }

    /// <summary>
    /// Gets multiple items by their IDs. Skips any IDs that are not found.
    /// </summary>
    public static List<ShopItem> GetItems(params string[] itemIds)
    {
        return itemIds.Select(GetItem).Where(item => item != null).Cast<ShopItem>().ToList();
    }

    /// <summary>
    /// Gets all registered items.
    /// </summary>
    public static IReadOnlyDictionary<string, ShopItem> GetAllItems()
    {
        return _items.AsReadOnly();
    }

    private static void InitializeItems()
    {
        // Rods
        RegisterItem(new ShopItem
        {
            ItemId = "basic_rod",
            Name = "Basic Fishing Rod",
            ItemType = "Rod",
            Price = 50,
            Description = "A simple fishing rod for beginners.",
            Properties = new() { ["power"] = 1, ["durability"] = 100 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "sturdy_rod",
            Name = "Sturdy Fishing Rod",
            ItemType = "Rod",
            Price = 150,
            Description = "A more durable rod with basic fish-finding capabilities.",
            Properties = new() { ["power"] = 2, ["durability"] = 150, ["abilities"] = (int)RodAbility.FishFinder }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "hook_master_rod",
            Name = "Hook Master Rod",
            ItemType = "Rod",
            Price = 200,
            Description = "Features sharp hooks that prevent fish from slipping away.",
            Properties = new() { ["power"] = 2, ["durability"] = 120, ["abilities"] = (int)RodAbility.SharpHook }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "enchanted_rod",
            Name = "Enchanted Fishing Rod",
            ItemType = "Rod",
            Price = 500,
            Description = "A rod imbued with mysterious powers.",
            Properties = new() { ["power"] = 3, ["durability"] = 200, ["abilities"] = (int)RodAbility.Precision }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "precision_rod",
            Name = "Rod of Precision",
            ItemType = "Rod",
            Price = 750,
            Description = "A masterwork rod that helps target evasive fish.",
            Properties = new() { ["power"] = 4, ["durability"] = 250, ["abilities"] = (int)RodAbility.Precision }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "sharp_hook_rod",
            Name = "Sharp Hook Rod",
            ItemType = "Rod",
            Price = 650,
            Description = "Prevents slippery fish from escaping your grasp.",
            Properties = new() { ["power"] = 3, ["durability"] = 200, ["abilities"] = (int)RodAbility.SharpHook }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "fish_finder_rod",
            Name = "Fish Finder Rod",
            ItemType = "Rod",
            Price = 800,
            Description = "Reveals camouflaged fish hiding in the depths.",
            Properties = new() { ["power"] = 4, ["durability"] = 280, ["abilities"] = (int)RodAbility.FishFinder }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "lure_master_rod",
            Name = "Lure Master Rod",
            ItemType = "Rod",
            Price = 900,
            Description = "Enhances magnetic fish to attract schools.",
            Properties = new() { ["power"] = 5, ["durability"] = 300, ["abilities"] = (int)RodAbility.Lure }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "legendary_trident_rod",
            Name = "Legendary Trident Rod",
            ItemType = "Rod",
            Price = 2000,
            Description = "A divine rod forged by Neptune himself. Combines all rod abilities.",
            Properties = new() { ["power"] = 7, ["durability"] = 500, ["abilities"] = (int)(RodAbility.Precision | RodAbility.SharpHook | RodAbility.FishFinder | RodAbility.Lure) }
        });

        // Bait
        RegisterItem(new ShopItem
        {
            ItemId = "worm_bait",
            Name = "Worm Bait",
            ItemType = "Bait",
            Price = 5,
            Description = "Common bait that attracts most fish.",
            Properties = new() { ["attraction"] = 1.1 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "spinner_lure",
            Name = "Spinner Lure",
            ItemType = "Bait",
            Price = 15,
            Description = "Attracts predatory fish like ember bass.",
            Properties = new() { ["attraction"] = 1.2 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "bread_bait",
            Name = "Bread Crumbs",
            ItemType = "Bait",
            Price = 3,
            Description = "Simple bait made from bread. Works well for common fish.",
            Properties = new() { ["attraction"] = 1.05 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "rare_bait",
            Name = "Golden Lure",
            ItemType = "Bait",
            Price = 50,
            Description = "Premium bait that significantly attracts rare fish.",
            Properties = new() { ["attraction"] = 1.8, ["rare_bonus"] = true }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "glowing_lure",
            Name = "Glowing Lure",
            ItemType = "Bait",
            Price = 50,
            Description = "Attracts rare fish from the depths.",
            Properties = new() { ["attraction"] = 1.5 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "ethereal_bait",
            Name = "Ethereal Bait",
            ItemType = "Bait",
            Price = 75,
            Description = "Mystical bait that draws rare ethereal fish.",
            Properties = new() { ["attraction"] = 2.0, ["rare_bonus"] = true }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "starlight_lure",
            Name = "Starlight Lure",
            ItemType = "Bait",
            Price = 100,
            Description = "Captures the essence of starlight to attract celestial fish.",
            Properties = new() { ["attraction"] = 2.5, ["rare_bonus"] = true }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "kraken_bait",
            Name = "Kraken Bait",
            ItemType = "Bait",
            Price = 200,
            Description = "Legendary bait that can attract even the mightiest sea creatures.",
            Properties = new() { ["attraction"] = 3.0, ["legendary_bonus"] = true }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "pearl_lure",
            Name = "Black Pearl Lure",
            ItemType = "Bait",
            Price = 150,
            Description = "A rare black pearl that mesmerizes deep sea creatures.",
            Properties = new() { ["attraction"] = 2.2, ["deep_water_bonus"] = true }
        });

        // Traps
        RegisterItem(new ShopItem
        {
            ItemId = "basic_trap",
            Name = "Basic Fish Trap",
            ItemType = "Trap",
            Price = 100,
            Description = "A simple fish trap for passive fishing. Lasts for several deployments.",
            Properties = new() { ["durability"] = 100, ["efficiency"] = 1.0 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "shallow_trap",
            Name = "Shallow Water Trap",
            ItemType = "Trap",
            Price = 200,
            Description = "Specialized trap for shallow waters. Better catch rate for shore fish.",
            Properties = new() { ["durability"] = 120, ["efficiency"] = 1.3, ["water_type"] = "shallow" }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "deep_trap",
            Name = "Deep Water Trap",
            ItemType = "Trap",
            Price = 350,
            Description = "Specialized trap for deep waters. Better catch rate for large fish.",
            Properties = new() { ["durability"] = 150, ["efficiency"] = 1.5, ["water_type"] = "deep" }
        });

        // Materials
        RegisterItem(new ShopItem
        {
            ItemId = "trap_material",
            Name = "Trap Materials",
            ItemType = "Material",
            Price = 25,
            Description = "Basic materials for crafting traps: rope, wire, and wooden planks.",
            Properties = new() { ["stack_size"] = 10 }
        });

        // Seeds
        RegisterItem(new ShopItem
        {
            ItemId = "corn_seeds",
            Name = "Corn Seeds",
            ItemType = "Seed",
            Price = 10,
            Description = "Basic corn seeds that grow in 4 hours. Yields corn for bait crafting.",
            Properties = new() { ["growth_time"] = 4 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "tomato_seeds",
            Name = "Tomato Seeds",
            ItemType = "Seed",
            Price = 15,
            Description = "Fresh tomato seeds that grow in 6 hours. Great for cooking.",
            Properties = new() { ["growth_time"] = 6 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "algae_spores",
            Name = "Algae Spores",
            ItemType = "Seed",
            Price = 20,
            Description = "Aquatic algae spores that grow quickly in 2 hours. Used for aquarium food.",
            Properties = new() { ["growth_time"] = 2 }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "herb_seeds",
            Name = "Spice Herb Seeds",
            ItemType = "Seed",
            Price = 25,
            Description = "Aromatic herb seeds that take 8 hours to grow. Used in premium bait crafting.",
            Properties = new() { ["growth_time"] = 8 }
        });

        // Boats
        RegisterItem(new ShopItem
        {
            ItemId = "skiff",
            Name = "Small Skiff",
            ItemType = "Boat",
            Price = 200,
            Description = "A simple wooden boat perfect for shallow waters.",
            Properties = new() { ["boat_type"] = "skiff" }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "canoe",
            Name = "Sturdy Canoe",
            ItemType = "Boat",
            Price = 500,
            Description = "A reliable canoe that can handle rougher waters.",
            Properties = new() { ["boat_type"] = "canoe" }
        });

        RegisterItem(new ShopItem
        {
            ItemId = "speedboat",
            Name = "Fast Speedboat",
            ItemType = "Boat",
            Price = 1500,
            Description = "A modern speedboat for deep sea exploration.",
            Properties = new() { ["boat_type"] = "speedboat" }
        });
    }

    private static void RegisterItem(ShopItem item)
    {
        _items[item.ItemId] = item;
    }
}