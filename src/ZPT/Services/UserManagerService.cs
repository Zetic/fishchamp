using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ZPT.Models;

namespace ZPT.Services;

public class UserManagerService
{
    private readonly ILogger<UserManagerService> _logger;
    private readonly string _usersFilePath;
    private Dictionary<string, UserProfile> _users = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public UserManagerService(ILogger<UserManagerService> logger)
    {
        _logger = logger;
        _usersFilePath = Path.Combine("Database", "users.json");
        
        // Ensure database directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_usersFilePath)!);
        
        // Load users on startup
        Task.Run(LoadUsersAsync);
    }

    public async Task<UserProfile?> GetUserAsync(string userId, bool createIfNotExists = false)
    {
        await _fileLock.WaitAsync();
        try
        {
            // Load users if cache is empty
            if (_users.Count == 0)
            {
                await LoadUsersInternalAsync();
            }

            if (_users.TryGetValue(userId, out var user))
            {
                user.LastActive = DateTime.UtcNow;
                return user;
            }

            if (createIfNotExists)
            {
                var newUser = CreateNewUserProfile(userId);
                _users[userId] = newUser;
                await SaveUsersInternalAsync();
                _logger.LogInformation("Created new user profile for {UserId}", userId);
                return newUser;
            }

            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<UserProfile> GetOrCreateUserAsync(string userId)
    {
        var user = await GetUserAsync(userId, true);
        return user!; // Will never be null since createIfNotExists is true
    }

    public async Task UpdateUserAsync(string userId, UserProfile user)
    {
        await _fileLock.WaitAsync();
        try
        {
            user.LastActive = DateTime.UtcNow;
            _users[userId] = user;
            await SaveUsersInternalAsync();
            _logger.LogDebug("Updated user profile for {UserId}", userId);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> UserExistsAsync(string userId)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (_users.Count == 0)
            {
                await LoadUsersInternalAsync();
            }
            return _users.ContainsKey(userId);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task LoadUsersAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            await LoadUsersInternalAsync();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task LoadUsersInternalAsync()
    {
        try
        {
            if (File.Exists(_usersFilePath))
            {
                var json = await File.ReadAllTextAsync(_usersFilePath);
                var loadedUsers = JsonConvert.DeserializeObject<Dictionary<string, UserProfile>>(json);
                _users = loadedUsers ?? new Dictionary<string, UserProfile>();
                _logger.LogInformation("Loaded {Count} user profiles", _users.Count);
            }
            else
            {
                _users = new Dictionary<string, UserProfile>();
                await SaveUsersInternalAsync();
                _logger.LogInformation("Created new users database file");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users from file");
            _users = new Dictionary<string, UserProfile>();
        }
    }

    private async Task SaveUsersInternalAsync()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_users, Formatting.Indented);
            await File.WriteAllTextAsync(_usersFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save users to file");
        }
    }

    private static UserProfile CreateNewUserProfile(string userId)
    {
        return new UserProfile
        {
            UserId = userId,
            Area = "Lake",
            Level = 1,
            Experience = 0,
            Gold = 100,
            EquippedRod = "Basic Rod",
            EquippedBait = "Worms",
            Inventory = new Dictionary<string, int>
            {
                { "Worms", 10 }
            },
            Equipment = new Dictionary<string, int>
            {
                { "Basic Rod", 1 }
            },
            Fish = new Dictionary<string, int>(),
            Aquariums = new List<Aquarium>(),
            Traps = new Dictionary<string, int>(),
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow
        };
    }
}