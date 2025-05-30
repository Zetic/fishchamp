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

        // Calculate success chance based on rod and bait
        var random = new Random();
        double baseSuccessRate = 0.7; // 70% base success rate
        double rodBonus = equippedRod?.Properties.TryGetValue("power", out var power) == true ? 
                         Convert.ToDouble(power) * 0.05 : 0; // 5% per rod power level
        double baitBonus = equippedBait != null ? 0.1 : 0; // 10% bonus for having bait equipped

        double successRate = Math.Min(0.95, baseSuccessRate + rodBonus + baitBonus); // Cap at 95%
        
        // Fishing simulation
        var success = random.NextDouble() <= successRate;

        if (success)
        {
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
            
            // Select random fish from available fish
            var caughtFish = availableFish[random.Next(availableFish.Count)];
            
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
            
            var fishItem = new InventoryItem
            {
                ItemId = caughtFish,
                ItemType = "Fish",
                Name = FormatFishName(caughtFish),
                Quantity = 1,
                Properties = new() { ["size"] = fishSize, ["rarity"] = rarity }
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
            
            return await feedbackService.SendContextualContentAsync(
                $"🎣 **Success!** You caught a {rarityEmoji} {fishItem.Name}!\n" +
                $"Size: {fishItem.Properties["size"]}cm (+{xpGained} XP)", Color.Green);
        }
        else
        {
            return await feedbackService.SendContextualContentAsync("🎣 Your line comes up empty... Try again!", Color.Red);
        }
    }

    [Command("profile")]
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