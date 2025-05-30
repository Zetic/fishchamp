using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonPlayerRepository : IPlayerRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "players.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<PlayerProfile?> GetPlayerAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var players = await LoadPlayersAsync();
            return players.FirstOrDefault(p => p.UserId == userId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<PlayerProfile> CreatePlayerAsync(ulong userId, string username)
    {
        await _semaphore.WaitAsync();
        try
        {
            var players = await LoadPlayersAsync();
            var newPlayer = new PlayerProfile
            {
                UserId = userId,
                Username = username,
                CurrentArea = "starter_lake",
                FishCoins = 100,
                Level = 1,
                Experience = 0,
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow
            };

            players.Add(newPlayer);
            await SavePlayersAsync(players);
            return newPlayer;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdatePlayerAsync(PlayerProfile player)
    {
        await _semaphore.WaitAsync();
        try
        {
            var players = await LoadPlayersAsync();
            var existingPlayer = players.FirstOrDefault(p => p.UserId == player.UserId);
            if (existingPlayer != null)
            {
                players.Remove(existingPlayer);
                players.Add(player);
                await SavePlayersAsync(players);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> PlayerExistsAsync(ulong userId)
    {
        var player = await GetPlayerAsync(userId);
        return player != null;
    }

    private async Task<List<PlayerProfile>> LoadPlayersAsync()
    {
        if (!File.Exists(_dataPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_dataPath);
        return JsonSerializer.Deserialize<List<PlayerProfile>>(json) ?? [];
    }

    private async Task SavePlayersAsync(List<PlayerProfile> players)
    {
        var json = JsonSerializer.Serialize(players, options: new JsonSerializerOptions() { WriteIndented = true });
        await File.WriteAllTextAsync(_dataPath, json);
    }
}