using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;

namespace FishChamp.Providers;

public class AreaAutocompleteProvider(IInteractionContext context, 
    IPlayerRepository playerRepository, 
    IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::area";

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
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return await ValueTask.FromException<IReadOnlyList<IApplicationCommandOptionChoice>>(new InvalidOperationException("User not found"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await ValueTask.FromException<IReadOnlyList<IApplicationCommandOptionChoice>>(new InvalidOperationException("Current area not found! Try using `/map` to navigate."));
        }

        var allAreas = await areaRepository.GetAllAreasAsync();

        // Get connected areas that the player has unlocked
        var availableAreas = allAreas
            .Where(area => currentArea.ConnectedAreas.Contains(area.AreaId) && player.UnlockedAreas.Contains(area.AreaId))
            .Where(area => string.IsNullOrEmpty(userInput) || area.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord limit
            .Select(area => new ApplicationCommandOptionChoice(area.Name, area.AreaId))
            .ToList();

        return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(availableAreas);
    }
}