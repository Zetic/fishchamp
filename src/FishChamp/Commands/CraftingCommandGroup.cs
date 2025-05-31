using System.ComponentModel;
using System.Text;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;

namespace FishChamp.Modules;

[Group("craft")]
[Description("Crafting commands for creating traps, equipment, and cooking meals")]
public class CraftingCommandGroup(IInteractionCommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    FeedbackService feedbackService) : CommandGroup
{
    [Command("trap")]
    [Description("Craft a fish trap using materials")]
    public async Task<IResult> CraftTrapAsync(
        [Description("Type of trap to craft")] string trapType = "basic")
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
        {
            return await feedbackService.SendContextualErrorAsync("Player profile not found. Use `/fish` to get started!");
        }

        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        if (inventory == null)
        {
            return await feedbackService.SendContextualErrorAsync("Inventory not found!");
        }

        // Get trap recipe
        var recipe = GetTrapRecipe(trapType.ToLower());
        if (recipe == null)
        {
            var availableTraps = "basic, shallow, deep, reinforced";
            return await feedbackService.SendContextualErrorAsync(
                $"Unknown trap type '{trapType}'. Available types: {availableTraps}");
        }

        // Check if player has required materials
        foreach (var requirement in recipe.Requirements)
        {
            var item = inventory.Items.FirstOrDefault(i => 
                i.ItemId == requirement.Key && i.Quantity >= requirement.Value);
            
            if (item == null)
            {
                var missing = inventory.Items.FirstOrDefault(i => i.ItemId == requirement.Key);
                var hasAmount = missing?.Quantity ?? 0;
                var itemName = GetItemDisplayName(requirement.Key);
                return await feedbackService.SendContextualErrorAsync(
                    $"Not enough materials! Need {requirement.Value} {itemName} (you have {hasAmount})");
            }
        }

        // Consume materials
        foreach (var requirement in recipe.Requirements)
        {
            await inventoryRepository.RemoveItemAsync(user.ID.Value, requirement.Key, requirement.Value);
        }

        // Create the trap item
        var craftedTrap = new InventoryItem
        {
            ItemId = recipe.Result.ItemId,
            ItemType = "Trap",
            Name = recipe.Result.Name,
            Quantity = 1,
            Properties = recipe.Result.Properties
        };

        await inventoryRepository.AddItemAsync(user.ID.Value, craftedTrap);

        var embed = new Embed
        {
            Title = "🔨 Crafting Successful!",
            Description = $"You crafted a **{recipe.Result.Name}**!\n\n" +
                         $"**Materials Used:**\n" +
                         string.Join("\n", recipe.Requirements.Select(r => 
                             $"• {r.Value}x {GetItemDisplayName(r.Key)}")) +
                         $"\n\n**Trap Properties:**\n" +
                         $"• Durability: {recipe.Result.Properties["durability"]}\n" +
                         $"• Efficiency: {recipe.Result.Properties["efficiency"]}x\n" +
                         (recipe.Result.Properties.ContainsKey("water_type") ? 
                             $"• Specialized for: {recipe.Result.Properties["water_type"]} water" : ""),
            Colour = Color.Gold,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("cook")]
    [Description("Cook a meal using crops")]
    public async Task<IResult> CookMealAsync(
        [Description("Type of meal to cook")] string mealType = "simple_salad")
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
        {
            return await feedbackService.SendContextualErrorAsync("Player profile not found. Use `/fish` to get started!");
        }

        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        if (inventory == null)
        {
            return await feedbackService.SendContextualErrorAsync("Inventory not found!");
        }

        // Get cooking recipe
        var recipe = GetCookingRecipe(mealType.ToLower());
        if (recipe == null)
        {
            var availableMeals = "simple_salad, hearty_stew, algae_smoothie, herb_boost, gourmet_fish_stew, power_smoothie";
            return await feedbackService.SendContextualErrorAsync(
                $"Unknown meal type '{mealType}'. Available meals: {availableMeals}");
        }

        // Check cooking level requirement
        if (player.CookingLevel < recipe.RequiredCookingLevel)
        {
            return await feedbackService.SendContextualErrorAsync(
                $"You need cooking level {recipe.RequiredCookingLevel} to cook {recipe.Result.Name}. Your cooking level is {player.CookingLevel}.");
        }

