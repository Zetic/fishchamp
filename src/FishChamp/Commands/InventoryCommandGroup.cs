using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using Remora.Rest.Core;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using FishChamp.Helpers;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using System.Text.Json;

namespace FishChamp.Modules;

[Group("inventory")]
[Description("Inventory management commands")]
public class InventoryCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository, 
    FeedbackService feedbackService) : CommandGroup
{
    [Command("view")]
    [Description("View your inventory")]
    public async Task<IResult> ViewInventoryAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null || inventory.Items.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🎒 Your inventory is empty! Try fishing to get some items.", Color.Yellow);
        }

        var groupedItems = inventory.Items.GroupBy(i => i.ItemType);
        var fields = new List<EmbedField>();

        foreach (var group in groupedItems)
        {
            var itemsText = string.Join("\n", group.Select(item =>
            {
                string additionalInfo = "";
                
                if (item.ItemType == "Fish")
                {
                    var size = item.Properties.GetInt("size", 0);
                    var sizeText = size > 0 ? $"{size}cm" : "";
                    var rarity = item.Properties.GetString("rarity", "");
                    var rarityEmoji = !string.IsNullOrEmpty(rarity) ? GetRarityEmoji(rarity) : "";
                    additionalInfo = $" {rarityEmoji} {sizeText}";
                }
                else if (item.ItemType == "Rod")
                {
                    var power = item.Properties.GetInt("power", 0);
                    var powerText = power > 0 ? $"Power: {power}" : "";
                    var durability = item.Properties.GetInt("durability", 0);
                    var durabilityText = durability > 0 ? $"Durability: {durability}" : "";
                    additionalInfo = $" ({powerText}, {durabilityText})";
                    
                    // Mark equipped rod
                    if (item.ItemId == player.EquippedRod)
                    {
                        additionalInfo += " [EQUIPPED]";
                    }
                }
                else if (item.ItemType == "Bait")
                {
                    // Mark equipped bait
                    if (item.ItemId == player.EquippedBait)
                    {
                        additionalInfo = " [EQUIPPED]";
                    }
                }
                
                return $"• **{item.Name}** x{item.Quantity}{additionalInfo}";
            }));

            fields.Add(new EmbedField($"{GetItemTypeEmoji(group.Key)} {group.Key}s ({group.Sum(i => i.Quantity)})", itemsText, false));
        }

        // Clean up expired buffs
        player.ActiveBuffs.RemoveAll(b => DateTime.UtcNow >= b.ExpiresAt);
        if (player.ActiveBuffs.Any())
        {
            await playerRepository.UpdatePlayerAsync(player);
        }

        var embed = new Embed
        {
            Title = $"🎒 {player.Username}'s Inventory",
            Colour = Color.Orange,
            Fields = fields,
            Footer = new EmbedFooter($"Use /inventory fish, /inventory rods, or /inventory baits for more details\nLast updated: {inventory.LastUpdated:yyyy-MM-dd HH:mm} UTC"),
            Timestamp = DateTimeOffset.UtcNow
        };

        // Add active buffs if any
        if (player.ActiveBuffs.Any())
        {
            var buffsText = string.Join("\n", player.ActiveBuffs.Select(buff =>
            {
                var timeLeft = buff.ExpiresAt - DateTime.UtcNow;
                var timeText = timeLeft.TotalMinutes > 60 
                    ? $"{timeLeft.Hours}h {timeLeft.Minutes}m" 
                    : $"{timeLeft.Minutes}m";
                return $"• **{buff.Name}** - {timeText} left";
            }));

            var buffFields = new List<EmbedField>(fields)
            {
                new("🎯 Active Buffs", buffsText, false)
            };
            
            embed = embed with { Fields = buffFields };
        }

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("fish")]
    [Description("View only your fish collection")]
    public async Task<IResult> ViewFishAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🐟 You haven't caught any fish yet! Use `/fish` to start fishing.", Color.Yellow);
        }

        var fish = inventory.Items.Where(i => i.ItemType == "Fish").ToList();
        if (fish.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🐟 You haven't caught any fish yet! Use `/fish` to start fishing.", Color.Red);
        }

        var fishText = string.Join("\n", fish.Select(f =>
        {
            var size = f.Properties.GetInt("size", 0);
            var sizeText = size > 0 ? $" ({size}cm)" : "";
            var weight = f.Properties.GetDouble("weight", 0);
            var weightText = weight > 0 ? $", {weight}g" : "";
            var rarity = f.Properties.GetString("rarity", "");
            var rarityEmoji = !string.IsNullOrEmpty(rarity) ? GetRarityEmoji(rarity) : "";
            
            // Add fish traits if any
            string traitsText = "";
            var fishTraits = (FishTrait)f.Properties.GetInt("traits", 0);
            if (fishTraits != FishTrait.None)
            {
                var traitsList = new List<string>();
                if ((fishTraits & FishTrait.Evasive) != 0) traitsList.Add("Evasive");
                if ((fishTraits & FishTrait.Slippery) != 0) traitsList.Add("Slippery");
                if ((fishTraits & FishTrait.Magnetic) != 0) traitsList.Add("Magnetic");
                if ((fishTraits & FishTrait.Camouflage) != 0) traitsList.Add("Camouflage");
                
                if (traitsList.Count > 0)
                {
                    traitsText = $" | {string.Join(", ", traitsList)}";
                }
            }
            
            return $"{rarityEmoji} **{f.Name}** x{f.Quantity}{sizeText}{weightText}{traitsText}";
        }));

        var totalFish = fish.Sum(f => f.Quantity);
        var uniqueSpecies = fish.Count;

        var embed = new Embed
        {
            Title = $"🐟 {player.Username}'s Fish Collection",
            Description = fishText,
            Colour = Color.Cyan,
            Fields = new List<EmbedField>
            {
                new("Total Fish", totalFish.ToString(), true),
                new("Unique Species", uniqueSpecies.ToString(), true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("rods")]
    [Description("View your fishing rod collection")]
    public async Task<IResult> ViewRodsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🎣 You don't have any fishing rods yet! Visit a shop to buy one.", Color.Yellow);
        }

        var rods = inventory.Items.Where(i => i.ItemType == "Rod").ToList();
        if (rods.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🎣 You don't have any fishing rods yet! Visit a shop to buy one.", Color.Red);
        }

        var rodsText = string.Join("\n", rods.Select(r =>
        {
            var power = r.Properties.GetInt("power", 0);
            var powerText = power > 0 ? $"Power: {power}" : "";
            var durability = r.Properties.GetInt("durability", 0);
            var durabilityText = durability > 0 ? $"Durability: {durability}" : "";
            var equipped = r.ItemId == player.EquippedRod ? " **[EQUIPPED]**" : "";
            
            return $"• **{r.Name}** ({powerText}, {durabilityText}){equipped}";
        }));

        var embed = new Embed
        {
            Title = $"🎣 {player.Username}'s Fishing Rods",
            Description = rodsText,
            Colour = Color.Brown,
            Footer = new EmbedFooter("Use /fishing equip-rod <rod name> to equip a rod"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("baits")]
    [Description("View your bait collection")]
    public async Task<IResult> ViewBaitsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🪱 You don't have any bait yet! Visit a shop to buy some.", Color.Yellow);
        }

        var baits = inventory.Items.Where(i => i.ItemType == "Bait").ToList();
        if (baits.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🪱 You don't have any bait yet! Visit a shop to buy some.", Color.Red);
        }

        var baitsText = string.Join("\n", baits.Select(b =>
        {
            var attraction = b.Properties.GetDouble("attraction", 1.0);
            var attractionText = attraction != 1.0 ? $"Attraction: {attraction}x" : "";
            var equipped = b.ItemId == player.EquippedBait ? " **[EQUIPPED]**" : "";
            
            return $"• **{b.Name}** x{b.Quantity} ({attractionText}){equipped}";
        }));

        var embed = new Embed
        {
            Title = $"🪱 {player.Username}'s Bait Collection",
            Description = baitsText,
            Colour = Color.SandyBrown,
            Footer = new EmbedFooter("Use /fishing equip-bait <bait name> to equip bait"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("consume")]
    [Description("Consume a meal to gain temporary buffs")]
    public async Task<IResult> ConsumeMealAsync(
        [Description("Name of the meal to consume")] string mealName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualErrorAsync("Inventory not found!");
        }

        // Find the meal in inventory
        var meal = inventory.Items.FirstOrDefault(i => 
            i.ItemType == "Meal" && 
            (i.Name.Contains(mealName, StringComparison.OrdinalIgnoreCase) || 
             i.ItemId.Contains(mealName, StringComparison.OrdinalIgnoreCase)));

        if (meal == null)
        {
            return await feedbackService.SendContextualErrorAsync($"Meal '{mealName}' not found in your inventory!");
        }

        if (meal.Quantity <= 0)
        {
            return await feedbackService.SendContextualErrorAsync($"You don't have any {meal.Name} left!");
        }

        // Remove expired buffs
        player.ActiveBuffs.RemoveAll(b => DateTime.UtcNow >= b.ExpiresAt);

        // Check if player already has this type of buff
        var buffType = meal.Properties.GetString("buff_type", "");
        var existingBuff = player.ActiveBuffs.FirstOrDefault(b => 
            b.Effects.GetString("buff_type", "") == buffType);

        if (existingBuff != null)
        {
            return await feedbackService.SendContextualErrorAsync($"You already have a {buffType} buff active! Wait for it to expire or it will be replaced.");
        }

        // Consume the meal
        await inventoryRepository.RemoveItemAsync(user.ID.Value, meal.ItemId, 1);

        // Create buff from meal properties
        var durationMinutes = meal.Properties.GetInt("duration_minutes", 30);
        var buff = new ActiveBuff
        {
            BuffId = meal.ItemId,
            Name = meal.Name,
            ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes),
            Effects = meal.Properties
        };

        player.ActiveBuffs.Add(buff);
        await playerRepository.UpdatePlayerAsync(player);

        var effectsDescription = GetMealEffectsDescription(meal.Properties);
        var embed = new Embed
        {
            Title = "🍽️ Meal Consumed!",
            Description = $"You consumed **{meal.Name}** and gained temporary buffs!\n\n" +
                         $"**Effects:** {effectsDescription}\n" +
                         $"**Duration:** {durationMinutes} minutes\n" +
                         $"**Expires:** <t:{((DateTimeOffset)buff.ExpiresAt).ToUnixTimeSeconds()}:R>",
            Colour = Color.Green,
            Footer = new EmbedFooter("These buffs will enhance your fishing abilities!"),
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

        return string.Join(", ", effects);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await playerRepository.CreatePlayerAsync(userId, username);
            await inventoryRepository.CreateInventoryAsync(userId);
        }
        return player;
    }

    private static string GetItemTypeEmoji(string itemType)
    {
        return itemType.ToLower() switch
        {
            "fish" => "🐟",
            "rod" => "🎣",
            "bait" => "🪱",
            "meal" => "🍽️",
            "crop" => "🌾",
            "trap" => "🪤",
            "tool" => "🔧",
            _ => "📦"
        };
    }

    private static string GetRarityEmoji(string? rarity)
    {
        return rarity?.ToLower() switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵",
            "epic" => "🟣",
            "legendary" => "🟡",
            _ => "⚪"
        };
    }

}