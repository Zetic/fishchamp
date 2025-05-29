using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace ZPT.Services;

public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;

    public BotService(ILogger<BotService> logger, IServiceProvider services, IConfiguration configuration)
    {
        _logger = logger;
        _services = services;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZPT Discord Bot starting...");

        // Test services
        using var scope = _services.CreateScope();
        
        var token = _configuration["DISCORD_TOKEN"] ?? Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("DISCORD_TOKEN not found in configuration or environment variables");
            return;
        }
        
        _logger.LogInformation("ZPT Discord Bot started successfully");
        
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