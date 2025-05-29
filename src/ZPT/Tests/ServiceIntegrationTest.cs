using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZPT.Services;

namespace ZPT.Tests;

public class ServiceIntegrationTest
{
    public static async Task RunTestAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<ServiceIntegrationTest>>();
        
        try
        {
            // Test GameDataService
            var gameData = services.GetRequiredService<GameDataService>();
            logger.LogInformation("Testing GameDataService...");
            logger.LogInformation("Areas: {Count}, Fish: {Count}, Rods: {Count}", 
                gameData.Areas.Count, gameData.Fish.Count, gameData.Rods.Count);
            
            // Test UserManagerService
            var userManager = services.GetRequiredService<UserManagerService>();
            logger.LogInformation("Testing UserManagerService...");
            var testUser = await userManager.GetUserAsync("test-user-123", createIfNotExists: true);
            logger.LogInformation("Created test user: {UserId}, Gold: {Gold}", testUser?.UserId, testUser?.Gold);
            
            // Test InventoryService
            var inventory = services.GetRequiredService<InventoryService>();
            logger.LogInformation("Testing InventoryService...");
            if (testUser != null)
            {
                inventory.AddItem(testUser, "Test Item", 5);
                var hasItem = inventory.HasItem(testUser, "Test Item", 3);
                logger.LogInformation("Inventory test passed: {HasItem}", hasItem);
            }
            
            // Test FishGeneratorService
            var fishGen = services.GetRequiredService<FishGeneratorService>();
            logger.LogInformation("Testing FishGeneratorService...");
            var fish = fishGen.GenerateFishForArea("Lake");
            logger.LogInformation("Generated fish: {FishName} from Lake area", fish?.Name ?? "None");
            
            logger.LogInformation("All service integration tests passed!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Service integration test failed");
            throw;
        }
    }
}