using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using ZPT.Models;

namespace ZPT.Services;

public class InteractionService : IResponder<IInteractionCreate>
{
    private readonly ILogger<InteractionService> _logger;

    public InteractionService(ILogger<InteractionService> logger)
    {
        _logger = logger;
    }

    public async Task<Result> RespondAsync(IInteractionCreate gatewayEvent, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Interaction received: {InteractionType}", gatewayEvent.GetType().Name);
            // For now, just log interactions - full implementation coming later
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling interaction");
        }

        return Result.FromSuccess();
    }
}

// Session models for tracking active game states
public class FishingSession
{
    public ulong UserId { get; set; }
    public string Fish { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public bool WaitingForBite { get; set; }
    public bool WaitingForReel { get; set; }
    public Timer? BiteTimer { get; set; }
    public Timer? EscapeTimer { get; set; }
}

public class GameSession
{
    public ulong UserId { get; set; }
    public DateTime LastActive { get; set; }
    public Dictionary<string, object> SessionData { get; set; } = new();
}