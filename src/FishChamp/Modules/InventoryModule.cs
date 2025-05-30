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
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;

namespace FishChamp.Modules;

[Group("inventory")]
[Description("Inventory management commands")]
public class InventoryModule(IDiscordRestChannelAPI channelAPI, ICommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository, IDiscordRestUserAPI userAPI, 
    FeedbackService feedbackService) : CommandGroup
{
    [Command("view")]
    [Description("View your inventory")]
    public async Task<IResult> ViewInventoryAsync()
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.FromError(new NotFoundError("Failed to get user id from context"));
        }

        var userResult = await userAPI.GetUserAsync(userId);

        if (!userResult.IsSuccess)
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(userId.Value, userResult.Entity.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(userId.Value);

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
                var properties = item.Properties.Count > 0 
                    ? $" ({string.Join(", ", item.Properties.Select(p => $"{p.Key}: {p.Value}"))})"
                    : "";
                return $"• {item.Name} x{item.Quantity}{properties}";
            }));

            fields.Add(new EmbedField($"{GetItemTypeEmoji(group.Key)} {group.Key}", itemsText, false));
        }

        var embed = new Embed
        {
            Title = $"🎒 {player.Username}'s Inventory",
            Colour = Color.Orange,
            Fields = fields,
            Footer = new EmbedFooter($"Last updated: {inventory.LastUpdated:yyyy-MM-dd HH:mm} UTC"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("fish")]
    [Description("View only your fish collection")]
    public async Task<IResult> ViewFishAsync()
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.FromError(new NotFoundError("Failed to get user id from context"));
        }

        var userResult = await userAPI.GetUserAsync(userId);

        if (!userResult.IsSuccess)
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(userId.Value, userResult.Entity.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(userId.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🐟 You haven't caught any fish yet! Use `/fishing cast` to start fishing.", Color.Yellow);
        }

        var fish = inventory.Items.Where(i => i.ItemType == "Fish").ToList();
        if (fish.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🐟 You haven't caught any fish yet! Use `/fishing cast` to start fishing.", Color.Red);
        }

        var fishText = string.Join("\n", fish.Select(f =>
        {
            var size = f.Properties.TryGetValue("size", out var propSize) ? $" ({propSize}cm)" : "";
            var rarity = f.Properties.TryGetValue("rarity", out var propRarity) ? GetRarityEmoji(propRarity.ToString()) : "";
            return $"{rarity} **{f.Name}** x{f.Quantity}{size}";
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