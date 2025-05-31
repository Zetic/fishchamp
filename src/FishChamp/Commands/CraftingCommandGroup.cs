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
[Description("Crafting commands for creating traps and equipment")]
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

    [Command("recipes")]
    [Description("View available trap crafting recipes")]
    public async Task<IResult> ViewRecipesAsync()
    {
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
            Footer = new EmbedFooter("Materials can be purchased from shops or found while fishing"),
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

    private static string GetItemDisplayName(string itemId)
    {
        return itemId switch
        {
            "trap_material" => "Trap Materials",
            "worm_bait" => "Worm Bait",
            "spinner_lure" => "Spinner Lure",
            "rare_bait" => "Golden Lure",
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