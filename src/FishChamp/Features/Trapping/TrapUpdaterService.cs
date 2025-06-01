using FishChamp.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FishChamp.Features.Trapping;

public class TrapUpdaterService(ITrapRepository trapRepository, ILogger<TrapUpdaterService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TrapUpdaterService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateTraps();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Check every 5 minutes
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TrapUpdaterService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }

        logger.LogInformation("TrapUpdaterService stopped");
    }

    private async Task UpdateTraps()
    {
        var activeTraps = await trapRepository.GetActiveTrapsAsync();
        var now = DateTime.UtcNow;

        foreach (var trap in activeTraps)
        {
            // Check if trap has completed
            if (now >= trap.CompletesAt && !trap.IsCompleted)
            {
                logger.LogInformation("Trap {TrapId} completed for user {UserId}", trap.TrapId, trap.UserId);

                // Mark as completed - fish generation will happen when checked by user
                trap.IsCompleted = true;
                await trapRepository.UpdateTrapAsync(trap);
            }
        }
    }
}