using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Helpers;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
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
    private readonly OrderMatchingService _orderMatchingService = new(tradeRepository, playerRepository, inventoryRepository);
    [Command("browse")]
    [Description("Browse the global market")]
    public async Task<IResult> BrowseMarketAsync([Description("Filter by item type")] string? itemType = null)
    {
        var listings = await tradeRepository.GetMarketListingsAsync();

        if (!string.IsNullOrEmpty(itemType))
        {
            listings = listings.Where(l => l.Item.ItemType.ToLower() == itemType.ToLower()).ToList();
        }

        // Show active order book summary
        var description = "**📊 Active Order Books:**\n\n";
        
        // Get all active orders and group by item
        var allBuyOrders = new List<MarketOrder>();
        var allSellOrders = new List<MarketOrder>();
        
        // For demo purposes, let's check for common items
        var commonItems = new[] { "fish", "salmon", "bait", "rod", "seeds" };
        var hasAnyOrders = false;

        foreach (var item in commonItems)
        {
            var itemId = item.ToLower().Replace(" ", "_");
            var buyOrders = await tradeRepository.GetOrderBookAsync(itemId, OrderType.Buy);
            var sellOrders = await tradeRepository.GetOrderBookAsync(itemId, OrderType.Sell);
            
            var activeBuys = buyOrders.Where(o => o.RemainingQuantity > 0).ToList();
            var activeSells = sellOrders.Where(o => o.RemainingQuantity > 0).ToList();

            if (activeBuys.Any() || activeSells.Any())
            {
                hasAnyOrders = true;
                var stats = await tradeRepository.GetMarketStatisticsAsync(itemId);
                
                description += $"**{item.ToUpper()}**\n";
                
                if (stats != null)
                {
                    if (stats.LastPrice.HasValue)
                        description += $"  📊 Last: {stats.LastPrice} 🪙";
                    if (stats.HighestBid.HasValue)
                        description += $" | 💚 Bid: {stats.HighestBid} 🪙";
                    if (stats.LowestAsk.HasValue)
                        description += $" | ❤️ Ask: {stats.LowestAsk} 🪙";
                    description += "\n";
                    description += $"  📦 24h Volume: {stats.Volume24h}\n";
                }
                
                description += $"  🟢 {activeBuys.Count} buy orders | 🔴 {activeSells.Count} sell orders\n\n";
            }
        }

        if (!hasAnyOrders && !listings.Any())
        {
            description = "🏪 The market is empty! No items for sale and no active orders.\n\n" +
                         "Use `/market order` to place buy/sell orders, or `/market sell` for legacy listings.";
        }
        else if (!hasAnyOrders)
        {
            description += "📭 No active order books.\n" +
                          "Use `/market order` to place the first buy/sell orders!";
        }

        var embed = new Embed
        {
            Title = "🏪 Market Overview",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /market book <item> for detailed order book | /market order to trade"),
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

    [Command("order")]
    [Description("Place a limit or market order")]
    public async Task<IResult> PlaceOrderAsync(
        [Description("Buy or Sell")] [Autocomplete] OrderType orderType,
        [Description("Item name")] string itemName,
        [Description("Quantity")] int quantity = 1,
        [Description("Price per unit (0 for market order)")] int price = 0)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (quantity <= 0)
        {
            return await feedbackService.SendContextualContentAsync("❌ Quantity must be greater than 0!", Color.Red);
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🎒 You don't have an inventory yet!", Color.Red);
        }

        InventoryItem? item = null;
        string itemId = "";
        string itemType = "";
        Dictionary<string, object> properties = new();

        if (orderType == OrderType.Sell)
        {
            // Find the item in seller's inventory
            item = inventory.Items.FirstOrDefault(i => i.Name.ToLower().Contains(itemName.ToLower()));
            if (item == null)
            {
                return await feedbackService.SendContextualContentAsync($"🚫 You don't have any '{itemName}' in your inventory!", Color.Red);
            }

            if (item.Quantity < quantity)
            {
                return await feedbackService.SendContextualContentAsync($"🚫 You only have {item.Quantity} {item.Name}, but you're trying to sell {quantity}!", Color.Red);
            }

            itemId = item.ItemId;
            itemType = item.ItemType;
            itemName = item.Name;
            properties = item.Properties;
        }
        else
        {
            // For buy orders, we need to know what item they want to buy
            // For now, we'll use a simple item lookup. In a real system, you'd have an item catalog
            itemId = itemName.ToLower().Replace(" ", "_");
            itemType = "Unknown"; // This would come from item catalog
        }

        var orderKind = price > 0 ? OrderKind.Limit : OrderKind.Market;

        if (orderKind == OrderKind.Market && price > 0)
        {
            return await feedbackService.SendContextualContentAsync("❌ Market orders should have price = 0!", Color.Red);
        }

        if (orderKind == OrderKind.Limit && price <= 0)
        {
            return await feedbackService.SendContextualContentAsync("❌ Limit orders must have a price > 0!", Color.Red);
        }

        var order = new MarketOrder
        {
            UserId = user.ID.Value,
            Username = user.Username,
            ItemId = itemId,
            ItemType = itemType,
            ItemName = itemName,
            OrderType = orderType,
            OrderKind = orderKind,
            Price = price,
            Quantity = quantity,
            Properties = properties
        };

        // Validate the order
        if (!await _orderMatchingService.ValidateOrderAsync(order))
        {
            if (orderType == OrderType.Buy)
            {
                return await feedbackService.SendContextualContentAsync($"💰 You don't have enough Fish Coins! You need {price * quantity} 🪙 but only have {player.FishCoins} 🪙.", Color.Red);
            }
            else
            {
                return await feedbackService.SendContextualContentAsync($"🚫 You don't have enough {itemName} to sell!", Color.Red);
            }
        }

        // Reserve assets
        await _orderMatchingService.ReserveAssetsAsync(order);

        // Process the order
        var executions = await _orderMatchingService.ProcessOrderAsync(order);

        var description = "";
        if (executions.Any())
        {
            var totalQuantity = executions.Sum(e => e.Quantity);
            var avgPrice = executions.Sum(e => e.Price * e.Quantity) / totalQuantity;
            description += $"✅ {orderType} order executed!\n";
            description += $"📦 Traded: {totalQuantity}x {itemName}\n";
            description += $"💰 Average price: {avgPrice} 🪙 per unit\n";
            description += $"💸 Total value: {executions.Sum(e => e.Price * e.Quantity)} 🪙\n\n";
        }

        if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.PartiallyFilled)
        {
            description += $"📋 Order placed: {orderKind} {orderType}\n";
            description += $"📦 Quantity: {order.RemainingQuantity}/{order.Quantity}\n";
            if (orderKind == OrderKind.Limit)
                description += $"💰 Price: {order.Price} 🪙 per unit\n";
            description += $"🆔 Order ID: {order.OrderId[..8]}...\n";
        }

        return await feedbackService.SendContextualContentAsync(description, Color.Green);
    }

    [Command("book")]
    [Description("View order book for an item")]
    public async Task<IResult> ViewOrderBookAsync([Description("Item name")] string itemName)
    {
        // For simplicity, we'll use itemName as itemId (lowercased and underscored)
        var itemId = itemName.ToLower().Replace(" ", "_");
        
        var buyOrders = await tradeRepository.GetOrderBookAsync(itemId, OrderType.Buy);
        var sellOrders = await tradeRepository.GetOrderBookAsync(itemId, OrderType.Sell);
        
        // Filter to only limit orders and sort appropriately
        var activeBuyOrders = buyOrders.Where(o => o.OrderKind == OrderKind.Limit && o.RemainingQuantity > 0)
                                      .OrderByDescending(o => o.Price)
                                      .Take(5)
                                      .ToList();
        
        var activeSellOrders = sellOrders.Where(o => o.OrderKind == OrderKind.Limit && o.RemainingQuantity > 0)
                                         .OrderBy(o => o.Price)
                                         .Take(5)
                                         .ToList();

        var description = $"**📊 Order Book: {itemName}**\n\n";

        if (activeSellOrders.Any())
        {
            description += "**🔴 Sell Orders (Asks):**\n";
            foreach (var order in activeSellOrders)
            {
                description += $"💰 {order.Price} 🪙 × {order.RemainingQuantity}\n";
            }
            description += "\n";
        }

        var stats = await tradeRepository.GetMarketStatisticsAsync(itemId);
        if (stats != null)
        {
            description += "**📈 Market Info:**\n";
            if (stats.LastPrice.HasValue)
                description += $"📊 Last Price: {stats.LastPrice} 🪙\n";
            if (stats.HighestBid.HasValue)
                description += $"💚 Best Bid: {stats.HighestBid} 🪙\n";
            if (stats.LowestAsk.HasValue)
                description += $"❤️ Best Ask: {stats.LowestAsk} 🪙\n";
            description += $"📦 24h Volume: {stats.Volume24h}\n\n";
        }

        if (activeBuyOrders.Any())
        {
            description += "**🟢 Buy Orders (Bids):**\n";
            foreach (var order in activeBuyOrders)
            {
                description += $"💰 {order.Price} 🪙 × {order.RemainingQuantity}\n";
            }
        }

        if (!activeBuyOrders.Any() && !activeSellOrders.Any())
        {
            description += "📭 No active orders in the book.\n";
        }

        var embed = new Embed
        {
            Title = $"📊 Order Book",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /market order to place orders"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("orders")]
    [Description("View your active orders")]
    public async Task<IResult> ViewMyOrdersAsync([Description("Show only specific status")] string? status = null)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        OrderStatus? filterStatus = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            {
                return await feedbackService.SendContextualContentAsync("❌ Invalid status! Use: Pending, PartiallyFilled, Filled, Cancelled", Color.Red);
            }
            filterStatus = parsedStatus;
        }

        var orders = await tradeRepository.GetUserOrdersAsync(user.ID.Value, filterStatus);

        if (!orders.Any())
        {
            return await feedbackService.SendContextualContentAsync("📭 You have no orders.", Color.Yellow);
        }

        var description = "**📋 Your Orders:**\n\n";
        
        foreach (var order in orders.Take(10))
        {
            var statusEmoji = order.Status switch
            {
                OrderStatus.Pending => "⏳",
                OrderStatus.PartiallyFilled => "📊",
                OrderStatus.Filled => "✅",
                OrderStatus.Cancelled => "❌",
                OrderStatus.Expired => "⏰",
                _ => "❓"
            };

            var typeEmoji = order.OrderType == OrderType.Buy ? "🟢" : "🔴";
            var kindEmoji = order.OrderKind == OrderKind.Market ? "⚡" : "📌";

            description += $"{statusEmoji} {typeEmoji} {kindEmoji} **{order.ItemName}**\n";
            description += $"   📦 {order.FilledQuantity}/{order.Quantity}";
            if (order.OrderKind == OrderKind.Limit)
                description += $" @ {order.Price} 🪙";
            description += $"\n   🆔 {order.OrderId[..8]}...\n\n";
        }

        if (orders.Count > 10)
        {
            description += $"... and {orders.Count - 10} more orders";
        }

        var embed = new Embed
        {
            Title = "📋 Your Orders",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use /market cancel <orderId> to cancel orders"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("cancel")]
    [Description("Cancel an order")]
    public async Task<IResult> CancelOrderAsync([Description("Order ID")] string orderId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var order = await tradeRepository.GetOrderAsync(orderId);
        if (order == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ Order not found!", Color.Red);
        }

        if (order.UserId != user.ID.Value)
        {
            return await feedbackService.SendContextualContentAsync("❌ You can only cancel your own orders!", Color.Red);
        }

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.PartiallyFilled)
        {
            return await feedbackService.SendContextualContentAsync($"❌ Cannot cancel order with status: {order.Status}", Color.Red);
        }

        // Cancel the order
        await tradeRepository.CancelOrderAsync(orderId);

        // Release reserved assets
        await _orderMatchingService.ReleaseReservedAssetsAsync(order);

        return await feedbackService.SendContextualContentAsync(
            $"✅ Order cancelled: {order.OrderType} {order.RemainingQuantity}x {order.ItemName}\n" +
            $"💰 Assets returned to your account.",
            Color.Green);
    }

    [Command("trades")]
    [Description("View recent trades for an item")]
    public async Task<IResult> ViewTradeHistoryAsync(
        [Description("Item name")] string itemName,
        [Description("Hours to look back")] int hours = 24)
    {
        if (hours < 1 || hours > 168) // Max 1 week
        {
            return await feedbackService.SendContextualContentAsync("❌ Hours must be between 1 and 168 (1 week)!", Color.Red);
        }

        var itemId = itemName.ToLower().Replace(" ", "_");
        var trades = await tradeRepository.GetTradeHistoryAsync(itemId, hours);

        if (!trades.Any())
        {
            return await feedbackService.SendContextualContentAsync($"📭 No trades found for {itemName} in the last {hours} hours.", Color.Yellow);
        }

        var description = $"**📈 Recent Trades: {itemName}** (Last {hours}h)\n\n";

        foreach (var trade in trades.Take(10))
        {
            var timeAgo = DateTime.UtcNow - trade.ExecutedAt;
            var timeStr = timeAgo.TotalHours < 1 
                ? $"{(int)timeAgo.TotalMinutes}m ago"
                : $"{(int)timeAgo.TotalHours}h ago";

            description += $"💰 **{trade.Price} 🪙** × {trade.Quantity} - {timeStr}\n";
        }

        if (trades.Count > 10)
        {
            description += $"\n... and {trades.Count - 10} more trades";
        }

        // Add summary statistics
        var totalVolume = trades.Sum(t => t.Quantity);
        var avgPrice = trades.Sum(t => t.Price * t.Quantity) / totalVolume;
        var minPrice = trades.Min(t => t.Price);
        var maxPrice = trades.Max(t => t.Price);

        description += "\n\n**📊 Summary:**\n";
        description += $"📦 Volume: {totalVolume}\n";
        description += $"💰 Avg Price: {avgPrice:F1} 🪙\n";
        description += $"📈 High: {maxPrice} 🪙 | 📉 Low: {minPrice} 🪙\n";

        var embed = new Embed
        {
            Title = "📈 Trade History",
            Description = description,
            Colour = Color.Blue,
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
