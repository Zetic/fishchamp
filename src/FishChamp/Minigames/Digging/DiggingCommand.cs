using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;
using System.ComponentModel;
using System.Drawing;
using FishChamp.Minigames.Digging;

namespace FishChamp.Modules;

public partial class FarmCommandGroup : CommandGroup
{
    [Command("dig")]
    [Description("Dig for worms and other treasures")]
    public async Task<IResult> DigForWormsAsync(
        [Description("Farm spot to dig at")]
        [AutocompleteProvider("autocomplete::farm_spot")]
        string farmSpotId)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);
        if (player == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "Player profile not found. Use `/fish` to get started!");
        }

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, "Current area not found.");
        }

        var farmSpot = currentArea.FarmSpots.FirstOrDefault(fs => fs.SpotId == farmSpotId);
        if (farmSpot == null)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"Farm spot '{farmSpotId}' not found in {currentArea.Name}.");
        }

        if (!farmSpot.CanDigForWorms)
        {
            return await discordHelper.ErrorInteractionEphemeral(context.Interaction, $"Cannot dig for worms at {farmSpot.Name}.");
        }

        var startDiggingButton = new ButtonComponent(ButtonComponentStyle.Primary, "Start Digging", CustomID: CustomIDHelpers.CreateButtonID(DirtDiggingInteractionGroup.Start));
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent([startDiggingButton])
        };

        var embed = new Embed
        {
            Title = "🪱 Dig for Worms",
            Description = $"You can dig for worms at {farmSpot.Name}. Click the button below to start digging!",
            Colour = Color.Brown,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await interactionAPI.CreateFollowupMessageAsync(context.Interaction.ApplicationID,
            context.Interaction.Token,
            embeds: new Optional<IReadOnlyList<IEmbed>>([embed]),
            components: components);
    }
}
