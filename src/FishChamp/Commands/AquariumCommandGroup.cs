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

        var fields = new List<EmbedField>
        {
            new("Fish Count", $"{aquarium.Fish.Count}/{aquarium.Capacity}", true),
            new("Temperature", $"{aquarium.Temperature:F1}°C", true),
            new("Cleanliness", $"{aquarium.Cleanliness:F0}%", true)
        };

        string description = "";
        
        // Fish list
        if (aquarium.Fish.Any())
        {
            var fishText = new StringBuilder();
            foreach (var fish in aquarium.Fish.Take(15)) // Limit display to 15 fish
            {
                var rarityEmoji = GetRarityEmoji(fish.Rarity);
                var healthEmoji = fish.Health > 80 ? "💚" : fish.Health > 50 ? "💛" : "❤️";
                var happinessEmoji = fish.Happiness > 80 ? "😊" : fish.Happiness > 50 ? "😐" : "😢";
                
                fishText.AppendLine($"{rarityEmoji} **{fish.Name}** {healthEmoji}{happinessEmoji}");
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
    public async Task<IResult> AddFishAsync([Description("Fish type to add")] string fishType)
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
    public async Task<IResult> RemoveFishAsync([Description("Fish name or type to remove")] string fishIdentifier)
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