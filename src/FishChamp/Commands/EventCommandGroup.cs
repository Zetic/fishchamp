using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using Remora.Discord.Commands.Feedback.Services;
using FishChamp.Helpers;

namespace FishChamp.Modules;

[Group("event")]
[Description("Seasonal event commands")]
public class EventCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IEventRepository eventRepository,
    IInventoryRepository inventoryRepository, DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("list")]
    [Description("List active and upcoming seasonal events")]
    public async Task<IResult> ListEventsAsync()
    {
        var activeEvents = await eventRepository.GetActiveEventsAsync();
        var upcomingEvents = await eventRepository.GetUpcomingEventsAsync();

        if (!activeEvents.Any() && !upcomingEvents.Any())
        {
            return await feedbackService.SendContextualContentAsync("🎃 No seasonal events are currently running!", Color.Yellow);
        }

        var description = "";

        if (activeEvents.Any())
        {
            description += "**🎉 Active Events:**\n";
            foreach (var ev in activeEvents.Take(3))
            {
                var timeLeft = ev.EndDate - DateTime.UtcNow;
                var emoji = GetSeasonEmoji(ev.Season);
                description += $"{emoji} **{ev.Name}**\n" +
                              $"  {ev.Description}\n" +
                              $"  Ends in: {timeLeft.Days}d {timeLeft.Hours}h\n\n";
            }
        }

        if (upcomingEvents.Any())
        {
            description += "**📅 Upcoming Events:**\n";
            foreach (var ev in upcomingEvents.Take(2))
            {
                var timeUntil = ev.StartDate - DateTime.UtcNow;
                var emoji = GetSeasonEmoji(ev.Season);
                description += $"{emoji} **{ev.Name}**\n" +
                              $"  Starts in: {timeUntil.Days}d {timeUntil.Hours}h\n\n";
            }
        }

        var embed = new Embed
        {
            Title = "🎪 Seasonal Events",
            Description = description,
            Colour = Color.Purple,
            Footer = new EmbedFooter("Use /event info <event_id> for more details"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("info")]
    [Description("Get detailed information about a specific event")]
    public async Task<IResult> ViewEventInfoAsync([Description("Event ID")] string eventId)
    {
        var seasonalEvent = await eventRepository.GetEventAsync(eventId);
        if (seasonalEvent == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Event not found!", Color.Red);
        }

        var emoji = GetSeasonEmoji(seasonalEvent.Season);
        var statusColor = seasonalEvent.Status switch
        {
            EventStatus.Active => Color.Green,
            EventStatus.Upcoming => Color.Orange,
            EventStatus.Ended => Color.Gray,
            _ => Color.Purple
        };

        var timeInfo = seasonalEvent.Status switch
        {
            EventStatus.Active => $"Ends: {seasonalEvent.EndDate:yyyy-MM-dd HH:mm} UTC",
            EventStatus.Upcoming => $"Starts: {seasonalEvent.StartDate:yyyy-MM-dd HH:mm} UTC",
            EventStatus.Ended => "Event has ended",
            _ => seasonalEvent.Status.ToString()
        };

        // Phase 11 addition - Show current phase info for multi-phase events
        string phaseInfo = "";
        if (seasonalEvent.Phases.Any() && seasonalEvent.Status == EventStatus.Active)
        {
            if (seasonalEvent.CurrentPhase < seasonalEvent.Phases.Count)
            {
                var currentPhase = seasonalEvent.Phases[seasonalEvent.CurrentPhase];
                phaseInfo = $"\n**Current Phase:** {currentPhase.PhaseEmoji} {currentPhase.Name}\n{currentPhase.Description}";
            }
        }

        // Show active event effects
        string effectsInfo = "";
        if (seasonalEvent.EffectModifiers.Any())
        {
            effectsInfo = "\n**Active Effects:**\n" + 
                         string.Join("\n", seasonalEvent.EffectModifiers.Take(3).Select(e => 
                             $"• {FormatEffectName(e.Key)}: {FormatEffectValue(e.Value)}"));
        }

        var specialFishText = seasonalEvent.SpecialFish.Any()
            ? string.Join("\n", seasonalEvent.SpecialFish.Take(5).Select(f => $"• {f.SpecialEmoji ?? "🐟"} {f.Name} ({f.Rarity})"))
            : "No special fish";

        var specialItemsText = seasonalEvent.SpecialItems.Any()
            ? string.Join("\n", seasonalEvent.SpecialItems.Take(5).Select(i => $"• {i.SpecialEmoji ?? "✨"} {i.Name} ({i.Rarity})"))
            : "No special items";

        var rewardsText = seasonalEvent.Rewards.Any()
            ? string.Join("\n", seasonalEvent.Rewards.Take(3).Select(r => $"• {r.Name} ({r.Type})"))
            : "No rewards available";

        var embed = new Embed
        {
            Title = $"{emoji} {seasonalEvent.Name}",
            Description = seasonalEvent.Description + phaseInfo + effectsInfo,
            Fields = new List<EmbedField>
            {
                new("📅 Schedule", timeInfo, true),
                new("🎪 Season", seasonalEvent.Season.ToString(), true),
                new("⚡ Type", seasonalEvent.Type.ToString(), true),
                new("🐟 Special Fish", specialFishText, true),
                new("🎁 Special Items", specialItemsText, true),
                new("🏆 Rewards", rewardsText, false)
            },
            Colour = statusColor,
            Footer = new EmbedFooter($"Event ID: {seasonalEvent.EventId}"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("join")]
    [Description("Join an active seasonal event")]
    public async Task<IResult> JoinEventAsync([Description("Event ID")] string eventId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var seasonalEvent = await eventRepository.GetEventAsync(eventId);
        if (seasonalEvent == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Event not found!", Color.Red);
        }

        if (seasonalEvent.Status != EventStatus.Active)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This event is not currently active!", Color.Red);
        }

        // Check if already participating
        var existingParticipation = await eventRepository.GetEventParticipationAsync(eventId, user.ID.Value);
        if (existingParticipation != null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are already participating in this event!", Color.Red);
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        var participation = new EventParticipation
        {
            EventId = eventId,
            UserId = user.ID.Value,
            Username = user.Username
        };

        await eventRepository.CreateEventParticipationAsync(participation);

        // Add event to player's participation list
        if (!player.EventParticipations.Contains(eventId))
        {
            player.EventParticipations.Add(eventId);
            await playerRepository.UpdatePlayerAsync(player);
        }

        var emoji = GetSeasonEmoji(seasonalEvent.Season);
        var embed = new Embed
        {
            Title = $"{emoji} Event Joined!",
            Description = $"Successfully joined **{seasonalEvent.Name}**!\n\n" +
                         $"**Season:** {seasonalEvent.Season}\n" +
                         $"**Description:** {seasonalEvent.Description}\n\n" +
                         $"Start fishing to participate in the event! 🎣",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("progress")]
    [Description("View your progress in active events")]
    public async Task<IResult> ViewEventProgressAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var participations = await eventRepository.GetUserEventParticipationsAsync(user.ID.Value);
        var activeParticipations = new List<EventParticipation>();

        foreach (var participation in participations)
        {
            var seasonalEvent = await eventRepository.GetEventAsync(participation.EventId);
            if (seasonalEvent?.Status == EventStatus.Active)
            {
                activeParticipations.Add(participation);
            }
        }

        if (!activeParticipations.Any())
        {
            return await feedbackService.SendContextualContentAsync("📊 You are not participating in any active events!", Color.Yellow);
        }

        var description = "**Your Event Progress:**\n\n";

        foreach (var participation in activeParticipations.Take(3))
        {
            var seasonalEvent = await eventRepository.GetEventAsync(participation.EventId);
            if (seasonalEvent == null) continue;

            var emoji = GetSeasonEmoji(seasonalEvent.Season);
            description += $"{emoji} **{seasonalEvent.Name}**\n" +
                          $"  Points: {participation.EventPoints}\n" +
                          $"  Rewards Claimed: {participation.ClaimedRewards.Count}\n\n";

            if (participation.EventProgress.Any())
            {
                foreach (var progress in participation.EventProgress.Take(3))
                {
                    description += $"  • {progress.Key}: {progress.Value}\n";
                }
                description += "\n";
            }
        }

        var embed = new Embed
        {
            Title = "📊 Event Progress",
            Description = description,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Keep fishing to earn more event points!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("rewards")]
    [Description("View and claim available event rewards")]
    public async Task<IResult> ViewEventRewardsAsync([Description("Event ID")] string eventId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var seasonalEvent = await eventRepository.GetEventAsync(eventId);
        if (seasonalEvent == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Event not found!", Color.Red);
        }

        var participation = await eventRepository.GetEventParticipationAsync(eventId, user.ID.Value);
        if (participation == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are not participating in this event!", Color.Red);
        }

        if (!seasonalEvent.Rewards.Any())
        {
            return await feedbackService.SendContextualContentAsync("🎁 This event has no rewards available!", Color.Yellow);
        }

        var description = "**Available Rewards:**\n\n";

        foreach (var reward in seasonalEvent.Rewards)
        {
            var canClaim = CanClaimReward(reward, participation);
            var status = participation.ClaimedRewards.Contains(reward.RewardId) ? "✅ Claimed" : 
                        canClaim ? "🎁 Available" : "🔒 Locked";

            description += $"**{reward.Name}** - {status}\n" +
                          $"  Type: {reward.Type}\n" +
                          $"  Requirement: {GetRequirementText(reward)}\n";

            if (reward.FishCoins > 0)
                description += $"  Coins: {reward.FishCoins}\n";
            if (reward.Items.Any())
                description += $"  Items: {string.Join(", ", reward.Items.Select(i => i.Name))}\n";
            if (!string.IsNullOrEmpty(reward.SpecialTitle))
                description += $"  Title: {reward.SpecialTitle}\n";

            description += "\n";
        }

        var embed = new Embed
        {
            Title = "🎁 Event Rewards",
            Description = description,
            Colour = Color.Gold,
            Footer = new EmbedFooter("Use /event claim <reward_id> to claim available rewards"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("claim")]
    [Description("Claim an event reward")]
    public async Task<IResult> ClaimEventRewardAsync(
        [Description("Event ID")] string eventId,
        [Description("Reward ID")] string rewardId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var seasonalEvent = await eventRepository.GetEventAsync(eventId);
        if (seasonalEvent == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Event not found!", Color.Red);
        }

        var participation = await eventRepository.GetEventParticipationAsync(eventId, user.ID.Value);
        if (participation == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are not participating in this event!", Color.Red);
        }

        var reward = seasonalEvent.Rewards.FirstOrDefault(r => r.RewardId == rewardId);
        if (reward == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Reward not found!", Color.Red);
        }

        if (participation.ClaimedRewards.Contains(rewardId))
        {
            return await feedbackService.SendContextualContentAsync("🚫 You have already claimed this reward!", Color.Red);
        }

        if (!CanClaimReward(reward, participation))
        {
            return await feedbackService.SendContextualContentAsync("🚫 You don't meet the requirements for this reward!", Color.Red);
        }

        // Give rewards to player
        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player != null && reward.FishCoins > 0)
        {
            player.FishCoins += reward.FishCoins;
            await playerRepository.UpdatePlayerAsync(player);
        }

        // Add items to inventory
        foreach (var item in reward.Items)
        {
            var inventoryItem = new InventoryItem
            {
                ItemId = item.ItemId,
                ItemType = item.ItemType,
                Name = item.Name,
                Quantity = item.Quantity,
                Properties = item.Properties
            };
            await inventoryRepository.AddItemAsync(user.ID.Value, inventoryItem);
        }

        // Mark reward as claimed
        participation.ClaimedRewards.Add(rewardId);
        await eventRepository.UpdateEventParticipationAsync(participation);

        var rewardText = "";
        if (reward.FishCoins > 0)
            rewardText += $"💰 {reward.FishCoins} Fish Coins\n";
        if (reward.Items.Any())
            rewardText += $"🎁 {string.Join(", ", reward.Items.Select(i => $"{i.Quantity}x {i.Name}"))}\n";
        if (!string.IsNullOrEmpty(reward.SpecialTitle))
            rewardText += $"🏆 Title: {reward.SpecialTitle}\n";

        var embed = new Embed
        {
            Title = "🎉 Reward Claimed!",
            Description = $"Successfully claimed **{reward.Name}**!\n\n{rewardText}",
            Colour = Color.Green,
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
        }
        return player;
    }

    private static string GetSeasonEmoji(EventSeason season)
    {
        return season switch
        {
            EventSeason.Spring => "🌸",
            EventSeason.Summer => "☀️",
            EventSeason.Autumn => "🍂",
            EventSeason.Winter => "❄️",
            EventSeason.Halloween => "🎃",
            EventSeason.Christmas => "🎄",
            EventSeason.Easter => "🐰",
            EventSeason.Special => "✨",
            _ => "🎪"
        };
    }

    private static bool CanClaimReward(EventReward reward, EventParticipation participation)
    {
        return reward.RequirementType switch
        {
            RequirementType.CatchEventFish => participation.EventProgress.GetValueOrDefault("event_fish_caught", 0) >= reward.RequiredAmount,
            RequirementType.CatchAnyFish => participation.EventProgress.GetValueOrDefault("total_fish_caught", 0) >= reward.RequiredAmount,
            RequirementType.EarnEventPoints => participation.EventPoints >= reward.RequiredAmount,
            RequirementType.LoginDaily => participation.EventProgress.GetValueOrDefault("daily_logins", 0) >= reward.RequiredAmount,
            RequirementType.UseEventItem => participation.EventProgress.GetValueOrDefault("event_items_used", 0) >= reward.RequiredAmount,
            RequirementType.CompleteObjective => participation.EventProgress.GetValueOrDefault("objectives_completed", 0) >= reward.RequiredAmount,
            _ => true
        };
    }

    private static string GetRequirementText(EventReward reward)
    {
        return reward.RequirementType switch
        {
            RequirementType.CatchEventFish => $"Catch {reward.RequiredAmount} event fish",
            RequirementType.CatchAnyFish => $"Catch {reward.RequiredAmount} fish",
            RequirementType.EarnEventPoints => $"Earn {reward.RequiredAmount} event points",
            RequirementType.LoginDaily => $"Login {reward.RequiredAmount} days",
            RequirementType.UseEventItem => $"Use event items {reward.RequiredAmount} times",
            RequirementType.CompleteObjective => $"Complete {reward.RequiredAmount} objectives",
            _ => "Participation"
        };
    }

    // Phase 11 additions - Helper methods for formatting
    private static string FormatEffectName(string effectKey)
    {
        return effectKey switch
        {
            "bite_rate_bonus" => "Bite Rate Bonus",
            "all_rods_anywhere" => "All Rods Work Anywhere",
            "eerie_fish_spawn" => "Eerie Fish Spawn Rate",
            "storm_fish_spawn" => "Storm Fish Spawn Rate",
            "lightning_bonus" => "Lightning Bonus",
            "reward_bonus" => "Reward Bonus",
            "dock_fishing_disabled" => "Dock Fishing Disabled",
            "boat_fishing_required" => "Boat Fishing Required",
            "flood_fish_bonus" => "Flood Fish Bonus",
            "lava_zone_opened" => "Lava Zone Opened",
            "heat_proof_required" => "Heat-Proof Rods Required",
            "volcanic_fish_bonus" => "Volcanic Fish Bonus",
            "cosmic_fish_everywhere" => "Cosmic Fish in All Zones",
            "stellar_bonus" => "Stellar Bonus",
            "zone_restrictions_removed" => "Zone Restrictions Removed",
            _ => effectKey
        };
    }

    private static string FormatEffectValue(double value)
    {
        if (value == 1.0)
            return "Active";
        else if (value > 0 && value < 1.0)
            return $"+{value * 100:F0}%";
        else if (value > 1.0)
            return $"+{(value - 1.0) * 100:F0}%";
        else
            return value.ToString("F2");
    }

    // World Boss Commands
    [Command("bosses")]
    [Description("List active world boss encounters")]
    public async Task<IResult> ListWorldBossesAsync()
    {
        var activeBosses = await eventRepository.GetActiveWorldBossesAsync();

        if (!activeBosses.Any())
        {
            return await feedbackService.SendContextualContentAsync("🐉 No world bosses are currently active!", Color.Yellow);
        }

        var description = "**🐉 Active World Bosses:**\n\n";

        foreach (var boss in activeBosses.Take(5))
        {
            var participants = boss.Participants.Count;
            var healthPercent = (boss.CurrentHealth * 100) / boss.MaxHealth;
            var statusIcon = boss.Status switch
            {
                BossStatus.Waiting => "⏳",
                BossStatus.Active => "⚔️",
                _ => "❓"
            };

            description += $"{statusIcon} **{boss.Name}** {boss.BossEmoji}\n" +
                          $"  Health: {healthPercent}% ({boss.CurrentHealth}/{boss.MaxHealth})\n" +
                          $"  Participants: {participants}/{boss.RequiredPlayers}\n" +
                          $"  Status: {boss.Status}\n\n";
        }

        var embed = new Embed
        {
            Title = "🐉 World Boss Encounters",
            Description = description,
            Colour = Color.Red,
            Footer = new EmbedFooter("Use /event boss <boss_id> to view details or join the fight!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("trigger")]
    [Description("Trigger a special event (admin only)")]
    public async Task<IResult> TriggerEventAsync([Description("Event type")] string eventType)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        // TODO: Add proper admin permission check
        // For now, allow anyone to trigger events for demonstration
        
        var now = DateTime.UtcNow;
        SeasonalEvent? triggeredEvent = eventType.ToLower() switch
        {
            "frenzy" => CreateFishingFrenzyEvent(now),
            "storm" => CreateDarkSkiesEvent(now),
            "flood" => CreateFloodSeasonEvent(now),
            "volcano" => CreateVolcanicUnrestEvent(now),
            "cosmic" => CreateCelestialDriftEvent(now),
            "boss" => null, // Handle world boss separately
            _ => null
        };

        if (triggeredEvent != null)
        {
            await eventRepository.CreateEventAsync(triggeredEvent);
            
            var embed = new Embed
            {
                Title = "⚡ Event Triggered!",
                Description = $"**{triggeredEvent.Name}** has been activated!\n\n{triggeredEvent.Description}",
                Colour = Color.Green,
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }
        else if (eventType.ToLower() == "boss")
        {
            return await TriggerWorldBossAsync();
        }
        else
        {
            return await feedbackService.SendContextualContentAsync(
                "🚫 Unknown event type! Available: frenzy, storm, flood, volcano, cosmic, boss", Color.Red);
        }
    }

    private async Task<IResult> TriggerWorldBossAsync()
    {
        var worldBoss = CreateAbyssalKingBoss();
        await eventRepository.CreateWorldBossAsync(worldBoss);

        var embed = new Embed
        {
            Title = "🐉 World Boss Awakened!",
            Description = $"**{worldBoss.Name}** {worldBoss.BossEmoji} has emerged from the depths!\n\n" +
                         $"{worldBoss.Description}\n\n" +
                         $"**Required Players:** {worldBoss.RequiredPlayers}\n" +
                         $"**Health:** {worldBoss.MaxHealth}\n\n" +
                         $"Use `/event join-boss {worldBoss.BossId}` to join the fight!",
            Colour = Color.DarkRed,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private static WorldBossEvent CreateAbyssalKingBoss()
    {
        return new WorldBossEvent
        {
            Name = "The Abyssal King",
            Description = "A massive leviathan from the deepest ocean trenches. Its tentacles can crush boats and its eyes glow with ancient malice.",
            RequiredPlayers = 3,
            MaxHealth = 1500,
            CurrentHealth = 1500,
            BossEmoji = "🐙",
            BossRewards = [
                new() { Name = "Abyssal Slayer", Type = RewardType.Completion, RequirementType = RequirementType.CompleteObjective, RequiredAmount = 1, FishCoins = 2000, SpecialTitle = "Abyssal Slayer" },
                new() { Name = "Leviathan's Bane", Type = RewardType.Leaderboard, RequirementType = RequirementType.CompleteObjective, RequiredAmount = 1, FishCoins = 5000, SpecialTitle = "Leviathan's Bane" }
            ]
        };
    }

    // Helper methods for creating triggered events
    private static SeasonalEvent CreateFishingFrenzyEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Fishing Frenzy",
            Description = "The fish are biting like crazy! All fishing spots are active with increased catch rates!",
            Season = EventSeason.Special,
            Type = EventType.Triggered,
            StartDate = now,
            EndDate = now.AddHours(6),
            Status = EventStatus.Active,
            SpecialEmoji = "🎣",
            EffectModifiers = new Dictionary<string, double>
            {
                ["bite_rate_bonus"] = 0.25,
                ["all_rods_anywhere"] = 1.0
            },
            Rewards = [
                new() { Name = "Frenzy Participant", Type = RewardType.Participation, RequirementType = RequirementType.CatchAnyFish, RequiredAmount = 3, FishCoins = 200 }
            ]
        };
    }

    private static SeasonalEvent CreateDarkSkiesEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Dark Skies",
            Description = "Ominous clouds gather as mysterious forces stir the waters...",
            Season = EventSeason.Special,
            Type = EventType.Triggered,
            StartDate = now,
            EndDate = now.AddHours(12),
            Status = EventStatus.Active,
            SpecialEmoji = "⚡",
            Phases = [
                new EventPhase
                {
                    Name = "Storm Gathering",
                    Description = "Dark clouds form overhead, eerie fish begin to appear",
                    Duration = TimeSpan.FromHours(4),
                    PhaseEmoji = "🌑"
                },
                new EventPhase
                {
                    Name = "Storm Peak",
                    Description = "Lightning strikes the water, storm fish emerge from the depths",
                    Duration = TimeSpan.FromHours(4),
                    PhaseEmoji = "⚡"
                },
                new EventPhase
                {
                    Name = "Calm After Storm",
                    Description = "The storm passes, leaving behind enriched waters",
                    Duration = TimeSpan.FromHours(4),
                    PhaseEmoji = "🌈"
                }
            ],
            EffectModifiers = new Dictionary<string, double>
            {
                ["storm_fish_spawn"] = 0.15
            }
        };
    }

    private static SeasonalEvent CreateFloodSeasonEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Flood Season",
            Description = "Rising waters have flooded the docks! Only boat fishing is possible.",
            Season = EventSeason.Special,
            Type = EventType.Triggered,
            StartDate = now,
            EndDate = now.AddDays(2),
            Status = EventStatus.Active,
            SpecialEmoji = "🌊",
            EffectModifiers = new Dictionary<string, double>
            {
                ["dock_fishing_disabled"] = 1.0,
                ["boat_fishing_required"] = 1.0
            }
        };
    }

    private static SeasonalEvent CreateVolcanicUnrestEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Volcanic Unrest",
            Description = "Underwater volcanic activity has opened new lava zones!",
            Season = EventSeason.Special,
            Type = EventType.Triggered,
            StartDate = now,
            EndDate = now.AddDays(3),
            Status = EventStatus.Active,
            SpecialEmoji = "🌋",
            EffectModifiers = new Dictionary<string, double>
            {
                ["lava_zone_opened"] = 1.0,
                ["heat_proof_required"] = 1.0
            }
        };
    }

    private static SeasonalEvent CreateCelestialDriftEvent(DateTime now)
    {
        return new SeasonalEvent
        {
            Name = "Celestial Drift",
            Description = "Cosmic energies flow through all fishing zones!",
            Season = EventSeason.Special,
            Type = EventType.Triggered,
            StartDate = now,
            EndDate = now.AddHours(18),
            Status = EventStatus.Active,
            SpecialEmoji = "✨",
            EffectModifiers = new Dictionary<string, double>
            {
                ["cosmic_fish_everywhere"] = 1.0,
                ["stellar_bonus"] = 0.15
            }
        };
    }
}