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

[Group("fishing")]
[Description("Fishing-related commands")]
public class FishingModule : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly ICommandContext _context;
    private readonly IPlayerRepository _playerRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IAreaRepository _areaRepository;

    public FishingModule(IDiscordRestChannelAPI channelAPI, ICommandContext context,
        IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
        IAreaRepository areaRepository)
    {
        _channelAPI = channelAPI;
        _context = context;
        _playerRepository = playerRepository;
        _inventoryRepository = inventoryRepository;
        _areaRepository = areaRepository;
    }

    [Command("cast")]
    [Description("Cast your fishing line")]
    public async Task<IResult> CastLineAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        
        var currentArea = await _areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await RespondAsync("🚫 Current area not found! Try using `/map` to navigate.");
        }

        if (currentArea.FishingSpots.Count == 0)
        {
            return await RespondAsync("🚫 No fishing spots available in this area!");
        }

        var fishingSpot = currentArea.FishingSpots.First();
        var availableFish = fishingSpot.AvailableFish;

        if (availableFish.Count == 0)
        {
            return await RespondAsync("🚫 No fish available in this spot!");
        }

        // Simple fishing simulation
        var random = new Random();
        var success = random.NextDouble() > 0.3; // 70% success rate

        if (success)
        {
            var caughtFish = availableFish[random.Next(availableFish.Count)];
            var fishItem = new InventoryItem
            {
                ItemId = caughtFish,
                ItemType = "Fish",
                Name = FormatFishName(caughtFish),
                Quantity = 1,
                Properties = new() { ["size"] = random.Next(10, 50), ["rarity"] = "common" }
            };

            await _inventoryRepository.AddItemAsync(userId, fishItem);
            
            player.Experience += 10;
            player.LastActive = DateTime.UtcNow;
            await _playerRepository.UpdatePlayerAsync(player);

            return await RespondAsync($"🎣 **Success!** You caught a {fishItem.Name}! " +
                                    $"Size: {fishItem.Properties["size"]}cm (+10 XP)");
        }
        else
        {
            return await RespondAsync("🎣 Your line comes up empty... Try again!");
        }
    }

    [Command("profile")]
    [Description("View your fishing profile")]
    public async Task<IResult> ViewProfileAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        var fishCount = inventory?.Items.Count(i => i.ItemType == "Fish") ?? 0;
        var totalFish = inventory?.Items.Where(i => i.ItemType == "Fish").Sum(i => i.Quantity) ?? 0;

        var embed = new Embed
        {
            Title = $"🎣 {player.Username}'s Fishing Profile",
            Colour = Color.Blue,
            Fields = new List<EmbedField>
            {
                new("Level", player.Level.ToString(), true),
                new("Experience", player.Experience.ToString(), true),
                new("Fish Coins", $"{player.FishCoins} 🪙", true),
                new("Current Area", player.CurrentArea.Replace("_", " "), true),
                new("Fish Species Caught", fishCount.ToString(), true),
                new("Total Fish Caught", totalFish.ToString(), true),
                new("Joined", player.CreatedAt.ToString("yyyy-MM-dd"), true)
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

    private static string FormatFishName(string fishId)
    {
        return fishId.Replace("_", " ").ToTitleCase();
    }

    private async Task<IResult> RespondAsync(string content = "", IReadOnlyList<Embed>? embeds = null)
    {
        var embedsParam = embeds != null ? new Optional<IReadOnlyList<IEmbed>>(embeds.Cast<IEmbed>().ToList()) : default;
        await _channelAPI.CreateMessageAsync(_context.ChannelID, content, embeds: embedsParam);
        return Result.FromSuccess();
    }
}

public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..].ToLower();
            }
        }
        return string.Join(' ', words);
    }
}