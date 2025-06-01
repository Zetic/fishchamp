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
using FishChamp.Providers;
using FishChamp.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FishChamp.Modules;

[Group("fishing")]
[Description("Fishing-related commands")]
public class FishingCommandGroup(IInteractionCommandContext context, IDiscordRestChannelAPI channelAPI,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository, DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    
    [Command("register")]
    [Description("Register as a new angler")]
    public async Task<IResult> RegisterAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }
        
        // Check if player already exists
        var existingPlayer = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (existingPlayer != null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "⚠️ You are already registered! Use `/fishing profile` to see your details.");
        }
        
        // Create new player and inventory
        var player = await playerRepository.CreatePlayerAsync(user.ID.Value, user.Username);
        await inventoryRepository.CreateInventoryAsync(user.ID.Value);
        
        // Get starter area info
        var starterArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        
        var embed = new Embed
        {
            Title = "🎣 Welcome to Fish Champ!",
            Description = $"You've successfully registered as an angler, {player.Username}!\n" +
                        $"You've been given 100 Fish Coins 🪙 and a Basic Fishing Rod to start your adventure.\n\n" +
                        $"You are currently at **{starterArea?.Name ?? "Starter Lake"}**.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Getting Started", "• Use `/map goto <spot>` to go to a fishing spot\n" +
                                      "• Use `/fish` to start fishing\n" + 
                                      "• Use `/map current` to see your current area\n" +
                                      "• Use `/inventory view` to check your inventory\n" +
                                      "• Use `/shop browse` to find shops in your area", false),
                new("Your Stats", $"Level: {player.Level}\nFish Coins: {player.FishCoins} 🪙", true),
                new("Equipped Items", "Rod: Basic Fishing Rod\nBait: None", true)
            },
            Footer = new EmbedFooter("Good luck and happy fishing!"),
            Timestamp = DateTimeOffset.UtcNow
        };
        
        return await feedbackService.SendContextualEmbedAsync(embed);
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
        
        // Get equipped items
        var equippedRod = inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedRod && i.ItemType == "Rod");
        var equippedBait = !string.IsNullOrEmpty(player.EquippedBait) ? 
                          inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedBait && i.ItemType == "Bait") : null;
        
        string rodText = equippedRod != null ? equippedRod.Name : "None";
        string baitText = equippedBait != null ? $"{equippedBait.Name} (x{equippedBait.Quantity})" : "None";

        var embed = new Embed
        {
            Title = $"🎣 {player.Username}'s Fishing Profile",
            Colour = Color.Blue,
            Fields = new List<EmbedField>
            {
                new("Level", player.Level.ToString(), true),
                new("Experience", player.Experience.ToString(), true),
                new("Fish Coins", $"{player.FishCoins} 🪙", true),
                new("Current Area", player.CurrentArea.Replace("_", " ").ToTitleCase(), true),
                new("Fish Species Caught", fishCount.ToString(), true),
                new("Total Fish Caught", totalFish.ToString(), true),
                new("Equipped Rod", rodText, true),
                new("Equipped Bait", baitText, true),
                new("Joined", player.CreatedAt.ToString("yyyy-MM-dd"), true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }
    
    [Command("equip-rod")]
    [Description("Equip a fishing rod from your inventory")]
    public async Task<IResult> EquipRodAsync([Description("Rod name or ID to equip")] string rodId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        
        if (inventory == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🎣 You don't have any inventory items yet!");
        }
        
        // Find the rod in inventory (by ID or name)
        var rod = inventory.Items.FirstOrDefault(i => 
            i.ItemType == "Rod" && 
            (i.ItemId.Equals(rodId, StringComparison.OrdinalIgnoreCase) || 
             i.Name.Equals(rodId, StringComparison.OrdinalIgnoreCase)));
        
        if (rod == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🎣 Rod '{rodId}' not found in your inventory!");
        }
        
        // Equip the rod
        player.EquippedRod = rod.ItemId;
        await playerRepository.UpdatePlayerAsync(player);
        
        return await feedbackService.SendContextualContentAsync($"🎣 You equipped the **{rod.Name}**!", Color.Green);
    }

    [Command("equip-bait")]
    [Description("Equip bait from your inventory")]
    public async Task<IResult> EquipBaitAsync([Description("Bait name or ID to equip")] string baitId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        
        if (inventory == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🪱 You don't have any inventory items yet!");
        }
        
        // Find the bait in inventory (by ID or name)
        var bait = inventory.Items.FirstOrDefault(i => 
            i.ItemType == "Bait" && 
            (i.ItemId.Equals(baitId, StringComparison.OrdinalIgnoreCase) || 
             i.Name.Equals(baitId, StringComparison.OrdinalIgnoreCase)));
        
        if (bait == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🪱 Bait '{baitId}' not found in your inventory!");
        }
        
        // Check if there's any bait left
        if (bait.Quantity <= 0)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"🪱 You don't have any {bait.Name} left!");
        }
        
        // Equip the bait
        player.EquippedBait = bait.ItemId;
        await playerRepository.UpdatePlayerAsync(player);
        
        return await feedbackService.SendContextualContentAsync($"🪱 You equipped **{bait.Name}** (x{bait.Quantity} remaining)!", Color.Green);
    }

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

    [Command("fish-together")]
    [Description("Start or join a multiplayer fishing session at your current location")]
    public async Task<IResult> FishTogetherAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        
        // Check if player is at a fishing spot
        if (string.IsNullOrEmpty(player.CurrentFishingSpot))
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, 
                "🎣 You need to be at a fishing spot first! Use `/map goto <fishing spot>` to go to one.");
        }
        
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, ":map: Current area not found! Try using `/map` to navigate.");
        }

        // Verify the fishing spot exists
        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => 
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
        {
            // Player's fishing spot is invalid, clear it
            player.CurrentFishingSpot = string.Empty;
            await playerRepository.UpdatePlayerAsync(player);
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, 
                "🚫 Your current fishing spot is no longer available. Use `/map goto <fishing spot>` to go to a new one.");
        }

        // Find other players in the same area and at the same spot
        var allPlayers = await playerRepository.GetAllPlayersAsync();
        var playersAtSameSpot = allPlayers
            .Where(p => p.UserId != player.UserId) // Not the current player
            .Where(p => p.CurrentArea == player.CurrentArea) // In the same area
            .Where(p => p.CurrentFishingSpot == player.CurrentFishingSpot) // At the same fishing spot
            .Where(p => p.LastActive > DateTime.UtcNow.AddMinutes(-5)) // Active in the last 5 minutes
            .ToList();

        if (playersAtSameSpot.Any())
        {
            var embed = new Embed
            {
                Title = "🎣 Multiplayer Fishing",
                Description = $"You're now fishing together with {playersAtSameSpot.Count} other player(s) at **{fishingSpot.Name}**!\n\n" +
                              "Fishing with friends increases your chances of catching rare fish!",
                Colour = Color.Green,
                Fields = new List<EmbedField>
                {
                    new EmbedField("Bonus", "• +10% catch rate\n• +15% chance for rare fish\n• Chance for bonus rewards", false)
                },
                Footer = new EmbedFooter("Use /fish or /fishing cast to start fishing at this spot"),
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }
        else
        {
            var embed = new Embed
            {
                Title = "🎣 Multiplayer Fishing",
                Description = $"You've started a multiplayer fishing session at **{fishingSpot.Name}**!\n\n" +
                              "Other players can join your session by going to the same fishing spot and using `/fishing fish-together`.\n\n" +
                              "Fishing with friends increases your chances of catching rare fish!",
                Colour = Color.Green,
                Footer = new EmbedFooter("Use /fish or /fishing cast to start fishing at this spot"),
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }
    }
    
    [Command("leaderboard")]
    [Description("View the fishing leaderboard for biggest catches")]
    public async Task<IResult> ViewLeaderboardAsync(
        [Description("Filter by specific fish type")] string fishType = "")
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var allPlayers = await playerRepository.GetAllPlayersAsync();
        
        // Filter players with at least one fish caught
        var fishingPlayers = allPlayers
            .Where(p => p.BiggestCatch.Count > 0)
            .ToList();
            
        if (fishingPlayers.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("📊 No fishing records found yet!", Color.Yellow);
        }
        
        // If a specific fish type was provided, filter for that
        if (!string.IsNullOrWhiteSpace(fishType))
        {
            // Find players who caught this specific fish
            var matchingFish = fishingPlayers
                .Where(p => p.BiggestCatch.Keys.Any(k => k.Contains(fishType, StringComparison.OrdinalIgnoreCase)))
                .ToList();
                
            if (matchingFish.Count == 0)
            {
                return await feedbackService.SendContextualContentAsync($"📊 No records found for fish matching '{fishType}'", Color.Yellow);
            }
            
            var specificFishType = matchingFish
                .SelectMany(p => p.BiggestCatch.Keys)
                .FirstOrDefault(k => k.Contains(fishType, StringComparison.OrdinalIgnoreCase)) ?? "";
                
            if (string.IsNullOrEmpty(specificFishType))
            {
                return await feedbackService.SendContextualContentAsync($"📊 No records found for fish matching '{fishType}'", Color.Yellow);
            }
            
            // Get top 10 catches for this fish
            var topCatches = fishingPlayers
                .Where(p => p.BiggestCatch.ContainsKey(specificFishType))
                .Select(p => new { Player = p, Weight = p.BiggestCatch[specificFishType] })
                .OrderByDescending(x => x.Weight)
                .Take(10)
                .ToList();
                
            var formattedFishName = FormatFishName(specificFishType);
            
            var leaderboardText = string.Join("\n", topCatches.Select((x, i) => 
                $"{i + 1}. **{x.Player.Username}**: {x.Weight}g"));
                
            var embed = new Embed
            {
                Title = $"🏆 Biggest {formattedFishName} Leaderboard",
                Description = leaderboardText,
                Colour = Color.Gold,
                Footer = new EmbedFooter("Catch more fish to climb the leaderboard!"),
                Timestamp = DateTimeOffset.UtcNow
            };
            
            return await feedbackService.SendContextualEmbedAsync(embed);
        }
        else
        {
            // Find the overall biggest fish caught by each player
            var biggestOverall = fishingPlayers
                .Select(p => new { 
                    Player = p, 
                    FishType = p.BiggestCatch.OrderByDescending(x => x.Value).FirstOrDefault().Key,
                    Weight = p.BiggestCatch.Values.Max() 
                })
                .OrderByDescending(x => x.Weight)
                .Take(10)
                .ToList();
                
            var leaderboardText = string.Join("\n", biggestOverall.Select((x, i) => 
                $"{i + 1}. **{x.Player.Username}**: {FormatFishName(x.FishType)} ({x.Weight}g)"));
                
            var embed = new Embed
            {
                Title = $"🏆 Biggest Fish Leaderboard",
                Description = leaderboardText,
                Colour = Color.Gold,
                Footer = new EmbedFooter("Use /fishing leaderboard <fish type> for specific fish rankings"),
                Timestamp = DateTimeOffset.UtcNow
            };
            
            return await feedbackService.SendContextualEmbedAsync(embed);
        }
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