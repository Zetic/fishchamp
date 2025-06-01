using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Modules;
using Polly;
using Remora.Commands.Attributes;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FishChamp.Features.FishDex;

public class FishDexCommand(IInteractionContext context,
    IFeedbackService feedbackService,
    IPlayerRepository playerRepository,
    IInventoryRepository inventoryRepository)
{
    [Command("fishdex")]
    [Description("View your fish collection catalog")]
    public async Task<IResult> ViewFishDexAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        if (player.FishDex.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("📖 Your FishDex is empty! Catch some fish to fill it.", Color.Yellow);
        }

        // Get all discovered fish species from the persistent FishDex
        var discoveredFish = player.FishDex.Values.OrderBy(f => f.FirstDiscovered).ToList();
        var totalSpecies = discoveredFish.Count;

        // Get discovered rarities
        var discoveredRarities = discoveredFish.Select(f => f.Rarity).Distinct().OrderBy(r => GetRarityOrder(r)).ToList();

        // Create a formatted list of fish with their stats and traits
        var fishEntries = new List<string>();
        foreach (var fishRecord in discoveredFish)
        {
            var rarityEmoji = GetRarityEmoji(fishRecord.Rarity);

            // Get traits if any
            string traitsText = "";
            if (fishRecord.ObservedTraits != FishTrait.None)
            {
                var traitsList = new List<string>();
                if ((fishRecord.ObservedTraits & FishTrait.Evasive) != 0) traitsList.Add("Evasive");
                if ((fishRecord.ObservedTraits & FishTrait.Slippery) != 0) traitsList.Add("Slippery");
                if ((fishRecord.ObservedTraits & FishTrait.Magnetic) != 0) traitsList.Add("Magnetic");
                if ((fishRecord.ObservedTraits & FishTrait.Camouflage) != 0) traitsList.Add("Camouflage");

                if (traitsList.Count > 0)
                {
                    traitsText = $" | Traits: {string.Join(", ", traitsList)}";
                }
            }

            var discoveryText = fishRecord.TimesDiscovered > 1 ? $" | Caught {fishRecord.TimesDiscovered}x" : "";
            fishEntries.Add($"{rarityEmoji} **{fishRecord.FishName}** - Heaviest: {fishRecord.HeaviestWeight}g | Largest: {fishRecord.LargestSize}cm{traitsText}{discoveryText}");
        }

        // Calculate progress (estimated total species for now - could be made dynamic later)
        int estimatedTotalSpecies = 50;
        double completionPercentage = Math.Round((double)totalSpecies / estimatedTotalSpecies * 100, 1);

        var rarityText = discoveredRarities.Count > 0 ? $"**Discovered Rarities:** {string.Join(", ", discoveredRarities.Select(r => $"{GetRarityEmoji(r)} {r}"))}\n\n" : "";

        // Create embed with fish entries
        var embed = new Embed
        {
            Title = $"📖 {player.Username}'s FishDex",
            Description = $"You've discovered **{totalSpecies}** fish species ({completionPercentage}% complete)\n\n" +
                          rarityText +
                          string.Join("\n", fishEntries),
            Colour = Color.Gold,
            Footer = new EmbedFooter("Catch more fish to complete your FishDex! Persistent records track your discoveries."),
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

    private static int GetRarityOrder(string rarity)
    {
        return rarity.ToLower() switch
        {
            "common" => 1,
            "uncommon" => 2,
            "rare" => 3,
            "epic" => 4,
            "legendary" => 5,
            _ => 0
        };
    }

    private static FishTrait DetermineFishTraits(string rarity, Random random)
    {
        // Higher rarity fish have more chance of having traits
        double traitChance = rarity switch
        {
            "common" => 0.1,    // 10% chance for any trait
            "uncommon" => 0.2,  // 20% chance for any trait
            "rare" => 0.4,      // 40% chance for any trait
            "epic" => 0.6,      // 60% chance for any trait
            "legendary" => 0.8, // 80% chance for any trait
            _ => 0.1
        };

        FishTrait traits = FishTrait.None;

        // Check for each trait
        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Evasive;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Slippery;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Magnetic;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Camouflage;

        return traits;
    }
}
