using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Helpers;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Features.Trading;

[Group("market")]
[Description("market commands")]
public class MarketCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    ITradeRepository tradeRepository, DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("browse")]
    [Description("Browse the global market")]
    public async Task<IResult> BrowseMarketAsync([Description("Filter by item type")] string? itemType = null)
    {
        var listings = await tradeRepository.GetMarketListingsAsync();

        if (!string.IsNullOrEmpty(itemType))
        {
            listings = listings.Where(l => l.Item.ItemType.ToLower() == itemType.ToLower()).ToList();
        }

        if (!listings.Any())
        {
            return await feedbackService.SendContextualContentAsync("🏪 No items are currently listed on the market!", Color.Yellow);
        }

        var description = "**Available Items:**\n\n";
        foreach (var listing in listings.Take(10))
        {
            description += $"• **{listing.Item.Name}** ({listing.Item.Quantity}x) - {listing.Price} coins\n" +
                          $"  Seller: {listing.SellerUsername}\n" +
                          $"  ID: {listing.ListingId}\n\n";
        }

        if (listings.Count > 10)
        {
            description += $"... and {listings.Count - 10} more items";
        }

        var embed = new Embed
        {
            Title = "🏪 Market",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /market buy <itemNumber> to purchase items"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("buy")]
    [Description("Buy an item from the player market")]
    public async Task<IResult> BuyFromMarketAsync([Description("Item number from market list")] int itemNumber)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var marketListings = await tradeRepository.GetMarketListingsAsync();

        if (marketListings.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🏪 The market is empty! No items for sale right now.", Color.Red);
        }

        if (itemNumber < 1 || itemNumber > marketListings.Count)
        {
            return await feedbackService.SendContextualContentAsync($"❌ Invalid item number! Please choose between 1 and {marketListings.Count}.", Color.Red);
        }

        var listing = marketListings[itemNumber - 1];

        // Check if buyer has enough coins
        if (player.FishCoins < listing.Price)
        {
            return await feedbackService.SendContextualContentAsync($"💰 You don't have enough Fish Coins! You need {listing.Price} 🪙 but only have {player.FishCoins} 🪙.", Color.Red);
        }

        // Check if buyer is trying to buy their own item
        if (listing.SellerId == user.ID.Value)
        {
            return await feedbackService.SendContextualContentAsync("❌ You cannot buy your own items!", Color.Red);
        }

        // Process the purchase
        player.FishCoins -= listing.Price;
        await playerRepository.UpdatePlayerAsync(player);

        // Add item to buyer's inventory
        var inventoryItem = new InventoryItem
        {
            ItemId = listing.Item.ItemId,
            ItemType = listing.Item.ItemType,
            Name = listing.Item.Name,
            Quantity = listing.Item.Quantity,
            Properties = listing.Item.Properties,
            AcquiredAt = DateTime.UtcNow
        };
        await inventoryRepository.AddItemAsync(user.ID.Value, inventoryItem);

        // Pay the seller
        var seller = await playerRepository.GetPlayerAsync(listing.SellerId);
        if (seller != null)
        {
            seller.FishCoins += listing.Price;
            await playerRepository.UpdatePlayerAsync(seller);
        }

        // Remove the listing
        await tradeRepository.DeleteMarketListingAsync(listing.ListingId);

        var rarity = listing.Item.Properties.ContainsKey("rarity") ? listing.Item.Properties["rarity"].ToString() : "common";
        var rarityEmoji = GetRarityEmoji(rarity);

        return await feedbackService.SendContextualContentAsync(
            $"✅ Successfully purchased {rarityEmoji} **{listing.Item.Name}** x{listing.Item.Quantity} for {listing.Price} 🪙!\n" +
            $"New balance: {player.FishCoins} 🪙",
            Color.Green);
    }

    [Command("sell")]
    [Description("Sell an item to the player market")]
    public async Task<IResult> SellFromMarketAsync(
        [Description("Fish/item name to offer")] string itemName,
        [Description("Quantity to offer")] int quantity = 1,
        [Description("Fish coins to request")] int requestedCoins = 0)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

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

        var sellerPlayer = await playerRepository.GetPlayerAsync(user.ID.Value);

        var listing = new MarketListing()
        {
            ListingId = Guid.NewGuid().ToString(),
            SellerId = user.ID.Value,
            SellerUsername = Mention.User(user),
            Item = new TradeItem
            {
                ItemId = item.ItemId,
                ItemType = item.ItemType,
                Name = item.Name,
                Quantity = quantity,
                Properties = item.Properties
            },
            Price = requestedCoins
        };

        await tradeRepository.CreateMarketListingAsync(listing);

        return await feedbackService.SendContextualContentAsync(
            $"✅ Successfully listed an item **{listing.Item.Name}** x{listing.Item.Quantity} for {listing.Price} 🪙!\n",
            Color.Green);
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
