using System.ComponentModel;
using System.Text;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using FishChamp.Helpers;
using FishChamp.Providers;

namespace FishChamp.Commands;

[Group("aquarium")]
[Description("Aquarium management commands")]
public class AquariumCommandGroup(IInteractionCommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAquariumRepository aquariumRepository, FeedbackService feedbackService) : CommandGroup
{
    [Command("view")]
    [Description("View your aquarium status and fish")]
    public async Task<IResult> ViewAquariumAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        // Apply degradation when viewing
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var fields = new List<EmbedField>
        {
            new("Fish Count", $"{aquarium.Fish.Count(f => f.IsAlive)}/{aquarium.Capacity}", true),
            new("Temperature", $"{aquarium.Temperature:F1}°C", true),
            new("Cleanliness", $"{aquarium.Cleanliness:F0}%", true)
        };

        // Add maintenance status
        var timeSinceLastFed = DateTime.UtcNow - aquarium.LastFed;
        var timeSinceLastCleaned = DateTime.UtcNow - aquarium.LastCleaned;
        
        var feedingStatus = timeSinceLastFed.TotalHours switch
        {
            < 4 => "🟢 Well Fed",
            < 6 => "🟡 Getting Hungry", 
            < 12 => "🟠 Hungry",
            _ => "🔴 Starving"
        };
        
        var cleanlinessStatus = aquarium.Cleanliness switch
        {
            >= 80 => "🟢 Sparkling Clean",
            >= 60 => "🟡 Mostly Clean",
            >= 40 => "🟠 Getting Dirty",
            >= 20 => "🔴 Very Dirty",
            _ => "⚫ Filthy"
        };

        fields.Add(new("Feeding Status", feedingStatus, true));
        fields.Add(new("Tank Status", cleanlinessStatus, true));

        string description = "";
        
        // Fish list
        if (aquarium.Fish.Any())
        {
            var fishText = new StringBuilder();
            var livingFish = aquarium.Fish.Where(f => f.IsAlive).Take(15);
            var deadFish = aquarium.Fish.Where(f => !f.IsAlive).Take(5);
            
            foreach (var fish in livingFish)
            {
                var rarityEmoji = GetRarityEmoji(fish.Rarity);
                var healthEmoji = fish.Health > 80 ? "💚" : fish.Health > 50 ? "💛" : "❤️";
                var happinessEmoji = fish.Happiness > 80 ? "😊" : fish.Happiness > 50 ? "😐" : "😢";
                
                fishText.AppendLine($"{rarityEmoji} **{fish.Name}** {healthEmoji}{happinessEmoji}");
            }

            if (deadFish.Any())
            {
                fishText.AppendLine("\n💀 **Deceased:**");
                foreach (var fish in deadFish)
                {
                    fishText.AppendLine($"☠️ {fish.Name}");
                }
            }

            if (aquarium.Fish.Count(f => f.IsAlive) > 15)
            {
                fishText.AppendLine($"... and {aquarium.Fish.Count(f => f.IsAlive) - 15} more living fish");
            }

            fields.Add(new EmbedField("🐟 Fish", fishText.ToString(), false));
        }
        else
        {
            description = "Your aquarium is empty. Use `/aquarium add` to transfer fish from your inventory.";
        }

