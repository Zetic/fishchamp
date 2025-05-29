using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ZPT.Services;

namespace ZPT.Services;

public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly IServiceProvider _services;

    public BotService(ILogger<BotService> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZPT Discord Bot starting...");

        // Test services
        using var scope = _services.CreateScope();
        var gameData = scope.ServiceProvider.GetRequiredService<GameDataService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManagerService>();
        
        _logger.LogInformation("Services initialized successfully");
        _logger.LogInformation("Game areas loaded: {Count}", gameData.Areas.Count);
        _logger.LogInformation("Fish species loaded: {Count}", gameData.Fish.Count);
        
        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ZPT Discord Bot stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("ZPT Discord Bot stopped");
    }
}