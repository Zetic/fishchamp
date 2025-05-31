using FishChamp.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FishChamp.Services;

public class AquariumMaintenanceService(IAquariumRepository aquariumRepository, ILogger<AquariumMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AquariumMaintenanceService started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAquariums();
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); // Check every 10 minutes
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AquariumMaintenanceService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }
        
        logger.LogInformation("AquariumMaintenanceService stopped");
    }

    private async Task UpdateAquariums()
    {
        // Note: For a JSON-based repository, we'd need to get all aquariums
        // This is a simplified implementation that would be more efficient with a real database
        logger.LogDebug("Updating aquarium maintenance states");
        
        // Since we don't have a method to get all aquariums, we'll implement
        // degradation logic when aquariums are accessed in commands
        // This is acceptable for the JSON-based implementation
    }

    public static void ApplyDegradation(Data.Models.Aquarium aquarium)
    {
        var now = DateTime.UtcNow;
        
        // Calculate time since last maintenance
        var timeSinceLastFed = now - aquarium.LastFed;
        var timeSinceLastCleaned = now - aquarium.LastCleaned;
        
        // Degrade cleanliness over time (1% per hour, faster if more fish)
        var cleanlinessDecay = Math.Min(100, (timeSinceLastCleaned.TotalHours * (1 + aquarium.Fish.Count * 0.1)));
        aquarium.Cleanliness = Math.Max(0, aquarium.Cleanliness - cleanlinessDecay);
        
        // Temperature drift (moves towards room temperature of 20°C)
        var temperatureDrift = timeSinceLastCleaned.TotalHours * 0.5;
        if (aquarium.Temperature > 20)
        {
            aquarium.Temperature = Math.Max(20, aquarium.Temperature - temperatureDrift);
        }
        else if (aquarium.Temperature < 20)
        {
            aquarium.Temperature = Math.Min(20, aquarium.Temperature + temperatureDrift);
        }
        
        // Apply degradation to fish based on conditions
        foreach (var fish in aquarium.Fish)
        {
            ApplyFishDegradation(fish, aquarium, timeSinceLastFed, timeSinceLastCleaned);
        }
        
        // Apply decoration bonuses
        ApplyDecorationBonuses(aquarium);
    }

    private static void ApplyFishDegradation(Data.Models.AquariumFish fish, Data.Models.Aquarium aquarium, TimeSpan timeSinceLastFed, TimeSpan timeSinceLastCleaned)
    {
        if (!fish.IsAlive) return;
        
        // Happiness degradation
        var happinessDecay = 0.0;
        
        // Hunger affects happiness (fish get unhappy if not fed for more than 6 hours)
        if (timeSinceLastFed.TotalHours > 6)
        {
            happinessDecay += (timeSinceLastFed.TotalHours - 6) * 2;
        }
        
        // Dirty tank affects happiness
        if (aquarium.Cleanliness < 50)
        {
            happinessDecay += (50 - aquarium.Cleanliness) * 0.5;
        }
        
        // Temperature stress affects happiness (optimal range: 20-25°C)
        if (aquarium.Temperature < 18 || aquarium.Temperature > 28)
        {
            var temperatureStress = Math.Abs(aquarium.Temperature - 23) - 5; // 23°C is optimal
            happinessDecay += temperatureStress * 2;
        }
        
        fish.Happiness = Math.Max(0, fish.Happiness - happinessDecay);
        
        // Health degradation (slower than happiness)
        var healthDecay = 0.0;
        
        // Very hungry fish lose health
        if (timeSinceLastFed.TotalDays > 1)
        {
            healthDecay += (timeSinceLastFed.TotalDays - 1) * 5;
        }
        
        // Very dirty tank affects health
        if (aquarium.Cleanliness < 20)
        {
            healthDecay += (20 - aquarium.Cleanliness) * 0.2;
        }
        
        // Extreme temperature affects health
        if (aquarium.Temperature < 15 || aquarium.Temperature > 30)
        {
            var temperatureStress = Math.Abs(aquarium.Temperature - 23) - 7;
            healthDecay += temperatureStress * 1;
        }
        
        fish.Health = Math.Max(0, fish.Health - healthDecay);
        
        // Fish die if health reaches 0
        if (fish.Health <= 0)
        {
            fish.IsAlive = false;
            fish.CanBreed = false;
        }
        
        // Fish can't breed if unhappy or unhealthy
        if (fish.Happiness < 70 || fish.Health < 80)
        {
            fish.CanBreed = false;
        }
        else
        {
            fish.CanBreed = true;
        }
    }

    private static void ApplyDecorationBonuses(Data.Models.Aquarium aquarium)
    {
        if (!aquarium.Decorations.Any()) return;

        // Calculate total decoration bonus
        var totalBonus = aquarium.Decorations.Sum(decoration => decoration.ToLower() switch
        {
            "plant" => 1.0,   // Slow but steady happiness boost
            "pebbles" => 0.5,
            "statue" => 2.0,  // Higher boost but slower
            "coral" => 1.5,
            "cave" => 1.2,
            _ => 0.5
        });

        // Apply decoration bonus to all living fish (small but continuous)
        foreach (var fish in aquarium.Fish.Where(f => f.IsAlive))
        {
            fish.Happiness = Math.Min(100, fish.Happiness + totalBonus * 0.1); // Very small continuous bonus
        }
    }
}