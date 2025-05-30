using FishChamp.Data.Models;
using Newtonsoft.Json;

namespace FishChamp.Services;

public interface IFishDataService
{
    Task<FishData?> GetFishDataAsync(string fishId);
    Task<Dictionary<string, FishData>> GetAllFishDataAsync();
}

public class FishDataService : IFishDataService
{
    private readonly string _fishDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "FishData.json");
    private Dictionary<string, FishData>? _fishDataCache;

    public async Task<FishData?> GetFishDataAsync(string fishId)
    {
        var allFishData = await GetAllFishDataAsync();
        return allFishData.TryGetValue(fishId, out var fishData) ? fishData : null;
    }

    public async Task<Dictionary<string, FishData>> GetAllFishDataAsync()
    {
        if (_fishDataCache != null)
            return _fishDataCache;

        if (!File.Exists(_fishDataPath))
        {
            _fishDataCache = new Dictionary<string, FishData>();
            return _fishDataCache;
        }

        var json = await File.ReadAllTextAsync(_fishDataPath);
        _fishDataCache = JsonConvert.DeserializeObject<Dictionary<string, FishData>>(json) ?? new Dictionary<string, FishData>();
        
        return _fishDataCache;
    }
}