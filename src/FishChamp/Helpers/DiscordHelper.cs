using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using Remora.Rest.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FishChamp.Helpers;

public class DiscordHelper(IDiscordRestChannelAPI channelAPI,
    IDiscordRestUserAPI userAPI,
    FeedbackService feedbackService,
    IDiscordRestInteractionAPI interactionAPI,
    ILogger<DiscordHelper> logger)
{
    private void LogDiscordResponse(string methodName, object? responseData = null, string? additionalInfo = null)
    {
        var logData = new
        {
            Method = methodName,
            Timestamp = DateTimeOffset.UtcNow,
            ResponseData = responseData,
            AdditionalInfo = additionalInfo
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonString = JsonSerializer.Serialize(logData, jsonOptions);
        logger.LogInformation("🤖 Discord Response Debug: {DebugData}", jsonString);
    }

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

        LogDiscordResponse("DeleteOriginalInteractionResponseAsync", new
        {
            ApplicationID = interaction.ApplicationID.Value,
            Method = "ErrorInteractionEphemeral"
        });

        var result = await interactionAPI.CreateFollowupMessageAsync(interaction.ApplicationID, interaction.Token, content, 
            flags: MessageFlags.Ephemeral);

        LogDiscordResponse("CreateFollowupMessageAsync", new
        {
            ApplicationID = interaction.ApplicationID.Value,
            Content = content,
            IsEphemeral = true,
            Success = result.IsSuccess,
            Method = "ErrorInteractionEphemeral"
        });

        return result;
    }

    public async Task<IResult> LoggedCreateFollowupMessage(Snowflake applicationID, string token, string content, bool isEphemeral = false, IReadOnlyList<IEmbed>? embeds = null, IReadOnlyList<IMessageComponent>? components = null, string? context = null)
    {
        var result = await interactionAPI.CreateFollowupMessageAsync(
            applicationID, 
            token, 
            content,
            embeds: embeds != null ? new Optional<IReadOnlyList<IEmbed>>(embeds) : default,
            components: components != null ? new Optional<IReadOnlyList<IMessageComponent>>(components) : default,
            flags: isEphemeral ? MessageFlags.Ephemeral : default);

        LogDiscordResponse("CreateFollowupMessageAsync", new
        {
            ApplicationID = applicationID.Value,
            Content = content,
            EmbedCount = embeds?.Count ?? 0,
            ComponentCount = components?.Count ?? 0,
            IsEphemeral = isEphemeral,
            Success = result.IsSuccess,
            Context = context ?? "Direct call"
        });

        return result;
    }

    public async Task<IResult> LoggedSendContextualEmbed(Embed embed, string? context = null)
    {
        var result = await feedbackService.SendContextualEmbedAsync(embed);
        
        LogDiscordResponse("FeedbackService.SendContextualEmbedAsync", new
        {
            EmbedTitle = embed.Title.HasValue ? embed.Title.Value : null,
            EmbedDescription = embed.Description.HasValue ? embed.Description.Value?.Length > 100 ? 
                embed.Description.Value[..100] + "..." : embed.Description.Value : null,
            EmbedColor = embed.Colour.HasValue ? embed.Colour.Value.ToString() : null,
            FieldCount = embed.Fields.HasValue ? embed.Fields.Value.Count : 0,
            Success = result.IsSuccess,
            Context = context ?? "Direct call"
        });

        return result;
    }

    public async Task<IResult> LoggedSendContextualContent(string content, Color color = default, string? context = null)
    {
        var result = await feedbackService.SendContextualContentAsync(content, color);
        
        LogDiscordResponse("FeedbackService.SendContextualContentAsync", new
        {
            Content = content.Length > 100 ? content[..100] + "..." : content,
            Color = color.ToString(),
            Success = result.IsSuccess,
            Context = context ?? "Direct call"
        });

        return result;
    }
}
