using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using Remora.Rest.Core;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using Remora.Discord.Commands.Extensions;
using Polly;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Attributes;

namespace FishChamp.Modules;

[Group("map")]
[Description("Map and navigation commands")]
public class MapCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IAreaRepository areaRepository, FeedbackService feedbackService) : CommandGroup
{
    [Command("current")]
    [Description("View your current area")]
    public async Task<IResult> ViewCurrentAreaAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        var spotsText = string.Join("\n", currentArea.FishingSpots.Select(spot => 
        {
            var isCurrentSpot = spot.SpotId == player.CurrentFishingSpot ? " 📍 **(Current)**" : "";
            return $"• **{spot.Name}** ({spot.Type}) - {spot.AvailableFish.Count} fish species{isCurrentSpot}";
        }));
            
        var farmSpotsText = "None";
        if (currentArea.FarmSpots.Count > 0)
        {
            farmSpotsText = string.Join("\n", currentArea.FarmSpots.Select(spot =>
                $"• **{spot.Name}** - {spot.AvailableCrops.Count} crops" + (spot.CanDigForWorms ? " (Can dig for worms)" : "")));
        }

        var connectedAreasText = "None";
        if (currentArea.ConnectedAreas.Count > 0)
        {
            var connectedAreas = await areaRepository.GetAllAreasAsync();
            connectedAreasText = string.Join(", ", 
                connectedAreas.Where(a => currentArea.ConnectedAreas.Contains(a.AreaId))
                             .Select(a => a.Name));
        }
        
        var shopsText = "None";
        if (currentArea.Shops?.Count > 0)
        {
            shopsText = string.Join("\n", currentArea.Shops.Values.Select(shop =>
                $"• **{shop.Name}** - {shop.Items.Count(i => i.InStock)} items"));
        }

        var embed = new Embed
        {
            Title = $"🗺️ {currentArea.Name}",
            Description = currentArea.Description,
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("🎣 Fishing Spots", spotsText.Length > 0 ? spotsText : "None", false),
                new("🌱 Farm Spots", farmSpotsText, false),
                new("🏪 Shops", shopsText, false),
                new("🧭 Connected Areas", connectedAreasText, false)
            },
            Footer = new EmbedFooter("Use /map travel <area> to move or /map goto <spot> to go to fishing spots"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("travel")]
    [Description("Travel to a connected area")]
    public async Task<IResult> TravelToAreaAsync([Description("Area to travel to")] [AutocompleteProvider("autocomplete::area")] string targetAreaName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        var allAreas = await areaRepository.GetAllAreasAsync();
        var targetArea = allAreas.FirstOrDefault(a => 
            a.Name.Equals(targetAreaName, StringComparison.OrdinalIgnoreCase) ||
            a.AreaId.Equals(targetAreaName.Replace(" ", "_").ToLower(), StringComparison.OrdinalIgnoreCase));

        if (targetArea == null)
        {
            return await feedbackService.SendContextualContentAsync($"🚫 Area '{targetAreaName}' not found!", Color.Red);
        }

        if (!currentArea.ConnectedAreas.Contains(targetArea.AreaId))
        {
            return await feedbackService.SendContextualContentAsync($"🚫 You cannot travel to {targetArea.Name} from your current location!", Color.Red);
        }

        if (!player.UnlockedAreas.Contains(targetArea.AreaId))
        {
            return await feedbackService.SendContextualContentAsync($"🔒 {targetArea.Name} is locked! Requirement: {targetArea.UnlockRequirement}", Color.Red);
        }

        player.CurrentArea = targetArea.AreaId;
        player.CurrentFishingSpot = string.Empty; // Clear fishing spot when traveling
        player.LastActive = DateTime.UtcNow;
        await playerRepository.UpdatePlayerAsync(player);

        return await feedbackService.SendContextualContentAsync($"🚶‍♂️ You have traveled to **{targetArea.Name}**!\n\n" +
                                $"*{targetArea.Description}*", Color.Green);
    }

    [Command("goto")]
    [Description("Go to a specific fishing spot in your current area")]
    public async Task<IResult> GoToFishingSpotAsync([Description("Fishing spot to go to")] [AutocompleteProvider("autocomplete::area_fishspot")] string fishingSpotName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        if (currentArea.FishingSpots.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🎣 No fishing spots available in this area!", Color.Red);
        }

        // Find the fishing spot by name or ID
        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => 
            f.SpotId.Equals(fishingSpotName, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
        {
            var availableSpots = string.Join(", ", currentArea.FishingSpots.Select(s => s.Name));
            return await feedbackService.SendContextualContentAsync(
                $"🚫 Fishing spot '{fishingSpotName}' not found!\n\nAvailable spots: {availableSpots}", Color.Red);
        }

        // Update player's current fishing spot
        player.CurrentFishingSpot = fishingSpot.SpotId;
        player.LastActive = DateTime.UtcNow;
        await playerRepository.UpdatePlayerAsync(player);

        return await feedbackService.SendContextualContentAsync(
            $"🎣 You are now at **{fishingSpot.Name}**!\n\n" +
            $"*{fishingSpot.Type} fishing spot with {fishingSpot.AvailableFish.Count} fish species available.*\n\n" +
            $"Use `/fish` to start fishing!", Color.Green);
    }

    [Command("areas")]
    [Description("List all available areas")]
    public async Task<IResult> ListAreasAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var allAreas = await areaRepository.GetAllAreasAsync();

        var areasText = string.Join("\n", allAreas.Select(area =>
        {
            var status = player.UnlockedAreas.Contains(area.AreaId) ? "🟢" : "🔒";
            var current = area.AreaId == player.CurrentArea ? " **(Current)**" : "";
            return $"{status} **{area.Name}**{current}";
        }));

        var embed = new Embed
        {
            Title = "🗺️ Available Areas",
            Description = areasText,
            Colour = Color.Purple,
            Footer = new EmbedFooter("Use `/map travel <area>` to travel to connected areas"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        return await playerRepository.GetPlayerAsync(userId) ?? await playerRepository.CreatePlayerAsync(userId, username);
    }
}