using System.ComponentModel;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using Remora.Discord.Commands.Feedback.Services;
using FishChamp.Helpers;

namespace FishChamp.Modules;

[Group("land")]
[Description("Land ownership and housing commands")]
public class LandCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IAreaRepository areaRepository, 
    IPlotRepository plotRepository, IHouseRepository houseRepository, FeedbackService feedbackService) : CommandGroup
{
    [Command("browse")]
    [Description("Browse available land plots in your current area")]
    public async Task<IResult> BrowsePlotsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        if (currentArea.AvailablePlots == null || currentArea.AvailablePlots.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🏞️ No land plots are available for purchase in this area!", Color.Orange);
        }

        var availablePlots = currentArea.AvailablePlots.Where(p => p.OwnerId == null).ToList();
        var ownedPlots = currentArea.AvailablePlots.Where(p => p.OwnerId != null).ToList();

        var plotsText = "";
        if (availablePlots.Count > 0)
        {
            plotsText += "**🟢 Available for Purchase:**\n";
            plotsText += string.Join("\n", availablePlots.Select(plot =>
                $"• **{plot.Name}** ({plot.Size}) - {plot.Price} 🪙\n  {plot.Description}"));
        }

        if (ownedPlots.Count > 0)
        {
            if (!string.IsNullOrEmpty(plotsText)) plotsText += "\n\n";
            plotsText += "**🔴 Already Owned:**\n";
            plotsText += string.Join("\n", ownedPlots.Select(plot =>
                $"• **{plot.Name}** ({plot.Size}) - Owned by <@{plot.OwnerId}>"));
        }

        if (string.IsNullOrEmpty(plotsText))
        {
            plotsText = "No plots available.";
        }

        var embed = new Embed
        {
            Title = $"🏞️ Land Plots - {currentArea.Name}",
            Description = plotsText,
            Colour = Color.Green,
            Footer = new EmbedFooter($"💰 Your Balance: {player.FishCoins} Fish Coins")
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("buy")]
    [Description("Purchase a land plot")]
    public async Task<IResult> BuyLandAsync(
        [Description("Plot name to purchase")] string plotName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);

        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("🚫 Current area not found!", Color.Red);
        }

        var plot = currentArea.AvailablePlots?.FirstOrDefault(p => 
            p.Name.Equals(plotName, StringComparison.OrdinalIgnoreCase));

        if (plot == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏞️ Plot '{plotName}' not found in this area!", Color.Red);
        }

        if (plot.OwnerId != null)
        {
            return await feedbackService.SendContextualContentAsync($"🚫 Plot '{plotName}' is already owned!", Color.Red);
        }

        if (player.FishCoins < plot.Price)
        {
            return await feedbackService.SendContextualContentAsync(
                $"💰 You need {plot.Price} Fish Coins to buy this plot! You have {player.FishCoins}.", Color.Red);
        }

        var success = await plotRepository.PurchasePlotAsync(user.ID.Value, currentArea.AreaId, plot.PlotId);

        if (!success)
        {
            return await feedbackService.SendContextualContentAsync("❌ Failed to purchase plot. Please try again.", Color.Red);
        }

        var embed = new Embed
        {
            Title = "🎉 Land Purchase Successful!",
            Description = $"You have successfully purchased **{plot.Name}** for {plot.Price} 🪙!\n\n" +
                         $"**Plot Details:**\n" +
                         $"📍 Location: {currentArea.Name}\n" +
                         $"📏 Size: {plot.Size}\n" +
                         $"💰 Remaining Balance: {player.FishCoins - plot.Price} Fish Coins\n\n" +
                         $"You can now build a house on this plot using `/land build`!",
            Colour = Color.Gold
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("list")]
    [Description("List your owned land plots")]
    public async Task<IResult> ListOwnedPlotsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var ownedPlots = await plotRepository.GetUserPlotsAsync(user.ID.Value);

        if (ownedPlots.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🏞️ You don't own any land plots yet! Use `/land browse` to see available plots.", Color.Orange);
        }

        var plotsText = string.Join("\n", ownedPlots.Select(plot =>
            $"• **{plot.Name}** ({plot.Size}) in {plot.AreaId}\n" +
            $"  📅 Purchased: {plot.PurchasedAt:MMM dd, yyyy}" +
            (plot.HouseId != null ? " 🏠 Has House" : " 🏗️ Empty")));

        var embed = new Embed
        {
            Title = "🏞️ Your Land Plots",
            Description = plotsText,
            Colour = Color.Green,
            Footer = new EmbedFooter($"Total Plots Owned: {ownedPlots.Count}")
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("build")]
    [Description("Build a house on your owned plot")]
    public async Task<IResult> BuildHouseAsync(
        [Description("Plot name to build on")] string plotName,
        [Description("House layout")] HouseLayout layout = HouseLayout.Cozy)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var ownedPlots = await plotRepository.GetUserPlotsAsync(user.ID.Value);
        var plot = ownedPlots.FirstOrDefault(p => 
            p.Name.Equals(plotName, StringComparison.OrdinalIgnoreCase));

        if (plot == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏞️ You don't own a plot named '{plotName}'! Use `/land list` to see your plots.", Color.Red);
        }

        if (plot.HouseId != null)
        {
            return await feedbackService.SendContextualContentAsync($"🏠 You already have a house built on '{plotName}'!", Color.Red);
        }

        // Check if player has enough coins for building
        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var buildCost = layout switch
        {
            HouseLayout.Cozy => 500,
            HouseLayout.Spacious => 1500,
            HouseLayout.Mansion => 5000,
            _ => 500
        };

        if (player.FishCoins < buildCost)
        {
            return await feedbackService.SendContextualContentAsync(
                $"💰 You need {buildCost} Fish Coins to build a {layout} house! You have {player.FishCoins}.", Color.Red);
        }

        // Create the house
        var house = new House
        {
            OwnerId = user.ID.Value,
            PlotId = plot.PlotId,
            AreaId = plot.AreaId,
            Name = $"{plot.Name} House",
            Layout = layout
        };

        var createdHouse = await houseRepository.CreateHouseAsync(house);

        // Deduct coins and update plot
        player.FishCoins -= buildCost;
        plot.HouseId = createdHouse.HouseId;
        
        // Update player (plot is updated via reference in player.OwnedPlots)
        await playerRepository.UpdatePlayerAsync(player);

        var embed = new Embed
        {
            Title = "🏗️ House Construction Complete!",
            Description = $"You have successfully built a **{layout}** house on **{plot.Name}**!\n\n" +
                         $"**Construction Details:**\n" +
                         $"🏠 House: {createdHouse.Name}\n" +
                         $"📏 Layout: {layout}\n" +
                         $"💰 Cost: {buildCost} Fish Coins\n" +
                         $"💰 Remaining Balance: {player.FishCoins} Fish Coins\n\n" +
                         $"Use `/land house {plotName}` to view your house interior!",
            Colour = Color.Gold
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("house")]
    [Description("View your house interior")]
    public async Task<IResult> ViewHouseAsync(
        [Description("Plot name with the house")] string plotName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var ownedPlots = await plotRepository.GetUserPlotsAsync(user.ID.Value);
        var plot = ownedPlots.FirstOrDefault(p => 
            p.Name.Equals(plotName, StringComparison.OrdinalIgnoreCase));

        if (plot == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏞️ You don't own a plot named '{plotName}'!", Color.Red);
        }

        if (plot.HouseId == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏗️ You haven't built a house on '{plotName}' yet! Use `/land build {plotName}` to build one.", Color.Red);
        }

        var house = await houseRepository.GetHouseAsync(plot.HouseId);
        if (house == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ House not found! Please contact an administrator.", Color.Red);
        }

        var roomsText = house.Rooms.Count > 0 
            ? string.Join("\n", house.Rooms.Select(room =>
                $"🏠 **{room.Name}** ({room.Type})\n" +
                $"   {room.Description}\n" +
                $"   Furniture: {room.Furniture.Count}" +
                (room.CraftingStations.Count > 0 ? $" | Stations: {room.CraftingStations.Count}" : "")))
            : "No rooms available.";

        var embed = new Embed
        {
            Title = $"🏠 {house.Name}",
            Description = $"**Layout:** {house.Layout}\n" +
                         $"**Location:** {plot.AreaId}\n\n" +
                         $"**Rooms:**\n{roomsText}",
            Colour = Color.Blue,
            Footer = new EmbedFooter($"Built on {house.CreatedAt:MMM dd, yyyy} | Last updated {house.LastUpdated:MMM dd, yyyy}")
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        return await playerRepository.GetPlayerAsync(userId) ?? await playerRepository.CreatePlayerAsync(userId, username);
    }
}