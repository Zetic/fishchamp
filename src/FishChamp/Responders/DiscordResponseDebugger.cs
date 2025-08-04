using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FishChamp.Responders;

/// <summary>
/// Responder that logs debug data for Discord interaction responses
/// </summary>
public class DiscordResponseDebugger : IResponder<IInteractionCreate>
{
    private readonly ILogger<DiscordResponseDebugger> _logger;

    public DiscordResponseDebugger(ILogger<DiscordResponseDebugger> logger)
    {
        _logger = logger;
    }

    public Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        // Log the incoming interaction for debugging
        var interactionData = new
        {
            InteractionId = gatewayEvent.ID.Value,
            Type = gatewayEvent.Type.ToString(),
            ApplicationId = gatewayEvent.ApplicationID.Value,
            User = gatewayEvent.User.HasValue ? gatewayEvent.User.Value.Username : 
                   gatewayEvent.Member.HasValue ? gatewayEvent.Member.Value.User.HasValue ? gatewayEvent.Member.Value.User.Value.Username : "Unknown" : "Unknown",
            ChannelId = gatewayEvent.Channel.HasValue ? gatewayEvent.Channel.Value.ID.Value.ToString() : "Unknown",
            Timestamp = DateTimeOffset.UtcNow
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonString = JsonSerializer.Serialize(interactionData, jsonOptions);
        _logger.LogInformation("🎯 Discord Interaction Received: {InteractionData}", jsonString);

        // Don't actually handle the interaction, just log it
        return Result.FromSuccess();
    }
}