        // Check if cooking station is required
        if (!string.IsNullOrEmpty(recipe.RequiredStation))
        {
            var station = inventory.Items.FirstOrDefault(i => 
                i.ItemType == "CookingStation" && i.ItemId == recipe.RequiredStation);
            
            if (station == null)
            {
                var stationName = GetItemDisplayName(recipe.RequiredStation);
                return await feedbackService.SendContextualErrorAsync(
                    $"You need a {stationName} to cook {recipe.Result.Name}. Craft one first or use simpler recipes.");
            }
        }

        // Check if player has required ingredients
        foreach (var ingredient in recipe.Ingredients)
        {
            var item = inventory.Items.FirstOrDefault(i => 
                i.ItemId == ingredient.Key && i.Quantity >= ingredient.Value);
            
            if (item == null)
            {
                var missing = inventory.Items.FirstOrDefault(i => i.ItemId == ingredient.Key);
                var hasAmount = missing?.Quantity ?? 0;
                var itemName = GetItemDisplayName(ingredient.Key);
                return await feedbackService.SendContextualErrorAsync(
                    $"Not enough ingredients! Need {ingredient.Value} {itemName} (you have {hasAmount})");
            }
        }

        // Consume ingredients
        foreach (var ingredient in recipe.Ingredients)
        {
            await inventoryRepository.RemoveItemAsync(user.ID.Value, ingredient.Key, ingredient.Value);
        }

        // Create the meal item
        var cookResults = new InventoryItem
        {
            ItemId = recipe.Result.ItemId,
            ItemType = "Meal",
            Name = recipe.Result.Name,
            Quantity = 1,
            Properties = recipe.Result.Properties
        };

        await inventoryRepository.AddItemAsync(user.ID.Value, cookResults);

        // Gain cooking experience
        var cookingXpGained = recipe.RequiredCookingLevel > 0 ? recipe.RequiredCookingLevel * 5 : 5;
        player.CookingExperience += cookingXpGained;
        
        // Check for cooking level up
        var newCookingLevel = CalculateCookingLevel(player.CookingExperience);
        var levelUpMessage = "";
        if (newCookingLevel > player.CookingLevel)
        {
            player.CookingLevel = newCookingLevel;
            levelUpMessage = $"\n\n🎉 **Cooking Level Up!** You are now cooking level {newCookingLevel}!";
        }
        
        await playerRepository.UpdatePlayerAsync(player);

