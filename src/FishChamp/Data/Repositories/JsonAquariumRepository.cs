using FishChamp.Data.Models;
using System.Text.Json;
using FishChamp.Helpers;
using FishChamp.Services;

namespace FishChamp.Data.Repositories;

public class JsonAquariumRepository : IAquariumRepository
{
    private readonly string _filePath;
    private readonly Dictionary<ulong, Aquarium> _aquariums = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonAquariumRepository()
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        _filePath = Path.Combine(dataDir, "aquariums.json");
        
        // Ensure directory exists
        Directory.CreateDirectory(dataDir);
        
        LoadAquariums();
    }

    public async Task<Aquarium?> GetAquariumAsync(ulong userId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_aquariums.TryGetValue(userId, out var aquarium))
            {
                // Apply maintenance updates when accessing aquarium
                AquariumMaintenanceService.UpdateAquariumConditions(aquarium);
                // Save changes if any fish conditions were updated
                await SaveAquariums();
                return aquarium;
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Aquarium> CreateAquariumAsync(ulong userId)
    {
        await _lock.WaitAsync();
        try
        {
            var aquarium = new Aquarium
            {
                UserId = userId,
                Name = "My Aquarium",
                Capacity = 10,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _aquariums[userId] = aquarium;
            await SaveAquariums();
            return aquarium;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAquariumAsync(Aquarium aquarium)
    {
        await _lock.WaitAsync();
        try
        {
            aquarium.LastUpdated = DateTime.UtcNow;
            _aquariums[aquarium.UserId] = aquarium;
            await SaveAquariums();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> AquariumExistsAsync(ulong userId)
    {
        await _lock.WaitAsync();
        try
        {
            return _aquariums.ContainsKey(userId);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void LoadAquariums()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var aquariumList = JsonSerializer.Deserialize<List<Aquarium>>(json) ?? new List<Aquarium>();
                
                _aquariums.Clear();
                foreach (var aquarium in aquariumList)
                {
                    _aquariums[aquarium.UserId] = aquarium;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading aquariums: {ex.Message}");
            _aquariums.Clear();
        }
    }

    private async Task SaveAquariums()
    {
        try
        {
            var aquariumList = _aquariums.Values.ToList();
            var json = JsonSerializer.Serialize(aquariumList, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving aquariums: {ex.Message}");
        }
    }
}