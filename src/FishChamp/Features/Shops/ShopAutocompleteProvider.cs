using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;

namespace FishChamp.Features.Shops;

public class ShopAutocompleteProvider(IInteractionContext context,
    IPlayerRepository playerRepository,
    IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::shop";

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

        if (currentArea.Shops == null || currentArea.Shops.Count == 0)
        {
            return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(new List<IApplicationCommandOptionChoice>());
        }

        // Get shops in current area
        var availableShops = currentArea.Shops.Values
            .Where(shop => string.IsNullOrEmpty(userInput) || shop.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord limit
            .Select(shop => new ApplicationCommandOptionChoice(shop.Name, shop.ShopId))
            .ToList();

        return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(availableShops);
    }
}