        var embed = new Embed
        {
            Title = $"🐠 {aquarium.Name}",
            Description = description,
            Colour = Color.Cyan,
            Fields = fields,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("add")]
    [Description("Add a fish from your inventory to the aquarium")]
    public async Task<IResult> AddFishAsync(
        [Description("Fish type to add")]
        [AutocompleteProvider("autocomplete::aquarium_fish")]
        string fishType)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualErrorAsync("You don't have an inventory yet. Go fishing first!");
        }

        // Check if aquarium has space
        if (aquarium.Fish.Count >= aquarium.Capacity)
        {
            return await feedbackService.SendContextualErrorAsync($"Your aquarium is full! ({aquarium.Fish.Count}/{aquarium.Capacity})");
        }

        // Find the fish in inventory
        var fishItem = inventory.Items.FirstOrDefault(i => i.ItemType == "Fish" && 
            (i.ItemId.Equals(fishType, StringComparison.OrdinalIgnoreCase) || 
             i.Name.Equals(fishType, StringComparison.OrdinalIgnoreCase)));

        if (fishItem == null)
        {
            return await feedbackService.SendContextualErrorAsync($"You don't have any **{fishType}** in your inventory.");
        }

        // Convert inventory item to aquarium fish
        var aquariumFish = AquariumFish.FromInventoryItem(fishItem);
        aquarium.Fish.Add(aquariumFish);

        // Remove from inventory
        await inventoryRepository.RemoveItemAsync(user.ID.Value, fishItem.ItemId, 1);

        // Update aquarium
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🐠 Fish Added to Aquarium!",
            Description = $"**{aquariumFish.Name}** has been added to your aquarium!",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Fish Count", $"{aquarium.Fish.Count}/{aquarium.Capacity}", true),
                new("Rarity", aquariumFish.Rarity, true),
                new("Size", $"{aquariumFish.Size}cm", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("remove")]
    [Description("Remove a fish from your aquarium back to inventory")]
    public async Task<IResult> RemoveFishAsync(
        [Description("Fish name or type to remove")]
        [AutocompleteProvider("autocomplete::aquarium_remove_fish")]
        string fishIdentifier)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        if (!aquarium.Fish.Any())
        {
            return await feedbackService.SendContextualErrorAsync("Your aquarium is empty!");
        }

        // Find the fish in aquarium
        var fish = aquarium.Fish.FirstOrDefault(f => 
            f.Name.Equals(fishIdentifier, StringComparison.OrdinalIgnoreCase) ||
            f.FishType.Equals(fishIdentifier, StringComparison.OrdinalIgnoreCase) ||
            f.FishId.Equals(fishIdentifier, StringComparison.OrdinalIgnoreCase));

        if (fish == null)
        {
            return await feedbackService.SendContextualErrorAsync($"No fish matching **{fishIdentifier}** found in your aquarium.");
        }

        // Convert back to inventory item
        var inventoryItem = fish.ToInventoryItem();
        await inventoryRepository.AddItemAsync(user.ID.Value, inventoryItem);

        // Remove from aquarium
        aquarium.Fish.Remove(fish);
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🐠 Fish Removed from Aquarium!",
            Description = $"**{fish.Name}** has been returned to your inventory.",
            Colour = Color.Orange,
            Fields = new List<EmbedField>
            {
                new("Fish Count", $"{aquarium.Fish.Count}/{aquarium.Capacity}", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("clean")]
    [Description("Clean your aquarium to improve cleanliness")]
    public async Task<IResult> CleanAquariumAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        // Cleaning improves cleanliness but takes time and has a cooldown
        var timeSinceLastCleaned = DateTime.UtcNow - aquarium.LastCleaned;
        if (timeSinceLastCleaned.TotalHours < 1)
        {
            var remainingTime = TimeSpan.FromHours(1) - timeSinceLastCleaned;
            return await feedbackService.SendContextualErrorAsync($"You just cleaned your aquarium! Please wait {remainingTime.Minutes} more minutes.");
        }

        var oldCleanliness = aquarium.Cleanliness;
        aquarium.Cleanliness = Math.Min(100, aquarium.Cleanliness + 30);
        aquarium.LastCleaned = DateTime.UtcNow;

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🧽 Aquarium Cleaned!",
            Description = "You've cleaned your aquarium! Your fish look much happier.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Cleanliness", $"{oldCleanliness:F0}% → {aquarium.Cleanliness:F0}%", true),
                new("Next Cleaning", "Available in 1 hour", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("feed")]
    [Description("Feed your fish to keep them happy and healthy")]
    public async Task<IResult> FeedFishAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        if (!aquarium.Fish.Any())
        {
            return await feedbackService.SendContextualErrorAsync("Your aquarium is empty! Add some fish first.");
        }

        // Feeding has a cooldown
        var timeSinceLastFed = DateTime.UtcNow - aquarium.LastFed;
        if (timeSinceLastFed.TotalHours < 4)
        {
            var remainingTime = TimeSpan.FromHours(4) - timeSinceLastFed;
            return await feedbackService.SendContextualErrorAsync($"Your fish are still full! Please wait {remainingTime.Hours}h {remainingTime.Minutes}m before feeding again.");
        }

        aquarium.LastFed = DateTime.UtcNow;

        // Feeding improves happiness of all living fish
        var fedCount = 0;
        foreach (var fish in aquarium.Fish.Where(f => f.IsAlive))
        {
            fish.Happiness = Math.Min(100, fish.Happiness + 15);
            fedCount++;
        }

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🍽️ Fish Fed!",
            Description = $"You've fed {fedCount} fish! They're swimming happily around the tank.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Fish Fed", fedCount.ToString(), true),
                new("Next Feeding", "Available in 4 hours", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("temperature")]
    [Description("Adjust the aquarium temperature")]
    public async Task<IResult> AdjustTemperatureAsync(
        [Description("Target temperature (15-30°C)")]
        double targetTemperature)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (targetTemperature < 15 || targetTemperature > 30)
        {
            return await feedbackService.SendContextualErrorAsync("Temperature must be between 15°C and 30°C.");
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        var oldTemperature = aquarium.Temperature;
        aquarium.Temperature = targetTemperature;

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var temperatureStatus = targetTemperature switch
        {
            >= 20 and <= 25 => "🌡️ Perfect temperature range!",
            >= 18 and < 20 => "❄️ A bit cool, but acceptable",
            > 25 and <= 28 => "🔥 A bit warm, but acceptable", 
            _ => "⚠️ Extreme temperature - your fish may become stressed!"
        };

        var embed = new Embed
        {
            Title = "🌡️ Temperature Adjusted!",
            Description = temperatureStatus,
            Colour = targetTemperature >= 20 && targetTemperature <= 25 ? Color.Green : Color.Orange,
            Fields = new List<EmbedField>
            {
                new("Temperature", $"{oldTemperature:F1}°C → {targetTemperature:F1}°C", true),
                new("Optimal Range", "20°C - 25°C", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("help")]
    [Description("Show aquarium system help and commands")]
    public async Task<IResult> HelpAsync()
    {
        var embed = new Embed
        {
            Title = "🐠 Aquarium System Help",
            Description = "Manage your personal fish tank with these commands:",
            Colour = Color.Blue,
            Fields = new List<EmbedField>
            {
                new("📋 Basic Commands", 
                    "`/aquarium view` - View your aquarium and fish\n" +
                    "`/aquarium add <fish>` - Add a fish from inventory\n" +
                    "`/aquarium remove <fish>` - Remove a fish to inventory\n" +
                    "`/aquarium help` - Show this help message", false),
                new("🔧 Maintenance Commands",
                    "`/aquarium clean` - Clean the tank (1 hour cooldown)\n" +
                    "`/aquarium feed` - Feed your fish (4 hour cooldown)\n" +
                    "`/aquarium temperature <temp>` - Adjust temperature (15-30°C)", false),
                new("🏠 Tank Features",
                    "• **Capacity**: Start with 10 fish slots\n" +
                    "• **Health**: Fish health and happiness tracking\n" +
                    "• **Environment**: Temperature and cleanliness monitoring\n" +
                    "• **Maintenance**: Keep your fish happy with regular care!", false),
                new("💡 Tips",
                    "• Feed fish every 4-6 hours to keep them happy\n" +
                    "• Clean the tank when cleanliness drops below 50%\n" +
                    "• Keep temperature between 20-25°C for optimal health\n" +
                    "• Unhappy or unhealthy fish can't breed!", false)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await playerRepository.CreatePlayerAsync(userId, username);
            await inventoryRepository.CreateInventoryAsync(userId);
        }
        return player;
    }

    private async Task<Aquarium> GetOrCreateAquariumAsync(ulong userId)
    {
        var aquarium = await aquariumRepository.GetAquariumAsync(userId);
        if (aquarium == null)
        {
            aquarium = await aquariumRepository.CreateAquariumAsync(userId);
        }
        return aquarium;
    }

    private static string GetRarityEmoji(string rarity)
    {
        return rarity.ToLower() switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵", 
            "epic" => "🟣",
            "legendary" => "🟡",
            "mythic" => "🔴",
            _ => "⚪"
        };
    }
}