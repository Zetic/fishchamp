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
using FishChamp.Services;
using FishChamp.Tracker;
using System.Collections.Concurrent;
using FishChamp.Minigames.Digging;
using FishChamp.Minigames.Fishing;
using FishChamp.Features.Tournaments;
using FishChamp.Features.Trading;
using FishChamp.Events;
using FishChamp.Features.Aquariums;
using FishChamp.Features.Farming;
using FishChamp.Features.Trapping;
using FishChamp.Features.Guilds;
using FishChamp.Features.Shops;
using FishChamp.Features.Crafting;
using FishChamp.Features.Housing;
using FishChamp.Features.FishDex;
using FishChamp.Commands;

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
                    .AddAutocompleteProvider<GuildInviteAutocompleteProvider>()
                    .AddAutocompleteProvider<ShopAutocompleteProvider>()
                    .AddAutocompleteProvider<ShopItemsAutocompleteProvider>()
                    .AddAutocompleteProvider<AquariumFishAutocompleteProvider>()
                    .AddAutocompleteProvider<AquariumRemoveFishAutocompleteProvider>()
                    .AddAutocompleteProvider<FarmSpotAutocompleteProvider>()
                    .AddAutocompleteProvider<SeedTypeAutocompleteProvider>()
                    .AddAutocompleteProvider<GuildInviteAutocompleteProvider>()

                    .AddCommandTree()
                        .WithCommandGroup<FishingCommandGroup>()
                        .WithCommandGroup<FishCommandGroup>()
                        .WithCommandGroup<MainGameHubCommandGroup>()
                        .WithCommandGroup<MapCommandGroup>()
                        .WithCommandGroup<InventoryCommandGroup>()
                        .WithCommandGroup<ShopCommandGroup>()
                        .WithCommandGroup<TrapCommandGroup>()
                        .WithCommandGroup<CraftingCommandGroup>()
                        .WithCommandGroup<AquariumCommandGroup>()
                        .WithCommandGroup<FarmCommandGroup>()
                        .WithCommandGroup<BoatCommandGroup>()
                        .WithCommandGroup<LandCommandGroup>()
                        .WithCommandGroup<TradeCommandGroup>()
                        .WithCommandGroup<MarketCommandGroup>()
                        .WithCommandGroup<TournamentCommandGroup>()
                        .WithCommandGroup<GuildCommandGroup>()
                        .WithCommandGroup<EventCommandGroup>()
                        .WithCommandGroup<WorldBossCommandGroup>()
                        .WithCommandGroup<FishDexCommand>()
                            .Finish()

                    .AddInteractivity()
                    .AddInteractionGroup<FishingInteractionGroup>()
                    .AddInteractionGroup<DirtDiggingInteractionGroup>()
                    .AddInteractionGroup<MainMenuInteractionGroup>();

                // Repositories and Services
                services.AddSingleton<DiscordHelper>();
                services.AddSingleton<IPlayerRepository, JsonPlayerRepository>();
                services.AddSingleton<IInventoryRepository, JsonInventoryRepository>();
                services.AddSingleton<IAreaRepository, JsonAreaRepository>();
                services.AddSingleton<ITrapRepository, JsonTrapRepository>();
                services.AddSingleton<IAquariumRepository, JsonAquariumRepository>();
                services.AddSingleton<IFarmRepository, JsonFarmRepository>();
                services.AddSingleton<IBoatRepository, JsonBoatRepository>();
                services.AddSingleton<IPlotRepository, JsonPlotRepository>();
                services.AddSingleton<IHouseRepository, JsonHouseRepository>();
                
                // Social System Repositories
                services.AddSingleton<ITradeRepository, JsonTradeRepository>();
                services.AddSingleton<ITournamentRepository, JsonTournamentRepository>();
                services.AddSingleton<IGuildRepository, JsonGuildRepository>();
                services.AddSingleton<IEventRepository, JsonEventRepository>();
                
                services.AddSingleton<IAreaUnlockService, AreaUnlockService>();

                // Event System
                services.AddSingleton<IEventBus, EventBus>();

                services.AddSingleton<IInstanceTracker<FishingInstance>, InstanceTracker<FishingInstance>>();
                services.AddSingleton<IInstanceTracker<DirtDiggingInstance>, InstanceTracker<DirtDiggingInstance>>();

                services.AddHostedService<FishingInstanceUpdaterService>();
                services.AddHostedService<TrapUpdaterService>();
                services.AddHostedService<AquariumMaintenanceService>();
                services.AddHostedService<CropGrowthService>();
                services.AddHostedService<TournamentService>();
                services.AddHostedService<EventService>();
                services.AddHostedService<FishDexUpdaterService>();



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