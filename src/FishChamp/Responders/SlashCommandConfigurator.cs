using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

namespace FishChamp.Responders;

public class SlashCommandConfigurator(ILogger<SlashCommandConfigurator> logger, SlashService slash) : IResponder<IGuildCreate>
{
    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent,
        CancellationToken ct = new CancellationToken())
    {
        var update = await slash.UpdateSlashCommandsAsync(ct: ct);
        if (!update.IsSuccess)
        {
            logger.LogWarning("Failed to update slash commands: {Reason}", update.Error?.Message);
        }

        return Result.FromSuccess();
    }
}
