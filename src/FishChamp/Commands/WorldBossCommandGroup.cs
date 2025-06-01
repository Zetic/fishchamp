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

[Group("boss")]
[Description("World boss encounter commands")]
public class WorldBossCommandGroup(IInteractionContext context,
    IEventRepository eventRepository, FeedbackService feedbackService) : CommandGroup
{
    [Command("join")]
    [Description("Join an active world boss encounter")]
    public async Task<IResult> JoinBossAsync([Description("Boss ID")] string bossId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var boss = await eventRepository.GetWorldBossAsync(bossId);
        if (boss == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 World boss not found!", Color.Red);
        }

        if (boss.Status == BossStatus.Defeated || boss.Status == BossStatus.Escaped || boss.Status == BossStatus.Failed)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This boss encounter has already ended!", Color.Red);
        }

        if (boss.Participants.Contains(user.ID.Value))
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are already participating in this boss fight!", Color.Red);
        }

        // Add player to participants
        boss.Participants.Add(user.ID.Value);
        boss.PlayerContributions[user.ID.Value] = 0;

        // Check if we have enough players to start the fight
        if (boss.Participants.Count >= boss.RequiredPlayers && boss.Status == BossStatus.Waiting)
        {
            boss.Status = BossStatus.Active;
            boss.StartTime = DateTime.UtcNow;
        }

        await eventRepository.UpdateWorldBossAsync(boss);

        var embed = new Embed
        {
            Title = $"⚔️ Joined Boss Fight!",
            Description = $"You have joined the battle against **{boss.Name}** {boss.BossEmoji}!\n\n" +
                         $"**Participants:** {boss.Participants.Count}/{boss.RequiredPlayers}\n" +
                         $"**Boss Health:** {boss.CurrentHealth}/{boss.MaxHealth}\n" +
                         $"**Status:** {boss.Status}\n\n" +
                         (boss.Status == BossStatus.Active 
                             ? "The battle has begun! Use `/boss strike`, `/boss hook`, or `/boss weaken` to attack!"
                             : "Waiting for more participants..."),
            Colour = boss.Status == BossStatus.Active ? Color.Red : Color.Orange,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("strike")]
    [Description("Perform a powerful strike attack on the world boss")]
    public async Task<IResult> StrikeAsync([Description("Boss ID")] string bossId)
    {
        return await PerformBossAction(bossId, "strike", 45, 65);
    }

    [Command("hook")]
    [Description("Use your fishing hook as a weapon against the world boss")]
    public async Task<IResult> HookAsync([Description("Boss ID")] string bossId)
    {
        return await PerformBossAction(bossId, "hook", 25, 40);
    }

    [Command("weaken")]
    [Description("Attempt to weaken the world boss's defenses")]
    public async Task<IResult> WeakenAsync([Description("Boss ID")] string bossId)
    {
        return await PerformBossAction(bossId, "weaken", 15, 30);
    }

    private async Task<IResult> PerformBossAction(string bossId, string actionType, int minDamage, int maxDamage)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var boss = await eventRepository.GetWorldBossAsync(bossId);
        if (boss == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 World boss not found!", Color.Red);
        }

        if (boss.Status != BossStatus.Active)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This boss fight is not currently active!", Color.Red);
        }

        if (!boss.Participants.Contains(user.ID.Value))
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are not participating in this boss fight! Use `/boss join` first.", Color.Red);
        }

        // Calculate damage
        var damage = Random.Shared.Next(minDamage, maxDamage + 1);
        boss.CurrentHealth = Math.Max(0, boss.CurrentHealth - damage);
        boss.PlayerContributions[user.ID.Value] += damage;

        // Check if boss is defeated
        bool bossDefeated = boss.CurrentHealth <= 0;
        if (bossDefeated)
        {
            boss.Status = BossStatus.Defeated;
            boss.EndTime = DateTime.UtcNow;
        }

        await eventRepository.UpdateWorldBossAsync(boss);

        var actionEmoji = actionType switch
        {
            "strike" => "⚔️",
            "hook" => "🪝",
            "weaken" => "💀",
            _ => "⚡"
        };

        var embed = new Embed
        {
            Title = bossDefeated ? "🎉 Boss Defeated!" : $"{actionEmoji} {actionType.ToUpper()}!",
            Description = bossDefeated
                ? $"**{boss.Name}** has been defeated by the combined efforts of all participants!\n\n" +
                  $"**Your Contribution:** {boss.PlayerContributions[user.ID.Value]} damage\n" +
                  $"**Total Participants:** {boss.Participants.Count}\n\n" +
                  "Rewards will be distributed based on participation!"
                : $"You performed a {actionType} attack for **{damage}** damage!\n\n" +
                  $"**Boss Health:** {boss.CurrentHealth}/{boss.MaxHealth}\n" +
                  $"**Your Total Damage:** {boss.PlayerContributions[user.ID.Value]}\n\n" +
                  "Keep attacking to defeat the boss!",
            Colour = bossDefeated ? Color.Gold : Color.Red,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("status")]
    [Description("Check the status of a world boss encounter")]
    public async Task<IResult> BossStatusAsync([Description("Boss ID")] string bossId)
    {
        var boss = await eventRepository.GetWorldBossAsync(bossId);
        if (boss == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 World boss not found!", Color.Red);
        }

        var healthPercent = boss.MaxHealth > 0 ? (boss.CurrentHealth * 100) / boss.MaxHealth : 0;
        var statusIcon = boss.Status switch
        {
            BossStatus.Waiting => "⏳",
            BossStatus.Active => "⚔️",
            BossStatus.Defeated => "💀",
            BossStatus.Escaped => "🏃",
            BossStatus.Failed => "❌",
            _ => "❓"
        };

        var description = $"{statusIcon} **Status:** {boss.Status}\n" +
                         $"🩸 **Health:** {boss.CurrentHealth}/{boss.MaxHealth} ({healthPercent}%)\n" +
                         $"👥 **Participants:** {boss.Participants.Count}/{boss.RequiredPlayers}\n";

        if (boss.Status == BossStatus.Active)
        {
            var timeRemaining = boss.StartTime.AddMinutes(30) - DateTime.UtcNow;
            if (timeRemaining.TotalMinutes > 0)
            {
                description += $"⏰ **Time Remaining:** {timeRemaining.Minutes}m {timeRemaining.Seconds}s\n";
            }
        }

        if (boss.PlayerContributions.Any())
        {
            description += "\n**Top Contributors:**\n";
            var topContributors = boss.PlayerContributions
                .OrderByDescending(kv => kv.Value)
                .Take(5);

            foreach (var contributor in topContributors)
            {
                description += $"• <@{contributor.Key}>: {contributor.Value} damage\n";
            }
        }

        var embed = new Embed
        {
            Title = $"{boss.BossEmoji} {boss.Name}",
            Description = boss.Description + "\n\n" + description,
            Colour = boss.Status switch
            {
                BossStatus.Active => Color.Red,
                BossStatus.Defeated => Color.Gold,
                BossStatus.Waiting => Color.Orange,
                _ => Color.Gray
            },
            Footer = new EmbedFooter($"Boss ID: {boss.BossId}"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }
}