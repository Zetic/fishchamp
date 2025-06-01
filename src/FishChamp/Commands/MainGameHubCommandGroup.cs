using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using Remora.Rest.Core;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using Remora.Discord.Commands.Feedback.Services;
using FishChamp.Helpers;
using Remora.Discord.Interactivity;
using FishChamp.Commands;

namespace FishChamp.Commands;

[Description("Main game hub")]
public class MainGameHubCommandGroup(
    IInteractionCommandContext context,
    IPlayerRepository playerRepository,
    IInventoryRepository inventoryRepository,
    IDiscordRestInteractionAPI interactionAPI,
    FeedbackService feedbackService) : CommandGroup
{
    [Command("fish")]
    [Description("Access the FishChamp game hub")]
    public async Task<IResult> MainHubAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        // Auto-register player if they don't exist
        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        
        // Create main menu embed
        var embed = new Embed
        {
            Title = "🐟 Welcome to FishChamp!",
            Description = $"Hello **{user.Username}**! Choose what you'd like to do:",
            Fields = new List<EmbedField>
            {
                new("🎣 Fish", "Cast your line and catch fish!", true),
                new("🚜 Farm", "Plant and harvest crops", true), 
                new("🎒 Inventory", "Manage your items", true),
                new("🏪 Shop", "Buy and sell goods", true),
                new("🏡 Home", "Your personal space", true),
                new("🗺 Map", "Navigate the world", true)
            },
            Colour = Color.DeepSkyBlue,
            Footer = new EmbedFooter($"Level {player.Level} • {player.FishCoins} 🪙"),
            Timestamp = DateTimeOffset.UtcNow
        };

        // Create navigation buttons
        var components = new List<IMessageComponent>
        {
            new ActionRowComponent(new List<IMessageComponent>
            {
                new ButtonComponent(ButtonComponentStyle.Primary, "🎣 Fish", 
                    new PartialEmoji(Name: "🎣"), 
                    CustomIDHelpers.CreateButtonID(MainMenuInteractionGroup.FishButton)),
                new ButtonComponent(ButtonComponentStyle.Success, "🚜 Farm", 
                    new PartialEmoji(Name: "🚜"), 
                    CustomIDHelpers.CreateButtonID(MainMenuInteractionGroup.FarmButton)),
                new ButtonComponent(ButtonComponentStyle.Secondary, "🎒 Inventory", 
                    new PartialEmoji(Name: "🎒"), 
                    CustomIDHelpers.CreateButtonID(MainMenuInteractionGroup.InventoryButton))
            }),
            new ActionRowComponent(new List<IMessageComponent>
            {
                new ButtonComponent(ButtonComponentStyle.Secondary, "🏪 Shop", 
                    new PartialEmoji(Name: "🏪"), 
                    CustomIDHelpers.CreateButtonID(MainMenuInteractionGroup.ShopButton)),
                new ButtonComponent(ButtonComponentStyle.Secondary, "🏡 Home", 
                    new PartialEmoji(Name: "🏡"), 
                    CustomIDHelpers.CreateButtonID(MainMenuInteractionGroup.HomeButton)),
                new ButtonComponent(ButtonComponentStyle.Secondary, "🗺 Map", 
                    new PartialEmoji(Name: "🗺"), 
                    CustomIDHelpers.CreateButtonID(MainMenuInteractionGroup.MapButton))
            })
        };

        // First send the embed
        var embedResult = await feedbackService.SendContextualEmbedAsync(embed);
        if (!embedResult.IsSuccess)
            return embedResult;

        // Then create a followup with the buttons
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            content: "Choose an option:",
            components: components
        );
    }

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
}