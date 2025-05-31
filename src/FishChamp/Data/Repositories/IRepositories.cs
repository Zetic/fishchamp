using FishChamp.Data.Models;

namespace FishChamp.Data.Repositories;

public interface IPlayerRepository
{
    Task<PlayerProfile?> GetPlayerAsync(ulong userId);
    Task<PlayerProfile> CreatePlayerAsync(ulong userId, string username);
    Task UpdatePlayerAsync(PlayerProfile player);
    Task<bool> PlayerExistsAsync(ulong userId);
    Task<List<PlayerProfile>> GetAllPlayersAsync();
}

public interface IInventoryRepository
{
    Task<Inventory?> GetInventoryAsync(ulong userId);
    Task<Inventory> CreateInventoryAsync(ulong userId);
    Task UpdateInventoryAsync(Inventory inventory);
    Task AddItemAsync(ulong userId, InventoryItem item);
    Task RemoveItemAsync(ulong userId, string itemId, int quantity = 1);
}

public interface IAreaRepository
{
    Task<AreaState?> GetAreaAsync(string areaId);
    Task<List<AreaState>> GetAllAreasAsync();
    Task UpdateAreaAsync(AreaState area);
    Task<List<AreaState>> GetConnectedAreasAsync(string areaId);
}

public interface ITrapRepository
{
    Task<FishTrap?> GetTrapAsync(string trapId);
    Task<List<FishTrap>> GetUserTrapsAsync(ulong userId);
    Task<List<FishTrap>> GetActiveTrapsAsync();
    Task<List<FishTrap>> GetCompletedTrapsAsync(ulong userId);
    Task<FishTrap> CreateTrapAsync(FishTrap trap);
    Task UpdateTrapAsync(FishTrap trap);
    Task DeleteTrapAsync(string trapId);
}

public interface IAquariumRepository
{
    Task<Aquarium?> GetAquariumAsync(ulong userId);
    Task<Aquarium> CreateAquariumAsync(ulong userId);
    Task UpdateAquariumAsync(Aquarium aquarium);
    Task<bool> AquariumExistsAsync(ulong userId);
}

public interface IFarmRepository
{
    Task<Farm?> GetFarmAsync(ulong userId, string areaId, string farmSpotId);
    Task<List<Farm>> GetUserFarmsAsync(ulong userId);
    Task<List<Farm>> GetAllFarmsAsync();
    Task<Farm> CreateFarmAsync(Farm farm);
    Task UpdateFarmAsync(Farm farm);
    Task DeleteFarmAsync(ulong userId, string areaId, string farmSpotId);
}

public interface IBoatRepository
{
    Task<Boat?> GetBoatAsync(string boatId);
    Task<List<Boat>> GetUserBoatsAsync(ulong userId);
    Task<Boat> CreateBoatAsync(Boat boat);
    Task UpdateBoatAsync(Boat boat);
    Task DeleteBoatAsync(string boatId);
}

public interface IPlotRepository
{
    Task<Plot?> GetPlotAsync(string areaId, string plotId);
    Task<List<Plot>> GetAreaPlotsAsync(string areaId);
    Task<List<OwnedPlot>> GetUserPlotsAsync(ulong userId);
    Task<bool> PurchasePlotAsync(ulong userId, string areaId, string plotId);
    Task<bool> IsPlotAvailableAsync(string areaId, string plotId);
}

public interface IHouseRepository
{
    Task<House?> GetHouseAsync(string houseId);
    Task<List<House>> GetUserHousesAsync(ulong userId);
    Task<House?> GetHouseByPlotAsync(string areaId, string plotId);
    Task<House> CreateHouseAsync(House house);
    Task UpdateHouseAsync(House house);
    Task DeleteHouseAsync(string houseId);
}