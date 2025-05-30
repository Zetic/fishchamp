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
using FishChamp.Services;
using Newtonsoft.Json;

namespace FishChamp.Modules;

[Group("shop")]
[Description("Shop commands for buying and selling")]
public class ShopModule : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly ICommandContext _context;
    private readonly IPlayerRepository _playerRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IAreaRepository _areaRepository;

    public ShopModule(IDiscordRestChannelAPI channelAPI, ICommandContext context,
        IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
        IAreaRepository areaRepository)
    {
        _channelAPI = channelAPI;
        _context = context;
        _playerRepository = playerRepository;
        _inventoryRepository = inventoryRepository;
        _areaRepository = areaRepository;
    }

    [Command("browse")]
    [Description("Browse items available in the current area's shop")]
    public async Task<IResult> BrowseShopAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var shopData = await GetShopDataAsync(player.CurrentArea);

        if (shopData == null)
        {
            return await RespondAsync("🚫 No shop available in this area!");
        }

        var itemsText = string.Join("\n", shopData.Items.Select(item =>
            $"• **{item.Name}** - {item.Price} 🪙 (Stock: {item.Stock})"));

        var embed = new Embed
        {
            Title = $"🏪 {shopData.Name}",
            Description = itemsText,
            Colour = Color.Purple,
            Footer = new EmbedFooter("Use `/shop buy <item>` to purchase items"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await RespondAsync(embeds: new[] { embed });
    }

    [Command("buy")]
    [Description("Buy an item from the shop")]
    public async Task<IResult> BuyItemAsync([Description("Item name to buy")] string itemName, 
                                          [Description("Quantity to buy")] int quantity = 1)
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var shopData = await GetShopDataAsync(player.CurrentArea);

        if (shopData == null)
        {
            return await RespondAsync("🚫 No shop available in this area!");
        }

        var shopItem = shopData.Items.FirstOrDefault(i => 
            i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        if (shopItem == null)
        {
            return await RespondAsync($"🚫 Item '{itemName}' not found in shop!");
        }

        if (quantity <= 0)
        {
            return await RespondAsync("🚫 Quantity must be greater than 0!");
        }

        if (shopItem.Stock < quantity)
        {
            return await RespondAsync($"🚫 Not enough stock! Available: {shopItem.Stock}");
        }

        var totalCost = shopItem.Price * quantity;
        if (player.FishCoins < totalCost)
        {
            return await RespondAsync($"🚫 Not enough coins! You need {totalCost} 🪙 but only have {player.FishCoins} 🪙");
        }

        // Process purchase
        player.FishCoins -= totalCost;
        player.LastActive = DateTime.UtcNow;
        await _playerRepository.UpdatePlayerAsync(player);

        var inventoryItem = new InventoryItem
        {
            ItemId = shopItem.ItemId,
            ItemType = shopItem.Type,
            Name = shopItem.Name,
            Quantity = quantity,
            Properties = shopItem.Properties.ToDictionary(p => p.Key, p => p.Value)
        };

        await _inventoryRepository.AddItemAsync(userId, inventoryItem);

        return await RespondAsync($"✅ Purchased **{quantity}x {shopItem.Name}** for {totalCost} 🪙!");
    }

    [Command("sell")]
    [Description("Sell fish for coins")]
    public async Task<IResult> SellFishAsync([Description("Fish name to sell")] string fishName, 
                                           [Description("Quantity to sell")] int quantity = 1)
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        if (inventory == null)
        {
            return await RespondAsync("🚫 You don't have an inventory!");
        }

        var fish = inventory.Items.FirstOrDefault(i => 
            i.ItemType == "Fish" && 
            i.Name.Equals(fishName, StringComparison.OrdinalIgnoreCase));

        if (fish == null)
        {
            return await RespondAsync($"🚫 You don't have any '{fishName}'!");
        }

        if (quantity <= 0)
        {
            return await RespondAsync("🚫 Quantity must be greater than 0!");
        }

        if (fish.Quantity < quantity)
        {
            return await RespondAsync($"🚫 You only have {fish.Quantity} {fish.Name}!");
        }

        // Calculate sell value
        var baseValue = fish.Properties.ContainsKey("value") ? Convert.ToInt32(fish.Properties["value"]) : 1;
        var size = fish.Properties.ContainsKey("size") ? Convert.ToInt32(fish.Properties["size"]) : 20;
        var sizeMultiplier = Math.Max(0.5, size / 30.0); // Bigger fish worth more
        var totalValue = (int)(baseValue * sizeMultiplier * quantity);

        // Remove fish from inventory
        await _inventoryRepository.RemoveItemAsync(userId, fish.ItemId, quantity);

        // Add coins to player
        player.FishCoins += totalValue;
        player.LastActive = DateTime.UtcNow;
        await _playerRepository.UpdatePlayerAsync(player);

        return await RespondAsync($"💰 Sold **{quantity}x {fish.Name}** for {totalValue} 🪙!");
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await _playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await _playerRepository.CreatePlayerAsync(userId, username);
            await _inventoryRepository.CreateInventoryAsync(userId);
        }
        return player;
    }

    private async Task<ShopData?> GetShopDataAsync(string areaId)
    {
        var shopDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "ShopData.json");
        if (!File.Exists(shopDataPath))
            return null;

        var json = await File.ReadAllTextAsync(shopDataPath);
        var allShops = JsonConvert.DeserializeObject<Dictionary<string, ShopData>>(json);
        
        return allShops?.TryGetValue(areaId, out var shopData) == true ? shopData : null;
    }

    private async Task<IResult> RespondAsync(string content = "", IReadOnlyList<Embed>? embeds = null)
    {
        var embedsParam = embeds != null ? new Optional<IReadOnlyList<IEmbed>>(embeds.Cast<IEmbed>().ToList()) : default;
        await _channelAPI.CreateMessageAsync(_context.ChannelID, content, embeds: embedsParam);
        return Result.FromSuccess();
    }
}

public class ShopData
{
    public string Name { get; set; } = string.Empty;
    public List<ShopItem> Items { get; set; } = new();
}

public class ShopItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Price { get; set; } = 0;
    public int Stock { get; set; } = 0;
    public Dictionary<string, object> Properties { get; set; } = new();
}