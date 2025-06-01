using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using Remora.Discord.Commands.Feedback.Services;
using FishChamp.Helpers;

namespace FishChamp.Features.Trading;

[Group("trade")]
[Description("Trading commands")]
public class TradeCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    ITradeRepository tradeRepository, DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("offer")]
    [Description("Create a trade offer")]
    public async Task<IResult> OfferTradeAsync(
        [Description("User to trade with")] IUser targetUser,
        [Description("Fish/item name to offer")] string itemName,
        [Description("Quantity to offer")] int quantity = 1,
        [Description("Fish coins to request")] int requestedCoins = 0,
        [Description("Message for the trade")] string? message = null)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (user.ID.Value == targetUser.ID.Value)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You cannot trade with yourself!", Color.Red);
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🎒 You don't have an inventory yet!", Color.Red);
        }

        // Find the item in inventory
        var item = inventory.Items.FirstOrDefault(i => i.Name.ToLower().Contains(itemName.ToLower()));
        if (item == null)
        {
            return await feedbackService.SendContextualContentAsync($"🚫 You don't have any '{itemName}' in your inventory!", Color.Red);
        }

        if (item.Quantity < quantity)
        {
            return await feedbackService.SendContextualContentAsync($"🚫 You only have {item.Quantity} of '{item.Name}' but tried to offer {quantity}!", Color.Red);
        }

        // Create trade offer
        var trade = new Trade
        {
            InitiatorUserId = user.ID.Value,
            TargetUserId = targetUser.ID.Value,
            OfferedItems = [new TradeItem
            {
                ItemId = item.ItemId,
                ItemType = item.ItemType,
                Name = item.Name,
                Quantity = quantity,
                Properties = item.Properties
            }],
            RequestedFishCoins = requestedCoins,
            Message = message,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await tradeRepository.CreateTradeAsync(trade);

        var embed = new Embed
        {
            Title = "🤝 Trade Offer Created!",
            Description = $"Trade offer sent to {targetUser.Username}!\n\n" +
                         $"**Offering:** {quantity}x {item.Name}\n" +
                         $"**Requesting:** {(requestedCoins > 0 ? $"{requestedCoins} fish coins" : "Nothing")}\n" +
                         $"**Message:** {message ?? "None"}\n\n" +
                         $"The offer expires in 7 days.",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("list")]
    [Description("List your pending trades")]
    public async Task<IResult> ListTradesAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var trades = await tradeRepository.GetUserTradesAsync(user.ID.Value);
        var activeTrades = trades.Where(t => t.Status == TradeStatus.Pending).ToList();

        if (!activeTrades.Any())
        {
            return await feedbackService.SendContextualContentAsync("📭 You have no active trades!", Color.Yellow);
        }

        var offeredTrades = activeTrades.Where(t => t.InitiatorUserId == user.ID.Value).ToList();
        var receivedTrades = activeTrades.Where(t => t.TargetUserId == user.ID.Value).ToList();

        var description = "";

        if (offeredTrades.Any())
        {
            description += "**Your Offers:**\n";
            foreach (var trade in offeredTrades.Take(5))
            {
                var item = trade.OfferedItems.FirstOrDefault();
                description += $"• {item?.Name} ({item?.Quantity}) → {(trade.RequestedFishCoins > 0 ? $"{trade.RequestedFishCoins} coins" : "Nothing")}\n";
            }
            description += "\n";
        }

        if (receivedTrades.Any())
        {
            description += "**Received Offers:**\n";
            foreach (var trade in receivedTrades.Take(5))
            {
                var item = trade.OfferedItems.FirstOrDefault();
                description += $"• {item?.Name} ({item?.Quantity}) for {(trade.RequestedFishCoins > 0 ? $"{trade.RequestedFishCoins} coins" : "Nothing")}\n";
            }
        }

        var embed = new Embed
        {
            Title = "🤝 Your Trades",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /trade accept or /trade reject to respond to offers"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("accept")]
    [Description("Accept a trade offer")]
    public async Task<IResult> AcceptTradeAsync([Description("Trade ID")] string tradeId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var trade = await tradeRepository.GetTradeAsync(tradeId);
        if (trade == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Trade not found!", Color.Red);
        }

        if (trade.TargetUserId != user.ID.Value)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This trade is not for you!", Color.Red);
        }

        if (trade.Status != TradeStatus.Pending)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This trade is no longer active!", Color.Red);
        }

        // Execute the trade
        var targetPlayer = await playerRepository.GetPlayerAsync(user.ID.Value);
        var initiatorPlayer = await playerRepository.GetPlayerAsync(trade.InitiatorUserId);

        if (targetPlayer == null || initiatorPlayer == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Error: Unable to find player profiles!", Color.Red);
        }

        // Check if target has enough coins
        if (trade.RequestedFishCoins > 0 && targetPlayer.FishCoins < trade.RequestedFishCoins)
        {
            return await feedbackService.SendContextualContentAsync($"🚫 You need {trade.RequestedFishCoins} fish coins but only have {targetPlayer.FishCoins}!", Color.Red);
        }

        // Transfer items and coins
        foreach (var tradeItem in trade.OfferedItems)
        {
            // Remove from initiator, add to target
            await inventoryRepository.RemoveItemAsync(trade.InitiatorUserId, tradeItem.ItemId, tradeItem.Quantity);

            var newItem = new InventoryItem
            {
                ItemId = tradeItem.ItemId,
                ItemType = tradeItem.ItemType,
                Name = tradeItem.Name,
                Quantity = tradeItem.Quantity,
                Properties = tradeItem.Properties
            };
            await inventoryRepository.AddItemAsync(user.ID.Value, newItem);
        }

        // Transfer coins
        if (trade.RequestedFishCoins > 0)
        {
            targetPlayer.FishCoins -= trade.RequestedFishCoins;
            initiatorPlayer.FishCoins += trade.RequestedFishCoins;
            await playerRepository.UpdatePlayerAsync(targetPlayer);
            await playerRepository.UpdatePlayerAsync(initiatorPlayer);
        }

        // Update trade status
        trade.Status = TradeStatus.Completed;
        await tradeRepository.UpdateTradeAsync(trade);

        var embed = new Embed
        {
            Title = "✅ Trade Completed!",
            Description = $"Trade with {initiatorPlayer.Username} completed successfully!\n\n" +
                         $"**You received:** {string.Join(", ", trade.OfferedItems.Select(i => $"{i.Quantity}x {i.Name}"))}\n" +
                         $"**You paid:** {trade.RequestedFishCoins} fish coins",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("reject")]
    [Description("Reject a trade offer")]
    public async Task<IResult> RejectTradeAsync([Description("Trade ID")] string tradeId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var trade = await tradeRepository.GetTradeAsync(tradeId);
        if (trade == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Trade not found!", Color.Red);
        }

        if (trade.TargetUserId != user.ID.Value)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This trade is not for you!", Color.Red);
        }

        if (trade.Status != TradeStatus.Pending)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This trade is no longer active!", Color.Red);
        }

        trade.Status = TradeStatus.Rejected;
        await tradeRepository.UpdateTradeAsync(trade);

        return await feedbackService.SendContextualContentAsync("❌ Trade offer rejected.", Color.Orange);
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