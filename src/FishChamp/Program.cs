using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.Gateway.Extensions;
using Remora.Commands.Extensions;
using FishChamp.Configuration;
using FishChamp.Services;
using FishChamp.Modules;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;

namespace FishChamp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<DiscordConfiguration>(context.Configuration.GetSection("Discord"));
                services.Configure<DatabaseConfiguration>(context.Configuration.GetSection("Database"));

                // Discord Bot
                var discordConfig = context.Configuration.GetSection("Discord").Get<DiscordConfiguration>();
                services
                    .AddDiscordGateway(_ => discordConfig?.Token ?? throw new InvalidOperationException("Discord token not configured"))
                    .AddCommands()
                    .AddCommandTree()
                        .WithCommandGroup<FishingModule>()
                        .WithCommandGroup<MapModule>()
                        .WithCommandGroup<InventoryModule>()
                        .WithCommandGroup<ShopModule>()
                    .Finish();

                // Repositories and Services
                services.AddSingleton<IPlayerRepository, JsonPlayerRepository>();
                services.AddSingleton<IInventoryRepository, JsonInventoryRepository>();
                services.AddSingleton<IAreaRepository, JsonAreaRepository>();
                services.AddSingleton<IFishDataService, FishDataService>();
                services.AddSingleton<BotService>();

                // Ensure data directory exists
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .UseConsoleLifetime();
}