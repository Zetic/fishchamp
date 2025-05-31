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
using FishChamp.Helpers;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;

namespace FishChamp.Modules;

[Group("boat")]
[Description("Boat management commands")]
public class BoatCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IBoatRepository boatRepository,
    IInventoryRepository inventoryRepository, IAreaRepository areaRepository, FeedbackService feedbackService) : CommandGroup
{
    [Command("list")]
    [Description("View your boats")]
    public async Task<IResult> ListBoatsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var boats = await boatRepository.GetUserBoatsAsync(user.ID.Value);

        if (boats.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("⛵ You don't have any boats yet! Visit a shop to purchase one.", Color.Blue);
        }

        var fields = new List<EmbedField>();
        
        foreach (var boat in boats)
        {
            var boatInfo = BoatTypes.BoatData.GetValueOrDefault(boat.BoatType);
            var isEquipped = player.EquippedBoat == boat.BoatId ? " ⭐" : "";
            var durabilityBar = GetDurabilityBar(boat.Durability, boat.MaxDurability);
            
            fields.Add(new EmbedField(
                $"{boatInfo?.Name ?? boat.Name}{isEquipped}",
                $"**Durability:** {durabilityBar} ({boat.Durability}/{boat.MaxDurability})\n" +
                $"**Storage:** {boat.Storage.Count}/{boat.StorageCapacity} slots used\n" +
                $"**Last Used:** {boat.LastUsed:d}",
                true
            ));
        }

        var embed = new Embed
        {
            Title = "⛵ Your Boats",
            Description = $"You have {boats.Count} boat(s). Use `/boat equip` to select one.",
            Fields = fields,
            Colour = Color.Blue,
            Footer = new EmbedFooter($"Equipped: {(player.EquippedBoat != null ? "Yes" : "None")}")
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("equip")]
    [Description("Equip a boat for water fishing")]
    public async Task<IResult> EquipBoatAsync(
        [Description("Boat number (1, 2, 3, etc.)")] int boatNumber)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var boats = await boatRepository.GetUserBoatsAsync(user.ID.Value);

        if (boats.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("⛵ You don't have any boats to equip!", Color.Red);
        }

        if (boatNumber < 1 || boatNumber > boats.Count)
        {
            return await feedbackService.SendContextualContentAsync($"❌ Invalid boat number. You have {boats.Count} boat(s).", Color.Red);
        }

        var selectedBoat = boats[boatNumber - 1];
        
        if (selectedBoat.Durability <= 0)
        {
            return await feedbackService.SendContextualContentAsync("🔧 This boat is broken and needs to be repaired before use!", Color.Red);
        }

        player.EquippedBoat = selectedBoat.BoatId;
        await playerRepository.UpdatePlayerAsync(player);

        var boatInfo = BoatTypes.BoatData.GetValueOrDefault(selectedBoat.BoatType);
        return await feedbackService.SendContextualContentAsync(
            $"⛵ Equipped **{boatInfo?.Name ?? selectedBoat.Name}**! You can now fish at water spots.", 
            Color.Green);
    }

    [Command("unequip")]
    [Description("Unequip your current boat")]
    public async Task<IResult> UnequipBoatAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        if (player.EquippedBoat == null)
        {
            return await feedbackService.SendContextualContentAsync("⛵ You don't have a boat equipped.", Color.Orange);
        }

        player.EquippedBoat = null;
        await playerRepository.UpdatePlayerAsync(player);

        return await feedbackService.SendContextualContentAsync("⛵ Boat unequipped. You can no longer fish at water spots.", Color.Green);
    }

    [Command("storage")]
    [Description("View your equipped boat's storage")]
    public async Task<IResult> ViewStorageAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        if (player.EquippedBoat == null)
        {
            return await feedbackService.SendContextualContentAsync("⛵ You need to equip a boat first!", Color.Red);
        }

        var boat = await boatRepository.GetBoatAsync(player.EquippedBoat);
        if (boat == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ Equipped boat not found!", Color.Red);
        }

        if (boat.Storage.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("📦 Your boat's storage is empty.", Color.Blue);
        }

        var fields = new List<EmbedField>();
        var groupedItems = boat.Storage.GroupBy(item => item.ItemId);

        foreach (var group in groupedItems)
        {
            var item = group.First();
            var totalQuantity = group.Sum(i => i.Quantity);
            
            fields.Add(new EmbedField(
                item.Name,
                $"**Type:** {item.ItemType}\n**Quantity:** {totalQuantity}",
                true
            ));
        }

        var embed = new Embed
        {
            Title = "📦 Boat Storage",
            Description = $"Storage used: {boat.Storage.Count}/{boat.StorageCapacity} slots",
            Fields = fields,
            Colour = Color.Blue
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("collect")]
    [Description("Collect resources from the sea in your current area")]
    public async Task<IResult> CollectResourcesAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        if (player.EquippedBoat == null)
        {
            return await feedbackService.SendContextualContentAsync("⛵ You need to equip a boat to collect sea resources!", Color.Red);
        }

        var boat = await boatRepository.GetBoatAsync(player.EquippedBoat);
        if (boat == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ Equipped boat not found!", Color.Red);
        }

        if (boat.Durability <= 0)
        {
            return await feedbackService.SendContextualContentAsync("🔧 Your boat is broken and needs to be repaired before use!", Color.Red);
        }

        if (boat.Storage.Count >= boat.StorageCapacity)
        {
            return await feedbackService.SendContextualContentAsync("📦 Your boat's storage is full! Use `/boat transfer` to move items to your inventory.", Color.Orange);
        }

        // Get current area to determine available resources
        var currentArea = await GetCurrentAreaAsync(player);
        if (currentArea == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ Current area not found!", Color.Red);
        }

        // Check if area has water-based resources
        var seaResources = GetSeaResourcesForArea(currentArea.AreaId);
        if (seaResources.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🌊 This area doesn't have any sea resources to collect.", Color.Blue);
        }

        // Randomly select a resource
        var random = new Random();
        var selectedResource = seaResources[random.Next(seaResources.Count)];
        var quantity = random.Next(1, 4); // 1-3 resources

        // Add to boat storage
        var resourceItem = new InventoryItem
        {
            ItemId = selectedResource.ItemId,
            ItemType = selectedResource.ItemType,
            Name = selectedResource.Name,
            Quantity = quantity,
            Properties = selectedResource.Properties
        };

        boat.Storage.Add(resourceItem);
        
        // Reduce boat durability
        boat.Durability = Math.Max(0, boat.Durability - 3);
        boat.LastUsed = DateTime.UtcNow;
        await boatRepository.UpdateBoatAsync(boat);

        return await feedbackService.SendContextualContentAsync(
            $"🌊 Your boat collected **{quantity}x {selectedResource.Name}**!\n" +
            $"📦 Storage: {boat.Storage.Count}/{boat.StorageCapacity} slots used\n" +
            $"🔧 Boat durability: {boat.Durability}/{boat.MaxDurability}",
            Color.Green);
    }

    [Command("transfer")]
    [Description("Transfer items from boat storage to your inventory")]
    public async Task<IResult> TransferFromBoatAsync(
        [Description("Item name to transfer")] string itemName,
        [Description("Quantity to transfer (default: all)")] int quantity = -1)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);

        if (player.EquippedBoat == null)
        {
            return await feedbackService.SendContextualContentAsync("⛵ You need to equip a boat first!", Color.Red);
        }

        var boat = await boatRepository.GetBoatAsync(player.EquippedBoat);
        if (boat == null)
        {
            return await feedbackService.SendContextualContentAsync("❌ Equipped boat not found!", Color.Red);
        }

        // Find the item in boat storage
        var item = boat.Storage.FirstOrDefault(i => 
            i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
            i.ItemId.Equals(itemName, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            return await feedbackService.SendContextualContentAsync($"❌ Item '{itemName}' not found in boat storage!", Color.Red);
        }

        // Determine transfer quantity
        var transferQuantity = quantity == -1 ? item.Quantity : Math.Min(quantity, item.Quantity);
        
        if (transferQuantity <= 0)
        {
            return await feedbackService.SendContextualContentAsync("❌ Invalid transfer quantity!", Color.Red);
        }

        // Create item for inventory
        var inventoryItem = new InventoryItem
        {
            ItemId = item.ItemId,
            ItemType = item.ItemType,
            Name = item.Name,
            Quantity = transferQuantity,
            Properties = item.Properties
        };

        // Add to player inventory
        await inventoryRepository.AddItemAsync(user.ID.Value, inventoryItem);

        // Remove from boat storage
        if (transferQuantity >= item.Quantity)
        {
            boat.Storage.Remove(item);
        }
        else
        {
            item.Quantity -= transferQuantity;
        }

        await boatRepository.UpdateBoatAsync(boat);

        return await feedbackService.SendContextualContentAsync(
            $"📦 Transferred **{transferQuantity}x {item.Name}** from boat to inventory!\n" +
            $"🚢 Boat storage: {boat.Storage.Count}/{boat.StorageCapacity} slots used",
            Color.Green);
    }

    private async Task<PlayerProfile> GetOrCreatePlayerAsync(ulong userId, string username)
    {
        var player = await playerRepository.GetPlayerAsync(userId);
        if (player == null)
        {
            player = await playerRepository.CreatePlayerAsync(userId, username);
        }
        return player;
    }

    private async Task<AreaState?> GetCurrentAreaAsync(PlayerProfile player)
    {
        return await areaRepository.GetAreaAsync(player.CurrentArea);
    }

    private static List<InventoryItem> GetSeaResourcesForArea(string areaId)
    {
        return areaId switch
        {
            "mystic_lake" => new List<InventoryItem>
            {
                new() { ItemId = "driftwood", ItemType = "Material", Name = "Driftwood", Properties = new() { ["value"] = 5 } },
                new() { ItemId = "lake_stone", ItemType = "Material", Name = "Smooth Lake Stone", Properties = new() { ["value"] = 3 } },
                new() { ItemId = "water_lily", ItemType = "Material", Name = "Water Lily", Properties = new() { ["value"] = 8 } }
            },
            "deep_ocean" => new List<InventoryItem>
            {
                new() { ItemId = "sea_coral", ItemType = "Material", Name = "Sea Coral", Properties = new() { ["value"] = 15 } },
                new() { ItemId = "metal_fragment", ItemType = "Material", Name = "Metal Fragment", Properties = new() { ["value"] = 25 } },
                new() { ItemId = "abyssal_wood", ItemType = "Material", Name = "Abyssal Wood", Properties = new() { ["value"] = 20 } },
                new() { ItemId = "pearl_shard", ItemType = "Material", Name = "Pearl Shard", Properties = new() { ["value"] = 50 } }
            },
            "enchanted_forest" => new List<InventoryItem>
            {
                new() { ItemId = "moonwater_coral", ItemType = "Material", Name = "Moonwater Coral", Properties = new() { ["value"] = 30 } },
                new() { ItemId = "ethereal_wood", ItemType = "Material", Name = "Ethereal Wood", Properties = new() { ["value"] = 35 } },
                new() { ItemId = "starlight_crystal", ItemType = "Material", Name = "Starlight Crystal", Properties = new() { ["value"] = 75 } }
            },
            _ => new List<InventoryItem>()
        };
    }

    private static string GetDurabilityBar(int current, int max)
    {
        var percentage = (double)current / max;
        var barLength = 10;
        var filledBars = (int)(percentage * barLength);
        
        var bar = new string('█', filledBars) + new string('░', barLength - filledBars);
        
        return percentage switch
        {
            > 0.7 => $"🟢 {bar}",
            > 0.3 => $"🟡 {bar}",
            _ => $"🔴 {bar}"
        };
    }
}