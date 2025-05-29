using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Remora.Discord.Gateway;

namespace ZPT.Services;

public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly IServiceProvider _services;
    private readonly DiscordGatewayClient _gatewayClient;

    public BotService(ILogger<BotService> logger, IServiceProvider services, DiscordGatewayClient gatewayClient)
    {
        _logger = logger;
        _services = services;
        _gatewayClient = gatewayClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZPT Discord Bot starting...");

        // Test services
        using var scope = _services.CreateScope();
        var gameData = scope.ServiceProvider.GetRequiredService<GameDataService>();
        _logger.LogInformation("Loaded {AreaCount} areas and {FishCount} fish species", 
            gameData.Areas.Count, gameData.Fish.Count);
        
        // Run the gateway client
        var result = await _gatewayClient.RunAsync(stoppingToken);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to run Discord gateway: {Error}", result.Error);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ZPT Discord Bot stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("ZPT Discord Bot stopped");
    }
}