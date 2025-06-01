using System.ComponentModel;
using System.Text;
using System.Drawing;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using FishChamp.Data.Repositories;
using FishChamp.Data.Models;
using FishChamp.Helpers;
using FishChamp.Modules;

namespace FishChamp.Features.Trapping;

[Group("trap")]
[Description("Fish trap commands for passive fishing")]
public class TrapCommandGroup(IInteractionCommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository, ITrapRepository trapRepository,
    FeedbackService feedbackService) : CommandGroup
{
    [Command("deploy")]
    [Description("Deploy a fish trap at your current fishing spot")]
    public async Task<IResult> DeployTrapAsync(
        [Description("Duration in hours (1-24)")] int hours = 2,
        [Description("Bait to use (optional)")] string? bait = null)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await playerRepository.GetPlayerAsync(user.ID.Value);

        if (player == null)
        {
            return await feedbackService.SendContextualErrorAsync("Player profile not found. Use `/fish` to get started!");
        }

        // Validate duration
        if (hours < 1 || hours > 24)
        {
            return await feedbackService.SendContextualErrorAsync("Trap duration must be between 1 and 24 hours.");
        }

        // Check if player has a trap available
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        var trapItem = inventory?.Items.FirstOrDefault(i => i.ItemType == "Trap" && i.Quantity > 0);

        if (trapItem == null)
        {
            return await feedbackService.SendContextualErrorAsync("You don't have any traps! Buy one from the shop first.");
        }

        // Check if player already has a trap deployed in this area
        var existingTraps = await trapRepository.GetUserTrapsAsync(user.ID.Value);
        var activeTrap = existingTraps.FirstOrDefault(t =>
            t.CurrentArea == player.CurrentArea &&
            t.FishingSpot == player.CurrentFishingSpot &&
            !t.IsCompleted && DateTime.UtcNow < t.CompletesAt);

        if (activeTrap != null)
        {
            var timeRemaining = activeTrap.CompletesAt - DateTime.UtcNow;
            return await feedbackService.SendContextualErrorAsync(
                $"You already have a trap deployed here! Check it in {timeRemaining.Hours}h {timeRemaining.Minutes}m.");
        }

        // Validate bait if specified
        string? equippedBait = null;
        if (!string.IsNullOrEmpty(bait))
        {
            var baitItem = inventory?.Items.FirstOrDefault(i =>
                i.ItemType == "Bait" &&
                (i.ItemId.Contains(bait.ToLower()) || i.Name.ToLower().Contains(bait.ToLower())) &&
                i.Quantity > 0);

            if (baitItem == null)
            {
                return await feedbackService.SendContextualErrorAsync($"You don't have any '{bait}' bait!");
            }

            equippedBait = baitItem.ItemId;
            // Consume bait
            await inventoryRepository.RemoveItemAsync(user.ID.Value, baitItem.ItemId, 1);
        }

        // Get current area for fish availability
        var area = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (area == null)
        {
            return await feedbackService.SendContextualErrorAsync("Current area not found!");
        }

        // Create and deploy trap
        var trap = new FishTrap
        {
            UserId = user.ID.Value,
            TrapType = trapItem.ItemId,
            CurrentArea = player.CurrentArea,
            FishingSpot = player.CurrentFishingSpot,
            EquippedBait = equippedBait,
            DeployedAt = DateTime.UtcNow,
            CompletesAt = DateTime.UtcNow.AddHours(hours),
            Durability = trapItem.Properties.GetInt("durability", 100)
        };

        await trapRepository.CreateTrapAsync(trap);

        // Consume trap item
        await inventoryRepository.RemoveItemAsync(user.ID.Value, trapItem.ItemId, 1);

        var baitText = !string.IsNullOrEmpty(equippedBait) ? $" with {bait} bait" : "";
        var embed = new Embed
        {
            Title = "🪤 Trap Deployed!",
            Description = $"You've deployed a {trapItem.Name} at **{player.CurrentFishingSpot}**{baitText}.\n\n" +
                         $"⏰ **Duration:** {hours} hour(s)\n" +
                         $"⚡ **Completes:** <t:{((DateTimeOffset)trap.CompletesAt).ToUnixTimeSeconds()}:R>\n\n" +
                         $"Use `/trap check` to see your active traps and collect any catches!",
            Colour = Color.Green,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("check")]
    [Description("Check your deployed traps for catches")]
    public async Task<IResult> CheckTrapsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var userTraps = await trapRepository.GetUserTrapsAsync(user.ID.Value);

        if (!userTraps.Any())
        {
            return await feedbackService.SendContextualErrorAsync("You don't have any traps deployed!");
        }

        var activeTraps = userTraps.Where(t => !t.IsCompleted && DateTime.UtcNow < t.CompletesAt).ToList();
        var completedTraps = userTraps.Where(t => !t.HasBeenChecked && (t.IsCompleted || DateTime.UtcNow >= t.CompletesAt)).ToList();

        var embed = new Embed
        {
            Title = "🪤 Your Fish Traps",
            Colour = Color.Blue,
            Timestamp = DateTimeOffset.UtcNow
        };

        var description = new StringBuilder();

        if (activeTraps.Any())
        {
            description.AppendLine("**🟢 Active Traps:**");
            foreach (var trap in activeTraps)
            {
                var timeRemaining = trap.CompletesAt - DateTime.UtcNow;
                var baitText = !string.IsNullOrEmpty(trap.EquippedBait) ? $" (with bait)" : "";
                description.AppendLine($"• **{trap.CurrentArea}** - {trap.FishingSpot}{baitText}");
                description.AppendLine($"  ⏰ Completes in {timeRemaining.Hours}h {timeRemaining.Minutes}m");
                description.AppendLine();
            }
        }

        if (completedTraps.Any())
        {
            description.AppendLine("**🎣 Ready to Collect:**");
            var totalCaught = 0;

            foreach (var trap in completedTraps)
            {
                // Generate catches for completed trap
                await GenerateTrapCatches(trap);

                var catchCount = trap.CaughtFish.Count;
                totalCaught += catchCount;

                var baitText = !string.IsNullOrEmpty(trap.EquippedBait) ? $" (with bait)" : "";
                description.AppendLine($"• **{trap.CurrentArea}** - {trap.FishingSpot}{baitText}");
                description.AppendLine($"  🐟 Caught {catchCount} fish!");

                // Add caught fish to inventory
                foreach (var fish in trap.CaughtFish)
                {
                    var fishItem = new InventoryItem
                    {
                        ItemId = fish.FishType,
                        ItemType = "Fish",
                        Name = fish.Name,
                        Quantity = 1,
                        Properties = new()
                        {
                            ["rarity"] = fish.Rarity,
                            ["size"] = fish.Size,
                            ["weight"] = fish.Weight,
                            ["traits"] = (int)fish.Traits,
                            ["caught_at"] = fish.CaughtAt
                        }
                    };

                    await inventoryRepository.AddItemAsync(user.ID.Value, fishItem);
                }

                // Mark trap as checked
                trap.HasBeenChecked = true;
                await trapRepository.UpdateTrapAsync(trap);

                description.AppendLine();
            }

            if (totalCaught > 0)
            {
                description.AppendLine($"**Total fish collected: {totalCaught}** 🎉");
                // Update player profile with any fish dex changes
                await playerRepository.UpdatePlayerAsync(player);
            }
        }

        if (!activeTraps.Any() && !completedTraps.Any())
        {
            description.AppendLine("No active or completed traps found.");
            description.AppendLine("Deploy a new trap with `/trap deploy`!");
        }

        embed = embed with { Description = description.ToString() };
        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("list")]
    [Description("List all your trap deployments and history")]
    public async Task<IResult> ListTrapsAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var userTraps = await trapRepository.GetUserTrapsAsync(user.ID.Value);

        if (!userTraps.Any())
        {
            return await feedbackService.SendContextualErrorAsync("You haven't deployed any traps yet!");
        }

        var embed = new Embed
        {
            Title = "🪤 Trap History",
            Colour = Color.Purple,
            Timestamp = DateTimeOffset.UtcNow
        };

        var description = new StringBuilder();
        var recentTraps = userTraps.OrderByDescending(t => t.DeployedAt).Take(10);

        foreach (var trap in recentTraps)
        {
            var status = trap.IsCompleted || DateTime.UtcNow >= trap.CompletesAt ? "✅ Completed" : "🟡 Active";
            var baitText = !string.IsNullOrEmpty(trap.EquippedBait) ? " with bait" : "";

            description.AppendLine($"**{trap.CurrentArea}** - {trap.FishingSpot}");
            description.AppendLine($"Status: {status} | Deployed: <t:{((DateTimeOffset)trap.DeployedAt).ToUnixTimeSeconds()}:R>");
            if (trap.CaughtFish.Any())
            {
                description.AppendLine($"Caught: {trap.CaughtFish.Count} fish");
            }
            description.AppendLine();
        }

        embed = embed with { Description = description.ToString() };
        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("info")]
    [Description("View information about trap types and their properties")]
    public async Task<IResult> TrapInfoAsync([Description("Trap type to view")] string? trapType = null)
    {
        if (string.IsNullOrEmpty(trapType))
        {
            // Show all trap types
            var embed = new Embed
            {
                Title = "🪤 Trap Types Information",
                Description = "**Available Trap Types:**\n\n" +
                             "• **Basic Trap** - Standard trap for all areas\n" +
                             "• **Shallow Water Trap** - Optimized for shore fishing\n" +
                             "• **Deep Water Trap** - Best for deep water spots\n" +
                             "• **Reinforced Trap** - High durability, works everywhere\n\n" +
                             "Use `/trap info <type>` for detailed information about a specific trap type.",
                Colour = Color.Teal,
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }

        var trapDetails = GetTrapTypeDetails(trapType.ToLower());
        if (trapDetails == null)
        {
            return await feedbackService.SendContextualErrorAsync(
                $"Unknown trap type '{trapType}'. Available types: basic, shallow, deep, reinforced");
        }

        var detailEmbed = new Embed
        {
            Title = $"🪤 {trapDetails.Name}",
            Description = $"**{trapDetails.Description}**\n\n" +
                         $"**Properties:**\n" +
                         $"• **Durability:** {trapDetails.Durability} (lasts longer)\n" +
                         $"• **Efficiency:** {trapDetails.Efficiency}x catch rate\n" +
                         (trapDetails.WaterType != null ? $"• **Specialized for:** {trapDetails.WaterType} water fishing\n" : "") +
                         $"• **Shop Price:** {trapDetails.ShopPrice} coins\n\n" +
                         $"**Best Used At:**\n{trapDetails.BestUse}",
            Colour = Color.DarkSlateBlue,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(detailEmbed);
    }

    [Command("repair")]
    [Description("Repair a damaged trap using materials")]
    public async Task<IResult> RepairTrapAsync([Description("Trap ID to repair")] string? trapId = null)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var userTraps = await trapRepository.GetUserTrapsAsync(user.ID.Value);
        var damagedTraps = userTraps.Where(t => t.Durability < 50 && !t.IsCompleted).ToList();

        if (!damagedTraps.Any())
        {
            return await feedbackService.SendContextualErrorAsync("You don't have any damaged traps that need repair!");
        }

        // If no specific trap ID provided, show list of damaged traps
        if (string.IsNullOrEmpty(trapId))
        {
            var listDescription = new StringBuilder();
            listDescription.AppendLine("**Damaged Traps Available for Repair:**\n");

            foreach (var trap in damagedTraps)
            {
                listDescription.AppendLine($"• **{trap.TrapId.Substring(0, 8)}...** - {trap.TrapType}");
                listDescription.AppendLine($"  Location: {trap.CurrentArea} - {trap.FishingSpot}");
                listDescription.AppendLine($"  Durability: {trap.Durability}/100 ({(trap.Durability < 25 ? "Critical" : "Damaged")})");
                listDescription.AppendLine();
            }

            listDescription.AppendLine("Use `/trap repair <trap_id>` to repair a specific trap.");
            listDescription.AppendLine("Repair cost: 1 Trap Material per 25 durability restored.");

            var listEmbed = new Embed
            {
                Title = "🔧 Trap Repair Service",
                Description = listDescription.ToString(),
                Colour = Color.Orange,
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(listEmbed);
        }

        // Find specific trap to repair
        var targetTrap = damagedTraps.FirstOrDefault(t => t.TrapId.StartsWith(trapId));
        if (targetTrap == null)
        {
            return await feedbackService.SendContextualErrorAsync($"Trap with ID '{trapId}' not found or doesn't need repair!");
        }

        // Check if player has repair materials
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        var materialItem = inventory?.Items.FirstOrDefault(i => i.ItemId == "trap_material");

        var durabilityToRestore = 100 - targetTrap.Durability;
        var materialsNeeded = Math.Ceiling(durabilityToRestore / 25.0);

        if (materialItem == null || materialItem.Quantity < materialsNeeded)
        {
            var hasAmount = materialItem?.Quantity ?? 0;
            return await feedbackService.SendContextualErrorAsync(
                $"Not enough Trap Materials! Need {materialsNeeded} (you have {hasAmount})");
        }

        // Perform repair
        await inventoryRepository.RemoveItemAsync(user.ID.Value, "trap_material", (int)materialsNeeded);
        targetTrap.Durability = 100;
        await trapRepository.UpdateTrapAsync(targetTrap);

        var repairEmbed = new Embed
        {
            Title = "🔧 Trap Repaired!",
            Description = $"Successfully repaired your **{targetTrap.TrapType}** trap!\n\n" +
                         $"**Location:** {targetTrap.CurrentArea} - {targetTrap.FishingSpot}\n" +
                         $"**Durability:** 100/100 ✨\n" +
                         $"**Materials Used:** {materialsNeeded} Trap Material(s)\n\n" +
                         "Your trap is now ready for deployment again!",
            Colour = Color.LimeGreen,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(repairEmbed);
    }

    private async Task GenerateTrapCatches(FishTrap trap)
    {
        // Don't regenerate if already has catches
        if (trap.CaughtFish.Any()) return;

        var area = await areaRepository.GetAreaAsync(trap.CurrentArea);
        if (area?.FishingSpots == null) return;

        var fishingSpot = area.FishingSpots.FirstOrDefault(fs => fs.Name == trap.FishingSpot);
        if (fishingSpot?.AvailableFish == null) return;

        var random = new Random();
        var baseCatchChance = 0.3; // 30% chance per hour base
        var hoursDuration = (trap.CompletesAt - trap.DeployedAt).TotalHours;
        var totalChance = Math.Min(0.9, baseCatchChance * hoursDuration); // Cap at 90%

        // Bait bonus
        if (!string.IsNullOrEmpty(trap.EquippedBait))
        {
            totalChance = Math.Min(0.95, totalChance * 1.5); // 50% bonus with bait
        }

        // Determine number of catches based on trap efficiency
        var efficiency = trap.Properties.GetDouble("efficiency", 1.0);

        var catchCount = 0;
        var adjustedTotalChance = totalChance * efficiency;

        for (int i = 0; i < Math.Ceiling(hoursDuration); i++)
        {
            if (random.NextDouble() < adjustedTotalChance)
            {
                catchCount++;
            }
        }

        // Generate each catch
        for (int i = 0; i < catchCount; i++)
        {
            var fishId = fishingSpot.AvailableFish[random.Next(fishingSpot.AvailableFish.Count)];

            // Determine rarity (better traps get better rates)
            var rarityRoll = random.NextDouble();
            var baseRarityBonus = efficiency > 1.5 ? 0.1 : efficiency > 1.2 ? 0.05 : 0.0;

            // Special bait bonus for rare fish
            var baitRareBonus = 0.0;
            if (!string.IsNullOrEmpty(trap.EquippedBait) && trap.EquippedBait.Contains("rare"))
            {
                baitRareBonus = 0.15;
            }

            var adjustedRarityRoll = rarityRoll - baseRarityBonus - baitRareBonus;
            var rarity = adjustedRarityRoll switch
            {
                < 0.65 => "common",
                < 0.85 => "uncommon",
                < 0.96 => "rare",
                _ => "epic"
            };

            // Generate size and weight based on rarity
            int minSize = rarity switch { "uncommon" => 20, "rare" => 30, "epic" => 40, _ => 10 };
            int maxSize = rarity switch { "uncommon" => 60, "rare" => 70, "epic" => 80, _ => 50 };
            var fishSize = random.Next(minSize, maxSize + 1);

            double weightMultiplier = rarity switch { "uncommon" => 1.2, "rare" => 1.5, "epic" => 1.8, _ => 1.0 };
            var fishWeight = Math.Round(Math.Pow(fishSize, 1.5) * weightMultiplier, 1);

            var caughtFish = new CaughtFish
            {
                FishType = fishId,
                Name = GetFishDisplayName(fishId), // We'll need this helper
                Rarity = rarity,
                Size = fishSize,
                Weight = fishWeight,
                Traits = DetermineFishTraits(rarity, random),
                CaughtAt = trap.DeployedAt.AddHours(random.NextDouble() * hoursDuration)
            };

            trap.CaughtFish.Add(caughtFish);
        }

        // Reduce trap durability based on usage and trap type
        var baseDurability = trap.Properties.GetInt("durability", 100);

        var durabilityLoss = Math.Max(1, (int)(hoursDuration * (100.0 / baseDurability) * 3)); // Better traps lose less durability
        trap.Durability = Math.Max(0, trap.Durability - durabilityLoss);
        trap.IsCompleted = true;

        await trapRepository.UpdateTrapAsync(trap);
    }

    private static FishTrait DetermineFishTraits(string rarity, Random random)
    {
        var traitChance = rarity switch
        {
            "common" => 0.1,
            "uncommon" => 0.2,
            "rare" => 0.4,
            "epic" => 0.6,
            _ => 0.1
        };

        FishTrait traits = FishTrait.None;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Evasive;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Slippery;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Magnetic;

        if (random.NextDouble() < traitChance)
            traits |= FishTrait.Camouflage;

        return traits;
    }

    private static string GetFishDisplayName(string fishId)
    {
        return fishId switch
        {
            "azure_finling" => "Azure Finling",
            "crystal_perch" => "Crystal Perch",
            "ember_bass" => "Ember Bass",
            "shadow_whiskers" => "Shadow Whiskers",
            "glimmer_minnow" => "Glimmer Minnow",
            "golden_sunfish" => "Golden Sunfish",
            "rainbow_trout" => "Rainbow Trout",
            "pike" => "Pike",
            "mysterious_eel" => "Mysterious Eel",
            "ghost_carp" => "Ghost Carp",
            "silver_perch" => "Silver Perch",
            "moonfish" => "Moonfish",
            "prism_trout" => "Prism Trout",
            "ethereal_guppy" => "Ethereal Guppy",
            "starlight_salmon" => "Starlight Salmon",
            "lunar_bass" => "Lunar Bass",
            "void_eel" => "Void Eel",
            "phoenix_koi" => "Phoenix Koi",
            "fairy_fin" => "Fairy Fin",
            "dream_carp" => "Dream Carp",
            "celestial_perch" => "Celestial Perch",
            "titan_tuna" => "Titan Tuna",
            "storm_marlin" => "Storm Marlin",
            "kraken_spawn" => "Kraken Spawn",
            "void_leviathan" => "Void Leviathan",
            "ancient_angler" => "Ancient Angler",
            "deep_dragon" => "Deep Dragon",
            "coral_emperor" => "Coral Emperor",
            "reef_spirit" => "Reef Spirit",
            "rainbow_ray" => "Rainbow Ray",
            _ => fishId.Replace("_", " ").ToTitleCase()
        };
    }

    private static TrapTypeDetails? GetTrapTypeDetails(string trapType)
    {
        return trapType switch
        {
            "basic" => new TrapTypeDetails
            {
                Name = "Basic Fish Trap",
                Description = "A simple trap suitable for all fishing areas. Good for beginners.",
                Durability = 100,
                Efficiency = 1.0,
                ShopPrice = 100,
                BestUse = "Any fishing spot - versatile but not specialized"
            },
            "shallow" => new TrapTypeDetails
            {
                Name = "Shallow Water Trap",
                Description = "Specialized trap designed for shore and shallow water fishing.",
                Durability = 120,
                Efficiency = 1.3,
                WaterType = "shallow",
                ShopPrice = 200,
                BestUse = "Shore fishing spots, lakeside areas, and shallow waters"
            },
            "deep" => new TrapTypeDetails
            {
                Name = "Deep Water Trap",
                Description = "Heavy-duty trap optimized for deep water fishing.",
                Durability = 150,
                Efficiency = 1.5,
                WaterType = "deep",
                ShopPrice = 350,
                BestUse = "Deep water fishing spots, ocean areas, and large lakes"
            },
            "reinforced" => new TrapTypeDetails
            {
                Name = "Reinforced Trap",
                Description = "Premium trap with exceptional durability and catch rate.",
                Durability = 200,
                Efficiency = 2.0,
                ShopPrice = 500,
                BestUse = "Any location - premium choice for serious trappers"
            },
            _ => null
        };
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

public class TrapTypeDetails
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Durability { get; set; }
    public double Efficiency { get; set; }
    public string? WaterType { get; set; }
    public int ShopPrice { get; set; }
    public string BestUse { get; set; } = string.Empty;
}