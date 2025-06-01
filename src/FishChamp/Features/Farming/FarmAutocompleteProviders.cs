using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;
using Remora.Results;

namespace FishChamp.Features.Farming;

public class FarmSpotAutocompleteProvider(IInteractionContext context,
    IPlayerRepository playerRepository,
    IAreaRepository areaRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::farm_spot";

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return Array.Empty<IApplicationCommandOptionChoice>();
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null || currentArea.FarmSpots.Count == 0)
        {
            return Array.Empty<IApplicationCommandOptionChoice>();
        }

        var suggestions = currentArea.FarmSpots
            .Where(fs => string.IsNullOrEmpty(userInput) || fs.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) || fs.SpotId.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(fs => new ApplicationCommandOptionChoice(fs.Name, fs.SpotId))
            .Cast<IApplicationCommandOptionChoice>()
            .ToList();

        return suggestions;
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        return await playerRepository.GetPlayerAsync(userId) ?? await playerRepository.CreatePlayerAsync(userId, username);
    }
}

public class SeedTypeAutocompleteProvider(IInteractionContext context,
    IInventoryRepository inventoryRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::seed_type";

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return Array.Empty<IApplicationCommandOptionChoice>();
        }

        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return Array.Empty<IApplicationCommandOptionChoice>();
        }

        var seedItems = inventory.Items
            .Where(i => i.ItemType == "Seed" && i.Quantity > 0)
            .Where(i => string.IsNullOrEmpty(userInput) || i.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) || i.ItemId.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(i => new ApplicationCommandOptionChoice($"{i.Name} (x{i.Quantity})", i.ItemId))
            .Cast<IApplicationCommandOptionChoice>()
            .ToList();

        return seedItems;
    }
}