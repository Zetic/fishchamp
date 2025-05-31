using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Commands.Extensions;
using FishChamp.Configuration;
using FishChamp.Modules;
using FishChamp.Data.Repositories;
using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using FishChamp.Providers;
using Remora.Discord.Gateway.Extensions;
using FishChamp.Responders;
using FishChamp.Helpers;
using Remora.Discord.Interactivity.Extensions;
using FishChamp.Interactions;
using FishChamp.Services;
using FishChamp.Tracker;
using FishChamp.Data.Models;
using System.Collections.Concurrent;

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

                if (string.IsNullOrEmpty(token))
                {
                    throw new Exception("Discord token is not configured. Please set the 'Discord:Token' in appsettings.json or environment variables.");
                }

                return token;
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<DatabaseConfiguration>(context.Configuration.GetSection("Discord"));
                services.Configure<DatabaseConfiguration>(context.Configuration.GetSection("Database"));

                // Discord Bot
                services.AddDiscordCommands(true)
                    .AddResponder<SlashCommandConfigurator>()
                    .AddAutocompleteProvider<AreaFishSpotAutocompleteProvider>()

                    .AddCommandTree()
                        .WithCommandGroup<FishingCommandGroup>()
                        .WithCommandGroup<FishCommandGroup>()
                        .WithCommandGroup<MapCommandGroup>()
                        .WithCommandGroup<InventoryCommandGroup>()
                        .WithCommandGroup<ShopCommandGroup>()
                        .WithCommandGroup<TrapCommandGroup>()
                            .Finish()

                    .AddInteractivity()
                    .AddInteractionGroup<FishingInteractionGroup>();

                // Repositories and Services
                services.AddSingleton<DiscordHelper>();
                services.AddSingleton<IPlayerRepository, JsonPlayerRepository>();
                services.AddSingleton<IInventoryRepository, JsonInventoryRepository>();
                services.AddSingleton<IAreaRepository, JsonAreaRepository>();
                services.AddSingleton<ITrapRepository, JsonTrapRepository>();

                services.AddSingleton<IInstanceTracker<FishingInstance>, InstanceTracker<FishingInstance>>();
                services.AddHostedService<FishingInstanceUpdaterService>();
                services.AddHostedService<TrapUpdaterService>();

                

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