        var embed = new Embed
        {
            Title = "🍽️ Cooking Successful!",
            Description = $"You cooked a **{recipe.Result.Name}**!\n\n" +
                         $"**Ingredients Used:**\n" +
                         string.Join("\n", recipe.Ingredients.Select(r => 
                             $"• {r.Value}x {GetItemDisplayName(r.Key)}")) +
                         $"\n\n**Meal Effects:**\n" +
                         GetMealEffectsDescription(recipe.Result.Properties) +
                         $"\n\n**Cooking XP:** +{cookingXpGained}" +
                         levelUpMessage,
            Colour = Color.Orange,
            Footer = new EmbedFooter("Use this meal to gain temporary fishing buffs!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("station")]
    [Description("Craft a cooking station")]
    public async Task<IResult> CraftStationAsync(
        [Description("Type of cooking station to craft")] string stationType = "cooking_pot")
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
        {
            return await feedbackService.SendContextualErrorAsync("Player profile not found. Use `/fish` to get started!");
        }

        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        if (inventory == null)
        {
            return await feedbackService.SendContextualErrorAsync("Inventory not found!");
        }

        // Get station recipe
        var recipe = GetStationRecipe(stationType.ToLower());
        if (recipe == null)
        {
            var availableStations = "cooking_pot, cooking_blender";
            return await feedbackService.SendContextualErrorAsync(
                $"Unknown station type '{stationType}'. Available stations: {availableStations}");
        }

        // Check if player has required materials
        foreach (var requirement in recipe.Requirements)
        {
            var item = inventory.Items.FirstOrDefault(i => 
                i.ItemId == requirement.Key && i.Quantity >= requirement.Value);
            
            if (item == null)
            {
                var missing = inventory.Items.FirstOrDefault(i => i.ItemId == requirement.Key);
                var hasAmount = missing?.Quantity ?? 0;
                var itemName = GetItemDisplayName(requirement.Key);
                return await feedbackService.SendContextualErrorAsync(
                    $"Not enough materials! Need {requirement.Value} {itemName} (you have {hasAmount})");
            }
        }

        // Consume materials
        foreach (var requirement in recipe.Requirements)
        {
            await inventoryRepository.RemoveItemAsync(user.ID.Value, requirement.Key, requirement.Value);
        }

        // Create the station item
        var craftedStation = new InventoryItem
        {
            ItemId = recipe.Result.ItemId,
            ItemType = "CookingStation",
            Name = recipe.Result.Name,
            Quantity = 1,
            Properties = recipe.Result.Properties
        };

        await inventoryRepository.AddItemAsync(user.ID.Value, craftedStation);

        var embed = new Embed
        {
            Title = "🔨 Cooking Station Crafted!",
            Description = $"You crafted a **{recipe.Result.Name}**!\n\n" +
                         $"**Materials Used:**\n" +
                         string.Join("\n", recipe.Requirements.Select(r => 
                             $"• {r.Value}x {GetItemDisplayName(r.Key)}")) +
                         $"\n\n**Station Features:**\n" +
                         GetStationFeaturesDescription(recipe.Result.Properties),
            Colour = Color.Gold,
            Footer = new EmbedFooter("You can now craft advanced recipes that require this station!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("recipes")]
    [Description("View available crafting recipes")]
    public async Task<IResult> ViewRecipesAsync(
        [Description("Recipe type to view")] string type = "trap")
    {
        if (type.ToLower() == "cooking" || type.ToLower() == "cook" || type.ToLower() == "meal")
        {
            return await ViewCookingRecipesAsync();
        }
        else if (type.ToLower() == "station" || type.ToLower() == "stations")
        {
            return await ViewStationRecipesAsync();
        }
        
        var recipes = GetAllTrapRecipes();
        var description = new StringBuilder();

        description.AppendLine("**Available Trap Recipes:**\n");

        foreach (var recipe in recipes)
        {
            description.AppendLine($"**{recipe.Result.Name}** (`/craft trap {recipe.TrapType}`)");
            description.AppendLine($"Materials needed:");
            foreach (var req in recipe.Requirements)
            {
                description.AppendLine($"• {req.Value}x {GetItemDisplayName(req.Key)}");
            }
            description.AppendLine($"Properties: Durability {recipe.Result.Properties["durability"]}, " +
                                 $"Efficiency {recipe.Result.Properties["efficiency"]}x");
            description.AppendLine();
        }

        var embed = new Embed
        {
            Title = "📜 Trap Crafting Recipes",
            Description = description.ToString(),
            Colour = Color.DarkGoldenrod,
            Footer = new EmbedFooter("Materials can be purchased from shops or found while fishing. Use '/craft recipes cooking' for meal recipes."),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<IResult> ViewCookingRecipesAsync()
    {
        var recipes = GetAllCookingRecipes();
        var description = new StringBuilder();

        description.AppendLine("**Available Cooking Recipes:**\n");

        foreach (var recipe in recipes)
        {
            description.AppendLine($"**{recipe.Result.Name}** (`/craft cook {recipe.MealType}`)");
            description.AppendLine($"Ingredients needed:");
            foreach (var ingredient in recipe.Ingredients)
            {
                description.AppendLine($"• {ingredient.Value}x {GetItemDisplayName(ingredient.Key)}");
            }
            description.AppendLine($"Effects: {GetMealEffectsDescription(recipe.Result.Properties)}");
            
            // Add requirements
            if (recipe.RequiredCookingLevel > 1)
            {
                description.AppendLine($"Requires: Cooking Level {recipe.RequiredCookingLevel}");
            }
            if (!string.IsNullOrEmpty(recipe.RequiredStation))
            {
                description.AppendLine($"Station: {GetItemDisplayName(recipe.RequiredStation)}");
            }
            
            description.AppendLine();
        }

        var embed = new Embed
        {
            Title = "📜 Cooking Recipes",
            Description = description.ToString(),
            Colour = Color.Orange,
            Footer = new EmbedFooter("Crops can be grown using the farming system"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<IResult> ViewStationRecipesAsync()
    {
        var recipes = GetAllStationRecipes();
        var description = new StringBuilder();

        description.AppendLine("**Available Cooking Station Recipes:**\n");

        foreach (var recipe in recipes)
        {
            description.AppendLine($"**{recipe.Result.Name}** (`/craft station {recipe.TrapType}`)");
            description.AppendLine($"Materials needed:");
            foreach (var requirement in recipe.Requirements)
            {
                description.AppendLine($"• {requirement.Value}x {GetItemDisplayName(requirement.Key)}");
            }
            description.AppendLine($"Features: {GetStationFeaturesDescription(recipe.Result.Properties)}");
            description.AppendLine();
        }

        var embed = new Embed
        {
            Title = "🏭 Cooking Station Recipes",
            Description = description.ToString(),
            Colour = Color.Purple,
            Footer = new EmbedFooter("Cooking stations unlock advanced recipes with better effects!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private static TrapRecipe? GetTrapRecipe(string trapType)
    {
        return trapType switch
        {
            "basic" => new TrapRecipe
            {
                TrapType = "basic",
                Requirements = new() { ["trap_material"] = 2 },
                Result = new()
                {
                    ItemId = "basic_trap",
                    Name = "Basic Fish Trap",
                    Properties = new() { ["durability"] = 100, ["efficiency"] = 1.0 }
                }
            },
            "shallow" => new TrapRecipe
            {
                TrapType = "shallow",
                Requirements = new() { ["trap_material"] = 3, ["worm_bait"] = 2 },
                Result = new()
                {
                    ItemId = "shallow_trap",
                    Name = "Shallow Water Trap",
                    Properties = new() { ["durability"] = 120, ["efficiency"] = 1.3, ["water_type"] = "shallow" }
                }
            },
            "deep" => new TrapRecipe
            {
                TrapType = "deep",
                Requirements = new() { ["trap_material"] = 5, ["spinner_lure"] = 1 },
                Result = new()
                {
                    ItemId = "deep_trap",
                    Name = "Deep Water Trap",
                    Properties = new() { ["durability"] = 150, ["efficiency"] = 1.5, ["water_type"] = "deep" }
                }
            },
            "reinforced" => new TrapRecipe
            {
                TrapType = "reinforced",
                Requirements = new() { ["trap_material"] = 8, ["rare_bait"] = 1 },
                Result = new()
                {
                    ItemId = "reinforced_trap",
                    Name = "Reinforced Trap",
                    Properties = new() { ["durability"] = 200, ["efficiency"] = 2.0 }
                }
            },
            _ => null
        };
    }

    private static TrapRecipe? GetStationRecipe(string stationType)
    {
        return stationType switch
        {
            "cooking_pot" => new TrapRecipe
            {
                TrapType = "cooking_pot",
                Requirements = new() { ["trap_material"] = 5, ["corn"] = 3 },
                Result = new()
                {
                    ItemId = "cooking_pot",
                    Name = "Cooking Pot",
                    Properties = new() { ["station_type"] = "pot", ["unlocks_recipes"] = "gourmet_fish_stew" }
                }
            },
            "cooking_blender" => new TrapRecipe
            {
                TrapType = "cooking_blender",
                Requirements = new() { ["trap_material"] = 8, ["premium_algae"] = 2, ["rare_herbs"] = 1 },
                Result = new()
                {
                    ItemId = "cooking_blender",
                    Name = "High-Tech Blender",
                    Properties = new() { ["station_type"] = "blender", ["unlocks_recipes"] = "power_smoothie" }
                }
            },
            _ => null
        };
    }

    private static string GetStationFeaturesDescription(Dictionary<string, object> properties)
    {
        var features = new List<string>();
        
        if (properties.ContainsKey("unlocks_recipes"))
        {
            var unlockedRecipe = properties["unlocks_recipes"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(unlockedRecipe))
            {
                features.Add($"Unlocks advanced recipe: {GetItemDisplayName(unlockedRecipe)}");
            }
        }
        
        if (properties.ContainsKey("station_type"))
        {
            var stationType = properties["station_type"].ToString();
            features.Add($"Station type: {stationType}");
        }

        return string.Join("\n• ", features.Prepend("").ToArray()).TrimStart();
    }

    private static List<TrapRecipe> GetAllStationRecipes()
    {
        return new List<TrapRecipe>
        {
            GetStationRecipe("cooking_pot")!,
            GetStationRecipe("cooking_blender")!
        };
    }

    private static List<TrapRecipe> GetAllTrapRecipes()
    {
        return new List<TrapRecipe>
        {
            GetTrapRecipe("basic")!,
            GetTrapRecipe("shallow")!,
            GetTrapRecipe("deep")!,
            GetTrapRecipe("reinforced")!
        };
    }

    private static CookingRecipe? GetCookingRecipe(string mealType)
    {
        return mealType switch
        {
            "simple_salad" => new CookingRecipe
            {
                MealType = "simple_salad",
                Ingredients = new() { ["corn"] = 1, ["tomato"] = 1 },
                Result = new()
                {
                    ItemId = "simple_salad",
                    Name = "Simple Salad",
                    Properties = new() 
                    { 
                        ["catch_rate_bonus"] = 0.1,
                        ["duration_minutes"] = 30,
                        ["buff_type"] = "catch_rate"
                    }
                }
            },
            "hearty_stew" => new CookingRecipe
            {
                MealType = "hearty_stew",
                Ingredients = new() { ["corn"] = 2, ["tomato"] = 1, ["spice_herbs"] = 1 },
                Result = new()
                {
                    ItemId = "hearty_stew",
                    Name = "Hearty Stew",
                    Properties = new() 
                    { 
                        ["xp_bonus"] = 0.25,
                        ["duration_minutes"] = 45,
                        ["buff_type"] = "xp_gain"
                    }
                }
            },
            "algae_smoothie" => new CookingRecipe
            {
                MealType = "algae_smoothie",
                Ingredients = new() { ["algae"] = 2, ["sweet_corn"] = 1 },
                Result = new()
                {
                    ItemId = "algae_smoothie",
                    Name = "Algae Smoothie",
                    Properties = new() 
                    { 
                        ["rare_fish_bonus"] = 0.15,
                        ["duration_minutes"] = 25,
                        ["buff_type"] = "rare_discovery"
                    }
                }
            },
            "herb_boost" => new CookingRecipe
            {
                MealType = "herb_boost",
                Ingredients = new() { ["spice_herbs"] = 1, ["rare_herbs"] = 1, ["premium_algae"] = 1 },
                RequiredCookingLevel = 2,
                Result = new()
                {
                    ItemId = "herb_boost",
                    Name = "Herb Energy Boost",
                    Properties = new() 
                    { 
                        ["catch_rate_bonus"] = 0.15,
                        ["xp_bonus"] = 0.2,
                        ["trait_discovery_bonus"] = 0.2,
                        ["duration_minutes"] = 60,
                        ["buff_type"] = "all_buffs"
                    }
                }
            },
            "gourmet_fish_stew" => new CookingRecipe
            {
                MealType = "gourmet_fish_stew",
                Ingredients = new() { ["spice_herbs"] = 2, ["corn"] = 2, ["tomato"] = 2, ["rare_herbs"] = 1 },
                RequiredStation = "cooking_pot",
                RequiredCookingLevel = 3,
                Result = new()
                {
                    ItemId = "gourmet_fish_stew",
                    Name = "Gourmet Fish Stew",
                    Properties = new() 
                    { 
                        ["catch_rate_bonus"] = 0.25,
                        ["xp_bonus"] = 0.3,
                        ["rare_fish_bonus"] = 0.2,
                        ["trait_discovery_bonus"] = 0.25,
                        ["duration_minutes"] = 90,
                        ["buff_type"] = "master_chef"
                    }
                }
            },
            "power_smoothie" => new CookingRecipe
            {
                MealType = "power_smoothie",
                Ingredients = new() { ["premium_algae"] = 3, ["sweet_corn"] = 2, ["rare_herbs"] = 2 },
                RequiredStation = "cooking_blender",
                RequiredCookingLevel = 4,
                Result = new()
                {
                    ItemId = "power_smoothie",
                    Name = "Ultimate Power Smoothie",
                    Properties = new() 
                    { 
                        ["catch_rate_bonus"] = 0.35,
                        ["xp_bonus"] = 0.4,
                        ["rare_fish_bonus"] = 0.3,
                        ["trait_discovery_bonus"] = 0.35,
                        ["duration_minutes"] = 120,
                        ["buff_type"] = "ultimate_chef"
                    }
                }
            },
            _ => null
        };
    }

    private static List<CookingRecipe> GetAllCookingRecipes()
    {
        return new List<CookingRecipe>
        {
            GetCookingRecipe("simple_salad")!,
            GetCookingRecipe("hearty_stew")!,
            GetCookingRecipe("algae_smoothie")!,
            GetCookingRecipe("herb_boost")!,
            GetCookingRecipe("gourmet_fish_stew")!,
            GetCookingRecipe("power_smoothie")!
        };
    }

    private static int CalculateCookingLevel(int experience)
    {
        // Simple level calculation: Level = 1 + floor(experience / 100)
        // Level 1: 0-99 XP, Level 2: 100-199 XP, etc.
        return 1 + (experience / 100);
    }

    private static string GetMealEffectsDescription(Dictionary<string, object> properties)
    {
        var effects = new List<string>();
        
        if (properties.ContainsKey("catch_rate_bonus"))
        {
            var bonus = Math.Round((double)properties["catch_rate_bonus"] * 100);
            effects.Add($"+{bonus}% catch rate");
        }
        
        if (properties.ContainsKey("xp_bonus"))
        {
            var bonus = Math.Round((double)properties["xp_bonus"] * 100);
            effects.Add($"+{bonus}% XP gain");
        }
        
        if (properties.ContainsKey("rare_fish_bonus"))
        {
            var bonus = Math.Round((double)properties["rare_fish_bonus"] * 100);
            effects.Add($"+{bonus}% rare fish chance");
        }
        
        if (properties.ContainsKey("trait_discovery_bonus"))
        {
            var bonus = Math.Round((double)properties["trait_discovery_bonus"] * 100);
            effects.Add($"+{bonus}% trait discovery");
        }
        
        if (properties.ContainsKey("duration_minutes"))
        {
            var duration = properties["duration_minutes"];
            effects.Add($"Lasts {duration} minutes");
        }

        return string.Join(", ", effects);
    }

    private static string GetItemDisplayName(string itemId)
    {
        return itemId switch
        {
            "trap_material" => "Trap Materials",
            "worm_bait" => "Worm Bait",
            "spinner_lure" => "Spinner Lure",
            "rare_bait" => "Golden Lure",
            // Crops
            "corn" => "Corn",
            "sweet_corn" => "Sweet Corn",
            "tomato" => "Tomato",
            "cherry_tomato" => "Cherry Tomato",
            "algae" => "Algae",
            "premium_algae" => "Premium Algae",
            "spice_herbs" => "Spice Herbs",
            "rare_herbs" => "Rare Herbs",
            // Cooking stations
            "cooking_pot" => "Cooking Pot",
            "cooking_blender" => "High-Tech Blender",
            // Meals
            "gourmet_fish_stew" => "Gourmet Fish Stew",
            "power_smoothie" => "Ultimate Power Smoothie",
            _ => itemId.Replace("_", " ").ToTitleCase()
        };
    }
}

public class TrapRecipe
{
    public string TrapType { get; set; } = string.Empty;
    public Dictionary<string, int> Requirements { get; set; } = new();
    public CraftResult Result { get; set; } = new();
}

public class CraftResult
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class CookingRecipe
{
    public string MealType { get; set; } = string.Empty;
    public Dictionary<string, int> Ingredients { get; set; } = new();
    public CraftResult Result { get; set; } = new();
    public string? RequiredStation { get; set; } = null; // null means can cook without station
    public int RequiredCookingLevel { get; set; } = 0;
}