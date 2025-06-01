using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonTournamentRepository : ITournamentRepository
{
    private readonly string _tournamentsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tournaments.json");
    private readonly string _leaderboardsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "leaderboards.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Tournament?> GetTournamentAsync(string tournamentId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            return tournaments.FirstOrDefault(t => t.TournamentId == tournamentId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Tournament>> GetActiveTournamentsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            return tournaments.Where(t => t.Status == TournamentStatus.Active).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Tournament>> GetUpcomingTournamentsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            return tournaments.Where(t => t.Status == TournamentStatus.Upcoming).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Tournament>> GetCompletedTournamentsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            return tournaments.Where(t => t.Status == TournamentStatus.Ended).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Tournament> CreateTournamentAsync(Tournament tournament)
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            tournaments.Add(tournament);
            await SaveTournamentsAsync(tournaments);
            return tournament;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateTournamentAsync(Tournament tournament)
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            var existingTournament = tournaments.FirstOrDefault(t => t.TournamentId == tournament.TournamentId);
            if (existingTournament != null)
            {
                tournaments.Remove(existingTournament);
                tournaments.Add(tournament);
                await SaveTournamentsAsync(tournaments);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteTournamentAsync(string tournamentId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var tournaments = await LoadTournamentsAsync();
            var tournament = tournaments.FirstOrDefault(t => t.TournamentId == tournamentId);
            if (tournament != null)
            {
                tournaments.Remove(tournament);
                await SaveTournamentsAsync(tournaments);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<TournamentEntry>> GetTournamentEntriesAsync(string tournamentId)
    {
        var tournament = await GetTournamentAsync(tournamentId);
        return tournament?.Entries ?? new List<TournamentEntry>();
    }

    public async Task<TournamentEntry?> GetUserTournamentEntryAsync(string tournamentId, ulong userId)
    {
        var tournament = await GetTournamentAsync(tournamentId);
        return tournament?.Entries.FirstOrDefault(e => e.UserId == userId);
    }

    public async Task UpdateTournamentEntryAsync(TournamentEntry entry)
    {
        var tournament = await GetTournamentAsync(entry.TournamentId);
        if (tournament != null)
        {
            var existingEntry = tournament.Entries.FirstOrDefault(e => e.EntryId == entry.EntryId);
            if (existingEntry != null)
            {
                tournament.Entries.Remove(existingEntry);
            }
            tournament.Entries.Add(entry);
            await UpdateTournamentAsync(tournament);
        }
    }

    public async Task<Leaderboard> GetLeaderboardAsync(LeaderboardType type)
    {
        await _semaphore.WaitAsync();
        try
        {
            var leaderboards = await LoadLeaderboardsAsync();
            return leaderboards.FirstOrDefault(l => l.Type == type) ?? new Leaderboard
            {
                LeaderboardId = type.ToString(),
                Name = GetLeaderboardName(type),
                Type = type,
                Entries = new List<LeaderboardEntry>()
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateLeaderboardAsync(Leaderboard leaderboard)
    {
        await _semaphore.WaitAsync();
        try
        {
            var leaderboards = await LoadLeaderboardsAsync();
            var existingLeaderboard = leaderboards.FirstOrDefault(l => l.Type == leaderboard.Type);
            if (existingLeaderboard != null)
            {
                leaderboards.Remove(existingLeaderboard);
            }
            leaderboard.LastUpdated = DateTime.UtcNow;
            leaderboards.Add(leaderboard);
            await SaveLeaderboardsAsync(leaderboards);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<Tournament>> LoadTournamentsAsync()
    {
        if (!File.Exists(_tournamentsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tournamentsDataPath)!);
            return new List<Tournament>();
        }

        var json = await File.ReadAllTextAsync(_tournamentsDataPath);
        return JsonSerializer.Deserialize<List<Tournament>>(json) ?? new List<Tournament>();
    }

    private async Task SaveTournamentsAsync(List<Tournament> tournaments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tournamentsDataPath)!);
        var json = JsonSerializer.Serialize(tournaments, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_tournamentsDataPath, json);
    }

    private async Task<List<Leaderboard>> LoadLeaderboardsAsync()
    {
        if (!File.Exists(_leaderboardsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_leaderboardsDataPath)!);
            return new List<Leaderboard>();
        }

        var json = await File.ReadAllTextAsync(_leaderboardsDataPath);
        return JsonSerializer.Deserialize<List<Leaderboard>>(json) ?? new List<Leaderboard>();
    }

    private async Task SaveLeaderboardsAsync(List<Leaderboard> leaderboards)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_leaderboardsDataPath)!);
        var json = JsonSerializer.Serialize(leaderboards, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_leaderboardsDataPath, json);
    }

    private static string GetLeaderboardName(LeaderboardType type)
    {
        return type switch
        {
            LeaderboardType.RichestPlayers => "Richest Players",
            LeaderboardType.HighestLevel => "Highest Level Players",
            LeaderboardType.HeaviestSingleCatch => "Heaviest Single Catch",
            LeaderboardType.MostUniqueFish => "Most Unique Fish Caught",
            LeaderboardType.BestGuilds => "Best Guilds",
            _ => type.ToString()
        };
    }
}