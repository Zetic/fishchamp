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

    [Command("item")]
    [Description("Craft tools, furniture, and equipment using blueprints")]
    public async Task<IResult> CraftItemAsync(
        [Description("Blueprint ID to craft")] string blueprintId)
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

        // Get blueprint
        var blueprint = GetBlueprint(blueprintId.ToLower());
        if (blueprint == null)
        {
            var availableBlueprints = "advanced_rod, expert_rod, wooden_chair, fishing_display, master_trap";
            return await feedbackService.SendContextualErrorAsync(
                $"Unknown blueprint '{blueprintId}'. Available blueprints: {availableBlueprints}");
        }

        // Check crafting level requirement
        if (player.CraftingLevel < blueprint.Requirements.CraftingLevel)
        {
            return await feedbackService.SendContextualErrorAsync(
                $"You need crafting level {blueprint.Requirements.CraftingLevel} to craft {blueprint.Name}. Your crafting level is {player.CraftingLevel}.");
        }

        // Check if blueprint is unlocked (auto-unlock basic tier blueprints)
        if (blueprint.Tier == BlueprintTier.Basic && !player.UnlockedBlueprints.Contains(blueprint.BlueprintId))
        {
            player.UnlockedBlueprints.Add(blueprint.BlueprintId);
            await playerRepository.UpdatePlayerAsync(player);
        }
        
        if (!HasUnlockedBlueprint(player.UnlockedBlueprints, blueprint))
        {
            var missingPrereq = blueprint.Requirements.UnlockedBy.FirstOrDefault(p => !player.UnlockedBlueprints.Contains(p));
            return await feedbackService.SendContextualErrorAsync(
                $"Blueprint locked! You need to craft {GetItemDisplayName(missingPrereq ?? "")} first to unlock this blueprint.");
        }

        // Check if crafting station is required
        if (!string.IsNullOrEmpty(blueprint.Requirements.RequiredStation))
        {
            var station = inventory.Items.FirstOrDefault(i => 
                i.ItemType == "CraftingStation" && i.ItemId == blueprint.Requirements.RequiredStation);
            
            if (station == null)
            {
                var stationName = GetItemDisplayName(blueprint.Requirements.RequiredStation);
                return await feedbackService.SendContextualErrorAsync(
                    $"You need a {stationName} to craft {blueprint.Name}.");
            }
        }

        // Check if player has required materials
        foreach (var material in blueprint.Materials)
        {
            var item = inventory.Items.FirstOrDefault(i => 
                i.ItemId == material.Key && i.Quantity >= material.Value);
            
            if (item == null)
            {
                var missing = inventory.Items.FirstOrDefault(i => i.ItemId == material.Key);
                var hasAmount = missing?.Quantity ?? 0;
                var itemName = GetItemDisplayName(material.Key);
                return await feedbackService.SendContextualErrorAsync(
                    $"Not enough materials! Need {material.Value} {itemName} (you have {hasAmount})");
            }
        }

        // Check if player is already crafting something with time delay
        var now = DateTime.UtcNow;
        var activeCrafting = player.ActiveCraftingJobs.FirstOrDefault(job => job.Value > now);
        if (activeCrafting.Key != null)
        {
            var timeLeft = activeCrafting.Value - now;
            return await feedbackService.SendContextualErrorAsync(
                $"You're already crafting {GetItemDisplayName(activeCrafting.Key)}! " +
                $"Time remaining: {(int)timeLeft.TotalMinutes} minutes.");
        }

        // Calculate success rate
        var successRate = CalculateSuccessRate(blueprint, player.CraftingLevel);
        var random = new Random();
        var craftingSuccess = random.NextDouble() <= successRate;

        // Consume materials regardless of success (realism)
        foreach (var material in blueprint.Materials)
        {
            await inventoryRepository.RemoveItemAsync(user.ID.Value, material.Key, material.Value);
        }

        // Handle time delay
        if (blueprint.Difficulty.CraftingTimeMinutes > 0)
        {
            var completionTime = now.AddMinutes(blueprint.Difficulty.CraftingTimeMinutes);
            player.ActiveCraftingJobs[blueprint.Result.ItemId] = completionTime;
            await playerRepository.UpdatePlayerAsync(player);

            var embed = new Embed
            {
                Title = "🔨 Crafting Started!",
                Description = $"You started crafting **{blueprint.Name}**!\n\n" +
                             $"**Materials Used:**\n" +
                             string.Join("\n", blueprint.Materials.Select(m => 
                                 $"• {m.Value}x {GetItemDisplayName(m.Key)}")) +
                             $"\n\n**Time Required:** {blueprint.Difficulty.CraftingTimeMinutes} minutes\n" +
                             $"**Success Rate:** {Math.Round(successRate * 100)}%\n\n" +
                             $"Use `/craft check` to see when your item will be ready!",
                Colour = Color.Orange,
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }

        // Instant crafting
        if (craftingSuccess)
        {
            // Create the crafted item
            var craftedItem = new InventoryItem
            {
                ItemId = blueprint.Result.ItemId,
                ItemType = blueprint.Type.ToString(),
                Name = blueprint.Result.Name,
                Quantity = 1,
                Properties = blueprint.Result.Properties
            };

            await inventoryRepository.AddItemAsync(user.ID.Value, craftedItem);

            // Unlock the blueprint for future prerequisite checks
            if (!player.UnlockedBlueprints.Contains(blueprint.BlueprintId))
            {
                player.UnlockedBlueprints.Add(blueprint.BlueprintId);
            }
        }

        // Gain crafting experience
        player.CraftingExperience += blueprint.Difficulty.ExperienceGained;
        
        // Check for crafting level up
        var newCraftingLevel = CalculateCraftingLevel(player.CraftingExperience);
        var levelUpMessage = "";
        if (newCraftingLevel > player.CraftingLevel)
        {
            player.CraftingLevel = newCraftingLevel;
            levelUpMessage = $"\n\n🎉 **Crafting Level Up!** You are now crafting level {newCraftingLevel}!";
        }
        
        await playerRepository.UpdatePlayerAsync(player);

        var resultEmbed = new Embed
        {
            Title = craftingSuccess ? "🔨 Crafting Successful!" : "💥 Crafting Failed!",
            Description = craftingSuccess 
                ? $"You crafted a **{blueprint.Name}**!\n\n" +
                  $"**Materials Used:**\n" +
                  string.Join("\n", blueprint.Materials.Select(m => 
                      $"• {m.Value}x {GetItemDisplayName(m.Key)}")) +
                  $"\n\n**Crafting XP:** +{blueprint.Difficulty.ExperienceGained}" +
                  levelUpMessage
                : $"Crafting **{blueprint.Name}** failed!\n\n" +
                  $"**Materials Lost:**\n" +
                  string.Join("\n", blueprint.Materials.Select(m => 
                      $"• {m.Value}x {GetItemDisplayName(m.Key)}")) +
                  $"\n\n**Success Rate:** {Math.Round(successRate * 100)}%\n" +
                  $"**Crafting XP:** +{blueprint.Difficulty.ExperienceGained}" +
                  levelUpMessage +
                  "\n\n*Higher crafting level improves success rate!*",
            Colour = craftingSuccess ? Color.Gold : Color.Red,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(resultEmbed);
    }

    [Command("check")]
    [Description("Check your active crafting jobs and claim completed items")]
    public async Task<IResult> CheckCraftingAsync()
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

        var now = DateTime.UtcNow;
        var completedJobs = new List<string>();
        var activeJobs = new List<string>();

        foreach (var job in player.ActiveCraftingJobs.ToList())
        {
            if (job.Value <= now)
            {
                completedJobs.Add(job.Key);
                player.ActiveCraftingJobs.Remove(job.Key);
            }
            else
            {
                var timeLeft = job.Value - now;
                activeJobs.Add($"• {GetItemDisplayName(job.Key)} - {(int)timeLeft.TotalMinutes} minutes remaining");
            }
        }

        // Claim completed items
        foreach (var itemId in completedJobs)
        {
            var blueprint = GetBlueprint(itemId);
            if (blueprint != null)
            {
                // Calculate success rate for completed job
                var successRate = CalculateSuccessRate(blueprint, player.CraftingLevel);
                var random = new Random();
                var craftingSuccess = random.NextDouble() <= successRate;

                if (craftingSuccess)
                {
                    var craftedItem = new InventoryItem
                    {
                        ItemId = blueprint.Result.ItemId,
                        ItemType = blueprint.Type.ToString(),
                        Name = blueprint.Result.Name,
                        Quantity = 1,
                        Properties = blueprint.Result.Properties
                    };

                    await inventoryRepository.AddItemAsync(user.ID.Value, craftedItem);

                    // Unlock the blueprint
                    if (!player.UnlockedBlueprints.Contains(blueprint.BlueprintId))
                    {
                        player.UnlockedBlueprints.Add(blueprint.BlueprintId);
                    }
                }
            }
        }

        await playerRepository.UpdatePlayerAsync(player);

        var description = new StringBuilder();

        if (completedJobs.Count > 0)
        {
            description.AppendLine("**🎉 Completed Crafting Jobs:**");
            foreach (var itemId in completedJobs)
            {
                description.AppendLine($"• {GetItemDisplayName(itemId)} - Ready for collection!");
            }
            description.AppendLine();
        }

        if (activeJobs.Count > 0)
        {
            description.AppendLine("**⏳ Active Crafting Jobs:**");
            description.AppendLine(string.Join("\n", activeJobs));
        }
        else if (completedJobs.Count == 0)
        {
            description.AppendLine("You have no active crafting jobs.");
            description.AppendLine("Use `/craft item <blueprint>` to start crafting!");
        }

        var embed = new Embed
        {
            Title = "🔧 Crafting Status",
            Description = description.ToString(),
            Colour = completedJobs.Count > 0 ? Color.Gold : Color.Blue,
            Footer = new EmbedFooter("Completed items have been automatically added to your inventory!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("upgrade")]
    [Description("Upgrade an existing item to a better version")]
    public async Task<IResult> UpgradeItemAsync(
        [Description("Item to upgrade (must be in inventory)")] string itemId)
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

        // Find the item to upgrade
        var itemToUpgrade = inventory.Items.FirstOrDefault(i => i.ItemId == itemId.ToLower());
        if (itemToUpgrade == null)
        {
            return await feedbackService.SendContextualErrorAsync(
                $"You don't have {GetItemDisplayName(itemId)} in your inventory!");
        }

        // Get upgrade path
        var upgradeBlueprint = GetUpgradeBlueprint(itemId.ToLower());
        if (upgradeBlueprint == null)
        {
            return await feedbackService.SendContextualErrorAsync(
                $"{GetItemDisplayName(itemId)} cannot be upgraded.");
        }

        // Check crafting level requirement
        if (player.CraftingLevel < upgradeBlueprint.Requirements.CraftingLevel)
        {
            return await feedbackService.SendContextualErrorAsync(
                $"You need crafting level {upgradeBlueprint.Requirements.CraftingLevel} to upgrade {itemToUpgrade.Name}.");
        }

        // Check if player has required materials (excluding the base item)
        foreach (var material in upgradeBlueprint.Materials)
        {
            var item = inventory.Items.FirstOrDefault(i => 
                i.ItemId == material.Key && i.Quantity >= material.Value);
            
            if (item == null)
            {
                var missing = inventory.Items.FirstOrDefault(i => i.ItemId == material.Key);
                var hasAmount = missing?.Quantity ?? 0;
                var itemName = GetItemDisplayName(material.Key);
                return await feedbackService.SendContextualErrorAsync(
                    $"Not enough materials! Need {material.Value} {itemName} (you have {hasAmount})");
            }
        }

        // Consume materials and original item
        foreach (var material in upgradeBlueprint.Materials)
        {
            await inventoryRepository.RemoveItemAsync(user.ID.Value, material.Key, material.Value);
        }
        await inventoryRepository.RemoveItemAsync(user.ID.Value, itemToUpgrade.ItemId, 1);

        // Calculate success rate
        var successRate = CalculateSuccessRate(upgradeBlueprint, player.CraftingLevel);
        var random = new Random();
        var upgradeSuccess = random.NextDouble() <= successRate;

        if (upgradeSuccess)
        {
            // Create upgraded item
            var upgradedItem = new InventoryItem
            {
                ItemId = upgradeBlueprint.Result.ItemId,
                ItemType = upgradeBlueprint.Type.ToString(),
                Name = upgradeBlueprint.Result.Name,
                Quantity = 1,
                Properties = upgradeBlueprint.Result.Properties
            };

            await inventoryRepository.AddItemAsync(user.ID.Value, upgradedItem);
        }

        // Gain experience
        player.CraftingExperience += upgradeBlueprint.Difficulty.ExperienceGained;
        var newCraftingLevel = CalculateCraftingLevel(player.CraftingExperience);
        var levelUpMessage = "";
        if (newCraftingLevel > player.CraftingLevel)
        {
            player.CraftingLevel = newCraftingLevel;
            levelUpMessage = $"\n\n🎉 **Crafting Level Up!** You are now crafting level {newCraftingLevel}!";
        }
        
        await playerRepository.UpdatePlayerAsync(player);

        var embed = new Embed
        {
            Title = upgradeSuccess ? "⬆️ Upgrade Successful!" : "💥 Upgrade Failed!",
            Description = upgradeSuccess 
                ? $"You upgraded **{itemToUpgrade.Name}** to **{upgradeBlueprint.Name}**!\n\n" +
                  $"**Materials Used:**\n" +
                  string.Join("\n", upgradeBlueprint.Materials.Select(m => 
                      $"• {m.Value}x {GetItemDisplayName(m.Key)}")) +
                  $"\n• 1x {itemToUpgrade.Name}" +
                  $"\n\n**Crafting XP:** +{upgradeBlueprint.Difficulty.ExperienceGained}" +
                  levelUpMessage
                : $"Upgrade of **{itemToUpgrade.Name}** failed!\n\n" +
                  $"**Materials Lost:**\n" +
                  string.Join("\n", upgradeBlueprint.Materials.Select(m => 
                      $"• {m.Value}x {GetItemDisplayName(m.Key)}")) +
                  $"\n• 1x {itemToUpgrade.Name}" +
                  $"\n\n**Success Rate:** {Math.Round(successRate * 100)}%" +
                  $"\n**Crafting XP:** +{upgradeBlueprint.Difficulty.ExperienceGained}" +
                  levelUpMessage +
                  "\n\n*Higher crafting level improves success rate!*",
            Colour = upgradeSuccess ? Color.Gold : Color.Red,
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
        else if (type.ToLower() == "blueprint" || type.ToLower() == "blueprints" || type.ToLower() == "item" || type.ToLower() == "items")
        {
            return await ViewBlueprintRecipesAsync();
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
            Footer = new EmbedFooter("Materials can be purchased from shops or found while fishing. Use '/craft recipes blueprints' for item blueprints."),
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

    private static int CalculateCraftingLevel(int experience)
    {
        // Similar level calculation for crafting: Level = 1 + floor(experience / 150)
        // Slightly harder progression than cooking
        return 1 + (experience / 150);
    }

    private static double CalculateSuccessRate(Blueprint blueprint, int craftingLevel)
    {
        // Base success rate improved by level difference
        var levelBonus = Math.Max(0, craftingLevel - blueprint.Requirements.CraftingLevel) * 0.05;
        return Math.Min(1.0, blueprint.Difficulty.BaseSuccessRate + levelBonus);
    }

    private static Blueprint? GetUpgradeBlueprint(string baseItemId)
    {
        return baseItemId switch
        {
            "basic_rod" => new Blueprint
            {
                BlueprintId = "advanced_rod_upgrade",
                Name = "Advanced Fishing Rod",
                Description = "Upgrade your basic rod to an advanced version",
                Type = BlueprintType.Tool,
                Tier = BlueprintTier.Advanced,
                Materials = new() { ["trap_material"] = 2, ["corn"] = 1 },
                Requirements = new() { CraftingLevel = 2 },
                Difficulty = new() { BaseSuccessRate = 0.90, ExperienceGained = 20 },
                Result = new()
                {
                    ItemId = "advanced_rod",
                    Name = "Advanced Fishing Rod",
                    Properties = new() { ["catch_rate_bonus"] = 0.15, ["durability"] = 200 }
                }
            },
            "advanced_rod" => new Blueprint
            {
                BlueprintId = "expert_rod_upgrade",
                Name = "Expert Fishing Rod",
                Description = "Upgrade your advanced rod to expert level",
                Type = BlueprintType.Tool,
                Tier = BlueprintTier.Expert,
                Materials = new() { ["trap_material"] = 4, ["rare_herbs"] = 1, ["premium_algae"] = 1 },
                Requirements = new() { CraftingLevel = 4 },
                Difficulty = new() { BaseSuccessRate = 0.75, ExperienceGained = 40 },
                Result = new()
                {
                    ItemId = "expert_rod",
                    Name = "Expert Fishing Rod",
                    Properties = new() { ["catch_rate_bonus"] = 0.25, ["rare_fish_bonus"] = 0.10, ["durability"] = 350 }
                }
            },
            "reinforced_trap" => new Blueprint
            {
                BlueprintId = "master_trap_upgrade",
                Name = "Master Fish Trap",
                Description = "Upgrade your reinforced trap to master level",
                Type = BlueprintType.Equipment,
                Tier = BlueprintTier.Master,
                Materials = new() { ["trap_material"] = 6, ["rare_bait"] = 1, ["premium_algae"] = 1 },
                Requirements = new() { CraftingLevel = 5 },
                Difficulty = new() { BaseSuccessRate = 0.65, ExperienceGained = 60 },
                Result = new()
                {
                    ItemId = "master_trap",
                    Name = "Master Fish Trap",
                    Properties = new() { ["durability"] = 300, ["efficiency"] = 3.0, ["rare_fish_bonus"] = 0.15 }
                }
            },
            _ => null
        };
    }

    private static bool HasUnlockedBlueprint(List<string> unlockedBlueprints, Blueprint blueprint)
    {
        // Check if all prerequisites are met
        return blueprint.Requirements.UnlockedBy.All(prereq => 
            unlockedBlueprints.Contains(prereq));
    }

    private static List<Blueprint> GetAllBlueprints()
    {
        return new List<Blueprint>
        {
            GetBlueprint("advanced_rod")!,
            GetBlueprint("expert_rod")!,
            GetBlueprint("wooden_chair")!,
            GetBlueprint("fishing_display")!,
            GetBlueprint("master_trap")!
        };
    }

    private async Task<IResult> ViewBlueprintRecipesAsync()
    {
        var blueprints = GetAllBlueprints();
        var description = new StringBuilder();

        description.AppendLine("**Available Item Blueprints:**\n");

        var groupedBlueprints = blueprints.GroupBy(b => b.Type);
        
        foreach (var group in groupedBlueprints)
        {
            description.AppendLine($"**{group.Key}s:**");
            
            foreach (var blueprint in group.OrderBy(b => b.Tier))
            {
                description.AppendLine($"• **{blueprint.Name}** (`/craft item {blueprint.BlueprintId}`)");
                description.AppendLine($"  Tier: {blueprint.Tier} | Level: {blueprint.Requirements.CraftingLevel}");
                
                if (blueprint.Requirements.UnlockedBy.Count > 0)
                {
                    var prereqs = string.Join(", ", blueprint.Requirements.UnlockedBy.Select(GetItemDisplayName));
                    description.AppendLine($"  Prerequisites: {prereqs}");
                }
                
                description.AppendLine($"  Materials: {string.Join(", ", blueprint.Materials.Select(m => $"{m.Value}x {GetItemDisplayName(m.Key)}"))}");
                description.AppendLine($"  Success Rate: {Math.Round(blueprint.Difficulty.BaseSuccessRate * 100)}% | Time: {(blueprint.Difficulty.CraftingTimeMinutes > 0 ? $"{blueprint.Difficulty.CraftingTimeMinutes}min" : "Instant")}");
                description.AppendLine();
            }
        }

        var embed = new Embed
        {
            Title = "📋 Item Blueprints",
            Description = description.ToString(),
            Colour = Color.Purple,
            Footer = new EmbedFooter("Higher crafting level improves success rates! Use '/craft recipes cooking' for meal recipes."),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
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

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i][1..].ToLower() : "");
            }
        }
        return string.Join(" ", words);
    }

    private static Blueprint? GetBlueprint(string blueprintId)
    {
        return blueprintId switch
        {
            // Tool Blueprints
            "advanced_rod" => new Blueprint
            {
                BlueprintId = "advanced_rod",
                Name = "Advanced Fishing Rod",
                Description = "A high-quality fishing rod with improved catch rates",
                Type = BlueprintType.Tool,
                Tier = BlueprintTier.Advanced,
                Materials = new() { ["trap_material"] = 4, ["corn"] = 2 },
                Requirements = new() { CraftingLevel = 2 },
                Difficulty = new() { BaseSuccessRate = 0.85, CraftingTimeMinutes = 15, ExperienceGained = 25 },
                Result = new()
                {
                    ItemId = "advanced_rod",
                    Name = "Advanced Fishing Rod",
                    Properties = new() { ["catch_rate_bonus"] = 0.15, ["durability"] = 200 }
                }
            },
            "expert_rod" => new Blueprint
            {
                BlueprintId = "expert_rod",
                Name = "Expert Fishing Rod",
                Description = "A masterwork fishing rod for the experienced angler",
                Type = BlueprintType.Tool,
                Tier = BlueprintTier.Expert,
                Materials = new() { ["trap_material"] = 8, ["rare_herbs"] = 2, ["premium_algae"] = 1 },
                Requirements = new() { CraftingLevel = 4, UnlockedBy = new() { "advanced_rod" } },
                Difficulty = new() { BaseSuccessRate = 0.70, CraftingTimeMinutes = 45, ExperienceGained = 50 },
                Result = new()
                {
                    ItemId = "expert_rod",
                    Name = "Expert Fishing Rod",
                    Properties = new() { ["catch_rate_bonus"] = 0.25, ["rare_fish_bonus"] = 0.10, ["durability"] = 350 }
                }
            },
            // Furniture Blueprints
            "wooden_chair" => new Blueprint
            {
                BlueprintId = "wooden_chair",
                Name = "Wooden Chair",
                Description = "A comfortable wooden chair for your home",
                Type = BlueprintType.Furniture,
                Tier = BlueprintTier.Basic,
                Materials = new() { ["trap_material"] = 3 },
                Requirements = new() { CraftingLevel = 1 },
                Difficulty = new() { BaseSuccessRate = 0.95, CraftingTimeMinutes = 10, ExperienceGained = 15 },
                Result = new()
                {
                    ItemId = "wooden_chair",
                    Name = "Wooden Chair",
                    Properties = new() { ["furniture_type"] = "seating", ["comfort_bonus"] = 5 }
                }
            },
            "fishing_display" => new Blueprint
            {
                BlueprintId = "fishing_display",
                Name = "Fish Display Case",
                Description = "Show off your best catches in style",
                Type = BlueprintType.Furniture,
                Tier = BlueprintTier.Advanced,
                Materials = new() { ["trap_material"] = 6, ["algae"] = 3 },
                Requirements = new() { CraftingLevel = 3 },
                Difficulty = new() { BaseSuccessRate = 0.80, CraftingTimeMinutes = 30, ExperienceGained = 35 },
                Result = new()
                {
                    ItemId = "fishing_display",
                    Name = "Fish Display Case",
                    Properties = new() { ["furniture_type"] = "decoration", ["fishing_inspiration"] = 0.05 }
                }
            },
            // Equipment Blueprints (extending existing traps)
            "master_trap" => new Blueprint
            {
                BlueprintId = "master_trap",
                Name = "Master Fish Trap",
                Description = "The ultimate passive fishing solution",
                Type = BlueprintType.Equipment,
                Tier = BlueprintTier.Master,
                Materials = new() { ["trap_material"] = 12, ["rare_bait"] = 2, ["premium_algae"] = 2 },
                Requirements = new() { CraftingLevel = 5, UnlockedBy = new() { "reinforced_trap" } },
                Difficulty = new() { BaseSuccessRate = 0.60, CraftingTimeMinutes = 90, ExperienceGained = 75 },
                Result = new()
                {
                    ItemId = "master_trap",
                    Name = "Master Fish Trap",
                    Properties = new() { ["durability"] = 300, ["efficiency"] = 3.0, ["rare_fish_bonus"] = 0.15 }
                }
            },
            _ => null
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
            // Tools
            "advanced_rod" => "Advanced Fishing Rod",
            "expert_rod" => "Expert Fishing Rod",
            // Furniture
            "wooden_chair" => "Wooden Chair",
            "fishing_display" => "Fish Display Case",
            // Equipment
            "master_trap" => "Master Fish Trap",
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

public class CookingRecipe
{
    public string MealType { get; set; } = string.Empty;
    public Dictionary<string, int> Ingredients { get; set; } = new();
    public CraftResult Result { get; set; } = new();
    public string? RequiredStation { get; set; } = null; // null means can cook without station
    public int RequiredCookingLevel { get; set; } = 0;
}