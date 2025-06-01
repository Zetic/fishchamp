using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using FishChamp.Services.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FishChamp.Services;

public class TournamentService : BackgroundService, IEventHandler<OnFishCatchEvent>
{
    private readonly ILogger<TournamentService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

    public TournamentService(ILogger<TournamentService> logger, IServiceProvider serviceProvider, IEventBus eventBus)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tournament Service started");
        
        // Subscribe to fish catch events
        _eventBus.Subscribe<OnFishCatchEvent>(this);
        _logger.LogInformation("Tournament Service subscribed to OnFishCatch events");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tournamentRepository = scope.ServiceProvider.GetRequiredService<ITournamentRepository>();
                var playerRepository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();

                await ProcessTournamentsAsync(tournamentRepository, playerRepository);
                await UpdateLeaderboardsAsync(tournamentRepository, playerRepository);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tournaments");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
        
        // Unsubscribe when service is stopping
        _eventBus.Unsubscribe<OnFishCatchEvent>(this);
        _logger.LogInformation("Tournament Service unsubscribed from OnFishCatch events");
    }

    private async Task ProcessTournamentsAsync(ITournamentRepository tournamentRepository, IPlayerRepository playerRepository)
    {
        var upcomingTournaments = await tournamentRepository.GetUpcomingTournamentsAsync();
        var activeTournaments = await tournamentRepository.GetActiveTournamentsAsync();

        // Start upcoming tournaments
        foreach (var tournament in upcomingTournaments.Where(t => t.StartTime <= DateTime.UtcNow))
        {
            tournament.Status = TournamentStatus.Active;
            await tournamentRepository.UpdateTournamentAsync(tournament);
            _logger.LogInformation($"Started tournament: {tournament.Name}");
        }

        // End active tournaments
        foreach (var tournament in activeTournaments.Where(t => t.EndTime <= DateTime.UtcNow))
        {
            tournament.Status = TournamentStatus.Ended;
            
            // Calculate final rankings
            var sortedEntries = tournament.Entries.OrderByDescending(e => e.Score).ToList();
            for (int i = 0; i < sortedEntries.Count; i++)
            {
                sortedEntries[i].Rank = i + 1;
            }
            
            // Award prizes
            await AwardTournamentPrizesAsync(tournament, playerRepository);
            
            await tournamentRepository.UpdateTournamentAsync(tournament);
            _logger.LogInformation($"Ended tournament: {tournament.Name} with {tournament.Entries.Count} participants");
        }
    }

    private async Task AwardTournamentPrizesAsync(Tournament tournament, IPlayerRepository playerRepository)
    {
        foreach (var reward in tournament.Rewards)
        {
            var winner = tournament.Entries.FirstOrDefault(e => e.Rank == reward.Rank);
            if (winner != null)
            {
                var player = await playerRepository.GetPlayerAsync(winner.UserId);
                if (player != null)
                {
                    // Award coins
                    if (reward.FishCoins > 0)
                    {
                        player.FishCoins += reward.FishCoins;
                    }

                    // Award title
                    if (!string.IsNullOrEmpty(reward.Title) && !player.TournamentTitles.Contains(reward.Title))
                    {
                        player.TournamentTitles.Add(reward.Title);
                    }

                    // Update tournament stats
                    if (!player.TournamentStats.ContainsKey("tournaments_won"))
                        player.TournamentStats["tournaments_won"] = 0;
                    if (!player.TournamentStats.ContainsKey("tournaments_participated"))
                        player.TournamentStats["tournaments_participated"] = 0;

                    if (reward.Rank == 1)
                        player.TournamentStats["tournaments_won"]++;
                    
                    player.TournamentStats["tournaments_participated"]++;

                    await playerRepository.UpdatePlayerAsync(player);
                    _logger.LogInformation($"Awarded {reward.FishCoins} coins and '{reward.Title}' title to {winner.Username}");
                }
            }
        }
    }

    private async Task UpdateLeaderboardsAsync(ITournamentRepository tournamentRepository, IPlayerRepository playerRepository)
    {
        var allPlayers = await playerRepository.GetAllPlayersAsync();

        // Update Richest Players leaderboard
        var richestPlayers = allPlayers.OrderByDescending(p => p.FishCoins).Take(50).ToList();
        var richestLeaderboard = new Leaderboard
        {
            LeaderboardId = LeaderboardType.RichestPlayers.ToString(),
            Name = "Richest Players",
            Type = LeaderboardType.RichestPlayers,
            Entries = richestPlayers.Select((p, i) => new LeaderboardEntry
            {
                UserId = p.UserId,
                Username = p.Username,
                Score = p.FishCoins,
                Rank = i + 1
            }).ToList()
        };
        await tournamentRepository.UpdateLeaderboardAsync(richestLeaderboard);

        // Update Highest Level leaderboard
        var highestLevel = allPlayers.OrderByDescending(p => p.Level).ThenByDescending(p => p.Experience).Take(50).ToList();
        var levelLeaderboard = new Leaderboard
        {
            LeaderboardId = LeaderboardType.HighestLevel.ToString(),
            Name = "Highest Level Players",
            Type = LeaderboardType.HighestLevel,
            Entries = highestLevel.Select((p, i) => new LeaderboardEntry
            {
                UserId = p.UserId,
                Username = p.Username,
                Score = p.Level,
                AdditionalInfo = $"{p.Experience} XP",
                Rank = i + 1
            }).ToList()
        };
        await tournamentRepository.UpdateLeaderboardAsync(levelLeaderboard);

        // Update Heaviest Single Catch leaderboard
        var heaviestCatch = allPlayers
            .Where(p => p.BiggestCatch.Any())
            .Select(p => new { Player = p, MaxWeight = p.BiggestCatch.Values.Max() })
            .OrderByDescending(x => x.MaxWeight)
            .Take(50)
            .ToList();

        var catchLeaderboard = new Leaderboard
        {
            LeaderboardId = LeaderboardType.HeaviestSingleCatch.ToString(),
            Name = "Heaviest Single Catch",
            Type = LeaderboardType.HeaviestSingleCatch,
            Entries = heaviestCatch.Select((x, i) => new LeaderboardEntry
            {
                UserId = x.Player.UserId,
                Username = x.Player.Username,
                Score = x.MaxWeight,
                AdditionalInfo = x.Player.BiggestCatch.FirstOrDefault(kvp => kvp.Value == x.MaxWeight).Key ?? "Unknown",
                Rank = i + 1
            }).ToList()
        };
        await tournamentRepository.UpdateLeaderboardAsync(catchLeaderboard);

        _logger.LogDebug("Updated all leaderboards");
    }

    public async Task CreateWeeklyTournamentsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var tournamentRepository = scope.ServiceProvider.GetRequiredService<ITournamentRepository>();

        var startTime = DateTime.UtcNow.Date.AddDays(1); // Start tomorrow
        var endTime = startTime.AddDays(7); // Run for a week

        // Create Heaviest Catch tournament
        var heaviestCatchTournament = new Tournament
        {
            Name = "Weekly Heaviest Catch Challenge",
            Description = "Catch the heaviest fish this week to win!",
            Type = TournamentType.HeaviestCatch,
            StartTime = startTime,
            EndTime = endTime,
            Status = TournamentStatus.Upcoming,
            Rewards = GetDefaultWeeklyRewards()
        };

        await tournamentRepository.CreateTournamentAsync(heaviestCatchTournament);

        // Create Most Unique Fish tournament  
        var uniqueFishTournament = new Tournament
        {
            Name = "Weekly Species Hunter Challenge",
            Description = "Catch the most unique fish species this week!",
            Type = TournamentType.MostUniqueFish,
            StartTime = startTime.AddHours(12), // Stagger start times
            EndTime = endTime.AddHours(12),
            Status = TournamentStatus.Upcoming,
            Rewards = GetDefaultWeeklyRewards()
        };

        await tournamentRepository.CreateTournamentAsync(uniqueFishTournament);

        _logger.LogInformation("Created weekly tournaments");
    }

    private static List<TournamentReward> GetDefaultWeeklyRewards()
    {
        return new List<TournamentReward>
        {
            new() { Rank = 1, FishCoins = 2000, Items = [], Title = "Weekly Champion" },
            new() { Rank = 2, FishCoins = 1000, Items = [], Title = "Weekly Runner-up" },
            new() { Rank = 3, FishCoins = 500, Items = [], Title = "Weekly Bronze" }
        };
    }

    /// <summary>
    /// Handle fish catch events and update tournament entries in real-time
    /// </summary>
    public async Task HandleAsync(OnFishCatchEvent eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tournamentRepository = scope.ServiceProvider.GetRequiredService<ITournamentRepository>();
            
            // Get all active tournaments
            var activeTournaments = await tournamentRepository.GetActiveTournamentsAsync();
            
            foreach (var tournament in activeTournaments)
            {
                // Skip tournaments with area restrictions that don't match
                if (!string.IsNullOrEmpty(tournament.AreaRestriction) && 
                    !tournament.AreaRestriction.Equals(eventData.AreaId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                await UpdateTournamentEntryForFishCatch(tournament, eventData, tournamentRepository);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OnFishCatch event for user {UserId}", eventData.UserId);
        }
    }

    /// <summary>
    /// Update a specific tournament entry based on the fish catch
    /// </summary>
    private async Task UpdateTournamentEntryForFishCatch(Tournament tournament, OnFishCatchEvent fishCatch, ITournamentRepository tournamentRepository)
    {
        // Find or create tournament entry for the user
        var entry = tournament.Entries.FirstOrDefault(e => e.UserId == fishCatch.UserId);
        if (entry == null)
        {
            entry = new TournamentEntry
            {
                TournamentId = tournament.TournamentId,
                UserId = fishCatch.UserId,
                Username = fishCatch.Username,
                LastUpdated = DateTime.UtcNow
            };
            tournament.Entries.Add(entry);
        }

        bool entryUpdated = false;

        // Update based on tournament type
        switch (tournament.Type)
        {
            case TournamentType.HeaviestCatch:
                if (fishCatch.FishWeight > entry.Score)
                {
                    entry.Score = fishCatch.FishWeight;
                    entry.FishType = fishCatch.FishItem.ItemId;
                    entryUpdated = true;
                }
                break;

            case TournamentType.MostUniqueFish:
                if (!entry.UniqueFishCaught.Contains(fishCatch.FishItem.ItemId))
                {
                    entry.UniqueFishCaught.Add(fishCatch.FishItem.ItemId);
                    entry.Score = entry.UniqueFishCaught.Count;
                    entryUpdated = true;
                }
                break;

            case TournamentType.MostFishCaught:
                entry.Score += 1; // Increment fish count
                entryUpdated = true;
                break;

            case TournamentType.SpecificFishType:
                // This would need tournament configuration to specify which fish type
                // For now, we'll skip this tournament type
                break;

            case TournamentType.BiggestCollectiveGuild:
                // This would need guild information
                // For now, we'll skip this tournament type  
                break;
        }

        if (entryUpdated)
        {
            entry.LastUpdated = DateTime.UtcNow;
            
            // Update rankings (simple approach - could be optimized)
            UpdateTournamentRankings(tournament);
            
            await tournamentRepository.UpdateTournamentAsync(tournament);
            
            _logger.LogDebug("Updated tournament entry for user {UserId} in tournament {TournamentName} with score {Score}", 
                fishCatch.UserId, tournament.Name, entry.Score);
        }
    }

    /// <summary>
    /// Update tournament rankings for all entries
    /// </summary>
    private static void UpdateTournamentRankings(Tournament tournament)
    {
        var sortedEntries = tournament.Entries.OrderByDescending(e => e.Score).ToList();
        
        for (int i = 0; i < sortedEntries.Count; i++)
        {
            sortedEntries[i].Rank = i + 1;
        }
    }
}