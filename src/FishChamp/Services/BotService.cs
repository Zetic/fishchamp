using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.Gateway;

namespace FishChamp.Services;

public class BotService(DiscordGatewayClient gatewayClient, ILogger<BotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FishChamp Discord Bot starting up...");
        
        var runResult = await gatewayClient.RunAsync(stoppingToken);
        
        if (!runResult.IsSuccess)
        {
            logger.LogError("Failed to start bot: {Error}", runResult.Error);
        }
    }
}