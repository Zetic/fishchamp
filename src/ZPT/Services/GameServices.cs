using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ZPT.Models;

namespace ZPT.Services;

public class GameDataService
{
    private readonly ILogger<GameDataService> _logger;
    private List<FishingArea> _areas = new();
    private List<Fish> _fish = new();
    private List<Rod> _rods = new();
    private List<Bait> _baits = new();
    private List<AquariumType> _aquariums = new();
    private List<Decoration> _decorations = new();
    private List<Trap> _traps = new();

    public GameDataService(ILogger<GameDataService> logger)
    {
        _logger = logger;
        LoadGameData();
    }

    public IReadOnlyList<FishingArea> Areas => _areas.AsReadOnly();
    public IReadOnlyList<Fish> Fish => _fish.AsReadOnly();
    public IReadOnlyList<Rod> Rods => _rods.AsReadOnly();
    public IReadOnlyList<Bait> Baits => _baits.AsReadOnly();
    public IReadOnlyList<AquariumType> Aquariums => _aquariums.AsReadOnly();
    public IReadOnlyList<Decoration> Decorations => _decorations.AsReadOnly();
    public IReadOnlyList<Trap> Traps => _traps.AsReadOnly();

    public FishingArea? GetAreaByName(string name) => 
        _areas.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public Fish? GetFishByName(string name) => 
        _fish.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public Rod? GetRodByName(string name) => 
        _rods.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public Bait? GetBaitByName(string name) => 
        _baits.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void LoadGameData()
    {
        try
        {
            _areas = LoadJsonData<FishingArea>("Data/areas.json");
            _fish = LoadJsonData<Fish>("Data/fish.json");
            _rods = LoadJsonData<Rod>("Data/rods.json");
            _baits = LoadJsonData<Bait>("Data/baits.json");
            _aquariums = LoadJsonData<AquariumType>("Data/aquariums.json");
            _decorations = LoadJsonData<Decoration>("Data/decorations.json");
            _traps = LoadJsonData<Trap>("Data/traps.json");

            _logger.LogInformation("Loaded game data: {Areas} areas, {Fish} fish, {Rods} rods, {Baits} baits", 
                _areas.Count, _fish.Count, _rods.Count, _baits.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load game data");
        }
    }

    private List<T> LoadJsonData<T>(string fileName)
    {
        try
        {
            if (File.Exists(fileName))
            {
                var json = File.ReadAllText(fileName);
                return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
            }
            
            _logger.LogWarning("Data file not found: {FileName}", fileName);
            return new List<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data from {FileName}", fileName);
            return new List<T>();
        }
    }
}

public class GameLogicService
{
    private readonly ILogger<GameLogicService> _logger;
    private readonly GameDataService _gameData;
    private readonly RandomService _random;

    public GameLogicService(ILogger<GameLogicService> logger, GameDataService gameData, RandomService random)
    {
        _logger = logger;
        _gameData = gameData;
        _random = random;
    }

    public bool CanCatchFish(UserProfile user, Fish fish, Rod rod, Bait bait)
    {
        // Basic logic for fish catching probability
        var baseChance = rod.SuccessRate * bait.Effectiveness;
        
        // Special abilities
        if (!string.IsNullOrEmpty(fish.SpecialEffect) && rod.SpecialAbility != fish.SpecialEffect)
        {
            baseChance *= 0.1; // Much harder without special ability
        }
        
        return _random.NextBool(baseChance);
    }
}

public class InventoryService
{
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(ILogger<InventoryService> logger)
    {
        _logger = logger;
    }

    public bool HasItem(UserProfile user, string itemName, int quantity = 1)
    {
        return user.Inventory.TryGetValue(itemName, out var count) && count >= quantity;
    }

    public void AddItem(UserProfile user, string itemName, int quantity)
    {
        if (user.Inventory.ContainsKey(itemName))
        {
            user.Inventory[itemName] += quantity;
        }
        else
        {
            user.Inventory[itemName] = quantity;
        }
        _logger.LogDebug("Added {Quantity} {Item} to user {UserId}", quantity, itemName, user.UserId);
    }

    public bool RemoveItem(UserProfile user, string itemName, int quantity)
    {
        if (!HasItem(user, itemName, quantity))
        {
            return false;
        }

        user.Inventory[itemName] -= quantity;
        if (user.Inventory[itemName] <= 0)
        {
            user.Inventory.Remove(itemName);
        }
        
        _logger.LogDebug("Removed {Quantity} {Item} from user {UserId}", quantity, itemName, user.UserId);
        return true;
    }
}

public class FishGeneratorService
{
    private readonly RandomService _random;
    private readonly GameDataService _gameData;

    public FishGeneratorService(RandomService random, GameDataService gameData)
    {
        _random = random;
        _gameData = gameData;
    }

    public Fish? GenerateFishForArea(string areaName)
    {
        var area = _gameData.GetAreaByName(areaName);
        if (area == null) return null;

        var availableFish = _gameData.Fish.Where(f => f.Areas.Contains(areaName)).ToList();
        if (!availableFish.Any()) return null;

        return _random.PickRandom(availableFish);
    }
}

public class RandomService
{
    private readonly Random _random = new();

    public double NextDouble() => _random.NextDouble();
    
    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
    
    public int Next(int maxValue) => _random.Next(maxValue);
    
    public bool NextBool(double probability) => _random.NextDouble() < probability;
    
    public T PickRandom<T>(IList<T> items) 
    {
        if (items.Count == 0)
            throw new ArgumentException("Cannot pick from empty collection", nameof(items));
        return items[_random.Next(items.Count)];
    }
}

public class OpenAIService
{
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(ILogger<OpenAIService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateSpeechAsync(string text, string voice = "alloy")
    {
        _logger.LogInformation("Generating speech for text: {Text} with voice: {Voice}", text, voice);
        
        // TODO: Implement OpenAI TTS integration
        await Task.Delay(100);
        return Array.Empty<byte>();
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        _logger.LogInformation("Generating AI response for prompt: {Prompt}", prompt);
        
        // TODO: Implement OpenAI Chat Completions integration
        await Task.Delay(100);
        return "This is a placeholder response.";
    }
}