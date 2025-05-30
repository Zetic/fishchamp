using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.Gateway;

namespace FishChamp.Services;

public class BotService : BackgroundService
{
    private readonly DiscordGatewayClient _gatewayClient;
    private readonly ILogger<BotService> _logger;

    public BotService(DiscordGatewayClient gatewayClient, ILogger<BotService> logger)
    {
        _gatewayClient = gatewayClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FishChamp Discord Bot starting up...");
        
        var runResult = await _gatewayClient.RunAsync(stoppingToken);
        
        if (!runResult.IsSuccess)
        {
            _logger.LogError("Failed to start bot: {Error}", runResult.Error);
        }
    }
}