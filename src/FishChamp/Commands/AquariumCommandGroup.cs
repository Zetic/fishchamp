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

        var aliveFish = aquarium.Fish.Count(f => f.IsAlive);
        var deadFish = aquarium.Fish.Count(f => !f.IsAlive);
        
        var fields = new List<EmbedField>
        {
            new("Fish Count", $"{aliveFish}/{aquarium.Capacity}" + (deadFish > 0 ? $" ({deadFish} dead)" : ""), true),
            new("Temperature", $"{aquarium.Temperature:F1}°C", true),
            new("Cleanliness", $"{aquarium.Cleanliness:F0}%", true)
        };

        // Add maintenance status
        var hoursSinceLastFed = (DateTime.UtcNow - aquarium.LastFed).TotalHours;
        var hoursSinceLastCleaned = (DateTime.UtcNow - aquarium.LastCleaned).TotalHours;
        var feedStatus = hoursSinceLastFed > 12 ? "🔴 Hungry!" : hoursSinceLastFed > 6 ? "🟡 Ready to feed" : "🟢 Well fed";
        var cleanStatus = hoursSinceLastCleaned > 8 ? "🔴 Very dirty!" : hoursSinceLastCleaned > 4 ? "🟡 Needs cleaning" : "🟢 Clean";
        
        fields.Add(new EmbedField("Feeding Status", feedStatus, true));
        fields.Add(new EmbedField("Cleanliness Status", cleanStatus, true));

        string description = "";
        
        // Fish list
        if (aquarium.Fish.Any())
        {
            var fishText = new StringBuilder();
            foreach (var fish in aquarium.Fish.Take(15)) // Limit display to 15 fish
            {
                var rarityEmoji = GetRarityEmoji(fish.Rarity);
                var statusEmoji = fish.IsAlive ? 
                    (fish.Health > 80 ? "💚" : fish.Health > 50 ? "💛" : "❤️") : "💀";
                var happinessEmoji = fish.IsAlive ? 
                    (fish.Happiness > 80 ? "😊" : fish.Happiness > 50 ? "😐" : "😢") : "";
                
                fishText.AppendLine($"{rarityEmoji} **{fish.Name}** {statusEmoji}{happinessEmoji}");
            }

            if (aquarium.Fish.Count > 15)
            {
                fishText.AppendLine($"... and {aquarium.Fish.Count - 15} more fish");
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

    [Command("feed")]
    [Description("Feed all fish in your aquarium")]
    public async Task<IResult> FeedFishAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        if (!aquarium.Fish.Any(f => f.IsAlive))
        {
            return await feedbackService.SendContextualErrorAsync("Your aquarium has no living fish to feed!");
        }

        // Check if already fed recently (within 6 hours)
        var hoursSinceLastFed = (DateTime.UtcNow - aquarium.LastFed).TotalHours;
        if (hoursSinceLastFed < 6)
        {
            var timeUntilNextFeed = TimeSpan.FromHours(6 - hoursSinceLastFed);
            return await feedbackService.SendContextualErrorAsync(
                $"Your fish don't need feeding yet! You can feed them again in {timeUntilNextFeed.Hours}h {timeUntilNextFeed.Minutes}m.");
        }

        // Feed the fish - boost happiness
        var fishFed = 0;
        foreach (var fish in aquarium.Fish.Where(f => f.IsAlive))
        {
            fish.Happiness = Math.Min(100.0, fish.Happiness + 15.0); // +15% happiness
            fishFed++;
        }

        aquarium.LastFed = DateTime.UtcNow;
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🍤 Fish Fed Successfully!",
            Description = $"You fed {fishFed} fish in your aquarium. They look much happier!",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Next Feeding", "Available in 6 hours", true),
                new("Happiness Boost", "+15% for all fish", true)
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

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        // Check if already cleaned recently (within 4 hours)
        var hoursSinceLastCleaned = (DateTime.UtcNow - aquarium.LastCleaned).TotalHours;
        if (hoursSinceLastCleaned < 4)
        {
            var timeUntilNextClean = TimeSpan.FromHours(4 - hoursSinceLastCleaned);
            return await feedbackService.SendContextualErrorAsync(
                $"Your aquarium doesn't need cleaning yet! You can clean it again in {timeUntilNextClean.Hours}h {timeUntilNextClean.Minutes}m.");
        }

        var oldCleanliness = aquarium.Cleanliness;
        aquarium.Cleanliness = Math.Min(100.0, aquarium.Cleanliness + 30.0); // +30% cleanliness
        aquarium.LastCleaned = DateTime.UtcNow;
        
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var cleanlinessIncrease = aquarium.Cleanliness - oldCleanliness;

        var embed = new Embed
        {
            Title = "🧽 Aquarium Cleaned!",
            Description = "You cleaned your aquarium, removing algae and debris.",
            Colour = Color.Blue,
            Fields = new List<EmbedField>
            {
                new("Cleanliness", $"{aquarium.Cleanliness:F0}% (+{cleanlinessIncrease:F0}%)", true),
                new("Next Cleaning", "Available in 4 hours", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("temperature")]
    [Description("Adjust the aquarium temperature")]
    public async Task<IResult> AdjustTemperatureAsync(
        [Description("Target temperature (18-26°C)")]
        double targetTemperature)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (targetTemperature < 18.0 || targetTemperature > 26.0)
        {
            return await feedbackService.SendContextualErrorAsync("Temperature must be between 18°C and 26°C!");
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        var oldTemperature = aquarium.Temperature;
        aquarium.Temperature = targetTemperature;
        
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var temperatureChange = Math.Abs(targetTemperature - oldTemperature);
        var idealRange = targetTemperature >= 20.0 && targetTemperature <= 24.0;

        var embed = new Embed
        {
            Title = "🌡️ Temperature Adjusted!",
            Description = $"Aquarium temperature set to {targetTemperature:F1}°C",
            Colour = idealRange ? Color.Green : Color.Orange,
            Fields = new List<EmbedField>
            {
                new("Previous Temperature", $"{oldTemperature:F1}°C", true),
                new("New Temperature", $"{targetTemperature:F1}°C", true),
                new("Ideal Range", "20-24°C", true)
            },
            Footer = idealRange ? 
                new EmbedFooter("Perfect temperature for happy fish! 🐠") :
                new EmbedFooter("Fish prefer temperatures between 20-24°C for optimal happiness."),
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
                new("🧽 Maintenance Commands",
                    "`/aquarium feed` - Feed all fish (every 6 hours)\n" +
                    "`/aquarium clean` - Clean the aquarium (every 4 hours)\n" +
                    "`/aquarium temperature <temp>` - Adjust temperature (18-26°C)", false),
                new("🏠 Tank Features",
                    "• **Capacity**: Start with 10 fish slots\n" +
                    "• **Health**: Fish health decays without proper care\n" +
                    "• **Happiness**: Affected by feeding, cleanliness, and temperature\n" +
                    "• **Consequences**: Unhappy fish become unhealthy and may die!", false),
                new("💡 Maintenance Tips",
                    "• Feed fish every 6 hours to keep them happy\n" +
                    "• Clean aquarium every 4 hours to prevent disease\n" +
                    "• Keep temperature between 20-24°C for optimal fish happiness\n" +
                    "• Neglected fish will become unhappy and eventually die!", false)
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