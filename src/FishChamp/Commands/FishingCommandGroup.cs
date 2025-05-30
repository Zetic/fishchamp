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
    [Command("cast")]
    [Description("Cast your fishing line")]
    public async Task<IResult> CastLineAsync(
        [AutocompleteProvider(AreaFishSpotAutocompleteProvider.ID)]
        [Description("Spot you would like to fish at in your area")]
        string fishSpot)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, ":map: Current area not found! Try using `/map` to navigate.");
        }

        if (currentArea.FishingSpots.Count == 0)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, ":fishing_pole_and_fish: No fishing spots available in this area!");
        }

        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => f.SpotId.Equals(fishSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🚫 Failed to find specified fish spot!");
        }

        if (fishingSpot.Type == FishingSpotType.Water)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, ":sailboat: Only boats are allowed to fish here.");
        }

        // If player is part of a multiplayer session, use that spot
        if (!string.IsNullOrEmpty(player.CurrentFishingSpot))
        {
            // Use the shared spot if it exists
            var sharedSpot = currentArea.FishingSpots.FirstOrDefault(f => f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));
            if (sharedSpot != null)
            {
                fishingSpot = sharedSpot;
            }
        }

        var availableFish = fishingSpot.AvailableFish;

        if (availableFish.Count == 0)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🚫:fish: No fish available in this spot!");
        }

        // Get equipped rod and bait
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        var equippedRod = inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedRod && i.ItemType == "Rod");
        var equippedBait = !string.IsNullOrEmpty(player.EquippedBait) ?
                          inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedBait && i.ItemType == "Bait") : null;

        // Get rod abilities (if any)
        RodAbility rodAbilities = RodAbility.None;
        if (equippedRod != null && equippedRod.Properties.TryGetValue("abilities", out var abilitiesObj))
        {
            rodAbilities = (RodAbility)GetValueFromProperty<int>(abilitiesObj);
        }

        // Calculate success chance based on rod and bait
        var random = new Random();
        double baseSuccessRate = 0.7; // 70% base success rate
        double rodBonus = (equippedRod != null && equippedRod.Properties.TryGetValue("power", out var powerValue)) ?
                         GetValueFromProperty<double>(powerValue) * 0.05 : 0; // 5% per rod power level
        double baitBonus = equippedBait != null ? 0.1 : 0; // 10% bonus for having bait equipped

        // Check for multiplayer fishing bonus
        double multiplayerBonus = 0;
        if (!string.IsNullOrEmpty(player.CurrentFishingSpot))
        {
            // Count other players fishing at the same spot
            var allPlayers = await playerRepository.GetAllPlayersAsync();
            var otherPlayersHere = allPlayers.Count(p =>
                p.UserId != player.UserId &&
                p.CurrentArea == player.CurrentArea &&
                p.CurrentFishingSpot == player.CurrentFishingSpot &&
                p.LastActive > DateTime.UtcNow.AddMinutes(-5));

            if (otherPlayersHere > 0)
            {
                // 10% base bonus for multiplayer fishing plus 2% per additional player
                multiplayerBonus = 0.1 + (Math.Min(otherPlayersHere, 5) * 0.02);
            }
        }

        double successRate = Math.Min(0.95, baseSuccessRate + rodBonus + baitBonus + multiplayerBonus); // Cap at 95%

        // Fishing simulation - first we'll determine the fish that might be caught
        var potentialCatch = availableFish[random.Next(availableFish.Count)];

        // Determine fish rarity based on rod and bait
        var rarityRoll = random.NextDouble();
        string rarity;

        if (equippedRod != null && equippedBait != null)
        {
            // Better chance for rare fish with both rod and bait
            if (rarityRoll < 0.6) rarity = "common";
            else if (rarityRoll < 0.85) rarity = "uncommon";
            else if (rarityRoll < 0.95) rarity = "rare";
            else if (rarityRoll < 0.99) rarity = "epic";
            else rarity = "legendary";
        }
        else if (equippedRod != null)
        {
            // Decent chance for uncommon with rod only
            if (rarityRoll < 0.7) rarity = "common";
            else if (rarityRoll < 0.9) rarity = "uncommon";
            else if (rarityRoll < 0.98) rarity = "rare";
            else rarity = "epic";
        }
        else
        {
            // Basic chances without rod
            if (rarityRoll < 0.8) rarity = "common";
            else if (rarityRoll < 0.95) rarity = "uncommon";
            else rarity = "rare";
        }

        // Randomly assign traits based on rarity
        var fishTraits = DetermineFishTraits(rarity, random);

        // Apply trait effects on success rate
        if ((fishTraits & FishTrait.Evasive) != 0)
        {
            // Evasive trait reduces catch chance by 20% unless countered by Precision rod
            if ((rodAbilities & RodAbility.Precision) == 0)
            {
                successRate *= 0.8;
            }
        }

        if ((fishTraits & FishTrait.Camouflage) != 0)
        {
            // Camouflage trait reduces catch chance by 20% unless countered by FishFinder rod
            if ((rodAbilities & RodAbility.FishFinder) == 0)
            {
                successRate *= 0.8;
            }
        }

        // Check for success
        var success = random.NextDouble() <= successRate;

        // Special handling for slippery trait - fish might escape after being caught
        if (success && (fishTraits & FishTrait.Slippery) != 0)
        {
            // 50% chance of slipping away unless countered by SharpHook
            if ((rodAbilities & RodAbility.SharpHook) == 0 && random.NextDouble() < 0.5)
            {
                success = false;
            }
        }

        // Select caught fish
        var caughtFish = potentialCatch;

        // Determine fish size based on rarity
        int minSize = 10;
        int maxSize = 50;

        switch (rarity)
        {
            case "uncommon": minSize = 20; maxSize = 60; break;
            case "rare": minSize = 30; maxSize = 70; break;
            case "epic": minSize = 40; maxSize = 80; break;
            case "legendary": minSize = 50; maxSize = 100; break;
        }

        var fishSize = random.Next(minSize, maxSize + 1);

        // Calculate fish weight based on size and rarity (weight in grams)
        double weightMultiplier = rarity switch
        {
            "common" => 1.0,
            "uncommon" => 1.2,
            "rare" => 1.5,
            "epic" => 1.8,
            "legendary" => 2.2,
            _ => 1.0
        };

        // Weight formula: size^1.5 * multiplier (approximates fish volume to weight)
        var fishWeight = Math.Round(Math.Pow(fishSize, 1.5) * weightMultiplier, 1);

        var fishItem = new InventoryItem
        {
            ItemId = caughtFish,
            ItemType = "Fish",
            Name = FormatFishName(caughtFish),
            Quantity = 1,
            Properties = new()
            {
                ["size"] = fishSize,
                ["weight"] = fishWeight,
                ["rarity"] = rarity,
                ["traits"] = (int)fishTraits
            }
        };

        await inventoryRepository.AddItemAsync(user.ID.Value, fishItem);

        // Calculate XP gained based on rarity
        int xpGained = rarity switch
        {
            "common" => 10,
            "uncommon" => 20,
            "rare" => 40,
            "epic" => 70,
            "legendary" => 100,
            _ => 10
        };

        player.Experience += xpGained;
        player.LastActive = DateTime.UtcNow;

        // Check if this is the biggest fish of this type the player has caught
        if (!player.BiggestCatch.TryGetValue(fishItem.ItemId, out var existingRecord) || fishWeight > existingRecord)
        {
            player.BiggestCatch[fishItem.ItemId] = fishWeight;
        }

        await playerRepository.UpdatePlayerAsync(player);

        // If bait was used, reduce its quantity
        if (equippedBait != null)
        {
            await inventoryRepository.RemoveItemAsync(user.ID.Value, player.EquippedBait, 1);

            // If that was the last bait, unequip it
            if ((equippedBait.Quantity - 1) <= 0)
            {
                player.EquippedBait = string.Empty;
                await playerRepository.UpdatePlayerAsync(player);
            }
        }

        // Get rarity emoji
        string rarityEmoji = rarity switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵",
            "epic" => "🟣",
            "legendary" => "🟡",
            _ => "⚪"
        };

        // Get fish traits display
        string traitsDisplay = "";
        if (fishTraits != FishTrait.None)
        {
            var traitsList = new List<string>();
            if ((fishTraits & FishTrait.Evasive) != 0) traitsList.Add("Evasive");
            if ((fishTraits & FishTrait.Slippery) != 0) traitsList.Add("Slippery");
            if ((fishTraits & FishTrait.Magnetic) != 0) traitsList.Add("Magnetic");
            if ((fishTraits & FishTrait.Camouflage) != 0) traitsList.Add("Camouflage");

            if (traitsList.Count > 0)
            {
                traitsDisplay = $"\nTraits: {string.Join(", ", traitsList)}";
            }
        }

        if (success)
        {
            return await feedbackService.SendContextualContentAsync(
                $"🎣 **Success!** You caught a {rarityEmoji} {fishItem.Name}!\n" +
                $"Size: {fishItem.Properties["size"]}cm | Weight: {fishItem.Properties["weight"]}g (+{xpGained} XP){traitsDisplay}", Color.Green);
        }
        else
        {
            return await feedbackService.SendContextualContentAsync("🎣 Your line comes up empty... Try again!", Color.Red);
        }
    }

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
                new("Getting Started", "• Use `/fishing cast <spot>` to start fishing\n" + 
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
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null || !inventory.Items.Any(i => i.ItemType == "Fish"))
        {
            return await feedbackService.SendContextualContentAsync("📖 Your FishDex is empty! Catch some fish to fill it.", Color.Yellow);
        }

        // Get all fish species the player has caught
        var fishSpecies = inventory.Items
            .Where(i => i.ItemType == "Fish")
            .GroupBy(i => i.ItemId)
            .Select(g => g.OrderByDescending(f => 
                f.Properties.TryGetValue("size", out var sizeElement) ? GetValueFromProperty<int>(sizeElement) : 0).First())
            .ToList();

        var totalSpecies = fishSpecies.Count;
        
        // Create a formatted list of fish with their stats and traits
        var fishEntries = new List<string>();
        foreach (var fish in fishSpecies)
        {
            var size = fish.Properties.TryGetValue("size", out var propSize) ? GetValueFromProperty<int>(propSize) : 0;
            var weight = fish.Properties.TryGetValue("weight", out var propWeight) ? GetValueFromProperty<double>(propWeight) : 0;
            var rarity = fish.Properties.TryGetValue("rarity", out var propRarity) ? GetValueFromProperty<string>(propRarity) : "common";
            var rarityEmoji = GetRarityEmoji(rarity);
            
            // Get traits if any
            string traitsText = "";
            if (fish.Properties.TryGetValue("traits", out var propTraits))
            {
                var fishTraits = (FishTrait)GetValueFromProperty<int>(propTraits);
                if (fishTraits != FishTrait.None)
                {
                    var traitsList = new List<string>();
                    if ((fishTraits & FishTrait.Evasive) != 0) traitsList.Add("Evasive");
                    if ((fishTraits & FishTrait.Slippery) != 0) traitsList.Add("Slippery");
                    if ((fishTraits & FishTrait.Magnetic) != 0) traitsList.Add("Magnetic");
                    if ((fishTraits & FishTrait.Camouflage) != 0) traitsList.Add("Camouflage");
                    
                    if (traitsList.Count > 0)
                    {
                        traitsText = $" | Traits: {string.Join(", ", traitsList)}";
                    }
                }
            }
            
            fishEntries.Add($"{rarityEmoji} **{fish.Name}** - Size: {size}cm | Weight: {weight}g{traitsText}");
        }

        // Calculate progress percentage
        int estimatedTotalSpecies = 50; // This would ideally come from a repository of all available fish
        double completionPercentage = Math.Round((double)totalSpecies / estimatedTotalSpecies * 100, 1);
        
        // Create embed with paginated fish entries (10 per page)
        var embed = new Embed
        {
            Title = $"📖 {player.Username}'s FishDex",
            Description = $"You've discovered {totalSpecies} fish species ({completionPercentage}% complete)\n\n" +
                          string.Join("\n", fishEntries),
            Colour = Color.Gold,
            Footer = new EmbedFooter("Catch more fish to complete your FishDex!"),
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
        
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, ":map: Current area not found! Try using `/map` to navigate.");
        }

        if (currentArea.FishingSpots.Count == 0)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, ":fishing_pole_and_fish: No fishing spots available in this area for multiplayer fishing!");
        }

        // Find other players in the same area and spot
        var allPlayers = await playerRepository.GetAllPlayersAsync();
        var playersInArea = allPlayers
            .Where(p => p.UserId != player.UserId) // Not the current player
            .Where(p => p.CurrentArea == player.CurrentArea) // In the same area
            .Where(p => !string.IsNullOrEmpty(p.CurrentFishingSpot)) // Currently at a fishing spot
            .Where(p => p.LastActive > DateTime.UtcNow.AddMinutes(-5)) // Active in the last 5 minutes
            .ToList();

        if (!playersInArea.Any())
        {
            // No other players fishing in this area, set up the spot for others to join
            var availableSpots = currentArea.FishingSpots
                .Where(s => s.Type != FishingSpotType.Water)
                .Select(s => s.SpotId)
                .ToList();
                
            if (!availableSpots.Any())
            {
                return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "🚫 No suitable fishing spots in this area for multiplayer fishing.");
            }

            // Select a random spot if the player isn't already at one
            var spotToUse = !string.IsNullOrEmpty(player.CurrentFishingSpot) && 
                            availableSpots.Contains(player.CurrentFishingSpot) 
                ? player.CurrentFishingSpot 
                : availableSpots[new Random().Next(availableSpots.Count)];
                
            player.CurrentFishingSpot = spotToUse;
            await playerRepository.UpdatePlayerAsync(player);

            var embed = new Embed
            {
                Title = "🎣 Multiplayer Fishing",
                Description = $"You've started a multiplayer fishing session at {spotToUse.Replace("_", " ").ToTitleCase()}!\n\n" +
                              "Other players can join your session by using `/fishing fish-together`.\n\n" +
                              "Fishing with friends increases your chances of catching rare fish!",
                Colour = Color.Green,
                Footer = new EmbedFooter("Use /fishing cast to start fishing at this spot"),
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }
        else
        {
            // Find an existing player to join
            var playerToJoin = playersInArea[new Random().Next(playersInArea.Count)];
            player.CurrentFishingSpot = playerToJoin.CurrentFishingSpot;
            await playerRepository.UpdatePlayerAsync(player);

            var embed = new Embed
            {
                Title = "🎣 Multiplayer Fishing",
                Description = $"You've joined {playerToJoin.Username}'s fishing session at {player.CurrentFishingSpot.Replace("_", " ").ToTitleCase()}!\n\n" +
                              "Fishing with friends increases your chances of catching rare fish!",
                Colour = Color.Green,
                Fields = new List<EmbedField>
                {
                    new EmbedField("Bonus", "• +10% catch rate\n• +15% chance for rare fish\n• Chance for bonus rewards", false)
                },
                Footer = new EmbedFooter("Use /fishing cast to start fishing at this spot"),
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

    public static T GetValueFromProperty<T>(object obj)
    {
        if (obj is not JsonElement element) return default;
        return JsonSerializer.Deserialize<T>(element);
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