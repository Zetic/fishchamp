using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ZPT.Services;
using ZPT.Tests;

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
        
        // Run integration tests
        await ServiceIntegrationTest.RunTestAsync(scope.ServiceProvider);
        
        _logger.LogInformation("ZPT Discord Bot initialization complete - ready for Discord integration");
        
        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ZPT Discord Bot stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("ZPT Discord Bot stopped");
    }
}