using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonInventoryRepository : IInventoryRepository
{
    private readonly string _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "inventories.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Inventory?> GetInventoryAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var inventories = await LoadInventoriesAsync();
            return inventories.FirstOrDefault(i => i.UserId == userId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Inventory> CreateInventoryAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var inventories = await LoadInventoriesAsync();
            var newInventory = new Inventory
            {
                UserId = userId,
                Items =
                [
                    new()
                    {
                        ItemId = "basic_rod",
                        ItemType = "Rod",
                        Name = "Basic Fishing Rod",
                        Quantity = 1,
                        Properties = new() { ["durability"] = 100, ["power"] = 1 }
                    }
                ],
                LastUpdated = DateTime.UtcNow
            };

            inventories.Add(newInventory);
            await SaveInventoriesAsync(inventories);
            return newInventory;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateInventoryAsync(Inventory inventory)
    {
        await _semaphore.WaitAsync();
        try
        {
            var inventories = await LoadInventoriesAsync();
            var existingInventory = inventories.FirstOrDefault(i => i.UserId == inventory.UserId);
            if (existingInventory != null)
            {
                inventories.Remove(existingInventory);
                inventory.LastUpdated = DateTime.UtcNow;
                inventories.Add(inventory);
                await SaveInventoriesAsync(inventories);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddItemAsync(ulong userId, InventoryItem item)
    {
        var inventory = await GetInventoryAsync(userId) ?? await CreateInventoryAsync(userId);

        var existingItem = inventory.Items.FirstOrDefault(i => i.ItemId == item.ItemId);
        if (existingItem != null)
        {
            existingItem.Quantity += item.Quantity;
        }
        else
        {
            inventory.Items.Add(item);
        }

        await UpdateInventoryAsync(inventory);
    }

    public async Task RemoveItemAsync(ulong userId, string itemId, int quantity = 1)
    {
        var inventory = await GetInventoryAsync(userId);
        if (inventory == null) return;

        var item = inventory.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (item != null)
        {
            item.Quantity -= quantity;
            if (item.Quantity <= 0)
            {
                inventory.Items.Remove(item);
            }
            await UpdateInventoryAsync(inventory);
        }
    }

    private async Task<List<Inventory>> LoadInventoriesAsync()
    {
        if (!File.Exists(_dataPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_dataPath);
        return JsonSerializer.Deserialize<List<Inventory>>(json) ?? [];
    }

    private async Task SaveInventoriesAsync(List<Inventory> inventories)
    {
        var json = JsonSerializer.Serialize(inventories, options: new JsonSerializerOptions() { WriteIndented = true });
        await File.WriteAllTextAsync(_dataPath, json);
    }
}