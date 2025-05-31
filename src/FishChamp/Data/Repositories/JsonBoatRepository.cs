using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonBoatRepository : IBoatRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "boats.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Boat?> GetBoatAsync(string boatId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var boats = await LoadBoatsAsync();
            return boats.FirstOrDefault(b => b.BoatId == boatId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Boat>> GetUserBoatsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var boats = await LoadBoatsAsync();
            return boats.Where(b => b.UserId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Boat> CreateBoatAsync(Boat boat)
    {
        await _semaphore.WaitAsync();
        try
        {
            var boats = await LoadBoatsAsync();
            
            // Generate unique boat ID if not provided
            if (string.IsNullOrEmpty(boat.BoatId))
            {
                boat.BoatId = Guid.NewGuid().ToString();
            }
            
            boats.Add(boat);
            await SaveBoatsAsync(boats);
            return boat;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateBoatAsync(Boat boat)
    {
        await _semaphore.WaitAsync();
        try
        {
            var boats = await LoadBoatsAsync();
            var index = boats.FindIndex(b => b.BoatId == boat.BoatId);
            if (index >= 0)
            {
                boats[index] = boat;
                await SaveBoatsAsync(boats);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteBoatAsync(string boatId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var boats = await LoadBoatsAsync();
            boats.RemoveAll(b => b.BoatId == boatId);
            await SaveBoatsAsync(boats);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<Boat>> LoadBoatsAsync()
    {
        try
        {
            if (!File.Exists(_dataPath))
            {
                await EnsureDataDirectoryExistsAsync();
                return new List<Boat>();
            }

            var json = await File.ReadAllTextAsync(_dataPath);
            return JsonSerializer.Deserialize<List<Boat>>(json) ?? new List<Boat>();
        }
        catch
        {
            return new List<Boat>();
        }
    }

    private async Task SaveBoatsAsync(List<Boat> boats)
    {
        await EnsureDataDirectoryExistsAsync();
        var json = JsonSerializer.Serialize(boats, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_dataPath, json);
    }

    private async Task EnsureDataDirectoryExistsAsync()
    {
        var directory = Path.GetDirectoryName(_dataPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }
        await Task.CompletedTask;
    }
}