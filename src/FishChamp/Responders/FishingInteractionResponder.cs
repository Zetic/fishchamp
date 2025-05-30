using System.Drawing;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using FishChamp.Modules;
using System.Text.Json;

namespace FishChamp.Responders;

public class FishingInteractionResponder : IResponder<IInteractionCreate>
{
    private readonly IDiscordRestInteractionAPI _interactionAPI;
    private readonly IPlayerRepository _playerRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IAreaRepository _areaRepository;

    public FishingInteractionResponder(
        IDiscordRestInteractionAPI interactionAPI,
        IPlayerRepository playerRepository,
        IInventoryRepository inventoryRepository,
        IAreaRepository areaRepository)
    {
        _interactionAPI = interactionAPI;
        _playerRepository = playerRepository;
        _inventoryRepository = inventoryRepository;
        _areaRepository = areaRepository;
    }

    public async Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        if (gatewayEvent.Interaction.Type != InteractionType.MessageComponent)
            return Result.FromSuccess();

        if (!gatewayEvent.Interaction.Data.TryGet(out var data) || !data.TryPickT0(out var componentData))
            return Result.FromSuccess();

        var customId = componentData.CustomID;

        return customId switch
        {
            "fish_cast_line" => await HandleCastLineAsync(gatewayEvent, ct),
            "fish_reel_in" => await HandleReelInAsync(gatewayEvent, ct),
            "fish_let_go" => await HandleLetGoAsync(gatewayEvent, ct),
            "fish_stop_reel" => await HandleStopReelAsync(gatewayEvent, ct),
            _ => Result.FromSuccess()
        };
    }

    private async Task<Result> HandleCastLineAsync(IInteractionCreate gatewayEvent, CancellationToken ct)
    {
        if (!gatewayEvent.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        var player = await _playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
            return Result.FromSuccess();

        var currentArea = await _areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
            return Result.FromSuccess();

        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => 
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
            return Result.FromSuccess();

        // Simulate waiting for a bite (1-3 seconds)
        var random = new Random();
        var waitTime = random.Next(1000, 4000); // 1-3 seconds
        
        // Create waiting embed
        var waitingEmbed = new Embed
        {
            Title = "🎣 Casting your line...",
            Description = $"You cast your line into the waters at **{fishingSpot.Name}**...\n\nWaiting for a bite...",
            Colour = Color.Orange,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Update the message with waiting state
        await _interactionAPI.EditOriginalInteractionResponseAsync(
            gatewayEvent.Interaction.ApplicationID,
            gatewayEvent.Interaction.Token,
            embeds: new[] { waitingEmbed },
            ct: ct);

        // Wait for the random amount of time
        await Task.Delay(waitTime, ct);

        // Generate random fishing event
        var eventRoll = random.NextDouble();
        string eventText;
        bool canCatch = false;

        if (eventRoll < 0.15) // 15% - Line snapped
        {
            eventText = "💥 **Line snapped!**\nYour fishing line broke! Maybe try with better equipment next time.";
        }
        else if (eventRoll < 0.3) // 15% - Fish escaped
        {
            eventText = "🐟 **Fish escaped!**\nA fish took the bait but swam away before you could react!";
        }
        else if (eventRoll < 0.5) // 20% - Nothing
        {
            eventText = "💧 **Nothing...**\nThe waters are quiet. No fish seem interested in your bait.";
        }
        else // 50% - Bite!
        {
            eventText = "🐟 **You feel a tug on the line...**\nSomething's biting! What will you do?";
            canCatch = true;
        }

        var resultEmbed = new Embed
        {
            Title = "🎣 Fishing Result",
            Description = eventText,
            Colour = canCatch ? Color.Green : Color.Red,
            Timestamp = DateTimeOffset.UtcNow
        };

        List<IMessageComponent> components = new();

        if (canCatch)
        {
            // Add reel in and let go buttons
            var reelButton = new ButtonComponent(ButtonComponentStyle.Success, "Reel In", new PartialEmoji(Name: "🎯"), "fish_reel_in");
            var letGoButton = new ButtonComponent(ButtonComponentStyle.Secondary, "Let it Go", new PartialEmoji(Name: "🪝"), "fish_let_go");
            components.Add(new ActionRowComponent(new[] { (IMessageComponent)reelButton, (IMessageComponent)letGoButton }));
        }
        else
        {
            // Add try again button
            var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), "fish_cast_line");
            components.Add(new ActionRowComponent(new[] { (IMessageComponent)tryAgainButton }));
        }

        return await _interactionAPI.EditOriginalInteractionResponseAsync(
            gatewayEvent.Interaction.ApplicationID,
            gatewayEvent.Interaction.Token,
            embeds: new[] { resultEmbed },
            components: components,
            ct: ct);
    }

    private async Task<Result> HandleReelInAsync(IInteractionCreate gatewayEvent, CancellationToken ct)
    {
        if (!gatewayEvent.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        // Start the timing mini-game
        var timingEmbed = new Embed
        {
            Title = "🎯 Timing Challenge!",
            Description = "Click **Stop Reel** when the 🟢 is in the center!\n\n⬛⬛🟢⬛⬛",
            Colour = Color.Yellow,
            Footer = new EmbedFooter("Quick! Don't let the fish escape!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        var stopButton = new ButtonComponent(ButtonComponentStyle.Danger, "Stop Reel", new PartialEmoji(Name: "🛑"), "fish_stop_reel");
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent(new[] { (IMessageComponent)stopButton })
        };

        return await _interactionAPI.EditOriginalInteractionResponseAsync(
            gatewayEvent.Interaction.ApplicationID,
            gatewayEvent.Interaction.Token,
            embeds: new[] { timingEmbed },
            components: components,
            ct: ct);
    }

    private async Task<Result> HandleLetGoAsync(IInteractionCreate gatewayEvent, CancellationToken ct)
    {
        var letGoEmbed = new Embed
        {
            Title = "🪝 Released",
            Description = "You decided to let the fish go. Sometimes it's better to be patient!\n\nTry casting again when you're ready.",
            Colour = Color.Gray,
            Timestamp = DateTimeOffset.UtcNow
        };

        var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), "fish_cast_line");
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent(new[] { (IMessageComponent)tryAgainButton })
        };

        return await _interactionAPI.EditOriginalInteractionResponseAsync(
            gatewayEvent.Interaction.ApplicationID,
            gatewayEvent.Interaction.Token,
            embeds: new[] { letGoEmbed },
            components: components,
            ct: ct);
    }

    private async Task<Result> HandleStopReelAsync(IInteractionCreate gatewayEvent, CancellationToken ct)
    {
        if (!gatewayEvent.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        var player = await _playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
            return Result.FromSuccess();

        var currentArea = await _areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
            return Result.FromSuccess();

        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => 
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
            return Result.FromSuccess();

        // Simulate timing success/failure
        var random = new Random();
        var timingSuccess = random.NextDouble() < 0.7; // 70% success rate

        if (!timingSuccess)
        {
            var failEmbed = new Embed
            {
                Title = "🐟 Fish Escaped!",
                Description = "Your timing was off and the fish got away!\n\nBetter luck next time!",
                Colour = Color.Red,
                Timestamp = DateTimeOffset.UtcNow
            };

            var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), "fish_cast_line");
            var components = new List<IMessageComponent>
            {
                new ActionRowComponent(new[] { (IMessageComponent)tryAgainButton })
            };

            return await _interactionAPI.EditOriginalInteractionResponseAsync(
                gatewayEvent.Interaction.ApplicationID,
                gatewayEvent.Interaction.Token,
                embeds: new[] { failEmbed },
                components: components,
                ct: ct);
        }

        // Success! Now simulate the actual fishing logic from the original cast command
        await SimulateFishCatch(gatewayEvent, player, fishingSpot, ct);

        return Result.FromSuccess();
    }

    private async Task SimulateFishCatch(IInteractionCreate gatewayEvent, PlayerProfile player, FishingSpot fishingSpot, CancellationToken ct)
    {
        var user = gatewayEvent.Interaction.Member.Value.User.Value;
        var random = new Random();

        var availableFish = fishingSpot.AvailableFish;
        if (availableFish.Count == 0)
            return;

        // Get equipped rod and bait
        var inventory = await _inventoryRepository.GetInventoryAsync(user.ID.Value);
        var equippedRod = inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedRod && i.ItemType == "Rod");
        var equippedBait = !string.IsNullOrEmpty(player.EquippedBait) ?
                          inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedBait && i.ItemType == "Bait") : null;

        // Calculate success chance and fish details (simplified version of original logic)
        var potentialCatch = availableFish[random.Next(availableFish.Count)];

        // Determine fish rarity
        var rarityRoll = random.NextDouble();
        string rarity = rarityRoll < 0.7 ? "common" : 
                       rarityRoll < 0.9 ? "uncommon" : 
                       rarityRoll < 0.98 ? "rare" : "epic";

        // Determine fish size and weight
        int minSize = rarity switch { "uncommon" => 20, "rare" => 30, "epic" => 40, _ => 10 };
        int maxSize = rarity switch { "uncommon" => 60, "rare" => 70, "epic" => 80, _ => 50 };
        var fishSize = random.Next(minSize, maxSize + 1);

        double weightMultiplier = rarity switch { "uncommon" => 1.2, "rare" => 1.5, "epic" => 1.8, _ => 1.0 };
        var fishWeight = Math.Round(Math.Pow(fishSize, 1.5) * weightMultiplier, 1);

        var fishItem = new InventoryItem
        {
            ItemId = potentialCatch,
            ItemType = "Fish",
            Name = FormatFishName(potentialCatch),
            Quantity = 1,
            Properties = new()
            {
                ["size"] = fishSize,
                ["weight"] = fishWeight,
                ["rarity"] = rarity,
                ["traits"] = 0 // Simplified for now
            }
        };

        await _inventoryRepository.AddItemAsync(user.ID.Value, fishItem);

        // Update player stats
        int xpGained = rarity switch { "uncommon" => 20, "rare" => 40, "epic" => 70, _ => 10 };
        player.Experience += xpGained;
        player.LastActive = DateTime.UtcNow;

        if (!player.BiggestCatch.TryGetValue(fishItem.ItemId, out var existingRecord) || fishWeight > existingRecord)
        {
            player.BiggestCatch[fishItem.ItemId] = fishWeight;
        }

        await _playerRepository.UpdatePlayerAsync(player);

        // Get rarity emoji
        string rarityEmoji = rarity switch
        {
            "common" => "⚪",
            "uncommon" => "🟢", 
            "rare" => "🔵",
            "epic" => "🟣",
            _ => "⚪"
        };

        var successEmbed = new Embed
        {
            Title = "🎣 Great Catch!",
            Description = $"**Success!** You caught a {rarityEmoji} {fishItem.Name}!\n\n" +
                         $"Size: {fishItem.Properties["size"]}cm | Weight: {fishItem.Properties["weight"]}g (+{xpGained} XP)",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        var castAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), "fish_cast_line");
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent(new[] { (IMessageComponent)castAgainButton })
        };

        await _interactionAPI.EditOriginalInteractionResponseAsync(
            gatewayEvent.Interaction.ApplicationID,
            gatewayEvent.Interaction.Token,
            embeds: new[] { successEmbed },
            components: components,
            ct: ct);
    }

    private static string FormatFishName(string fishId)
    {
        return fishId.Replace("_", " ").ToTitleCase();
    }

    public static T GetValueFromProperty<T>(object obj)
    {
        if (obj is not JsonElement element) return default;
        return JsonSerializer.Deserialize<T>(element);
    }
}