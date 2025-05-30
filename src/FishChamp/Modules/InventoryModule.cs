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

namespace FishChamp.Modules;

[Group("inventory")]
[Description("Inventory management commands")]
public class InventoryModule : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly ICommandContext _context;
    private readonly IPlayerRepository _playerRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryModule(IDiscordRestChannelAPI channelAPI, ICommandContext context,
        IPlayerRepository playerRepository, IInventoryRepository inventoryRepository)
    {
        _channelAPI = channelAPI;
        _context = context;
        _playerRepository = playerRepository;
        _inventoryRepository = inventoryRepository;
    }

    [Command("view")]
    [Description("View your inventory")]
    public async Task<IResult> ViewInventoryAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        if (inventory == null || inventory.Items.Count == 0)
        {
            return await RespondAsync("🎒 Your inventory is empty! Try fishing to get some items.");
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

        return await RespondAsync(embeds: new[] { embed });
    }

    [Command("fish")]
    [Description("View only your fish collection")]
    public async Task<IResult> ViewFishAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        if (inventory == null)
        {
            return await RespondAsync("🐟 You haven't caught any fish yet! Use `/fishing cast` to start fishing.");
        }

        var fish = inventory.Items.Where(i => i.ItemType == "Fish").ToList();
        if (fish.Count == 0)
        {
            return await RespondAsync("🐟 You haven't caught any fish yet! Use `/fishing cast` to start fishing.");
        }

        var fishText = string.Join("\n", fish.Select(f =>
        {
            var size = f.Properties.ContainsKey("size") ? $" ({f.Properties["size"]}cm)" : "";
            var rarity = f.Properties.ContainsKey("rarity") ? GetRarityEmoji(f.Properties["rarity"].ToString()) : "";
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

        return await RespondAsync(embeds: new[] { embed });
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

    private async Task<IResult> RespondAsync(string content = "", IReadOnlyList<Embed>? embeds = null)
    {
        var embedsParam = embeds != null ? new Optional<IReadOnlyList<IEmbed>>(embeds.Cast<IEmbed>().ToList()) : default;
        await _channelAPI.CreateMessageAsync(_context.ChannelID, content, embeds: embedsParam);
        return Result.FromSuccess();
    }
}