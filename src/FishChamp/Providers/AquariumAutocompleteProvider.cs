using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;

namespace FishChamp.Providers;

public class AquariumFishAutocompleteProvider(IInteractionContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::aquarium_fish";
    
    public string Identity => ID;
    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return new List<IApplicationCommandOptionChoice>();
        }

        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        if (inventory == null)
        {
            return new List<IApplicationCommandOptionChoice>();
        }

        // Get only fish from inventory
        var fishItems = inventory.Items
            .Where(i => i.ItemType == "Fish")
            .Where(i => string.IsNullOrEmpty(userInput) || 
                       i.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                       i.ItemId.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord limit
            .Select(f => new ApplicationCommandOptionChoice(
                $"{f.Name} (x{f.Quantity})", 
                f.ItemId))
            .ToList();

        return fishItems;
    }
}

public class AquariumRemoveFishAutocompleteProvider(IInteractionContext context,
    IPlayerRepository playerRepository, IAquariumRepository aquariumRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::aquarium_remove_fish";
    
    public string Identity => ID;
    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return new List<IApplicationCommandOptionChoice>();
        }

        var aquarium = await aquariumRepository.GetAquariumAsync(user.ID.Value);
        if (aquarium == null || !aquarium.Fish.Any())
        {
            return new List<IApplicationCommandOptionChoice>();
        }

        // Get fish from aquarium
        var aquariumFish = aquarium.Fish
            .Where(f => string.IsNullOrEmpty(userInput) || 
                       f.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                       f.FishType.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord limit
            .Select(f => new ApplicationCommandOptionChoice(
                $"{f.Name} ({f.Rarity})", 
                f.FishId))
            .ToList();

        return aquariumFish;
    }
}