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
using FishChamp.Providers;

namespace FishChamp.Commands;

[Group("aquarium")]
[Description("Aquarium management commands")]
public class AquariumCommandGroup(IInteractionCommandContext context,
    IPlayerRepository playerRepository, IInventoryRepository inventoryRepository,
    IAquariumRepository aquariumRepository, FeedbackService feedbackService) : CommandGroup
{
    [Command("view")]
    [Description("View your aquarium status and fish")]
    public async Task<IResult> ViewAquariumAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        // Apply degradation when viewing
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var fields = new List<EmbedField>
        {
            new("Fish Count", $"{aquarium.Fish.Count(f => f.IsAlive)}/{aquarium.Capacity}", true),
            new("Temperature", $"{aquarium.Temperature:F1}°C", true),
            new("Cleanliness", $"{aquarium.Cleanliness:F0}%", true)
        };

        // Add maintenance status
        var timeSinceLastFed = DateTime.UtcNow - aquarium.LastFed;
        var timeSinceLastCleaned = DateTime.UtcNow - aquarium.LastCleaned;
        
        var feedingStatus = timeSinceLastFed.TotalHours switch
        {
            < 4 => "🟢 Well Fed",
            < 6 => "🟡 Getting Hungry", 
            < 12 => "🟠 Hungry",
            _ => "🔴 Starving"
        };
        
        var cleanlinessStatus = aquarium.Cleanliness switch
        {
            >= 80 => "🟢 Sparkling Clean",
            >= 60 => "🟡 Mostly Clean",
            >= 40 => "🟠 Getting Dirty",
            >= 20 => "🔴 Very Dirty",
            _ => "⚫ Filthy"
        };

        fields.Add(new("Feeding Status", feedingStatus, true));
        fields.Add(new("Tank Status", cleanlinessStatus, true));

        // Add decorations field if any exist
        if (aquarium.Decorations.Any())
        {
            var decorationText = string.Join(" ", aquarium.Decorations.Select(d => $"{GetDecorationEmoji(d)} {d}"));
            fields.Add(new("🎨 Decorations", decorationText, false));
        }

        string description = "";
        
        // Fish list
        if (aquarium.Fish.Any())
        {
            var fishText = new StringBuilder();
            var livingFish = aquarium.Fish.Where(f => f.IsAlive).Take(15);
            var deadFish = aquarium.Fish.Where(f => !f.IsAlive).Take(5);
            
            foreach (var fish in livingFish)
            {
                var rarityEmoji = GetRarityEmoji(fish.Rarity);
                var healthEmoji = fish.Health > 80 ? "💚" : fish.Health > 50 ? "💛" : "❤️";
                var happinessEmoji = fish.Happiness > 80 ? "😊" : fish.Happiness > 50 ? "😐" : "😢";
                
                fishText.AppendLine($"{rarityEmoji} **{fish.Name}** {healthEmoji}{happinessEmoji}");
            }

            if (deadFish.Any())
            {
                fishText.AppendLine("\n💀 **Deceased:**");
                foreach (var fish in deadFish)
                {
                    fishText.AppendLine($"☠️ {fish.Name}");
                }
            }

            if (aquarium.Fish.Count(f => f.IsAlive) > 15)
            {
                fishText.AppendLine($"... and {aquarium.Fish.Count(f => f.IsAlive) - 15} more living fish");
            }

            fields.Add(new EmbedField("🐟 Fish", fishText.ToString(), false));
        }
        else
        {
            description = "Your aquarium is empty. Use `/aquarium add` to transfer fish from your inventory.";
        }

        var embed = new Embed
        {
            Title = $"🐠 {aquarium.Name}",
            Description = description,
            Colour = Color.Cyan,
            Fields = fields,
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("add")]
    [Description("Add a fish from your inventory to the aquarium")]
    public async Task<IResult> AddFishAsync(
        [Description("Fish type to add")]
        [AutocompleteProvider("autocomplete::aquarium_fish")]
        string fishType)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        var inventory = await inventoryRepository.GetInventoryAsync(user.ID.Value);

        if (inventory == null)
        {
            return await feedbackService.SendContextualErrorAsync("You don't have an inventory yet. Go fishing first!");
        }

        // Check if aquarium has space
        if (aquarium.Fish.Count >= aquarium.Capacity)
        {
            return await feedbackService.SendContextualErrorAsync($"Your aquarium is full! ({aquarium.Fish.Count}/{aquarium.Capacity})");
        }

        // Find the fish in inventory
        var fishItem = inventory.Items.FirstOrDefault(i => i.ItemType == "Fish" && 
            (i.ItemId.Equals(fishType, StringComparison.OrdinalIgnoreCase) || 
             i.Name.Equals(fishType, StringComparison.OrdinalIgnoreCase)));

        if (fishItem == null)
        {
            return await feedbackService.SendContextualErrorAsync($"You don't have any **{fishType}** in your inventory.");
        }

        // Convert inventory item to aquarium fish
        var aquariumFish = AquariumFish.FromInventoryItem(fishItem);
        aquarium.Fish.Add(aquariumFish);

        // Remove from inventory
        await inventoryRepository.RemoveItemAsync(user.ID.Value, fishItem.ItemId, 1);

        // Update aquarium
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🐠 Fish Added to Aquarium!",
            Description = $"**{aquariumFish.Name}** has been added to your aquarium!",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Fish Count", $"{aquarium.Fish.Count}/{aquarium.Capacity}", true),
                new("Rarity", aquariumFish.Rarity, true),
                new("Size", $"{aquariumFish.Size}cm", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("remove")]
    [Description("Remove a fish from your aquarium back to inventory")]
    public async Task<IResult> RemoveFishAsync(
        [Description("Fish name or type to remove")]
        [AutocompleteProvider("autocomplete::aquarium_remove_fish")]
        string fishIdentifier)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var player = await GetOrCreatePlayerAsync(user.ID.Value, user.Username);
        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        if (!aquarium.Fish.Any())
        {
            return await feedbackService.SendContextualErrorAsync("Your aquarium is empty!");
        }

        // Find the fish in aquarium
        var fish = aquarium.Fish.FirstOrDefault(f => 
            f.Name.Equals(fishIdentifier, StringComparison.OrdinalIgnoreCase) ||
            f.FishType.Equals(fishIdentifier, StringComparison.OrdinalIgnoreCase) ||
            f.FishId.Equals(fishIdentifier, StringComparison.OrdinalIgnoreCase));

        if (fish == null)
        {
            return await feedbackService.SendContextualErrorAsync($"No fish matching **{fishIdentifier}** found in your aquarium.");
        }

        // Convert back to inventory item
        var inventoryItem = fish.ToInventoryItem();
        await inventoryRepository.AddItemAsync(user.ID.Value, inventoryItem);

        // Remove from aquarium
        aquarium.Fish.Remove(fish);
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🐠 Fish Removed from Aquarium!",
            Description = $"**{fish.Name}** has been returned to your inventory.",
            Colour = Color.Orange,
            Fields = new List<EmbedField>
            {
                new("Fish Count", $"{aquarium.Fish.Count}/{aquarium.Capacity}", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("clean")]
    [Description("Clean your aquarium to improve cleanliness")]
    public async Task<IResult> CleanAquariumAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        // Cleaning improves cleanliness but takes time and has a cooldown
        var timeSinceLastCleaned = DateTime.UtcNow - aquarium.LastCleaned;
        if (timeSinceLastCleaned.TotalHours < 1)
        {
            var remainingTime = TimeSpan.FromHours(1) - timeSinceLastCleaned;
            return await feedbackService.SendContextualErrorAsync($"You just cleaned your aquarium! Please wait {remainingTime.Minutes} more minutes.");
        }

        var oldCleanliness = aquarium.Cleanliness;
        aquarium.Cleanliness = Math.Min(100, aquarium.Cleanliness + 30);
        aquarium.LastCleaned = DateTime.UtcNow;

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🧽 Aquarium Cleaned!",
            Description = "You've cleaned your aquarium! Your fish look much happier.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Cleanliness", $"{oldCleanliness:F0}% → {aquarium.Cleanliness:F0}%", true),
                new("Next Cleaning", "Available in 1 hour", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("feed")]
    [Description("Feed your fish to keep them happy and healthy")]
    public async Task<IResult> FeedFishAsync()
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        if (!aquarium.Fish.Any())
        {
            return await feedbackService.SendContextualErrorAsync("Your aquarium is empty! Add some fish first.");
        }

        // Feeding has a cooldown
        var timeSinceLastFed = DateTime.UtcNow - aquarium.LastFed;
        if (timeSinceLastFed.TotalHours < 4)
        {
            var remainingTime = TimeSpan.FromHours(4) - timeSinceLastFed;
            return await feedbackService.SendContextualErrorAsync($"Your fish are still full! Please wait {remainingTime.Hours}h {remainingTime.Minutes}m before feeding again.");
        }

        aquarium.LastFed = DateTime.UtcNow;

        // Feeding improves happiness of all living fish
        var fedCount = 0;
        foreach (var fish in aquarium.Fish.Where(f => f.IsAlive))
        {
            fish.Happiness = Math.Min(100, fish.Happiness + 15);
            fedCount++;
        }

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🍽️ Fish Fed!",
            Description = $"You've fed {fedCount} fish! They're swimming happily around the tank.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Fish Fed", fedCount.ToString(), true),
                new("Next Feeding", "Available in 4 hours", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("breed")]
    [Description("Breed two compatible fish to create offspring")]
    public async Task<IResult> BreedFishAsync(
        [Description("First parent fish name")]
        [AutocompleteProvider("autocomplete::aquarium_remove_fish")]
        string parent1Name,
        [Description("Second parent fish name")]
        [AutocompleteProvider("autocomplete::aquarium_remove_fish")]
        string parent2Name)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        if (aquarium.Fish.Count >= aquarium.Capacity)
        {
            return await feedbackService.SendContextualErrorAsync($"Your aquarium is full! ({aquarium.Fish.Count}/{aquarium.Capacity}) Remove some fish first.");
        }

        // Find parent fish
        var parent1 = aquarium.Fish.FirstOrDefault(f => 
            f.FishId.Equals(parent1Name, StringComparison.OrdinalIgnoreCase) && f.IsAlive);
        var parent2 = aquarium.Fish.FirstOrDefault(f => 
            f.FishId.Equals(parent2Name, StringComparison.OrdinalIgnoreCase) && f.IsAlive);

        if (parent1 == null)
        {
            return await feedbackService.SendContextualErrorAsync($"No living fish named **{parent1Name}** found in your aquarium.");
        }

        if (parent2 == null)
        {
            return await feedbackService.SendContextualErrorAsync($"No living fish named **{parent2Name}** found in your aquarium.");
        }

        if (parent1.FishId == parent2.FishId)
        {
            return await feedbackService.SendContextualErrorAsync("A fish cannot breed with itself!");
        }

        // Check breeding eligibility
        var breedingCheck = CheckBreedingCompatibility(parent1, parent2);
        if (!breedingCheck.CanBreed)
        {
            return await feedbackService.SendContextualErrorAsync(breedingCheck.Reason);
        }

        // Create offspring
        var offspring = CreateOffspring(parent1, parent2);
        aquarium.Fish.Add(offspring);

        // Update parent breeding status
        var now = DateTime.UtcNow;
        parent1.LastBred = now;
        parent2.LastBred = now;
        parent1.CanBreed = false;
        parent2.CanBreed = false;

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🐣 Successful Breeding!",
            Description = $"**{parent1.Name}** and **{parent2.Name}** have produced offspring!",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("New Fish", offspring.Name, true),
                new("Rarity", offspring.Rarity, true),
                new("Inherited Traits", offspring.Traits.ToString(), true),
                new("Size", $"{offspring.Size}cm", true),
                new("Weight", $"{offspring.Weight:F1}kg", true),
                new("Fish Count", $"{aquarium.Fish.Count}/{aquarium.Capacity}", true),
                new("Breeding Cooldown", "Parents need 24 hours to recover", false)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private static (bool CanBreed, string Reason) CheckBreedingCompatibility(Data.Models.AquariumFish parent1, Data.Models.AquariumFish parent2)
    {
        // Check if both fish can breed
        if (!parent1.CanBreed)
        {
            var timeSinceLastBred = DateTime.UtcNow - parent1.LastBred;
            if (timeSinceLastBred.TotalHours < 24)
            {
                var remaining = TimeSpan.FromHours(24) - timeSinceLastBred;
                return (false, $"**{parent1.Name}** needs {remaining.Hours}h {remaining.Minutes}m more to recover from last breeding.");
            }
        }

        if (!parent2.CanBreed)
        {
            var timeSinceLastBred = DateTime.UtcNow - parent2.LastBred;
            if (timeSinceLastBred.TotalHours < 24)
            {
                var remaining = TimeSpan.FromHours(24) - timeSinceLastBred;
                return (false, $"**{parent2.Name}** needs {remaining.Hours}h {remaining.Minutes}m more to recover from last breeding.");
            }
        }

        // Check fish health and happiness
        if (parent1.Health < 80 || parent1.Happiness < 70)
        {
            return (false, $"**{parent1.Name}** is not healthy or happy enough to breed (needs 80+ health, 70+ happiness).");
        }

        if (parent2.Health < 80 || parent2.Happiness < 70)
        {
            return (false, $"**{parent2.Name}** is not healthy or happy enough to breed (needs 80+ health, 70+ happiness).");
        }

        // Fish of the same type are more compatible
        if (parent1.FishType != parent2.FishType)
        {
            // Different species have lower compatibility
            var random = new Random();
            if (random.NextDouble() < 0.3) // 30% failure rate for cross-breeding
            {
                return (false, "These fish species are not compatible for breeding.");
            }
        }

        return (true, "");
    }

    private static Data.Models.AquariumFish CreateOffspring(Data.Models.AquariumFish parent1, Data.Models.AquariumFish parent2)
    {
        var random = new Random();
        
        // Choose which parent's species the offspring will be
        var isPrimarySpecies = random.NextDouble() < 0.7; // 70% chance to be parent1's species
        var baseSpecies = isPrimarySpecies ? parent1 : parent2;
        var otherParent = isPrimarySpecies ? parent2 : parent1;
        
        // Inherit traits from both parents
        var inheritedTraits = Data.Models.FishTrait.None;
        
        // Each parent has a chance to pass on their traits
        if (random.NextDouble() < 0.6) // 60% chance to inherit from parent1
        {
            inheritedTraits |= parent1.Traits;
        }
        if (random.NextDouble() < 0.6) // 60% chance to inherit from parent2
        {
            inheritedTraits |= parent2.Traits;
        }
        
        // Rare chance for mutation (new trait)
        if (random.NextDouble() < 0.1) // 10% chance for mutation
        {
            var possibleTraits = new[] { Data.Models.FishTrait.Evasive, Data.Models.FishTrait.Slippery, Data.Models.FishTrait.Magnetic, Data.Models.FishTrait.Camouflage };
            inheritedTraits |= possibleTraits[random.Next(possibleTraits.Length)];
        }
        
        // Determine offspring rarity (chance for improvement)
        var parentRarities = new[] { parent1.Rarity, parent2.Rarity };
        var rarityHierarchy = new[] { "common", "uncommon", "rare", "epic", "legendary", "mythic" };
        
        var maxParentRarityIndex = parentRarities.Max(r => Array.IndexOf(rarityHierarchy, r));
        var offspringRarityIndex = maxParentRarityIndex;
        
        // Small chance to improve rarity
        if (random.NextDouble() < 0.15 && maxParentRarityIndex < rarityHierarchy.Length - 1) // 15% chance to improve
        {
            offspringRarityIndex++;
        }
        
        var offspringRarity = rarityHierarchy[offspringRarityIndex];
        
        // Size and weight are averages with some variation
        var avgSize = (parent1.Size + parent2.Size) / 2.0;
        var avgWeight = (parent1.Weight + parent2.Weight) / 2.0;
        
        var sizeVariation = random.NextDouble() * 0.4 - 0.2; // ±20% variation
        var weightVariation = random.NextDouble() * 0.4 - 0.2; // ±20% variation
        
        var offspringSize = Math.Max(1, (int)(avgSize * (1 + sizeVariation)));
        var offspringWeight = Math.Max(0.1, avgWeight * (1 + weightVariation));
        
        // Generate name
        var offspringName = GenerateOffspringName(baseSpecies.Name, otherParent.Name);
        
        return new Data.Models.AquariumFish
        {
            FishId = Guid.NewGuid().ToString(),
            FishType = baseSpecies.FishType,
            Name = offspringName,
            Rarity = offspringRarity,
            Size = offspringSize,
            Weight = offspringWeight,
            Traits = inheritedTraits,
            Happiness = 100.0, // Babies start happy
            Health = 100.0,    // Babies start healthy
            IsAlive = true,
            CanBreed = true,
            LastBred = DateTime.MinValue,
            Properties = new Dictionary<string, object>
            {
                ["parent1"] = parent1.Name,
                ["parent2"] = parent2.Name,
                ["generation"] = Math.Max(
                    parent1.Properties.GetInt("generation", 0),
                    parent2.Properties.GetInt("generation", 0)
                ) + 1
            }
        };
    }

    private static string GenerateOffspringName(string parent1Name, string parent2Name)
    {
        var random = new Random();
        
        // Simple name generation: take parts from both parents
        var names = new[]
        {
            $"Baby {parent1Name}",
            $"Little {parent2Name}",
            $"{parent1Name} Jr.",
            $"{parent2Name} II",
            "Offspring",
            "Fry", // Young fish
            "Fingerling"
        };
        
        return names[random.Next(names.Length)];
    }

    [Command("decorate")]
    [Description("Add decorations to your aquarium to improve fish mood")]
    public async Task<IResult> DecorateAquariumAsync(
        [Description("Decoration type (plant, pebbles, statue, coral, cave)")]
        string decorationType)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        // Validate decoration type
        var validDecorations = new[] { "plant", "pebbles", "statue", "coral", "cave" };
        if (!validDecorations.Contains(decorationType.ToLower()))
        {
            return await feedbackService.SendContextualErrorAsync($"Invalid decoration type! Valid types: {string.Join(", ", validDecorations)}");
        }
        
        decorationType = decorationType.ToLower(); // Normalize

        // Check if decoration already exists
        if (aquarium.Decorations.Contains(decorationType))
        {
            return await feedbackService.SendContextualErrorAsync($"You already have **{decorationType}** in your aquarium!");
        }

        // Limit number of decorations
        if (aquarium.Decorations.Count >= 5)
        {
            return await feedbackService.SendContextualErrorAsync("Your aquarium can only hold 5 decorations! Remove some first.");
        }

        // Add decoration
        aquarium.Decorations.Add(decorationType);

        // Apply immediate happiness bonus to all living fish
        var decorationBonus = GetDecorationBonus(decorationType);
        foreach (var fish in aquarium.Fish.Where(f => f.IsAlive))
        {
            fish.Happiness = Math.Min(100, fish.Happiness + decorationBonus);
        }

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🎨 Decoration Added!",
            Description = $"You've added **{decorationType}** to your aquarium! Your fish look happier already.",
            Colour = Color.Green,
            Fields = new List<EmbedField>
            {
                new("Decoration", decorationType, true),
                new("Happiness Bonus", $"+{decorationBonus}", true),
                new("Total Decorations", $"{aquarium.Decorations.Count}/5", true),
                new("Current Decorations", string.Join(", ", aquarium.Decorations), false)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("remove-decoration")]
    [Description("Remove a decoration from your aquarium")]
    public async Task<IResult> RemoveDecorationAsync(
        [Description("Decoration to remove")]
        string decorationType)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);

        if (!aquarium.Decorations.Contains(decorationType))
        {
            return await feedbackService.SendContextualErrorAsync($"You don't have **{decorationType}** in your aquarium.");
        }

        aquarium.Decorations.Remove(decorationType);
        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var embed = new Embed
        {
            Title = "🗑️ Decoration Removed",
            Description = $"**{decorationType}** has been removed from your aquarium.",
            Colour = Color.Orange,
            Fields = new List<EmbedField>
            {
                new("Total Decorations", $"{aquarium.Decorations.Count}/5", true),
                new("Remaining Decorations", aquarium.Decorations.Any() ? string.Join(", ", aquarium.Decorations) : "None", false)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    private static int GetDecorationBonus(string decorationType)
    {
        return decorationType.ToLower() switch
        {
            "plant" => 5,
            "pebbles" => 3,
            "statue" => 8,
            "coral" => 7,
            "cave" => 6,
            _ => 2
        };
    }

    private static string GetDecorationEmoji(string decorationType)
    {
        return decorationType.ToLower() switch
        {
            "plant" => "🌱",
            "pebbles" => "🪨",
            "statue" => "🗿",
            "coral" => "🪸",
            "cave" => "🕳️",
            _ => "🎨"
        };
    }

    [Command("temperature")]
    [Description("Adjust the aquarium temperature")]
    public async Task<IResult> AdjustTemperatureAsync(
        [Description("Target temperature (15-30°C)")]
        double targetTemperature)
    {
        if (!(context.Interaction.Member.TryGet(out var member) && member.User.TryGet(out var user)))
        {
            return Result.FromError(new NotFoundError("Failed to get user"));
        }

        if (targetTemperature < 15 || targetTemperature > 30)
        {
            return await feedbackService.SendContextualErrorAsync("Temperature must be between 15°C and 30°C.");
        }

        var aquarium = await GetOrCreateAquariumAsync(user.ID.Value);
        Services.AquariumMaintenanceService.ApplyDegradation(aquarium);

        var oldTemperature = aquarium.Temperature;
        aquarium.Temperature = targetTemperature;

        await aquariumRepository.UpdateAquariumAsync(aquarium);

        var temperatureStatus = targetTemperature switch
        {
            >= 20 and <= 25 => "🌡️ Perfect temperature range!",
            >= 18 and < 20 => "❄️ A bit cool, but acceptable",
            > 25 and <= 28 => "🔥 A bit warm, but acceptable", 
            _ => "⚠️ Extreme temperature - your fish may become stressed!"
        };

        var embed = new Embed
        {
            Title = "🌡️ Temperature Adjusted!",
            Description = temperatureStatus,
            Colour = targetTemperature >= 20 && targetTemperature <= 25 ? Color.Green : Color.Orange,
            Fields = new List<EmbedField>
            {
                new("Temperature", $"{oldTemperature:F1}°C → {targetTemperature:F1}°C", true),
                new("Optimal Range", "20°C - 25°C", true)
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        return await feedbackService.SendContextualEmbedAsync(embed);
    }

    [Command("help")]
    [Description("Show aquarium system help and commands")]
    public async Task<IResult> HelpAsync()
    {
        var embed = new Embed
        {
            Title = "🐠 Aquarium System Help",
            Description = "Manage your personal fish tank with these commands:",
            Colour = Color.Blue,
            Fields = new List<EmbedField>
            {
                new("📋 Basic Commands", 
                    "`/aquarium view` - View your aquarium and fish\n" +
                    "`/aquarium add <fish>` - Add a fish from inventory\n" +
                    "`/aquarium remove <fish>` - Remove a fish to inventory\n" +
                    "`/aquarium help` - Show this help message", false),
                new("🔧 Maintenance Commands",
                    "`/aquarium clean` - Clean the tank (1 hour cooldown)\n" +
                    "`/aquarium feed` - Feed your fish (4 hour cooldown)\n" +
                    "`/aquarium temperature <temp>` - Adjust temperature (15-30°C)", false),
                new("🐣 Breeding Commands",
                    "`/aquarium breed <parent1> <parent2>` - Breed two compatible fish", false),
                new("🎨 Decoration Commands",
                    "`/aquarium decorate <type>` - Add decorations (plant, pebbles, statue, coral, cave)\n" +
                    "`/aquarium remove-decoration <type>` - Remove a decoration", false),
                new("🏠 Tank Features",
                    "• **Capacity**: Start with 10 fish slots\n" +
                    "• **Health**: Fish health and happiness tracking\n" +
                    "• **Environment**: Temperature and cleanliness monitoring\n" +
                    "• **Maintenance**: Keep your fish happy with regular care!\n" +
                    "• **Decorations**: Add up to 5 decorations for mood bonuses", false),
                new("💡 Tips",
                    "• Feed fish every 4-6 hours to keep them happy\n" +
                    "• Clean the tank when cleanliness drops below 50%\n" +
                    "• Keep temperature between 20-25°C for optimal health\n" +
                    "• Fish need 80+ health and 70+ happiness to breed\n" +
                    "• Same species breed more successfully than different species\n" +
                    "• Decorations provide permanent happiness bonuses\n" +
                    "• Offspring inherit traits from both parents!", false)
            },
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

    private async Task<Aquarium> GetOrCreateAquariumAsync(ulong userId)
    {
        var aquarium = await aquariumRepository.GetAquariumAsync(userId);
        if (aquarium == null)
        {
            aquarium = await aquariumRepository.CreateAquariumAsync(userId);
        }
        return aquarium;
    }

    private static string GetRarityEmoji(string rarity)
    {
        return rarity.ToLower() switch
        {
            "common" => "⚪",
            "uncommon" => "🟢",
            "rare" => "🔵", 
            "epic" => "🟣",
            "legendary" => "🟡",
            "mythic" => "🔴",
            _ => "⚪"
        };
    }
}