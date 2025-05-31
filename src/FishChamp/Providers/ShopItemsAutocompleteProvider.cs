using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;

namespace FishChamp.Providers;

public class ShopItemsAutocompleteProvider(IInteractionContext context, 
    IPlayerRepository playerRepository, 
    IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::shop_items";

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

        var shopOption = options.First().Options.Value.FirstOrDefault(o => o.Name.Contains("shop", StringComparison.OrdinalIgnoreCase));

        if (shopOption == null)
        {
            return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(new List<IApplicationCommandOptionChoice>());
        }

        var shop = currentArea.Shops.Values.FirstOrDefault(s => s.ShopId == shopOption.Value.Value.AsT0);

        if (shop == null)
        {
            return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(new List<IApplicationCommandOptionChoice>());
        }

        // Get items in defined shop
        var availableItems = shop.Items
            .Where(item => string.IsNullOrEmpty(userInput) || item.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord limit
            .Select(item => new ApplicationCommandOptionChoice(item.Name, item.ItemId))
            .ToList();

        return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(availableItems);
    }
}