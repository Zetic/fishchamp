﻿using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Modules;
using FishChamp.Tracker;
using FishChamp.Services;
using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Interactivity;
using Remora.Results;
using System.Drawing;
using System.Text;
using FishChamp.Events;

namespace FishChamp.Minigames.Fishing;

public class FishingInteractionGroup(
    IDiscordRestInteractionAPI interactionAPI,
    IInteractionContext context,
    IPlayerRepository playerRepository,
    IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository,
    IInstanceTracker<FishingInstance> fishingTracker,
    IAreaUnlockService areaUnlockService,
    IEventBus eventBus,
    IEventRepository eventRepository,
    IServiceProvider services) : InteractionGroup
{
    public const string CastLine = "fish_cast_line";
    public const string ReelIn = "fish_reel_in";
    public const string LetGo = "fish_let_go";
    public const string StopReel = "fish_stop_reel";

    [Button(CastLine)]
    public async Task<IResult> CastLineAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot cast a line on someone else's fishing spot!",
                    flags: MessageFlags.Ephemeral);
            }
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
            return Result.FromSuccess();

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
            return Result.FromSuccess();

        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f =>
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
        {
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                "🎣 You need to be at a fishing spot first! Use `/map goto <fishing spot>` to go to one.",
                flags: MessageFlags.Ephemeral);
        }

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
        await interactionAPI.EditFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            context.Interaction.Message.Value.ID,
            embeds: new Remora.Rest.Core.Optional<IReadOnlyList<IEmbed>?>([waitingEmbed]),
            components: new Remora.Rest.Core.Optional<IReadOnlyList<IMessageComponent>?>([]));

        // Wait for the random amount of time
        await Task.Delay(waitTime);

        // Phase 11 addition - Check for active event effects
        var eventModifiers = await GetActiveEventModifiersAsync();
        var biteRateBonus = eventModifiers.GetValueOrDefault("bite_rate_bonus", 0.0);

        // Generate random fishing event (with event modifiers)
        var baseEventRoll = random.NextDouble();
        var adjustedEventRoll = baseEventRoll * (1.0 - biteRateBonus); // Lower roll = better chance

        string eventText;
        bool canCatch = false;

        if (adjustedEventRoll < 0.15) // 15% - Line snapped
        {
            eventText = "💥 **Line snapped!**\nYour fishing line broke! Maybe try with better equipment next time.";
        }
        else if (adjustedEventRoll < 0.3) // 15% - Fish escaped
        {
            eventText = "🐟 **Fish escaped!**\nA fish took the bait but swam away before you could react!";
        }
        else if (adjustedEventRoll < 0.5) // 20% - Nothing
        {
            eventText = "💧 **Nothing...**\nThe waters are quiet. No fish seem interested in your bait.";
        }
        else // 50%+ - Bite! (improved with bite rate bonus)
        {
            var eventBonus = biteRateBonus > 0 ? "\n🎣 *Event bonus active!*" : "";
            eventText = "🐟 **You feel a tug on the line...**\nSomething's biting! What will you do?" + eventBonus;
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
            var reelButton = new ButtonComponent(ButtonComponentStyle.Success, "Reel In", new PartialEmoji(Name: "🎯"), CustomIDHelpers.CreateButtonID(ReelIn));
            var letGoButton = new ButtonComponent(ButtonComponentStyle.Secondary, "Let it Go", new PartialEmoji(Name: "🪝"), CustomIDHelpers.CreateButtonID(LetGo));
            components.Add(new ActionRowComponent([reelButton, letGoButton]));
        }
        else
        {
            // Add try again button
            var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), CustomIDHelpers.CreateButtonID(CastLine));
            components.Add(new ActionRowComponent([tryAgainButton]));
        }

        return await interactionAPI.EditOriginalInteractionResponseAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            embeds: new[] { resultEmbed },
            components: components);
    }

    [Button(ReelIn)]
    public async Task<IResult> ReelInAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot reel in on someone else's fishing spot!",
                    flags: MessageFlags.Ephemeral);
            }
        }

        StringBuilder builder = new StringBuilder();

        for (int i = FishingInstance.MaxWater; i >= 0; i--)
        {
            if (i == 0)
            {
                builder.Append("<:newfish2:1378160844864487504>");
            }
            else
            {
                builder.Append(":blue_square:");
            }
        }


        // Start the timing mini-game
        var timingEmbed = new Embed
        {
            Title = "🎯 Timing Challenge!",
            Description = $"Click **Stop Reel** when the :fish: is in the center!\n\n{builder}",
            Colour = Color.Yellow,
            Footer = new EmbedFooter("Quick! Don't let the fish escape!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        var stopButton = new ButtonComponent(ButtonComponentStyle.Danger, "Stop Reel", new PartialEmoji(Name: "🛑"), CustomIDHelpers.CreateButtonID(StopReel));
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent([stopButton])
        };

        var response = await interactionAPI.EditOriginalInteractionResponseAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            embeds: new[] { timingEmbed },
            components: components);

        FishingInstance fishingInstance = ActivatorUtilities.CreateInstance<FishingInstance>(services);
        fishingInstance.Context = context;
        fishingTracker.Add(user.ID, fishingInstance);

        return response;
    }

    [Button(LetGo)]
    public async Task<IResult> LetGoAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot let go on someone else's fishing spot!",
                    flags: MessageFlags.Ephemeral);
            }
        }

        var letGoEmbed = new Embed
        {
            Title = "🪝 Released",
            Description = "You decided to let the fish go. Sometimes it's better to be patient!\n\nTry casting again when you're ready.",
            Colour = Color.Gray,
            Timestamp = DateTimeOffset.UtcNow
        };

        var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), CustomIDHelpers.CreateButtonID(CastLine));
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent([tryAgainButton])
        };

        return await interactionAPI.EditOriginalInteractionResponseAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            embeds: new[] { letGoEmbed },
            components: components);
    }

    [Button(StopReel)]
    public async Task<IResult> StopReelAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        if (context.Interaction.Message.HasValue && context.Interaction.Message.Value.Interaction.HasValue)
        {
            if (user.ID != context.Interaction.Message.Value.Interaction.Value.User.ID)
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    "You cannot stop reeling on someone else's fishing spot!",
                    flags: MessageFlags.Ephemeral);
            }
        }

        float timingPerecent = 0;
        if (fishingTracker.TryRemove(user.ID, out var fishingInstance))
        {
            timingPerecent = fishingInstance.GetFishPositionPercent();
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
            return Result.FromSuccess();

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
            return Result.FromSuccess();

        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f =>
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
            return Result.FromSuccess();

        // Simulate timing success/failure
        var timingSuccess = Random.Shared.NextDouble() < 0.9 * timingPerecent; // 90% chance of success if perfectly timed, less if not

        if (!timingSuccess)
        {
            var failEmbed = new Embed
            {
                Title = "🐟 Fish Escaped!",
                Description = "The fish got away!\n\nBetter luck next time!",
                Colour = Color.Red,
                Timestamp = DateTimeOffset.UtcNow
            };

            var tryAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), CustomIDHelpers.CreateButtonID(CastLine));
            var components = new List<IMessageComponent>
            {
                new ActionRowComponent(new[] { (IMessageComponent)tryAgainButton })
            };

            return await interactionAPI.EditOriginalInteractionResponseAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                embeds: new[] { failEmbed },
                components: components);

        }

        // Success! Now simulate the actual fishing logic from the original cast command
        return await SimulateFishCatch(player, fishingSpot, timingPerecent);
    }

    private async Task<IResult> SimulateFishCatch(PlayerProfile player, FishingSpot fishingSpot, float timingPercent)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        var random = new Random();

        var availableFish = fishingSpot.AvailableFish;
        if (availableFish.Count == 0)
        {
            return await interactionAPI.EditOriginalInteractionResponseAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                embeds: new[] { new Embed("No fish :(") });
        }

        // Get equipped rod and bait
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        var equippedRod = inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedRod && i.ItemType == "Rod");
        var equippedBait = !string.IsNullOrEmpty(player.EquippedBait) ?
                          inventory?.Items.FirstOrDefault(i => i.ItemId == player.EquippedBait && i.ItemType == "Bait") : null;

        // Clean up expired buffs
        player.ActiveBuffs.RemoveAll(b => DateTime.UtcNow >= b.ExpiresAt);

        // Calculate buff modifiers
        double catchRateBonus = 0.0;
        double rareBonus = 0.0;
        double xpBonus = 0.0;
        double traitBonus = 0.0;

        foreach (var buff in player.ActiveBuffs)
        {
            if (buff.Effects.ContainsKey("catch_rate_bonus"))
                catchRateBonus += (double)buff.Effects["catch_rate_bonus"];
            if (buff.Effects.ContainsKey("rare_fish_bonus"))
                rareBonus += (double)buff.Effects["rare_fish_bonus"];
            if (buff.Effects.ContainsKey("xp_bonus"))
                xpBonus += (double)buff.Effects["xp_bonus"];
            if (buff.Effects.ContainsKey("trait_discovery_bonus"))
                traitBonus += (double)buff.Effects["trait_discovery_bonus"];
        }

        // Calculate success chance and fish details (simplified version of original logic)
        var potentialCatch = availableFish[random.Next(availableFish.Count)];

        // Phase 11 addition - Check for event fish spawning
        var eventModifiers = await GetActiveEventModifiersAsync();
        var eventFish = await GetAvailableEventFishAsync();
        if (eventFish.Any())
        {
            var eventFishRoll = random.NextDouble();
            var eventSpawnBonus = eventModifiers.GetValueOrDefault("eerie_fish_spawn", 0.0) +
                                eventModifiers.GetValueOrDefault("storm_fish_spawn", 0.0) +
                                eventModifiers.GetValueOrDefault("cosmic_fish_everywhere", 0.0);

            if (eventFishRoll < eventSpawnBonus)
            {
                var selectedEventFish = eventFish[random.Next(eventFish.Count)];
                potentialCatch = selectedEventFish.FishId;
            }
        }

        // Determine fish rarity (with rare fish bonus from meals)
        var rarityRoll = random.NextDouble() * (1.0 - rareBonus); // Lower roll means better chance
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

        await inventoryRepository.AddItemAsync(user.ID.Value, fishItem);

        // Update player stats (with XP bonus from meals)
        int baseXp = rarity switch { "uncommon" => 20, "rare" => 40, "epic" => 70, _ => 10 };
        int xpGained = (int)Math.Round(baseXp * (1.0 + xpBonus));
        player.Experience += xpGained;
        player.LastActive = DateTime.UtcNow;

        if (!player.BiggestCatch.TryGetValue(fishItem.ItemId, out var existingRecord) || fishWeight > existingRecord)
        {
            player.BiggestCatch[fishItem.ItemId] = fishWeight;
        }

        await playerRepository.UpdatePlayerAsync(player);

        // Publish OnFishCatch event for other systems (like tournaments) to handle
        var fishCatchEvent = new OnFishCatchEvent(
            user.ID.Value,
            user.Username,
            fishItem,
            fishWeight,
            player.CurrentArea,
            player.CurrentFishingSpot,
            timingPercent
        );
        await eventBus.PublishAsync(fishCatchEvent);

        // Check for newly unlocked areas
        var newlyUnlockedAreas = await areaUnlockService.CheckAndUnlockAreasAsync(player);
        var unlockMessage = newlyUnlockedAreas.Count > 0
            ? $"\n\n🗺️ **New area{(newlyUnlockedAreas.Count > 1 ? "s" : "")} unlocked:** {string.Join(", ", newlyUnlockedAreas)}!"
            : "";

        // Get rarity emoji
        string rarityEmoji = rarity switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵",
            "epic" => "🟣",
            _ => "⚪"
        };

        static string GetTimingMessage(float timing)
        {
            string[] messages = timing switch
            {
                >= 0.95f => new[] {
                "Perfect catch!", "Bullseye!", "Flawless!"
            },
                >= 0.85f => new[] {
                "Great timing!", "Right on!", "Sharp reflexes!"
            },
                >= 0.7f => new[] {
                "Solid!", "Nice one!", "You’re getting there."
            },
                >= 0.5f => new[] {
                "Just in time!", "Cutting it close.", "Barely made it."
            },
                >= 0.4f => new[] {
                "So close!", "Almost!", "That slipped."
            },
                >= 0.2f => new[] {
                "Way off.", "Try again.", "Off tempo."
            },
                _ => new[] {
                "Total miss.", "Ouch.", "Epic fail."
            }
            };

            return messages[Random.Shared.Next(messages.Length)];
        }

        var successEmbed = new Embed
        {
            Title = $"🎣 {GetTimingMessage(timingPercent)}",
            Description = $"**Success!** You caught a {rarityEmoji} {fishItem.Name}!\n\n" +
                         $"Size: {fishItem.Properties["size"]}cm | Weight: {fishItem.Properties["weight"]}g (+{xpGained} XP){unlockMessage}",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        var castAgainButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Again", new PartialEmoji(Name: "🎣"), CustomIDHelpers.CreateButtonID(CastLine));
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent([castAgainButton ])
        };

        return await interactionAPI.EditOriginalInteractionResponseAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            embeds: new[] { successEmbed },
            components: components);
    }

    private static string FormatFishName(string fishId)
    {
        return fishId.Replace("_", " ").ToTitleCase();
    }

    // Phase 11 addition - Helper methods for event integration
    private async Task<Dictionary<string, double>> GetActiveEventModifiersAsync()
    {
        var activeEvents = await eventRepository.GetActiveEventsAsync();
        var combinedModifiers = new Dictionary<string, double>();

        foreach (var ev in activeEvents)
        {
            foreach (var modifier in ev.EffectModifiers)
            {
                if (combinedModifiers.ContainsKey(modifier.Key))
                {
                    combinedModifiers[modifier.Key] += modifier.Value;
                }
                else
                {
                    combinedModifiers[modifier.Key] = modifier.Value;
                }
            }

            // Add phase-specific modifiers for multi-phase events
            if (ev.Phases.Any() && ev.CurrentPhase < ev.Phases.Count)
            {
                var currentPhase = ev.Phases[ev.CurrentPhase];
                foreach (var modifier in currentPhase.PhaseModifiers)
                {
                    if (combinedModifiers.ContainsKey(modifier.Key))
                    {
                        combinedModifiers[modifier.Key] += modifier.Value;
                    }
                    else
                    {
                        combinedModifiers[modifier.Key] = modifier.Value;
                    }
                }
            }
        }

        return combinedModifiers;
    }

    private async Task<List<EventFish>> GetAvailableEventFishAsync()
    {
        var activeEvents = await eventRepository.GetActiveEventsAsync();
        var eventFish = new List<EventFish>();

        foreach (var ev in activeEvents)
        {
            eventFish.AddRange(ev.SpecialFish);

            // Add phase-specific fish for multi-phase events
            if (ev.Phases.Any() && ev.CurrentPhase < ev.Phases.Count)
            {
                var currentPhase = ev.Phases[ev.CurrentPhase];
                eventFish.AddRange(currentPhase.PhaseFish);
            }
        }

        return eventFish;
    }
}
