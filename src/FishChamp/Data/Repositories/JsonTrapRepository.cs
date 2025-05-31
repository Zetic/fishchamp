using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonTrapRepository : ITrapRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "traps.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<FishTrap?> GetTrapAsync(string trapId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            return traps.FirstOrDefault(t => t.TrapId == trapId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<FishTrap>> GetUserTrapsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            return traps.Where(t => t.UserId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<FishTrap>> GetActiveTrapsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            return traps.Where(t => !t.IsCompleted && DateTime.UtcNow < t.CompletesAt).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<FishTrap>> GetCompletedTrapsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            return traps.Where(t => t.UserId == userId && (t.IsCompleted || DateTime.UtcNow >= t.CompletesAt)).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<FishTrap> CreateTrapAsync(FishTrap trap)
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            trap.TrapId = Guid.NewGuid().ToString();
            traps.Add(trap);
            await SaveTrapsAsync(traps);
            return trap;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateTrapAsync(FishTrap trap)
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            var existingTrap = traps.FirstOrDefault(t => t.TrapId == trap.TrapId);
            if (existingTrap != null)
            {
                var index = traps.IndexOf(existingTrap);
                traps[index] = trap;
                await SaveTrapsAsync(traps);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteTrapAsync(string trapId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var traps = await LoadTrapsAsync();
            var trap = traps.FirstOrDefault(t => t.TrapId == trapId);
            if (trap != null)
            {
                traps.Remove(trap);
                await SaveTrapsAsync(traps);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<FishTrap>> LoadTrapsAsync()
    {
        if (!File.Exists(_dataPath))
        {
            var directory = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return new List<FishTrap>();
        }

        var json = await File.ReadAllTextAsync(_dataPath);
        return JsonSerializer.Deserialize<List<FishTrap>>(json) ?? new List<FishTrap>();
    }

    private async Task SaveTrapsAsync(List<FishTrap> traps)
    {
        var directory = Path.GetDirectoryName(_dataPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(traps, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_dataPath, json);
    }
}