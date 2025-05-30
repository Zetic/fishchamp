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
    private readonly IFishDataService _fishDataService;

    public FishingModule(IDiscordRestChannelAPI channelAPI, ICommandContext context,
        IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
        IAreaRepository areaRepository, IFishDataService fishDataService)
    {
        _channelAPI = channelAPI;
        _context = context;
        _playerRepository = playerRepository;
        _inventoryRepository = inventoryRepository;
        _areaRepository = areaRepository;
        _fishDataService = fishDataService;
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

        // Improved fishing simulation with fish data
        var random = new Random();
        var allFishData = await _fishDataService.GetAllFishDataAsync();
        
        // Calculate weighted catch chances
        var weightedFish = new List<(string fishId, double weight)>();
        
        foreach (var fishId in availableFish)
        {
            var fishData = allFishData.TryGetValue(fishId, out var data) ? data : null;
            var catchChance = fishData?.CatchChance ?? 0.5;
            weightedFish.Add((fishId, catchChance));
        }

        // Overall success rate based on best available fish
        var maxCatchChance = weightedFish.Max(f => f.weight);
        var success = random.NextDouble() < maxCatchChance;

        if (success)
        {
            // Select fish based on weighted probabilities
            var totalWeight = weightedFish.Sum(f => f.weight);
            var randomValue = random.NextDouble() * totalWeight;
            var currentWeight = 0.0;
            
            string caughtFishId = availableFish.First(); // fallback
            foreach (var (fishId, weight) in weightedFish)
            {
                currentWeight += weight;
                if (randomValue <= currentWeight)
                {
                    caughtFishId = fishId;
                    break;
                }
            }

            var fishData = allFishData.TryGetValue(caughtFishId, out var data) ? data : null;
            var fishName = fishData?.Name ?? FormatFishName(caughtFishId);
            var rarity = fishData?.Rarity ?? "common";
            var size = random.Next(fishData?.MinSize ?? 10, fishData?.MaxSize ?? 50);
            
            var fishItem = new InventoryItem
            {
                ItemId = caughtFishId,
                ItemType = "Fish",
                Name = fishName,
                Quantity = 1,
                Properties = new() { ["size"] = size, ["rarity"] = rarity, ["value"] = fishData?.BaseValue ?? 1 }
            };

            await _inventoryRepository.AddItemAsync(userId, fishItem);
            
            player.Experience += GetExperienceForRarity(rarity);
            player.LastActive = DateTime.UtcNow;
            await _playerRepository.UpdatePlayerAsync(player);

            var rarityEmoji = GetRarityEmoji(rarity);
            return await RespondAsync($"🎣 **Success!** You caught a {rarityEmoji} {fishItem.Name}! " +
                                    $"Size: {size}cm (+{GetExperienceForRarity(rarity)} XP)");
        }
        else
        {
            return await RespondAsync("🎣 Your line comes up empty... Try again!");
        }
    }

    [Command("register")]
    [Description("Register as a new player")]
    public async Task<IResult> RegisterAsync()
    {
        var userId = _context.User.ID.Value;
        var existingPlayer = await _playerRepository.GetPlayerAsync(userId);
        
        if (existingPlayer != null)
        {
            return await RespondAsync($"🎣 You're already registered! Welcome back, {existingPlayer.Username}!");
        }

        var player = await _playerRepository.CreatePlayerAsync(userId, _context.User.Username);
        await _inventoryRepository.CreateInventoryAsync(userId);

        var embed = new Embed
        {
            Title = "🎉 Welcome to FishChamp!",
            Description = $"Welcome, **{player.Username}**! You've been registered as a new angler.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Starting Location", "Starter Lake", true),
                new("Starting Coins", "100 🪙", true),
                new("Starting Equipment", "Basic Fishing Rod", true)
            },
            Footer = new EmbedFooter("Use `/fishing cast` to start fishing!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await RespondAsync(embeds: new[] { embed });
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
    [Command("equip-rod")]
    [Description("Equip a fishing rod")]
    public async Task<IResult> EquipRodAsync([Description("Rod name to equip")] string rodName)
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        if (inventory == null)
        {
            return await RespondAsync("🚫 You don't have an inventory!");
        }

        var rod = inventory.Items.FirstOrDefault(i => 
            i.ItemType == "Rod" && 
            (i.Name.Equals(rodName, StringComparison.OrdinalIgnoreCase) || 
             i.ItemId.Equals(rodName.Replace(" ", "_").ToLower(), StringComparison.OrdinalIgnoreCase)));

        if (rod == null)
        {
            return await RespondAsync($"🚫 You don't have a rod called '{rodName}'!");
        }

        player.EquippedRod = rod.ItemId;
        player.LastActive = DateTime.UtcNow;
        await _playerRepository.UpdatePlayerAsync(player);

        return await RespondAsync($"🎣 Equipped **{rod.Name}**!");
    }

    [Command("equip-bait")]
    [Description("Equip bait for fishing")]
    public async Task<IResult> EquipBaitAsync([Description("Bait name to equip")] string baitName)
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        if (inventory == null)
        {
            return await RespondAsync("🚫 You don't have an inventory!");
        }

        var bait = inventory.Items.FirstOrDefault(i => 
            i.ItemType == "Bait" && 
            (i.Name.Equals(baitName, StringComparison.OrdinalIgnoreCase) || 
             i.ItemId.Equals(baitName.Replace(" ", "_").ToLower(), StringComparison.OrdinalIgnoreCase)));

        if (bait == null)
        {
            return await RespondAsync($"🚫 You don't have bait called '{baitName}'!");
        }

        if (bait.Quantity <= 0)
        {
            return await RespondAsync($"🚫 You're out of {bait.Name}!");
        }

        player.EquippedBait = bait.ItemId;
        player.LastActive = DateTime.UtcNow;
        await _playerRepository.UpdatePlayerAsync(player);

        return await RespondAsync($"🪱 Equipped **{bait.Name}**!");
    }

    [Command("equipment")]
    [Description("View your current equipment")]
    public async Task<IResult> ViewEquipmentAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var inventory = await _inventoryRepository.GetInventoryAsync(userId);

        var equippedRod = inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedRod);
        var equippedBait = !string.IsNullOrEmpty(player.EquippedBait) 
            ? inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedBait) 
            : null;

        var embed = new Embed
        {
            Title = $"🎣 {player.Username}'s Equipment",
            Colour = Color.Gold,
            Fields = new List<EmbedField>
            {
                new("🎣 Equipped Rod", equippedRod?.Name ?? "None", true),
                new("🪱 Equipped Bait", equippedBait?.Name ?? "None", true)
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

    private static int GetExperienceForRarity(string rarity)
    {
        return rarity.ToLower() switch
        {
            "common" => 10,
            "uncommon" => 15,
            "rare" => 25,
            "epic" => 50,
            "legendary" => 100,
            _ => 10
        };
    }

    private static string GetRarityEmoji(string rarity)
    {
        return rarity.ToLower() switch
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