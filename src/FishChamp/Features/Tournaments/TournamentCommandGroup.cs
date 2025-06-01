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

namespace FishChamp.Features.Tournaments;

[Group("tournament")]
[Description("Tournament and leaderboard commands")]
public class TournamentCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, ITournamentRepository tournamentRepository,
    DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("list")]
    [Description("List active and upcoming tournaments")]
    public async Task<IResult> ListTournamentsAsync()
    {
        var activeTournaments = await tournamentRepository.GetActiveTournamentsAsync();
        var upcomingTournaments = await tournamentRepository.GetUpcomingTournamentsAsync();

        if (!activeTournaments.Any() && !upcomingTournaments.Any())
        {
            return await feedbackService.SendContextualContentAsync("🏆 No tournaments are currently available!", Color.Yellow);
        }

        var description = "";

        if (activeTournaments.Any())
        {
            description += "**🔥 Active Tournaments:**\n";
            foreach (var tournament in activeTournaments.Take(5))
            {
                var timeLeft = tournament.EndTime - DateTime.UtcNow;
                description += $"• **{tournament.Name}** ({tournament.Type})\n" +
                              $"  Ends in: {timeLeft.Days}d {timeLeft.Hours}h\n" +
                              $"  Participants: {tournament.Entries.Count}\n\n";
            }
        }

        if (upcomingTournaments.Any())
        {
            description += "**📅 Upcoming Tournaments:**\n";
            foreach (var tournament in upcomingTournaments.Take(3))
            {
                var timeUntil = tournament.StartTime - DateTime.UtcNow;
                description += $"• **{tournament.Name}** ({tournament.Type})\n" +
                              $"  Starts in: {timeUntil.Days}d {timeUntil.Hours}h\n\n";
            }
        }

        var embed = new Embed
        {
            Title = "🏆 Tournaments",
            Description = description,
            Colour = Color.Gold,
            Footer = new EmbedFooter("Use /tournament join <tournament_id> to participate"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("join")]
    [Description("Join an active tournament")]
    public async Task<IResult> JoinTournamentAsync([Description("Tournament ID")] string tournamentId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var tournament = await tournamentRepository.GetTournamentAsync(tournamentId);
        if (tournament == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Tournament not found!", Color.Red);
        }

        if (tournament.Status != TournamentStatus.Active && tournament.Status != TournamentStatus.Upcoming)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This tournament is not accepting participants!", Color.Red);
        }

        if (tournament.MaxParticipants > 0 && tournament.Entries.Count >= tournament.MaxParticipants)
        {
            return await feedbackService.SendContextualContentAsync("🚫 This tournament is full!", Color.Red);
        }

        var existingEntry = tournament.Entries.FirstOrDefault(e => e.UserId == user.ID.Value);
        if (existingEntry != null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 You are already participating in this tournament!", Color.Red);
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        var entry = new TournamentEntry
        {
            TournamentId = tournament.TournamentId,
            UserId = user.ID.Value,
            Username = user.Username
        };

        tournament.Entries.Add(entry);
        await tournamentRepository.UpdateTournamentAsync(tournament);

        var embed = new Embed
        {
            Title = "🎯 Tournament Joined!",
            Description = $"Successfully joined **{tournament.Name}**!\n\n" +
                         $"**Type:** {tournament.Type}\n" +
                         $"**Description:** {tournament.Description}\n" +
                         $"**Participants:** {tournament.Entries.Count}{(tournament.MaxParticipants > 0 ? $"/{tournament.MaxParticipants}" : "")}\n\n" +
                         $"Good luck! 🍀",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("leaderboard")]
    [Description("View leaderboards")]
    public async Task<IResult> ViewLeaderboardAsync([Description("Leaderboard type")] LeaderboardType type = LeaderboardType.RichestPlayers)
    {
        var leaderboard = await tournamentRepository.GetLeaderboardAsync(type);

        if (!leaderboard.Entries.Any())
        {
            return await feedbackService.SendContextualContentAsync($"📊 The {leaderboard.Name} leaderboard is empty!", Color.Yellow);
        }

        var description = "";
        for (int i = 0; i < Math.Min(10, leaderboard.Entries.Count); i++)
        {
            var entry = leaderboard.Entries[i];
            var rank = i + 1;
            var trophy = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"{rank}."
            };

            var scoreDisplay = type switch
            {
                LeaderboardType.RichestPlayers => $"{entry.Score:N0} coins",
                LeaderboardType.HighestLevel => $"Level {entry.Score}",
                LeaderboardType.HeaviestSingleCatch => $"{entry.Score:F2} kg",
                LeaderboardType.MostUniqueFish => $"{entry.Score} species",
                LeaderboardType.BestGuilds => $"{entry.Score} points",
                _ => entry.Score.ToString()
            };

            description += $"{trophy} **{entry.Username}** - {scoreDisplay}\n";
            if (!string.IsNullOrEmpty(entry.AdditionalInfo))
            {
                description += $"    _{entry.AdditionalInfo}_\n";
            }
        }

        var embed = new Embed
        {
            Title = $"📊 {leaderboard.Name}",
            Description = description,
            Colour = Color.Gold,
            Footer = new EmbedFooter($"Last updated: {leaderboard.LastUpdated:yyyy-MM-dd HH:mm} UTC"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("standings")]
    [Description("View tournament standings")]
    public async Task<IResult> ViewTournamentStandingsAsync([Description("Tournament ID")] string tournamentId)
    {
        var tournament = await tournamentRepository.GetTournamentAsync(tournamentId);
        if (tournament == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Tournament not found!", Color.Red);
        }

        if (!tournament.Entries.Any())
        {
            return await feedbackService.SendContextualContentAsync("📊 No participants in this tournament yet!", Color.Yellow);
        }

        // Sort entries by score (descending)
        var sortedEntries = tournament.Entries.OrderByDescending(e => e.Score).ToList();

        var description = $"**{tournament.Name}** ({tournament.Type})\n\n";

        for (int i = 0; i < Math.Min(10, sortedEntries.Count); i++)
        {
            var entry = sortedEntries[i];
            var rank = i + 1;
            var trophy = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"{rank}."
            };

            var scoreDisplay = tournament.Type switch
            {
                TournamentType.HeaviestCatch => $"{entry.Score:F2} kg",
                TournamentType.MostUniqueFish => $"{entry.Score} species",
                TournamentType.MostFishCaught => $"{entry.Score} fish",
                _ => entry.Score.ToString()
            };

            description += $"{trophy} **{entry.Username}** - {scoreDisplay}\n";
        }

        var timeStatus = tournament.Status switch
        {
            TournamentStatus.Active => $"Ends: {tournament.EndTime:yyyy-MM-dd HH:mm} UTC",
            TournamentStatus.Ended => "Tournament Ended",
            TournamentStatus.Upcoming => $"Starts: {tournament.StartTime:yyyy-MM-dd HH:mm} UTC",
            _ => tournament.Status.ToString()
        };

        var embed = new Embed
        {
            Title = "🏆 Tournament Standings",
            Description = description,
            Colour = Color.Gold,
            Footer = new EmbedFooter(timeStatus),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("create")]
    [Description("Create a new tournament (Admin only)")]
    public async Task<IResult> CreateTournamentAsync(
        [Description("Tournament name")] string name,
        [Description("Tournament type")] TournamentType type,
        [Description("Duration in hours")] int durationHours = 168,
        [Description("Max participants (0 = unlimited)")] int maxParticipants = 0)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        // Simple admin check - in a real implementation, you'd check roles or permissions
        // For now, just allow anyone to create tournaments for testing
        var startTime = DateTime.UtcNow.AddMinutes(5); // Start in 5 minutes
        var endTime = startTime.AddHours(durationHours);

        var tournament = new Tournament
        {
            Name = name,
            Description = GetTournamentDescription(type),
            Type = type,
            StartTime = startTime,
            EndTime = endTime,
            MaxParticipants = maxParticipants,
            Status = TournamentStatus.Upcoming,
            Rewards = GetDefaultRewards(type)
        };

        await tournamentRepository.CreateTournamentAsync(tournament);

        var embed = new Embed
        {
            Title = "🏆 Tournament Created!",
            Description = $"**{tournament.Name}** has been created!\n\n" +
                         $"**Type:** {tournament.Type}\n" +
                         $"**Starts:** {tournament.StartTime:yyyy-MM-dd HH:mm} UTC\n" +
                         $"**Ends:** {tournament.EndTime:yyyy-MM-dd HH:mm} UTC\n" +
                         $"**Max Participants:** {(tournament.MaxParticipants > 0 ? tournament.MaxParticipants.ToString() : "Unlimited")}\n\n" +
                         $"**ID:** `{tournament.TournamentId}`",
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

    private static string GetTournamentDescription(TournamentType type)
    {
        return type switch
        {
            TournamentType.HeaviestCatch => "Catch the heaviest fish to win!",
            TournamentType.MostUniqueFish => "Catch the most different species of fish!",
            TournamentType.MostFishCaught => "Catch the most fish overall!",
            TournamentType.SpecificFishType => "Catch specific types of fish!",
            TournamentType.BiggestCollectiveGuild => "Guild tournament - work together!",
            _ => "Compete with other players!"
        };
    }

    private static List<TournamentReward> GetDefaultRewards(TournamentType type)
    {
        return new List<TournamentReward>
        {
            new() { Rank = 1, FishCoins = 1000, Items = [], Title = "Tournament Champion" },
            new() { Rank = 2, FishCoins = 500, Items = [], Title = "Runner-up" },
            new() { Rank = 3, FishCoins = 250, Items = [], Title = "Bronze Medalist" }
        };
    }
}