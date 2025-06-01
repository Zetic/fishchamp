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
    public async Task<IResult> ViewInventoryAsync(
        [Description("Filter by item type (fish, rods, baits, meals, etc.)")] string? filter = null)
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

        var groupedItems = inventory.Items.AsEnumerable();
        
        // Apply filter if specified
        if (!string.IsNullOrEmpty(filter))
        {
            var filterLower = filter.ToLower();
            groupedItems = filterLower switch
            {
                "fish" => groupedItems.Where(i => i.ItemType == "Fish"),
                "rods" or "rod" => groupedItems.Where(i => i.ItemType == "Rod"),
                "baits" or "bait" => groupedItems.Where(i => i.ItemType == "Bait"),
                "meals" or "meal" => groupedItems.Where(i => i.ItemType == "Meal"),
                "crops" or "crop" => groupedItems.Where(i => i.ItemType == "Crop"),
                "traps" or "trap" => groupedItems.Where(i => i.ItemType == "Trap"),
                "tools" or "tool" => groupedItems.Where(i => i.ItemType == "Tool"),
                _ => groupedItems.Where(i => i.ItemType.ToLower().Contains(filterLower))
            };
        }
        
        var filteredGroupedItems = groupedItems.GroupBy(i => i.ItemType);
        var fields = new List<EmbedField>();

        foreach (var group in filteredGroupedItems)
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
            Title = !string.IsNullOrEmpty(filter) ? $"🎒 {player.Username}'s {char.ToUpper(filter[0])}{filter[1..]} Inventory" : $"🎒 {player.Username}'s Inventory",
            Colour = Color.Orange,
            Fields = fields,
            Footer = new EmbedFooter($"Use /inventory view [filter] to filter items (fish, rods, baits, meals, etc.)\nLast updated: {inventory.LastUpdated:yyyy-MM-dd HH:mm} UTC"),
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