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

namespace FishChamp.Modules;

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
            Durability = (int)(trapItem.Properties.TryGetValue("durability", out var dur) ? 
                Convert.ToInt32(dur) : 100)
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
            Colour = System.Drawing.Color.Green,
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
            Colour = System.Drawing.Color.Blue,
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
            Colour = System.Drawing.Color.Purple,
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

        // Determine number of catches
        var catchCount = 0;
        for (int i = 0; i < Math.Ceiling(hoursDuration); i++)
        {
            if (random.NextDouble() < totalChance)
            {
                catchCount++;
            }
        }

        // Generate each catch
        for (int i = 0; i < catchCount; i++)
        {
            var fishId = fishingSpot.AvailableFish[random.Next(fishingSpot.AvailableFish.Count)];
            
            // Determine rarity (passive traps get slightly lower rare fish rates)
            var rarityRoll = random.NextDouble();
            var rarity = rarityRoll switch
            {
                < 0.7 => "common",
                < 0.9 => "uncommon",
                < 0.98 => "rare",
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

        // Reduce trap durability
        trap.Durability = Math.Max(0, trap.Durability - (int)(hoursDuration * 5)); // 5 durability per hour
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
            "bluegill" => "Bluegill",
            "bass" => "Bass",
            "catfish" => "Catfish",
            "minnow" => "Minnow",
            "sunfish" => "Sunfish",
            "rainbow_trout" => "Rainbow Trout",
            "pike" => "Pike",
            "mysterious_eel" => "Mysterious Eel",
            "ghost_carp" => "Ghost Carp",
            "silver_perch" => "Silver Perch",
            "moonfish" => "Moonfish",
            _ => fishId.Replace("_", " ").ToTitleCase()
        };
    }
}