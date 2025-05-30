using Microsoft.Extensions.Options;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Helpers;

public class DiscordHelper(IDiscordRestChannelAPI channelAPI,
    IDiscordRestUserAPI userAPI,
    FeedbackService feedbackService,
    IDiscordRestInteractionAPI interactionAPI)
{
    public async Task<IResult> ErrorInteractionEphemeral(IInteraction interaction, string content)
    {
        if (!interaction.Channel.TryGet(out var channel) || !channel.ID.HasValue)
        {
            return Result.FromError(new NotFoundError("Channel of interaction couldn't be found!"));
        }

        if (!interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return Result.FromError(new NotFoundError("User of interaction couldn't be found!"));
        }

        await interactionAPI.DeleteOriginalInteractionResponseAsync(interaction.ApplicationID, interaction.Token);        

        return await interactionAPI.CreateFollowupMessageAsync(interaction.ApplicationID, interaction.Token, content, 
            flags: MessageFlags.Ephemeral);
    }
}
