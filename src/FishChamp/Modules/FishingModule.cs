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
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Commands.Responders;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Attributes;

namespace FishChamp.Modules;

[Group("fishing")]
[Description("Fishing-related commands")]
public class FishingModule(IDiscordRestChannelAPI channelAPI, IInteractionCommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository, IDiscordRestUserAPI userAPI, FeedbackService feedbackService) : CommandGroup
{
    [Command("cast")]
    [Description("Cast your fishing line")]
    public async Task<IResult> CastLineAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found! Try using `/map` to navigate.", Color.Red);
        }

        if (currentArea.FishingSpots.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🚫 No fishing spots available in this area!", Color.Red);
        }

        var fishingSpot = currentArea.FishingSpots.First();
        var availableFish = fishingSpot.AvailableFish;

        if (availableFish.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🚫 No fish available in this spot!", Color.Red);
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

            await inventoryRepository.AddItemAsync(user.ID.Value, fishItem);
            
            player.Experience += 10;
            player.LastActive = DateTime.UtcNow;
            await playerRepository.UpdatePlayerAsync(player);

            return await feedbackService.SendContextualContentAsync($"🎣 **Success!** You caught a {fishItem.Name}! " +
                                    $"Size: {fishItem.Properties["size"]}cm (+10 XP)", Color.Green);
        }
        else
        {
            return await feedbackService.SendContextualContentAsync("🎣 Your line comes up empty... Try again!", Color.Red);
        }
    }

    [Command("profile")]
    [Description("View your fishing profile")]
    public async Task<IResult> ViewProfileAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

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

    private static string FormatFishName(string fishId)
    {
        return fishId.Replace("_", " ").ToTitleCase();
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