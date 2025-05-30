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

namespace FishChamp.Modules;

[Group("map")]
[Description("Map and navigation commands")]
public class MapModule(IDiscordRestChannelAPI channelAPI, IDiscordRestUserAPI userAPI, ICommandContext context,
    IPlayerRepository playerRepository, IAreaRepository areaRepository) : CommandGroup
{
    [Command("current")]
    [Description("View your current area")]
    public async Task<IResult> ViewCurrentAreaAsync()
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.FromError(new NotFoundError("Failed to get user id from context"));
        }

        var userResult = await userAPI.GetUserAsync(userId);

        if (!userResult.IsSuccess)
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(userId.Value, userResult.Entity.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await RespondAsync("🚫 Current area not found!");
        }

        var spotsText = string.Join("\n", currentArea.FishingSpots.Select(spot => 
            $"• **{spot.Name}** ({spot.Type}) - {spot.AvailableFish.Count} fish species"));

        var connectedAreasText = "None";
        if (currentArea.ConnectedAreas.Count > 0)
        {
            var connectedAreas = await areaRepository.GetAllAreasAsync();
            connectedAreasText = string.Join(", ", 
                connectedAreas.Where(a => currentArea.ConnectedAreas.Contains(a.AreaId))
                             .Select(a => a.Name));
        }

        var embed = new Embed
        {
            Title = $"🗺️ {currentArea.Name}",
            Description = currentArea.Description,
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Fishing Spots", spotsText.Length > 0 ? spotsText : "None", false),
                new("Connected Areas", connectedAreasText, false)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await RespondAsync(embeds: [embed]);
    }

    [Command("travel")]
    [Description("Travel to a connected area")]
    public async Task<IResult> TravelToAreaAsync([Description("Area to travel to")] string targetAreaName)
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.FromError(new NotFoundError("Failed to get user id from context"));
        }

        var userResult = await userAPI.GetUserAsync(userId);

        if (!userResult.IsSuccess)
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(userId.Value, userResult.Entity.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await RespondAsync("🚫 Current area not found!");
        }

        var allAreas = await areaRepository.GetAllAreasAsync();
        var targetArea = allAreas.FirstOrDefault(a => 
            a.Name.Equals(targetAreaName, StringComparison.OrdinalIgnoreCase) ||
            a.AreaId.Equals(targetAreaName.Replace(" ", "_").ToLower(), StringComparison.OrdinalIgnoreCase));

        if (targetArea == null)
        {
            return await RespondAsync($"🚫 Area '{targetAreaName}' not found!");
        }

        if (!currentArea.ConnectedAreas.Contains(targetArea.AreaId))
        {
            return await RespondAsync($"🚫 You cannot travel to {targetArea.Name} from your current location!");
        }

        if (!targetArea.IsUnlocked)
        {
            return await RespondAsync($"🔒 {targetArea.Name} is locked! Requirement: {targetArea.UnlockRequirement}");
        }

        player.CurrentArea = targetArea.AreaId;
        player.LastActive = DateTime.UtcNow;
        await playerRepository.UpdatePlayerAsync(player);

        return await RespondAsync($"🚶‍♂️ You have traveled to **{targetArea.Name}**!\n\n" +
                                $"*{targetArea.Description}*");
    }

    [Command("areas")]
    [Description("List all available areas")]
    public async Task<IResult> ListAreasAsync()
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.FromError(new NotFoundError("Failed to get user id from context"));
        }

        var userResult = await userAPI.GetUserAsync(userId);

        if (!userResult.IsSuccess)
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(userId.Value, userResult.Entity.Username);
        var allAreas = await areaRepository.GetAllAreasAsync();

        var areasText = string.Join("\n", allAreas.Select(area =>
        {
            var status = area.IsUnlocked ? "🟢" : "🔒";
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

        return await RespondAsync(embeds: [embed]);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        return await playerRepository.GetPlayerAsync(userId) ?? await playerRepository.CreatePlayerAsync(userId, username);
    }

    private async Task<IResult> RespondAsync(string content = "", IReadOnlyList<Embed>? embeds = null)
    {
        var embedsParam = embeds != null ? new Optional<IReadOnlyList<IEmbed>>(embeds.Cast<IEmbed>().ToList()) : default;

        if (!context.TryGetChannelID(out var channelID))
        {
            return Result.FromError(new NotFoundError("Failed to get channel id from context"));
        }

        await channelAPI.CreateMessageAsync(channelID, content, embeds: embedsParam);
        return Result.FromSuccess();
    }
}