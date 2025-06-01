using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;

namespace FishChamp.Features.Farming;

public class CropGrowthService(IFarmRepository farmRepository, ILogger<CropGrowthService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CropGrowthService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateCropGrowth();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Check every 5 minutes
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in CropGrowthService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }

        logger.LogInformation("CropGrowthService stopped");
    }

    private async Task UpdateCropGrowth()
    {
        try
        {
            var allFarms = await farmRepository.GetAllFarmsAsync();
            var now = DateTime.UtcNow;
            var farmsToUpdate = new List<Farm>();

            foreach (var farm in allFarms)
            {
                bool farmUpdated = false;

                foreach (var crop in farm.Crops)
                {
                    if (crop.Stage == CropStage.Planted && now >= crop.PlantedAt.AddMinutes(5))
                    {
                        crop.Stage = CropStage.Growing;
                        farmUpdated = true;
                    }
                    else if (crop.Stage == CropStage.Growing && now >= crop.ReadyAt)
                    {
                        crop.Stage = CropStage.Ready;
                        farmUpdated = true;
                    }
                }

                if (farmUpdated)
                {
                    farm.LastUpdated = now;
                    farmsToUpdate.Add(farm);
                }
            }

            // Update all farms that had changes
            foreach (var farm in farmsToUpdate)
            {
                await farmRepository.UpdateFarmAsync(farm);
            }

            if (farmsToUpdate.Count > 0)
            {
                logger.LogInformation("Updated growth status for {FarmCount} farms", farmsToUpdate.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating crop growth");
        }
    }
}