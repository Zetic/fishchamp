using FishChamp.Data.Models;
using FishChamp.Data.Repositories;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using System.Drawing;

namespace FishChamp.Providers;

public class GuildInviteAutocompleteProvider(IInteractionContext context,
    IGuildRepository guildRepository) : IAutocompleteProvider
{
    public const string ID = "autocomplete::guild_invite";

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput, 
        CancellationToken ct = default)
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
        {
            return await ValueTask.FromException<IReadOnlyList<IApplicationCommandOptionChoice>>(new InvalidOperationException("User not found"));
        }

        var invitations = await guildRepository.GetUserInvitationsAsync(user.ID.Value);

        if (invitations.Count == 0)
        {
            return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>([]);
        }

        var availableInvitations = invitations
            .Where(i => string.IsNullOrEmpty(userInput) || i.GuildName.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25) // Discord limit
            .Select(i => new ApplicationCommandOptionChoice(i.GuildName, i.GuildId))
            .ToList();

        return await new ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>>(availableInvitations);
    }
}