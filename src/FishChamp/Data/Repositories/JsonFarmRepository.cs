using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonFarmRepository : IFarmRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "farms.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Farm?> GetFarmAsync(ulong userId, string areaId, string farmSpotId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var farms = await LoadFarmsAsync();
            return farms.FirstOrDefault(f => f.UserId == userId && f.AreaId == areaId && f.FarmSpotId == farmSpotId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Farm>> GetUserFarmsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var farms = await LoadFarmsAsync();
            return farms.Where(f => f.UserId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Farm>> GetAllFarmsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadFarmsAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Farm> CreateFarmAsync(Farm farm)
    {
        await _semaphore.WaitAsync();
        try
        {
            var farms = await LoadFarmsAsync();
            farms.Add(farm);
            await SaveFarmsAsync(farms);
            return farm;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateFarmAsync(Farm farm)
    {
        await _semaphore.WaitAsync();
        try
        {
            var farms = await LoadFarmsAsync();
            var existingFarm = farms.FirstOrDefault(f => f.UserId == farm.UserId && f.AreaId == farm.AreaId && f.FarmSpotId == farm.FarmSpotId);
            if (existingFarm != null)
            {
                farms.Remove(existingFarm);
                farms.Add(farm);
                await SaveFarmsAsync(farms);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteFarmAsync(ulong userId, string areaId, string farmSpotId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var farms = await LoadFarmsAsync();
            var farm = farms.FirstOrDefault(f => f.UserId == userId && f.AreaId == areaId && f.FarmSpotId == farmSpotId);
            if (farm != null)
            {
                farms.Remove(farm);
                await SaveFarmsAsync(farms);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<Farm>> LoadFarmsAsync()
    {
        if (!File.Exists(_dataPath))
        {
            return new List<Farm>();
        }

        var json = await File.ReadAllTextAsync(_dataPath);
        return JsonSerializer.Deserialize<List<Farm>>(json) ?? new List<Farm>();
    }

    private async Task SaveFarmsAsync(List<Farm> farms)
    {
        var json = JsonSerializer.Serialize(farms, options: new JsonSerializerOptions() { WriteIndented = true });
        await File.WriteAllTextAsync(_dataPath, json);
    }
}