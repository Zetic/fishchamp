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
    IPlotRepository plotRepository, FeedbackService feedbackService) : CommandGroup
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

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        return await playerRepository.GetPlayerAsync(userId) ?? await playerRepository.CreatePlayerAsync(userId, username);
    }
}