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
using System.Text.Json;
using Remora.Discord.Commands.Attributes;

namespace FishChamp.Features.Shops;

[Group("shop")]
[Description("Shop and trading commands")]
public class ShopCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository, IBoatRepository boatRepository, ITradeRepository tradeRepository, DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("browse")]
    [Description("Browse the shop in your current area")]
    public async Task<IResult> BrowseShopAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        if (currentArea.Shops == null || currentArea.Shops.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🏪 There are no shops in this area!", Color.Red);
        }

        var shopListText = string.Join("\n", currentArea.Shops.Values.Select(shop =>
            $"**{shop.Name}** - {shop.Items.Count} items available"));

        var embed = new Embed
        {
            Title = $"🏪 Shops in {currentArea.Name}",
            Description = shopListText,
            Colour = Color.Gold,
            Footer = new EmbedFooter("Use /shop view <shop name> to see what's for sale"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("view")]
    [Description("View items in a specific shop")]
    public async Task<IResult> ViewShopAsync([Description("Shop name")][AutocompleteProvider("autocomplete::shop")] string shopName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        if (currentArea.Shops == null || currentArea.Shops.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🏪 There are no shops in this area!", Color.Red);
        }

        var shop = currentArea.Shops.Values.FirstOrDefault(s => s.ShopId.Equals(shopName, StringComparison.OrdinalIgnoreCase));

        if (shop == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏪 Shop '{shopName}' not found in this area!", Color.Red);
        }

        var itemsText = string.Join("\n", shop.Items
            .Where(i => i.InStock)
            .Select(item => $"• **{item.Name}** - {item.Price} 🪙\n   *{item.Description}*"));

        if (string.IsNullOrEmpty(itemsText))
        {
            itemsText = "No items currently in stock.";
        }

        var embed = new Embed
        {
            Title = $"🏪 {shop.Name}",
            Description = itemsText,
            Colour = Color.Gold,
            Fields = new List<EmbedField>
            {
                new("Your Balance", $"{player.FishCoins} 🪙", true),
            },
            Footer = new EmbedFooter("Use /shop buy <item name> to purchase an item"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("buy")]
    [Description("Buy an item from the shop")]
    public async Task<IResult> BuyItemAsync(
        [Description("Shop name")][AutocompleteProvider(ShopAutocompleteProvider.ID)] string shopName,
        [Description("Item to buy")][AutocompleteProvider(ShopItemsAutocompleteProvider.ID)] string itemName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🚫 Current area not found!");
        }

        if (currentArea.Shops == null || currentArea.Shops.Count == 0)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🏪 There are no shops in this area!");
        }

        var shop = currentArea.Shops.Values.FirstOrDefault(s => s.ShopId.Equals(shopName, StringComparison.OrdinalIgnoreCase));

        if (shop == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🏪 Shop '{shopName}' not found in this area!");
        }

        var item = shop.Items.FirstOrDefault(i =>
            i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
            i.ItemId.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        if (item == null || !item.InStock)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🏪 Item '{itemName}' not available in this shop!");
        }

        if (player.FishCoins < item.Price)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🪙 You don't have enough fish coins! (Need {item.Price}, have {player.FishCoins})");
        }

        // Special handling for boats
        if (item.ItemType == "Boat")
        {
            // Get boat type from properties
            var boatType = item.Properties.GetValueOrDefault("boat_type", "skiff").ToString();
            if (string.IsNullOrEmpty(boatType))
            {
                return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🚫 Invalid boat type!");
            }

            // Create boat
            var boat = new Boat
            {
                UserId = user.ID.Value,
                BoatType = boatType,
                Name = item.Name,
                Durability = BoatTypes.BoatData.GetValueOrDefault(boatType)?.MaxDurability ?? 100,
                MaxDurability = BoatTypes.BoatData.GetValueOrDefault(boatType)?.MaxDurability ?? 100,
                StorageCapacity = BoatTypes.BoatData.GetValueOrDefault(boatType)?.StorageCapacity ?? 5
            };

            await boatRepository.CreateBoatAsync(boat);

            // Subtract the cost
            player.FishCoins -= item.Price;
            await playerRepository.UpdatePlayerAsync(player);

            return await feedbackService.SendContextualContentAsync(
                $"⛵ You purchased **{item.Name}** for {item.Price} 🪙\n" +
                $"Use `/boat equip` to start using it for water fishing!\n" +
                $"New balance: {player.FishCoins} 🪙",
                Color.Green);
        }

        // Regular item handling
        var inventoryItem = new InventoryItem
        {
            ItemId = item.ItemId,
            ItemType = item.ItemType,
            Name = item.Name,
            Quantity = 1,
            Properties = item.Properties
        };

        // Subtract the cost and add the item to inventory
        player.FishCoins -= item.Price;
        await playerRepository.UpdatePlayerAsync(player);
        await inventoryRepository.AddItemAsync(user.ID.Value, inventoryItem);

        return await feedbackService.SendContextualContentAsync(
            $"🛒 You purchased **{item.Name}** for {item.Price} 🪙\n" +
            $"New balance: {player.FishCoins} 🪙",
            Color.Green);
    }

    [Command("sell")]
    [Description("Sell a fish from your inventory")]
    public async Task<IResult> SellItemAsync(
        [Description("Fish name or ID to sell")] string fishName,
        [Description("Quantity to sell (default: 1)")] int quantity = 1)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (quantity <= 0)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🚫 Quantity must be positive!");
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🐟 You don't have any items to sell!");
        }

        // Find the fish in inventory (by ID or name)
        var fish = inventory.Items.FirstOrDefault(i =>
            i.ItemType == "Fish" &&
            (i.ItemId.Equals(fishName, StringComparison.OrdinalIgnoreCase) ||
             i.Name.Equals(fishName, StringComparison.OrdinalIgnoreCase)));

        if (fish == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🐟 Fish '{fishName}' not found in your inventory!");
        }

        if (fish.Quantity < quantity)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🐟 You only have {fish.Quantity} {fish.Name}!");
        }

        // Calculate sale price based on fish rarity and size
        string rarity = fish.Properties.GetString("rarity", "common");
        int fishSize = fish.Properties.GetInt("size", 10);

        int basePrice = rarity switch
        {
            "common" => 5,
            "uncommon" => 15,
            "rare" => 30,
            "epic" => 50,
            "legendary" => 100,
            _ => 5
        };

        // Bonus for larger fish
        int sizeBonus = (fishSize - 10) / 5; // Every 5cm over 10cm gives a bonus
        int totalPrice = (basePrice + sizeBonus) * quantity;

        // Remove the fish from inventory and add coins
        await inventoryRepository.RemoveItemAsync(user.ID.Value, fish.ItemId, quantity);
        player.FishCoins += totalPrice;
        await playerRepository.UpdatePlayerAsync(player);

        string rarityEmoji = rarity switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵",
            "epic" => "🟣",
            "legendary" => "🟡",
            _ => "⚪"
        };

        return await feedbackService.SendContextualContentAsync(
            $"💰 Sold {quantity}x {rarityEmoji} **{fish.Name}** ({fishSize}cm) for {totalPrice} 🪙\n" +
            $"New balance: {player.FishCoins} 🪙",
            Color.Green);
    }

    [Command("market-list")]
    [Description("View available items on the player market")]
    public async Task<IResult> ViewMarketAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var marketListings = await tradeRepository.GetMarketListingsAsync();
        
        if (marketListings.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🏪 The market is empty! No items for sale right now.", Color.Yellow);
        }

        var listingsText = string.Join("\n", marketListings.Take(10).Select((listing, index) =>
        {
            var rarity = listing.Item.Properties.ContainsKey("rarity") ? listing.Item.Properties["rarity"].ToString() : "common";
            var rarityEmoji = GetRarityEmoji(rarity);
            var expiresIn = listing.ExpiresAt - DateTime.UtcNow;
            var expiresText = expiresIn.TotalDays > 1 ? $"{expiresIn.Days}d" : $"{expiresIn.Hours}h";
            
            return $"`{index + 1}.` {rarityEmoji} **{listing.Item.Name}** x{listing.Item.Quantity} - {listing.Price} 🪙\n" +
                   $"   *Seller: {listing.SellerUsername} | Expires: {expiresText}*";
        }));

        var embed = new Embed
        {
            Title = "🏪 Player Market",
            Description = $"**Available Items:**\n\n{listingsText}",
            Colour = Color.Purple,
            Footer = new EmbedFooter($"Use /shop market-buy <number> to purchase • Showing {Math.Min(10, marketListings.Count)} of {marketListings.Count} listings"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("market-buy")]
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

}