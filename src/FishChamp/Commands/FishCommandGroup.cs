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
using Remora.Discord.Commands.Feedback.Services;
using FishChamp.Helpers;
using System.Text.Json;

namespace FishChamp.Modules;

[Description("Quick fishing command")]
public class FishCommandGroup(IInteractionCommandContext context, IDiscordRestChannelAPI channelAPI,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository, DiscordHelper discordHelper, FeedbackService feedbackService) : CommandGroup
{
    [Command("fish")]
    [Description("Start fishing at your current fishing spot")]
    public async Task<IResult> StartFishingAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        // Check if player is at a fishing spot
        if (string.IsNullOrEmpty(player.CurrentFishingSpot))
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, 
                "🎣 You need to be at a fishing spot first! Use `/map goto <fishing spot>` to go to one.");
        }

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, 
                ":map: Current area not found! Try using `/map` to navigate.");
        }

        // Find the fishing spot the player is at
        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => 
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));

        if (fishingSpot == null)
        {
            // Player's fishing spot is invalid, clear it
            player.CurrentFishingSpot = string.Empty;
            await playerRepository.UpdatePlayerAsync(player);
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, 
                "🚫 Your current fishing spot is no longer available. Use `/map goto <fishing spot>` to go to a new one.");
        }

        if (fishingSpot.Type == FishingSpotType.Water)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, 
                ":sailboat: Only boats are allowed to fish here.");
        }

        // Create the initial fishing embed
        var embed = new Embed
        {
            Title = "🎣 Fishing Time!",
            Description = $"You're at **{fishingSpot.Name}**.\nReady to cast your line?",
            Colour = Color.Blue,
            Footer = new EmbedFooter("Click the Cast Line button to start!"),
            Timestamp = DateTimeOffset.UtcNow
        };

        // Create the Cast Line button
        var castButton = new ButtonComponent(ButtonComponentStyle.Primary, "Cast Line", new PartialEmoji(Name: "🎣"), "fish_cast_line");
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent(new[] { (IMessageComponent)castButton })
        };

        return await feedbackService.SendContextualEmbedAsync(embed, components);
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
}