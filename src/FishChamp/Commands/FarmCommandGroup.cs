using System.ComponentModel;
using System.Drawing;
using System.Text;
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

[Group("farm")]
[Description("Farming system commands")]
public class FarmCommandGroup(IInteractionCommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAreaRepository areaRepository, IFarmRepository farmRepository,
    FeedbackService feedbackService) : CommandGroup
{
    [Command("view")]
    [Description("View your current farm plots and crops")]
    public async Task<IResult> ViewFarmAsync()
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

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await feedbackService.SendContextualErrorAsync("Current area not found.");
        }

        if (currentArea.FarmSpots.Count == 0)
        {
            return await feedbackService.SendContextualErrorAsync($"No farm spots available in {currentArea.Name}.");
        }

        var userFarms = await farmRepository.GetUserFarmsAsync(user.ID.Value);
        var farmsInCurrentArea = userFarms.Where(f => f.AreaId == player.CurrentArea).ToList();

        var description = new StringBuilder();
        description.AppendLine($"**Available Farm Spots:** {currentArea.FarmSpots.Count}");
        description.AppendLine($"**Your Active Farms:** {farmsInCurrentArea.Count}");

        var fields = new List<EmbedField>();

        foreach (var farmSpot in currentArea.FarmSpots)
        {
            var userFarm = farmsInCurrentArea.FirstOrDefault(f => f.FarmSpotId == farmSpot.SpotId);
            var fieldValue = new StringBuilder();

            if (userFarm == null)
            {
                fieldValue.AppendLine("*Untilled - Use `/farm plant` to start*");
            }
            else
            {
                if (userFarm.Crops.Count == 0)
                {
                    fieldValue.AppendLine("*Empty plot - Use `/farm plant` to plant seeds*");
                }
                else
                {
                    foreach (var crop in userFarm.Crops)
                    {
                        var timeLeft = crop.ReadyAt - DateTime.UtcNow;
                        var status = crop.Stage switch
                        {
                            CropStage.Ready => "🌾 **Ready to harvest!**",
                            CropStage.Growing when timeLeft.TotalMinutes <= 0 => "🌾 **Ready to harvest!**",
                            CropStage.Growing => $"🌱 Growing ({timeLeft.Hours}h {timeLeft.Minutes}m left)",
                            _ => "🌱 Recently planted"
                        };
                        fieldValue.AppendLine($"• {crop.Name}: {status}");
                    }
                }
            }

            if (farmSpot.CanDigForWorms)
            {
                fieldValue.AppendLine("🪱 *Can dig for worms here*");
            }

            fields.Add(new EmbedField($"📍 {farmSpot.Name}", fieldValue.ToString(), true));
        }

        var embed = new Embed
        {
            Title = $"🌱 {currentArea.Name} - Farm Status",
            Description = description.ToString(),
            Colour = Color.Green,
            Fields = fields
        };
        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("plant")]
    [Description("Plant seeds at a farm spot")]
    public async Task<IResult> PlantSeedsAsync(
        [Description("Farm spot to plant at")] 
        [AutocompleteProvider("autocomplete::farm_spot")] 
        string farmSpotId,
        [Description("Type of seed to plant")] 
        [AutocompleteProvider("autocomplete::seed_type")] 
        string seedType,
        [Description("Number of seeds to plant")] int quantity = 1)
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

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await feedbackService.SendContextualErrorAsync("Current area not found.");
        }

        var farmSpot = currentArea.FarmSpots.FirstOrDefault(fs => fs.SpotId == farmSpotId);
        if (farmSpot == null)
        {
            return await feedbackService.SendContextualErrorAsync($"Farm spot '{farmSpotId}' not found in {currentArea.Name}.");
        }

        if (quantity <= 0 || quantity > 10)
        {
            return await feedbackService.SendContextualErrorAsync("You can plant 1-10 seeds at a time.");
        }

        // Check if player has the seeds
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);
        if (inventory == null)
        {
            inventory = await inventoryRepository.CreateInventoryAsync(user.ID.Value);
        }

        var seedItem = inventory.Items.FirstOrDefault(i => i.ItemType == "Seed" && i.ItemId == seedType);
        if (seedItem == null || seedItem.Quantity < quantity)
        {
            return await feedbackService.SendContextualErrorAsync($"You don't have enough {seedType} seeds. You need {quantity} but have {seedItem?.Quantity ?? 0}.");
        }

        // Get or create farm for this spot
        var userFarm = await farmRepository.GetFarmAsync(user.ID.Value, player.CurrentArea, farmSpotId);
        if (userFarm == null)
        {
            userFarm = new Farm
            {
                UserId = user.ID.Value,
                AreaId = player.CurrentArea,
                FarmSpotId = farmSpotId
            };
            userFarm = await farmRepository.CreateFarmAsync(userFarm);
        }

        // Define seed types and their properties
        var seedInfo = GetSeedInfo(seedType);
        if (seedInfo == null)
        {
            return await feedbackService.SendContextualErrorAsync($"Unknown seed type: {seedType}");
        }

        // Check if farm spot supports this crop type
        if (!farmSpot.AvailableCrops.Contains(seedType) && !farmSpot.AvailableCrops.Contains("any"))
        {
            return await feedbackService.SendContextualErrorAsync($"This farm spot doesn't support {seedType}. Available crops: {string.Join(", ", farmSpot.AvailableCrops)}");
        }

        // Plant the crops
        for (int i = 0; i < quantity; i++)
        {
            var crop = new Crop
            {
                SeedType = seedType,
                Name = seedInfo.Name,
                Stage = CropStage.Planted,
                PlantedAt = DateTime.UtcNow,
                GrowthTimeHours = seedInfo.GrowthTimeHours,
                ReadyAt = DateTime.UtcNow.AddHours(seedInfo.GrowthTimeHours)
            };
            userFarm.Crops.Add(crop);
        }

        userFarm.LastUpdated = DateTime.UtcNow;
        await farmRepository.UpdateFarmAsync(userFarm);

        // Remove seeds from inventory
        await inventoryRepository.RemoveItemAsync(user.ID.Value, seedType, quantity);

        var embed = new Embed
        {
            Title = "🌱 Seeds Planted!",
            Description = $"Successfully planted {quantity} {seedInfo.Name} seed(s) at {farmSpot.Name}.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Growth Time", $"{seedInfo.GrowthTimeHours} hours", true),
                new("Ready At", $"<t:{((DateTimeOffset)DateTime.UtcNow.AddHours(seedInfo.GrowthTimeHours)).ToUnixTimeSeconds()}:R>", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("harvest")]
    [Description("Harvest ready crops")]
    public async Task<IResult> HarvestCropsAsync(
        [Description("Farm spot to harvest from")] 
        [AutocompleteProvider("autocomplete::farm_spot")] 
        string farmSpotId)
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

        var userFarm = await farmRepository.GetFarmAsync(user.ID.Value, player.CurrentArea, farmSpotId);
        if (userFarm == null || userFarm.Crops.Count == 0)
        {
            return await feedbackService.SendContextualErrorAsync($"No crops planted at {farmSpotId}.");
        }

        var readyCrops = userFarm.Crops.Where(c => c.Stage != CropStage.Harvested && DateTime.UtcNow >= c.ReadyAt).ToList();
        if (readyCrops.Count == 0)
        {
            return await feedbackService.SendContextualErrorAsync("No crops are ready for harvest yet.");
        }

        var harvestResults = new List<InventoryItem>();
        var random = new Random();

        foreach (var crop in readyCrops)
        {
            crop.Stage = CropStage.Harvested;
            
            var seedInfo = GetSeedInfo(crop.SeedType);
            if (seedInfo != null)
            {
                // Determine harvest yield (1-3 items typically)
                var yieldCount = random.Next(1, 4);
                var harvestItem = seedInfo.PossibleHarvests[random.Next(seedInfo.PossibleHarvests.Count)];

                var item = new InventoryItem
                {
                    ItemId = harvestItem,
                    ItemType = "Crop",
                    Name = harvestItem.Replace("_", " ").ToTitleCase(),
                    Quantity = yieldCount
                };

                var existingHarvest = harvestResults.FirstOrDefault(h => h.ItemId == harvestItem);
                if (existingHarvest != null)
                {
                    existingHarvest.Quantity += yieldCount;
                }
                else
                {
                    harvestResults.Add(item);
                }
            }
        }

        // Add harvested items to inventory
        foreach (var item in harvestResults)
        {
            await inventoryRepository.AddItemAsync(user.ID.Value, item);
        }

        // Remove harvested crops
        userFarm.Crops.RemoveAll(c => c.Stage == CropStage.Harvested);
        userFarm.LastUpdated = DateTime.UtcNow;
        await farmRepository.UpdateFarmAsync(userFarm);

        var embed = new Embed
        {
            Title = "🌾 Harvest Complete!",
            Description = $"Successfully harvested {readyCrops.Count} crop(s) at {farmSpotId}.",
            Colour = Color.Orange,
            Fields = harvestResults.Select(h => new EmbedField("Harvested", $"{h.Quantity}x {h.Name}", true)).ToList(),
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("dig")]
    [Description("Dig for worms and other treasures")]
    public async Task<IResult> DigForWormsAsync(
        [Description("Farm spot to dig at")] 
        [AutocompleteProvider("autocomplete::farm_spot")] 
        string farmSpotId)
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

        var currentArea = await areaRepository.GetAreaAsync(player.CurrentArea);
        if (currentArea == null)
        {
            return await feedbackService.SendContextualErrorAsync("Current area not found.");
        }

        var farmSpot = currentArea.FarmSpots.FirstOrDefault(fs => fs.SpotId == farmSpotId);
        if (farmSpot == null)
        {
            return await feedbackService.SendContextualErrorAsync($"Farm spot '{farmSpotId}' not found in {currentArea.Name}.");
        }

        if (!farmSpot.CanDigForWorms)
        {
            return await feedbackService.SendContextualErrorAsync($"Cannot dig for worms at {farmSpot.Name}.");
        }

        var random = new Random();
        var digResults = new List<InventoryItem>();

        // Simple dig results - could be expanded into a mini-game
        var success = random.NextDouble() < 0.7; // 70% success rate

        if (success)
        {
            // Determine what was found
            var findRoll = random.NextDouble();
            if (findRoll < 0.4) // 40% - common worms
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "worm_bait",
                    ItemType = "Bait",
                    Name = "Worm Bait",
                    Quantity = random.Next(1, 4),
                    Properties = new() { ["attraction"] = 1.1 }
                });
            }
            else if (findRoll < 0.7) // 30% - quality worms
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "quality_worms",
                    ItemType = "Bait",
                    Name = "Quality Worms",
                    Quantity = random.Next(1, 3),
                    Properties = new() { ["attraction"] = 1.3 }
                });
            }
            else if (findRoll < 0.9) // 20% - grubs
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "grub_bait",
                    ItemType = "Bait",
                    Name = "Grub Bait",
                    Quantity = random.Next(1, 2),
                    Properties = new() { ["attraction"] = 1.4 }
                });
            }
            else // 10% - rare find
            {
                digResults.Add(new InventoryItem
                {
                    ItemId = "magical_larvae",
                    ItemType = "Bait",
                    Name = "Magical Larvae",
                    Quantity = 1,
                    Properties = new() { ["attraction"] = 1.8, ["rare_bonus"] = true }
                });
            }
        }

        if (digResults.Count > 0)
        {
            foreach (var item in digResults)
            {
                await inventoryRepository.AddItemAsync(user.ID.Value, item);
            }

            var embed = new Embed
            {
                Title = "🪱 Digging Success!",
                Description = $"You found something while digging at {farmSpot.Name}!",
                Colour = Color.Brown,
                Fields = digResults.Select(item => new EmbedField("Found", $"{item.Quantity}x {item.Name}", true)).ToList(),
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }
        else
        {
            var embed = new Embed
            {
                Title = "🪱 No Luck",
                Description = $"You dug around at {farmSpot.Name} but didn't find anything useful this time.",
                Colour = Color.Gray,
                Timestamp = DateTimeOffset.UtcNow
            };

            return await feedbackService.SendContextualEmbedAsync(embed);
        }
    }

    private static SeedType? GetSeedInfo(string seedType)
    {
        return seedType switch
        {
            "corn_seeds" => new SeedType
            {
                SeedId = "corn_seeds",
                Name = "Corn",
                GrowthTimeHours = 4,
                PossibleHarvests = ["corn", "sweet_corn"],
                Rarity = "common"
            },
            "tomato_seeds" => new SeedType
            {
                SeedId = "tomato_seeds",
                Name = "Tomato",
                GrowthTimeHours = 6,
                PossibleHarvests = ["tomato", "cherry_tomato"],
                Rarity = "common"
            },
            "algae_spores" => new SeedType
            {
                SeedId = "algae_spores",
                Name = "Algae",
                GrowthTimeHours = 2,
                PossibleHarvests = ["algae", "premium_algae"],
                Rarity = "uncommon"
            },
            "herb_seeds" => new SeedType
            {
                SeedId = "herb_seeds",
                Name = "Spice Herbs",
                GrowthTimeHours = 8,
                PossibleHarvests = ["spice_herbs", "rare_herbs"],
                Rarity = "uncommon"
            },
            _ => null
        };
    }
}