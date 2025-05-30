using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Humanizer;
using Polly;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Providers;

public class AreaFishSpotAutocompleteProvider(IInteractionContext context, 
    IPlayerRepository playerRepository, 
    IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::area_fishspot";

    public string Identity => ID;

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

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput, 
        CancellationToken ct = default)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return await ValueTask.FromException<IReadOnlyList<IApplicationCommandOptionChoice>>(new InvalidOperationException("User not found"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await ValueTask.FromException<IReadOnlyList<IApplicationCommandOptionChoice>>(new InvalidOperationException("Current area not found! Try using `/map` to navigate."));
        }

        return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>
        (
            currentArea.FishingSpots
                .Select(f => new ApplicationCommandOptionChoice(f.Name, f.SpotId))
                .ToList()
        );
    }
}
