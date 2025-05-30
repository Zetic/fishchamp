using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Commands.Extensions;
using FishChamp.Configuration;
using FishChamp.Services;
using FishChamp.Modules;
using FishChamp.Data.Repositories;
using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;

namespace FishChamp;

public class Program
{
    private static IConfiguration configuration;

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

                configuration = config.Build();
            })
            .AddDiscordService((context) =>
            {
                var token = configuration["Discord:Token"];
                return token;
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<DatabaseConfiguration>(context.Configuration.GetSection("Discord"));
                services.Configure<DatabaseConfiguration>(context.Configuration.GetSection("Database"));

                // Discord Bot
                services.AddDiscordCommands(true);
                services.AddCommandTree()
                    .WithCommandGroup<FishingModule>()
                    .WithCommandGroup<MapModule>()
                    .WithCommandGroup<InventoryModule>()
                    .Finish();

                // Repositories and Services
                services.AddSingleton<IPlayerRepository, JsonPlayerRepository>();
                services.AddSingleton<IInventoryRepository, JsonInventoryRepository>();
                services.AddSingleton<IAreaRepository, JsonAreaRepository>();
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