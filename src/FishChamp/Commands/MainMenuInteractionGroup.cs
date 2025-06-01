using System.ComponentModel;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Interactivity;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using FishChamp.Helpers;
using Remora.Discord.Commands.Feedback.Services;

namespace FishChamp.Commands;

public class MainMenuInteractionGroup(
    IDiscordRestInteractionAPI interactionAPI,
    IInteractionContext context,
    IPlayerRepository playerRepository,
    IInventoryRepository inventoryRepository,
    DiscordHelper discordHelper,
    FeedbackService feedbackService) : InteractionGroup
{
    public const string FishButton = "main_menu_fish";
    public const string FarmButton = "main_menu_farm";
    public const string InventoryButton = "main_menu_inventory";
    public const string ShopButton = "main_menu_shop";
    public const string HomeButton = "main_menu_home";
    public const string MapButton = "main_menu_map";

    [Button(FishButton)]
    public async Task<IResult> FishAsync()
    {
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        // Check if player is at a fishing spot
        if (string.IsNullOrEmpty(player.CurrentFishingSpot))
        {
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                "🎣 **Fishing Module**\n\nTo start fishing, first go to a fishing spot using `/map goto <fishing spot>`, then use this fish button again to cast your line!",
                flags: MessageFlags.Ephemeral);
        }

        // Player is at a fishing spot, redirect to actual fishing
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🎣 **Ready to Fish!**\n\nYou're at a fishing spot! Use `/start-fishing` to cast your line and start the fishing minigame!",
            flags: MessageFlags.Ephemeral);
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

    [Button(FarmButton)]
    public async Task<IResult> FarmAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🚜 **Farming Module**\n\nUse farming commands like `/farm plant` and `/farm harvest` to manage your crops!",
            flags: MessageFlags.Ephemeral);
    }

    [Button(InventoryButton)]
    public async Task<IResult> InventoryAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🎒 **Inventory Module**\n\nUse `/inventory view` to see your items and `/inventory use` to use consumables!",
            flags: MessageFlags.Ephemeral);
    }

    [Button(ShopButton)]
    public async Task<IResult> ShopAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🏪 **Shop Module**\n\nUse `/shop browse` to see what's available in your current area!",
            flags: MessageFlags.Ephemeral);
    }

    [Button(HomeButton)]
    public async Task<IResult> HomeAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🏡 **Home Module**\n\nYour personal space for relaxation and item storage. Features coming soon!",
            flags: MessageFlags.Ephemeral);
    }

    [Button(MapButton)]
    public async Task<IResult> MapAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🗺 **Map & Navigation**\n\nUse `/map current` to see where you are and `/map goto` to travel to new areas!",
            flags: MessageFlags.Ephemeral);
    }
}