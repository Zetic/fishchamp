using FishChamp.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FishChamp.Services;

/// <summary>
/// Background service that handles aquarium maintenance degradation over time
/// </summary>
public class AquariumMaintenanceService(IAquariumRepository aquariumRepository, ILogger<AquariumMaintenanceService> logger) : BackgroundService
{
    private const double CLEANLINESS_DECAY_PER_HOUR = 2.0; // 2% per hour
    private const double HAPPINESS_DECAY_PER_HOUR = 1.5; // 1.5% per hour when not fed
    private const double HEALTH_DECAY_PER_HOUR = 1.0; // 1% per hour when happiness is low
    private const double TEMPERATURE_CHANGE_PER_HOUR = 0.5; // Temperature drifts toward room temp (20°C)
    private const double IDEAL_TEMPERATURE_MIN = 20.0;
    private const double IDEAL_TEMPERATURE_MAX = 24.0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AquariumMaintenanceService started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAllAquariums();
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Check every hour
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AquariumMaintenanceService");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retrying
            }
        }
        
        logger.LogInformation("AquariumMaintenanceService stopped");
    }

    private async Task UpdateAllAquariums()
    {
        // Note: In a real implementation, we'd need a method to get all aquariums
        // For now, this service will work when aquariums are accessed
        logger.LogDebug("Aquarium maintenance check completed");
    }

    /// <summary>
    /// Updates fish health and happiness based on aquarium conditions
    /// This method should be called when accessing an aquarium
    /// </summary>
    public static void UpdateAquariumConditions(Data.Models.Aquarium aquarium)
    {
        var now = DateTime.UtcNow;
        var hoursSinceLastUpdate = (now - aquarium.LastUpdated).TotalHours;
        
        if (hoursSinceLastUpdate < 0.1) return; // Skip if updated recently (less than 6 minutes)

        // Update cleanliness (always decays)
        aquarium.Cleanliness = Math.Max(0, aquarium.Cleanliness - (CLEANLINESS_DECAY_PER_HOUR * hoursSinceLastUpdate));

        // Update temperature (drifts toward 20°C)
        var targetTemp = 20.0;
        var tempDiff = targetTemp - aquarium.Temperature;
        aquarium.Temperature += tempDiff * (TEMPERATURE_CHANGE_PER_HOUR * hoursSinceLastUpdate / 10.0);

        // Update fish conditions
        foreach (var fish in aquarium.Fish.Where(f => f.IsAlive))
        {
            UpdateFishConditions(fish, aquarium, hoursSinceLastUpdate);
        }

        aquarium.LastUpdated = now;
    }

    private static void UpdateFishConditions(Data.Models.AquariumFish fish, Data.Models.Aquarium aquarium, double hoursPassed)
    {
        var now = DateTime.UtcNow;

        // Calculate happiness decay based on feeding and aquarium conditions
        var hoursSinceLastFed = (now - aquarium.LastFed).TotalHours;
        
        // Fish get unhappy if not fed for more than 12 hours
        if (hoursSinceLastFed > 12)
        {
            var feedingPenalty = Math.Min(3.0, (hoursSinceLastFed - 12) * 0.5); // Up to 3% per hour after 12 hours
            fish.Happiness = Math.Max(0, fish.Happiness - (feedingPenalty * hoursPassed));
        }

        // Cleanliness affects happiness
        if (aquarium.Cleanliness < 30)
        {
            var cleanlinessPenalty = (30 - aquarium.Cleanliness) * 0.05; // Up to 1.5% per hour at 0% cleanliness
            fish.Happiness = Math.Max(0, fish.Happiness - (cleanlinessPenalty * hoursPassed));
        }

        // Temperature affects happiness
        if (aquarium.Temperature < IDEAL_TEMPERATURE_MIN || aquarium.Temperature > IDEAL_TEMPERATURE_MAX)
        {
            var tempPenalty = Math.Abs(aquarium.Temperature - 22.0) * 0.1; // 0.1% per degree away from ideal
            fish.Happiness = Math.Max(0, fish.Happiness - (tempPenalty * hoursPassed));
        }

        // Health decays if happiness is low
        if (fish.Happiness < 30)
        {
            var healthPenalty = (30 - fish.Happiness) * 0.05; // Up to 1.5% per hour at 0% happiness
            fish.Health = Math.Max(0, fish.Health - (healthPenalty * hoursPassed));
        }

        // Fish dies if health reaches 0
        if (fish.Health <= 0)
        {
            fish.IsAlive = false;
            fish.Health = 0;
        }
    }
}