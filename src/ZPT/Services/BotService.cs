using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZPT.Services;

public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;

    public BotService(ILogger<BotService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot service starting...");
        
        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}