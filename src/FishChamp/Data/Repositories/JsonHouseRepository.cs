using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonHouseRepository : IHouseRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "houses.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<House?> GetHouseAsync(string houseId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var houses = await LoadHousesAsync();
            return houses.FirstOrDefault(h => h.HouseId == houseId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<House>> GetUserHousesAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var houses = await LoadHousesAsync();
            return houses.Where(h => h.OwnerId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<House?> GetHouseByPlotAsync(string areaId, string plotId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var houses = await LoadHousesAsync();
            return houses.FirstOrDefault(h => h.AreaId == areaId && h.PlotId == plotId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<House> CreateHouseAsync(House house)
    {
        await _semaphore.WaitAsync();
        try
        {
            var houses = await LoadHousesAsync();
            house.HouseId = Guid.NewGuid().ToString();
            house.CreatedAt = DateTime.UtcNow;
            house.LastUpdated = DateTime.UtcNow;
            
            // Create default room based on layout
            var defaultRoom = CreateDefaultRoom(house.Layout);
            house.Rooms = new List<Room> { defaultRoom };
            
            houses.Add(house);
            await SaveHousesAsync(houses);
            return house;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateHouseAsync(House house)
    {
        await _semaphore.WaitAsync();
        try
        {
            var houses = await LoadHousesAsync();
            var index = houses.FindIndex(h => h.HouseId == house.HouseId);
            if (index >= 0)
            {
                house.LastUpdated = DateTime.UtcNow;
                houses[index] = house;
                await SaveHousesAsync(houses);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteHouseAsync(string houseId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var houses = await LoadHousesAsync();
            houses.RemoveAll(h => h.HouseId == houseId);
            await SaveHousesAsync(houses);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<House>> LoadHousesAsync()
    {
        if (!File.Exists(_dataPath))
        {
            return new List<House>();
        }

        var json = await File.ReadAllTextAsync(_dataPath);
        return JsonSerializer.Deserialize<List<House>>(json) ?? new List<House>();
    }

    private async Task SaveHousesAsync(List<House> houses)
    {
        var json = JsonSerializer.Serialize(houses, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_dataPath, json);
    }

    private static Room CreateDefaultRoom(HouseLayout layout)
    {
        return layout switch
        {
            HouseLayout.Cozy => new Room
            {
                Name = "Living Room",
                Type = RoomType.Living,
                Description = "A cozy living space where you can relax and entertain guests."
            },
            HouseLayout.Spacious => new Room
            {
                Name = "Main Hall",
                Type = RoomType.Living,
                Description = "A spacious main hall perfect for gatherings and activities."
            },
            HouseLayout.Mansion => new Room
            {
                Name = "Grand Hall",
                Type = RoomType.Living,
                Description = "An impressive grand hall that showcases your success and status."
            },
            _ => new Room
            {
                Name = "Room",
                Type = RoomType.Living,
                Description = "A basic room."
            }
        };
    }
}