using System.ComponentModel;
using System.Linq;
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
    IAreaRepository areaRepository,
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
        
        // Check current area and fishing spots
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                "🗺️ **Location Error**\n\nYour current area wasn't found. Use `/map current` to check your location!",
                flags: MessageFlags.Ephemeral);
        }

        // Check if player is at a fishing spot
        if (string.IsNullOrEmpty(player.CurrentFishingSpot))
        {
            // Show available fishing spots in current area
            if (currentArea.FishingSpots.Any())
            {
                var spotsText = string.Join("\n", currentArea.FishingSpots.Take(5).Select(spot => $"• **{spot.SpotId}** ({spot.Type})"));
                
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    $"🎣 **Available Fishing Spots in {currentArea.AreaId}**\n\n{spotsText}\n\n" +
                    "📍 Use `/map goto <fishing_spot>` to travel to a spot, then click 🎣 Fish again!",
                    flags: MessageFlags.Ephemeral);
            }
            else
            {
                return await interactionAPI.CreateFollowupMessageAsync(
                    context.Interaction.ApplicationID,
                    context.Interaction.Token,
                    $"🎣 **No Fishing Spots Here**\n\nThere are no fishing spots in **{currentArea.AreaId}**.\n\n" +
                    "🗺️ Use `/map areas` to see other areas with fishing spots!",
                    flags: MessageFlags.Ephemeral);
            }
        }

        // Player is at a fishing spot - provide fishing guidance
        var fishingSpot = currentArea.FishingSpots.FirstOrDefault(f => 
            f.SpotId.Equals(player.CurrentFishingSpot, StringComparison.OrdinalIgnoreCase));
            
        if (fishingSpot != null)
        {
            var equipmentInfo = $"🎣 **Rod:** {player.EquippedRod ?? "Basic Rod"}\n🪱 **Bait:** {player.EquippedBait ?? "None"}";
            var spotInfo = $"📍 **Location:** {fishingSpot.SpotId} ({fishingSpot.Type})";
            
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                $"🎣 **Ready to Fish!**\n\n{spotInfo}\n{equipmentInfo}\n\n" +
                "🚀 Use `/start-fishing` to cast your line and start fishing!\n" +
                "⚙️ Use `/inventory equip` to change your equipment",
                flags: MessageFlags.Ephemeral);
        }

        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🎣 **Fishing spot not found!** Use `/map goto <fishing_spot>` to go to a valid spot.",
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
            "🚜 **Farming Module**\n\n" +
            "🌱 Use `/farm plant <seed>` to plant crops\n" +
            "🌾 Use `/farm harvest` to collect mature crops\n" +
            "💧 Use `/farm water` to speed up growth\n" +
            "🗺️ First find farmable land using `/map goto <farm_area>`",
            flags: MessageFlags.Ephemeral);
    }

    [Button(InventoryButton)]
    public async Task<IResult> InventoryAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🎒 **Inventory Module**\n\n" +
            "📦 Use `/inventory view` to see your items\n" +
            "🔧 Use `/inventory equip <item>` to equip rods and bait\n" +
            "⚗️ Use `/inventory use <item>` to use consumables\n" +
            "📊 Use `/inventory stats` to see detailed item information",
            flags: MessageFlags.Ephemeral);
    }

    [Button(ShopButton)]
    public async Task<IResult> ShopAsync()
    {
        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            "🏪 **Shop Module**\n\n" +
            "🛒 Use `/shop browse` to see what's available in your current area\n" +
            "💰 Use `/shop buy <item>` to purchase items\n" +
            "💸 Use `/shop sell <item>` to sell your items\n" +
            "⛵ Visit different areas to find specialized shops!",
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
        if (!context.Interaction.Member.TryGet(out var member) || !member.User.TryGet(out var user))
            return Result.FromSuccess();

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        
        if (currentArea == null)
        {
            return await interactionAPI.CreateFollowupMessageAsync(
                context.Interaction.ApplicationID,
                context.Interaction.Token,
                "🗺️ **Map Module**\n\nLocation error! Use `/map current` to refresh your location.",
                flags: MessageFlags.Ephemeral);
        }

        var locationInfo = $"📍 **Current Area:** {currentArea.AreaId}";
        var fishingSpotInfo = string.IsNullOrEmpty(player.CurrentFishingSpot) 
            ? "🎣 **Fishing Spot:** None" 
            : $"🎣 **Fishing Spot:** {player.CurrentFishingSpot}";
            
        var availableSpots = currentArea.FishingSpots.Any() 
            ? $"🎯 **Available Spots:** {currentArea.FishingSpots.Count}"
            : "🚫 **No fishing spots here**";

        return await interactionAPI.CreateFollowupMessageAsync(
            context.Interaction.ApplicationID,
            context.Interaction.Token,
            $"🗺️ **Map & Navigation**\n\n{locationInfo}\n{fishingSpotInfo}\n{availableSpots}\n\n" +
            "📍 Use `/map current` for detailed area info\n" +
            "🧭 Use `/map areas` to see all areas\n" +
            "🚶 Use `/map goto <area/spot>` to travel",
            flags: MessageFlags.Ephemeral);
    }
}