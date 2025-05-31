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
            var availableMeals = "simple_salad, hearty_stew, algae_smoothie, herb_boost";
            return await feedbackService.SendContextualErrorAsync(
                $"Unknown meal type '{mealType}'. Available meals: {availableMeals}");
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

        var embed = new Embed
        {
            Title = "🍽️ Cooking Successful!",
            Description = $"You cooked a **{recipe.Result.Name}**!\n\n" +
                         $"**Ingredients Used:**\n" +
                         string.Join("\n", recipe.Ingredients.Select(r => 
                             $"• {r.Value}x {GetItemDisplayName(r.Key)}")) +
                         $"\n\n**Meal Effects:**\n" +
                         GetMealEffectsDescription(recipe.Result.Properties),
            Colour = Color.Orange,
            Footer = new EmbedFooter("Use this meal to gain temporary fishing buffs!"),
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
            GetCookingRecipe("herb_boost")!
        };
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
}