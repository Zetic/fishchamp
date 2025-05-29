using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Commands.Extensions;
using Remora.Discord.Gateway.Extensions;
using ZPT.Services;
using ZPT.Commands;

namespace ZPT;

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
                config.AddJsonFile("appsettings.json", optional: false)
                      .AddEnvironmentVariables()
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Discord bot token setup
                var token = context.Configuration["DISCORD_TOKEN"] ?? Environment.GetEnvironmentVariable("DISCORD_TOKEN");
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("DISCORD_TOKEN not found in configuration or environment variables");
                    Environment.Exit(1);
                }

                // Add Discord services
                services
                    .AddDiscordGateway(_ => token);

                // Register responders
                services.AddResponder<InteractionService>();
                services.AddResponder<MessageResponder>();

                // Add application services
                services.AddSingleton<GameDataService>();
                services.AddSingleton<UserManagerService>();
                services.AddSingleton<GameLogicService>();
                services.AddSingleton<OpenAIService>();
                services.AddSingleton<InventoryService>();
                services.AddSingleton<FishGeneratorService>();
                services.AddSingleton<RandomService>();

                // Add hosted service
                services.AddHostedService<BotService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}