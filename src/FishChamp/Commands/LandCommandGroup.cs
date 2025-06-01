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
    IPlotRepository plotRepository, IHouseRepository houseRepository, 
    IInventoryRepository inventoryRepository, FeedbackService feedbackService) : CommandGroup
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

        player.OwnedPlots = ownedPlots;

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

    [Command("decorate")]
    [Description("Add furniture to your house room")]
    public async Task<IResult> DecoratePHouseAsync(
        [Description("Plot name with the house")] string plotName,
        [Description("Room name")] string roomName,
        [Description("Furniture item name from inventory")] string itemName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var ownedPlots = await plotRepository.GetUserPlotsAsync(user.ID.Value);
        var plot = ownedPlots.FirstOrDefault(p => 
            p.Name.Equals(plotName, StringComparison.OrdinalIgnoreCase));

        if (plot == null || plot.HouseId == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏞️ You don't have a house on plot '{plotName}'!", Color.Red);
        }

        var house = await houseRepository.GetHouseAsync(plot.HouseId);
        if (house == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ House not found!", Color.Red);
        }

        var room = house.Rooms.FirstOrDefault(r => 
            r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));

        if (room == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏠 Room '{roomName}' not found in your house!", Color.Red);
        }

        // Check if player has the item in inventory
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("📦 Your inventory is empty!", Color.Red);
        }

        var item = inventory.Items.FirstOrDefault(i => 
            i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) && 
            (i.ItemType.Equals("Furniture", StringComparison.OrdinalIgnoreCase) || 
             i.ItemType.Equals("Decoration", StringComparison.OrdinalIgnoreCase)));

        if (item == null)
        {
            return await feedbackService.SendContextualContentAsync($"🪑 You don't have a furniture/decoration item named '{itemName}' in your inventory!", Color.Red);
        }

        if (item.Quantity < 1)
        {
            return await feedbackService.SendContextualContentAsync($"🪑 You don't have any '{itemName}' left!", Color.Red);
        }

        // Create furniture from inventory item
        var furniture = new Furniture
        {
            ItemId = item.ItemId,
            Name = item.Name,
            Type = item.ItemType.Equals("Furniture", StringComparison.OrdinalIgnoreCase) 
                ? FurnitureType.Decoration 
                : FurnitureType.Decoration,
            Position = "center",
            Properties = new Dictionary<string, object>(item.Properties)
        };

        // Add buffs based on furniture properties
        if (item.Properties.ContainsKey("buffs"))
        {
            // This is a simplified buff system - in a real implementation, 
            // you'd have more complex buff definitions
            furniture.Buffs.Add(new FurnitureBuff
            {
                BuffId = $"furniture_{item.ItemId}",
                Name = $"{item.Name} Comfort",
                Description = "Provides comfort and happiness in your home",
                Effects = new Dictionary<string, object> { { "happiness", 5 } }
            });
        }

        // Add furniture to room
        room.Furniture.Add(furniture);
        house.LastUpdated = DateTime.UtcNow;

        // Remove item from inventory
        await inventoryRepository.RemoveItemAsync(user.ID.Value, item.ItemId, 1);

        // Update house
        await houseRepository.UpdateHouseAsync(house);

        var embed = new Embed
        {
            Title = "🪑 Furniture Placed!",
            Description = $"You have successfully placed **{item.Name}** in **{room.Name}**!\n\n" +
                         $"**Placement Details:**\n" +
                         $"🏠 House: {house.Name}\n" +
                         $"🚪 Room: {room.Name}\n" +
                         $"🪑 Furniture: {furniture.Name}\n" +
                         (furniture.Buffs.Count > 0 ? $"✨ Buffs: {string.Join(", ", furniture.Buffs.Select(b => b.Name))}" : ""),
            Colour = Color.Purple
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("room")]
    [Description("View details of a specific room in your house")]
    public async Task<IResult> ViewRoomAsync(
        [Description("Plot name with the house")] string plotName,
        [Description("Room name")] string roomName)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var ownedPlots = await plotRepository.GetUserPlotsAsync(user.ID.Value);
        var plot = ownedPlots.FirstOrDefault(p => 
            p.Name.Equals(plotName, StringComparison.OrdinalIgnoreCase));

        if (plot == null || plot.HouseId == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏞️ You don't have a house on plot '{plotName}'!", Color.Red);
        }

        var house = await houseRepository.GetHouseAsync(plot.HouseId);
        if (house == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ House not found!", Color.Red);
        }

        var room = house.Rooms.FirstOrDefault(r => 
            r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));

        if (room == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏠 Room '{roomName}' not found in your house!", Color.Red);
        }

        var furnitureText = room.Furniture.Count > 0
            ? string.Join("\n", room.Furniture.Select(f =>
                $"🪑 **{f.Name}** ({f.Type})" +
                (f.Buffs.Count > 0 ? $" - ✨ {string.Join(", ", f.Buffs.Select(b => b.Name))}" : "")))
            : "No furniture placed yet.";

        var stationsText = room.CraftingStations.Count > 0
            ? string.Join(", ", room.CraftingStations)
            : "None";

        var totalBuffs = room.Furniture.SelectMany(f => f.Buffs).ToList();
        var buffsText = totalBuffs.Count > 0
            ? string.Join("\n", totalBuffs.GroupBy(b => b.Name).Select(g =>
                $"✨ **{g.Key}** (+{g.Sum(b => GetBuffValue(b, "happiness"))})"))
            : "No active buffs.";

        var embed = new Embed
        {
            Title = $"🚪 {room.Name}",
            Description = room.Description,
            Colour = Color.Blue,
            Fields = new List<EmbedField>
            {
                new("🪑 Furniture", furnitureText, false),
                new("🔧 Crafting Stations", stationsText, true),
                new("✨ Active Buffs", buffsText, true)
            },
            Footer = new EmbedFooter($"Room Type: {room.Type}")
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("station")]
    [Description("Install a crafting station in your house room")]
    public async Task<IResult> InstallCraftingStationAsync(
        [Description("Plot name with the house")] string plotName,
        [Description("Room name")] string roomName,
        [Description("Station type (bait_maker, stove, anvil, workbench)")] string stationType)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var ownedPlots = await plotRepository.GetUserPlotsAsync(user.ID.Value);
        var plot = ownedPlots.FirstOrDefault(p => 
            p.Name.Equals(plotName, StringComparison.OrdinalIgnoreCase));

        if (plot == null || plot.HouseId == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏞️ You don't have a house on plot '{plotName}'!", Color.Red);
        }

        var house = await houseRepository.GetHouseAsync(plot.HouseId);
        if (house == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ House not found!", Color.Red);
        }

        var room = house.Rooms.FirstOrDefault(r => 
            r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));

        if (room == null)
        {
            return await feedbackService.SendContextualContentAsync($"🏠 Room '{roomName}' not found in your house!", Color.Red);
        }

        // Validate station type
        var validStations = new[] { "bait_maker", "stove", "anvil", "workbench", "loom", "alchemy_table" };
        if (!validStations.Contains(stationType.ToLower()))
        {
            return await feedbackService.SendContextualContentAsync($"🔧 Invalid station type! Valid types: {string.Join(", ", validStations)}", Color.Red);
        }

        // Check if station already exists in this room
        if (room.CraftingStations.Contains(stationType.ToLower()))
        {
            return await feedbackService.SendContextualContentAsync($"🔧 A {stationType} is already installed in this room!", Color.Red);
        }

        // Check if player has enough coins
        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var stationCost = GetStationCost(stationType.ToLower());

        if (player.FishCoins < stationCost)
        {
            return await feedbackService.SendContextualContentAsync(
                $"💰 You need {stationCost} Fish Coins to install a {stationType}! You have {player.FishCoins}.", Color.Red);
        }

        // Install the station
        room.CraftingStations.Add(stationType.ToLower());
        house.LastUpdated = DateTime.UtcNow;

        // Deduct coins
        player.FishCoins -= stationCost;

        // Update both player and house
        await playerRepository.UpdatePlayerAsync(player);
        await houseRepository.UpdateHouseAsync(house);

        var embed = new Embed
        {
            Title = "🔧 Crafting Station Installed!",
            Description = $"You have successfully installed a **{ToTitleCase(stationType.Replace("_", " "))}** in **{room.Name}**!\n\n" +
                         $"**Installation Details:**\n" +
                         $"🏠 House: {house.Name}\n" +
                         $"🚪 Room: {room.Name}\n" +
                         $"🔧 Station: {ToTitleCase(stationType.Replace("_", " "))}\n" +
                         $"💰 Cost: {stationCost} Fish Coins\n" +
                         $"💰 Remaining Balance: {player.FishCoins} Fish Coins\n\n" +
                         $"You can now use advanced crafting recipes that require this station!",
            Colour = Color.Orange
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("stations")]
    [Description("List all available crafting station types")]
    public async Task<IResult> ListCraftingStationsAsync()
    {
        var stations = new Dictionary<string, (string name, int cost, string description)>
        {
            ["bait_maker"] = ("Bait Maker", 300, "Craft advanced bait for better fishing results"),
            ["stove"] = ("Cooking Stove", 400, "Cook meals and food for powerful buffs"),
            ["anvil"] = ("Blacksmith Anvil", 800, "Forge fishing rods and metal tools"),
            ["workbench"] = ("Crafting Workbench", 250, "Build furniture and basic items"),
            ["loom"] = ("Textile Loom", 500, "Weave fabrics and decorative items"),
            ["alchemy_table"] = ("Alchemy Table", 1000, "Brew potions and magical enhancements")
        };

        var stationsText = string.Join("\n", stations.Select(kvp =>
            $"🔧 **{kvp.Value.name}** - {kvp.Value.cost} 🪙\n" +
            $"   {kvp.Value.description}"));

        var embed = new Embed
        {
            Title = "🔧 Available Crafting Stations",
            Description = stationsText,
            Colour = Color.Blue,
            Footer = new EmbedFooter("Use `/land station <plot> <room> <station_type>` to install a station")
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        return await playerRepository.GetPlayerAsync(userId) ?? await playerRepository.CreatePlayerAsync(userId, username);
    }

    private static int GetBuffValue(FurnitureBuff buff, string effectKey)
    {
        if (buff.Effects.TryGetValue(effectKey, out var value))
        {
            return value is int intValue ? intValue : 0;
        }
        return 0;
    }

    private static int GetStationCost(string stationType)
    {
        return stationType switch
        {
            "bait_maker" => 300,
            "stove" => 400,
            "anvil" => 800,
            "workbench" => 250,
            "loom" => 500,
            "alchemy_table" => 1000,
            _ => 500
        };
    }

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i][1..].ToLower() : "");
            }
        }
        return string.Join(" ", words);
    }
}