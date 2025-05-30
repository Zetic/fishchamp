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

namespace FishChamp.Modules;

[Group("map")]
[Description("Map and navigation commands")]
public class MapModule : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly ICommandContext _context;
    private readonly IPlayerRepository _playerRepository;
    private readonly IAreaRepository _areaRepository;

    public MapModule(IDiscordRestChannelAPI channelAPI, ICommandContext context,
        IPlayerRepository playerRepository, IAreaRepository areaRepository)
    {
        _channelAPI = channelAPI;
        _context = context;
        _playerRepository = playerRepository;
        _areaRepository = areaRepository;
    }

    [Command("current")]
    [Description("View your current area")]
    public async Task<IResult> ViewCurrentAreaAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var currentArea = await _areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await RespondAsync("🚫 Current area not found!");
        }

        var spotsText = string.Join("\n", currentArea.FishingSpots.Select(spot => 
            $"• **{spot.Name}** ({spot.Type}) - {spot.AvailableFish.Count} fish species"));

        var connectedAreasText = "None";
        if (currentArea.ConnectedAreas.Count > 0)
        {
            var connectedAreas = await _areaRepository.GetAllAreasAsync();
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

        return await RespondAsync(embeds: new[] { embed });
    }

    [Command("travel")]
    [Description("Travel to a connected area")]
    public async Task<IResult> TravelToAreaAsync([Description("Area to travel to")] string targetAreaName)
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var currentArea = await _areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await RespondAsync("🚫 Current area not found!");
        }

        var allAreas = await _areaRepository.GetAllAreasAsync();
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
        await _playerRepository.UpdatePlayerAsync(player);

        return await RespondAsync($"🚶‍♂️ You have traveled to **{targetArea.Name}**!\n\n" +
                                $"*{targetArea.Description}*");
    }

    [Command("areas")]
    [Description("List all available areas")]
    public async Task<IResult> ListAreasAsync()
    {
        var userId = _context.User.ID.Value;
        var player = await GetOrCreatePlayerAsync(userId, _context.User.Username);
        var allAreas = await _areaRepository.GetAllAreasAsync();

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

        return await RespondAsync(embeds: new[] { embed });
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await _playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await _playerRepository.CreatePlayerAsync(userId, username);
        }
        return player;
    }

    private async Task<IResult> RespondAsync(string content = "", IReadOnlyList<Embed>? embeds = null)
    {
        var embedsParam = embeds != null ? new Optional<IReadOnlyList<IEmbed>>(embeds.Cast<IEmbed>().ToList()) : default;
        await _channelAPI.CreateMessageAsync(_context.ChannelID, content, embeds: embedsParam);
        return Result.FromSuccess();
    }
}