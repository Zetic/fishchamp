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