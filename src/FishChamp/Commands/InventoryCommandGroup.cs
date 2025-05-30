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
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using System.Text.Json;

namespace FishChamp.Modules;

[Group("inventory")]
[Description("Inventory management commands")]
public class InventoryCommandGroup(IInteractionContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository, 
    FeedbackService feedbackService) : CommandGroup
{
    [Command("view")]
    [Description("View your inventory")]
    public async Task<IResult> ViewInventoryAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null || inventory.Items.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🎒 Your inventory is empty! Try fishing to get some items.", Color.Yellow);
        }

        var groupedItems = inventory.Items.GroupBy(i => i.ItemType);
        var fields = new List<EmbedField>();

        foreach (var group in groupedItems)
        {
            var itemsText = string.Join("\n", group.Select(item =>
            {
                string additionalInfo = "";
                
                if (item.ItemType == "Fish")
                {
                    var size = item.Properties.TryGetValue("size", out var propSize) ? $"{propSize}cm" : "";
                    var rarity = item.Properties.TryGetValue("rarity", out var propRarity) ? GetRarityEmoji(propRarity.ToString()) : "";
                    additionalInfo = $" {rarity} {size}";
                }
                else if (item.ItemType == "Rod")
                {
                    var power = item.Properties.TryGetValue("power", out var propPower) ? $"Power: {propPower}" : "";
                    var durability = item.Properties.TryGetValue("durability", out var propDur) ? $"Durability: {propDur}" : "";
                    additionalInfo = $" ({power}, {durability})";
                    
                    // Mark equipped rod
                    if (item.ItemId == player.EquippedRod)
                    {
                        additionalInfo += " [EQUIPPED]";
                    }
                }
                else if (item.ItemType == "Bait")
                {
                    // Mark equipped bait
                    if (item.ItemId == player.EquippedBait)
                    {
                        additionalInfo = " [EQUIPPED]";
                    }
                }
                
                return $"• **{item.Name}** x{item.Quantity}{additionalInfo}";
            }));

            fields.Add(new EmbedField($"{GetItemTypeEmoji(group.Key)} {group.Key}s ({group.Sum(i => i.Quantity)})", itemsText, false));
        }

        var embed = new Embed
        {
            Title = $"🎒 {player.Username}'s Inventory",
            Colour = Color.Orange,
            Fields = fields,
            Footer = new EmbedFooter($"Use /inventory fish, /inventory rods, or /inventory baits for more details\nLast updated: {inventory.LastUpdated:yyyy-MM-dd HH:mm} UTC"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("fish")]
    [Description("View only your fish collection")]
    public async Task<IResult> ViewFishAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🐟 You haven't caught any fish yet! Use `/fish` to start fishing.", Color.Yellow);
        }

        var fish = inventory.Items.Where(i => i.ItemType == "Fish").ToList();
        if (fish.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🐟 You haven't caught any fish yet! Use `/fish` to start fishing.", Color.Red);
        }

        var fishText = string.Join("\n", fish.Select(f =>
        {
            var size = f.Properties.TryGetValue("size", out var propSize) ? $" ({propSize}cm)" : "";
            var weight = f.Properties.TryGetValue("weight", out var propWeight) ? $", {propWeight}g" : "";
            var rarity = f.Properties.TryGetValue("rarity", out var propRarity) ? GetRarityEmoji(propRarity.ToString()) : "";
            
            // Add fish traits if any
            string traitsText = "";
            if (f.Properties.TryGetValue("traits", out var propTraits))
            {
                var fishTraits = (FishTrait)GetValueFromProperty<int>(propTraits);
                if (fishTraits != FishTrait.None)
                {
                    var traitsList = new List<string>();
                    if ((fishTraits & FishTrait.Evasive) != 0) traitsList.Add("Evasive");
                    if ((fishTraits & FishTrait.Slippery) != 0) traitsList.Add("Slippery");
                    if ((fishTraits & FishTrait.Magnetic) != 0) traitsList.Add("Magnetic");
                    if ((fishTraits & FishTrait.Camouflage) != 0) traitsList.Add("Camouflage");
                    
                    if (traitsList.Count > 0)
                    {
                        traitsText = $" | {string.Join(", ", traitsList)}";
                    }
                }
            }
            
            return $"{rarity} **{f.Name}** x{f.Quantity}{size}{weight}{traitsText}";
        }));

        var totalFish = fish.Sum(f => f.Quantity);
        var uniqueSpecies = fish.Count;

        var embed = new Embed
        {
            Title = $"🐟 {player.Username}'s Fish Collection",
            Description = fishText,
            Colour = Color.Cyan,
            Fields = new List<EmbedField>
            {
                new("Total Fish", totalFish.ToString(), true),
                new("Unique Species", uniqueSpecies.ToString(), true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("rods")]
    [Description("View your fishing rod collection")]
    public async Task<IResult> ViewRodsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🎣 You don't have any fishing rods yet! Visit a shop to buy one.", Color.Yellow);
        }

        var rods = inventory.Items.Where(i => i.ItemType == "Rod").ToList();
        if (rods.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🎣 You don't have any fishing rods yet! Visit a shop to buy one.", Color.Red);
        }

        var rodsText = string.Join("\n", rods.Select(r =>
        {
            var power = r.Properties.TryGetValue("power", out var propPower) ? $"Power: {propPower}" : "";
            var durability = r.Properties.TryGetValue("durability", out var propDur) ? $"Durability: {propDur}" : "";
            var equipped = r.ItemId == player.EquippedRod ? " **[EQUIPPED]**" : "";
            
            return $"• **{r.Name}** ({power}, {durability}){equipped}";
        }));

        var embed = new Embed
        {
            Title = $"🎣 {player.Username}'s Fishing Rods",
            Description = rodsText,
            Colour = Color.Brown,
            Footer = new EmbedFooter("Use /fishing equip-rod <rod name> to equip a rod"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("baits")]
    [Description("View your bait collection")]
    public async Task<IResult> ViewBaitsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualContentAsync("🪱 You don't have any bait yet! Visit a shop to buy some.", Color.Yellow);
        }

        var baits = inventory.Items.Where(i => i.ItemType == "Bait").ToList();
        if (baits.Count == 0)
        {
            return await feedbackService.SendContextualContentAsync("🪱 You don't have any bait yet! Visit a shop to buy some.", Color.Red);
        }

        var baitsText = string.Join("\n", baits.Select(b =>
        {
            var attraction = b.Properties.TryGetValue("attraction", out var propAttr) ? $"Attraction: {propAttr}x" : "";
            var equipped = b.ItemId == player.EquippedBait ? " **[EQUIPPED]**" : "";
            
            return $"• **{b.Name}** x{b.Quantity} ({attraction}){equipped}";
        }));

        var embed = new Embed
        {
            Title = $"🪱 {player.Username}'s Bait Collection",
            Description = baitsText,
            Colour = Color.SandyBrown,
            Footer = new EmbedFooter("Use /fishing equip-bait <bait name> to equip bait"),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
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

    private static string GetItemTypeEmoji(string itemType)
    {
        return itemType.ToLower() switch
        {
            "fish" => "🐟",
            "rod" => "🎣",
            "bait" => "🪱",
            "tool" => "🔧",
            _ => "📦"
        };
    }

    private static string GetRarityEmoji(string? rarity)
    {
        return rarity?.ToLower() switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵",
            "epic" => "🟣",
            "legendary" => "🟡",
            _ => "⚪"
        };
    }

    public static T GetValueFromProperty<T>(object obj)
    {
        if (obj is not JsonElement element) return default;
        return JsonSerializer.Deserialize<T>(element);
    }
}