using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Responders;
using Remora.Results;
using System.Drawing;
using ZPT.Models;

namespace ZPT.Services;

public class MessageResponder : IResponder<IMessageCreate>
{
    private readonly ILogger<MessageResponder> _logger;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly UserManagerService _userManager;
    private readonly GameDataService _gameData;
    private readonly GameLogicService _gameLogic;

    public MessageResponder(
        ILogger<MessageResponder> logger,
        IDiscordRestChannelAPI channelAPI,
        UserManagerService userManager,
        GameDataService gameData,
        GameLogicService gameLogic)
    {
        _logger = logger;
        _channelAPI = channelAPI;
        _userManager = userManager;
        _gameData = gameData;
        _gameLogic = gameLogic;
    }

    public async Task<Result> RespondAsync(IMessageCreate gatewayEvent, CancellationToken ct = default)
    {
        var message = gatewayEvent;
        
        // Ignore bot messages
        if (message.Author.IsBot.HasValue && message.Author.IsBot.Value)
            return Result.FromSuccess();

        var content = message.Content.Trim();
        
        // Check for commands
        if (content.StartsWith("!") || content.StartsWith("/"))
        {
            var parts = content.Substring(1).ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                await HandleCommand(message, parts[0], parts.Skip(1).ToArray(), ct);
            }
        }

        return Result.FromSuccess();
    }

    private async Task HandleCommand(IMessageCreate message, string command, string[] args, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Handling command: {Command} from user {UserId}", command, message.Author.ID);

            var response = command switch
            {
                "start" or "play" => await HandleStartCommand(message.Author.ID.Value),
                "fish" => await HandleFishCommand(message.Author.ID.Value),
                "inventory" or "inv" => await HandleInventoryCommand(message.Author.ID.Value),
                "shop" => await HandleShopCommand(message.Author.ID.Value),
                "move" => await HandleMoveCommand(message.Author.ID.Value),
                "moveto" => await HandleMoveToCommand(message.Author.ID.Value, args),
                "sell" => await HandleSellCommand(message.Author.ID.Value, args),
                "buy" => await HandleBuyCommand(message.Author.ID.Value, args),
                "dig" => await HandleDigCommand(message.Author.ID.Value),
                "traps" => await HandleTrapsCommand(message.Author.ID.Value, args),
                "aquarium" => await HandleAquariumCommand(message.Author.ID.Value, args),
                "areas" => await HandleAreasCommand(),
                "help" => await HandleHelpCommand(),
                _ => "Unknown command. Type `!help` for a list of available commands."
            };

            // Create embed response
            var embed = new Embed(
                Description: response,
                Colour: Color.CornflowerBlue
            );

            await _channelAPI.CreateMessageAsync(
                message.ChannelID,
                embeds: new[] { embed },
                ct: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command: {Command}", command);
            
            await _channelAPI.CreateMessageAsync(
                message.ChannelID,
                "Sorry, there was an error processing your command. Please try again.",
                ct: ct
            );
        }
    }

    private async Task<string> HandleStartCommand(ulong userId)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        var isNewUser = userProfile.CreatedAt > DateTime.UtcNow.AddMinutes(-5);

        if (isNewUser)
        {
            return "🎣 **Welcome to the Fishing Adventure!**\n\n" +
                   "You've started your fishing journey! You begin at the Lake with basic equipment.\n\n" +
                   $"**Your Stats:**\n" +
                   $"📍 Area: {userProfile.Area}\n" +
                   $"⭐ Level: {userProfile.Level}\n" +
                   $"🪙 Gold: {userProfile.Gold}\n" +
                   $"🎣 Rod: {userProfile.EquippedRod}\n" +
                   $"🪱 Bait: {userProfile.EquippedBait}\n\n" +
                   "**Available Commands:**\n" +
                   "`!fish` - Start fishing\n" +
                   "`!inventory` - View your items\n" +
                   "`!shop` - Visit the shop\n" +
                   "`!move` - Change areas\n" +
                   "`!help` - Show all commands";
        }
        else
        {
            return "🎣 **Welcome back to your fishing adventure!**\n\n" +
                   $"**Your Stats:**\n" +
                   $"📍 Area: {userProfile.Area}\n" +
                   $"⭐ Level: {userProfile.Level} (XP: {userProfile.Experience})\n" +
                   $"🪙 Gold: {userProfile.Gold}\n" +
                   $"🎣 Rod: {userProfile.EquippedRod}\n" +
                   $"🪱 Bait: {userProfile.EquippedBait}\n\n" +
                   "Type `!fish` to start fishing or `!help` for all commands.";
        }
    }

    private async Task<string> HandleFishCommand(ulong userId)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        
        // Check if user has equipped bait
        if (string.IsNullOrEmpty(userProfile.EquippedBait))
        {
            return "❌ You need to equip some bait first! You start with basic Worms.";
        }

        // Check if user has bait in inventory and auto-switch if needed
        if (string.IsNullOrEmpty(userProfile.EquippedBait) || 
            !userProfile.Inventory.ContainsKey(userProfile.EquippedBait) || 
            userProfile.Inventory[userProfile.EquippedBait] <= 0)
        {
            // Try to auto-switch to available bait
            var availableBait = userProfile.Inventory.Where(i => 
                _gameData.GetBaitByName(i.Key) != null && i.Value > 0)
                .FirstOrDefault();
            
            if (availableBait.Key != null)
            {
                userProfile.EquippedBait = availableBait.Key;
                await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
                // Continue with fishing using the new bait
            }
            else
            {
                return $"❌ You don't have any bait left! \n\n" +
                       "💡 **Options:**\n" +
                       "• Use `!dig` to search for free worms\n" +
                       "• Use `!buy worms` to purchase bait\n" +
                       "• Visit `!shop` to see all available bait";
            }
        }

        // Get current area
        var currentArea = _gameData.GetArea(userProfile.Area);
        if (currentArea == null)
        {
            return "❌ Invalid area. Use `!move` to select a valid fishing area.";
        }

        // Simple fishing logic - consume bait and attempt to catch a fish
        userProfile.Inventory[userProfile.EquippedBait]--;
        
        // Get a random fish from the current area
        var random = new Random();
        var availableFish = currentArea.Fish;
        var selectedFishName = availableFish[random.Next(availableFish.Count)];
        var fishData = _gameData.GetFishByName(selectedFishName);
        
        if (fishData == null)
        {
            return "❌ Something went wrong with fishing. Please try again.";
        }

        // Calculate success chance based on rod and area difficulty
        var rod = _gameData.GetRodByName(userProfile.EquippedRod);
        var baseSuccess = rod?.SuccessRate ?? 0.5;
        var difficultyPenalty = (currentArea.Difficulty - 1) * 0.1;
        var finalChance = Math.Max(0.1, baseSuccess - difficultyPenalty);
        
        // Check if fish requires special ability
        var requiresSpecialAbility = !string.IsNullOrEmpty(fishData.SpecialEffect);
        var hasCorrectAbility = rod != null && !string.IsNullOrEmpty(rod.SpecialAbility) && 
                               rod.SpecialAbility.Equals(fishData.SpecialEffect, StringComparison.OrdinalIgnoreCase);
        
        if (requiresSpecialAbility && !hasCorrectAbility)
        {
            // Fish that requires special ability but player doesn't have the right rod
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
            
            return $"🎣 **Fish Spotted!** A {selectedFishName} is on your line!\n\n" +
                   $"❌ **Special Ability Required!** This fish needs a rod with **{fishData.SpecialEffect}** ability to catch.\n\n" +
                   $"🛍️ Visit the shop to get the right equipment:\n" +
                   $"• Ice Rod (freeze ability)\n" +
                   $"• Lightning Rod (shock ability)\n" +
                   $"• Mystic Rod (charm ability)\n\n" +
                   $"🪱 {userProfile.EquippedBait} remaining: {userProfile.Inventory[userProfile.EquippedBait]}";
        }
        
        bool success;
        string specialStageMessage = "";
        
        if (requiresSpecialAbility && hasCorrectAbility)
        {
            // Multi-stage fishing for special fish
            specialStageMessage = $"⚡ **Using {rod.SpecialAbility} ability!** ";
            switch (fishData.SpecialEffect?.ToLower())
            {
                case "freeze":
                    specialStageMessage += "The fish is frozen solid, making it easier to catch!";
                    break;
                case "shock":
                    specialStageMessage += "Electric shock stuns the fish!";
                    break;
                case "charm":
                    specialStageMessage += "Mystical charm calms the fish!";
                    break;
            }
            specialStageMessage += "\n\n";
            
            // Higher success rate with correct ability
            finalChance = Math.Min(0.9, finalChance + 0.3);
        }
        
        success = random.NextDouble() < finalChance;
        
        if (success)
        {
            // Add fish to inventory
            if (!userProfile.Fish.ContainsKey(selectedFishName))
                userProfile.Fish[selectedFishName] = 0;
            userProfile.Fish[selectedFishName]++;
            
            // Award experience
            var expGain = fishData.BaseValue;
            userProfile.Experience += expGain;
            
            // Check for level up
            var newLevel = CalculateLevel(userProfile.Experience);
            var leveledUp = newLevel > userProfile.Level;
            userProfile.Level = newLevel;
            
            // Save profile
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
            
            var result = $"🎣 **Success!** You caught a {selectedFishName}!\n\n" +
                        specialStageMessage +
                        $"📊 **Fish Stats:**\n" +
                        $"🏆 Rarity: {fishData.Rarity}\n" +
                        $"⚖️ Weight: {fishData.Weight}kg\n" +
                        $"💰 Value: {fishData.BaseValue} gold\n";
            
            if (!string.IsNullOrEmpty(fishData.SpecialEffect))
            {
                result += $"⚡ Special: {fishData.SpecialEffect}\n";
            }
            
            result += $"✨ XP Gained: +{expGain}\n\n" +
                     $"📈 **Your Progress:**\n" +
                     $"⭐ Level: {userProfile.Level} (XP: {userProfile.Experience})\n" +
                     $"🪱 {userProfile.EquippedBait} remaining: {userProfile.Inventory[userProfile.EquippedBait]}";
            
            if (leveledUp)
            {
                result += $"\n\n🎉 **LEVEL UP!** You are now level {userProfile.Level}!";
            }
            
            return result;
        }
        else
        {
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
            
            return $"🎣 **The fish got away!**\n\n" +
                   $"Better luck next time. You still used your bait.\n" +
                   $"🪱 {userProfile.EquippedBait} remaining: {userProfile.Inventory[userProfile.EquippedBait]}\n\n" +
                   "💡 Try upgrading your rod at the shop for better success rates!";
        }
    }

    private async Task<string> HandleInventoryCommand(ulong userId)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());

        var result = $"🎒 **Your Inventory**\n\n" +
                    $"💰 **Gold:** {userProfile.Gold} 🪙\n" +
                    $"⭐ **Level:** {userProfile.Level} (XP: {userProfile.Experience})\n\n";

        // Equipment
        result += "🛠️ **Equipment:**\n";
        if (userProfile.Inventory.Any())
        {
            foreach (var item in userProfile.Inventory.OrderBy(x => x.Key))
            {
                result += $"  {item.Key}: {item.Value}\n";
            }
        }
        else
        {
            result += "  No items\n";
        }

        result += "\n🐟 **Fish Collection:**\n";
        if (userProfile.Fish.Any())
        {
            foreach (var fish in userProfile.Fish.OrderBy(x => x.Key))
            {
                result += $"  {fish.Key}: {fish.Value}\n";
            }
        }
        else
        {
            result += "  No fish caught yet\n";
        }

        result += $"\n⚙️ **Currently Equipped:**\n" +
                 $"🎣 Rod: {userProfile.EquippedRod}\n" +
                 $"🪱 Bait: {userProfile.EquippedBait}";

        return result;
    }

    private async Task<string> HandleShopCommand(ulong userId)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());

        return $"🏪 **Fishing Shop**\n\n" +
               $"💰 You have: {userProfile.Gold} 🪙\n\n" +
               "🎣 **Fishing Rods:**\n" +
               "• Steel Rod: 150 🪙 (Level 2+, 60% success)\n" +
               "• Pro Rod: 300 🪙 (Level 3+, 70% success)\n" +
               "• Ice Rod: 450 🪙 (Level 4+, 70% + freeze ability)\n" +
               "• Lightning Rod: 600 🪙 (Level 5+, 80% + shock ability)\n" +
               "• Mystic Rod: 750 🪙 (Level 6+, 80% + charm ability)\n\n" +
               "🪱 **Bait:**\n" +
               "• Worms: 2 🪙 for 10 (Basic bait)\n" +
               "• Minnows: 4 🪙 each (Better success rate)\n" +
               "• Insects: 6 🪙 each (High success rate)\n" +
               "• Trap Bait: 1 🪙 for 50 (For fish traps only)\n\n" +
               "🪤 **Fish Traps:**\n" +
               "• Basic Trap: 50 🪙 (20 bait capacity, 5%/hour)\n" +
               "• Steel Trap: 150 🪙 (50 bait capacity, 8%/hour)\n" +
               "• Pro Trap: 300 🪙 (100 bait capacity, 12%/hour)\n\n" +
               "🐠 **Aquariums:**\n" +
               "• Basic Aquarium: 200 🪙 (10 fish capacity)\n" +
               "• Large Aquarium: 500 🪙 (25 fish capacity)\n" +
               "• Deluxe Aquarium: 1000 🪙 (50 fish capacity)\n\n" +
               "🛍️ **Buy Items:**\n" +
               "Use `!buy <item_name>` to purchase items!\n\n" +
               "💰 **Sell Fish:**\n" +
               "Use `!sell <fish_name>` or `!sell all` to sell your fish!";
    }

    private async Task<string> HandleMoveCommand(ulong userId)
    {
        var areas = _gameData.GetAreas();
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());

        var result = $"🚶 **Available Fishing Areas**\n\n" +
                    $"📍 Current: {userProfile.Area}\n\n";

        foreach (var area in areas)
        {
            var status = area.Name == userProfile.Area ? "📍 (Current)" : "🌊";
            result += $"{status} **{area.Name}** (Level {area.Level})\n" +
                     $"  🐟 Fish: {string.Join(", ", area.Fish.Take(3))}{(area.Fish.Count > 3 ? "..." : "")}\n" +
                     $"  📖 {area.Description.Substring(0, Math.Min(60, area.Description.Length))}...\n\n";
        }

        result += "Use `!moveto <area_name>` to travel to a different area!";
        return result;
    }

    private async Task<string> HandleMoveToCommand(ulong userId, string[] args)
    {
        if (args.Length == 0)
        {
            return "❌ Please specify an area to move to. Example: `!moveto lake`";
        }

        var targetAreaName = string.Join(" ", args);
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        var targetArea = _gameData.GetAreaByName(targetAreaName);

        if (targetArea == null)
        {
            return $"❌ Area '{targetAreaName}' not found. Use `!move` to see available areas.";
        }

        if (targetArea.Name.Equals(userProfile.Area, StringComparison.OrdinalIgnoreCase))
        {
            return $"📍 You are already in {targetArea.Name}!";
        }

        // Check level requirement
        if (userProfile.Level < targetArea.Level)
        {
            return $"❌ You need to be level {targetArea.Level} to access {targetArea.Name}. You are level {userProfile.Level}.";
        }

        // Update user's location
        userProfile.Area = targetArea.Name;
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"🚶 You traveled to **{targetArea.Name}**!\n\n" +
               $"📖 {targetArea.Description}\n\n" +
               $"🐟 Available Fish: {string.Join(", ", targetArea.Fish)}\n" +
               $"⭐ Difficulty: {targetArea.Difficulty}/4\n\n" +
               "Ready to start fishing? Use `!fish` to cast your line!";
    }

    private async Task<string> HandleSellCommand(ulong userId, string[] args)
    {
        if (args.Length == 0)
        {
            return "❌ Please specify which fish to sell. Example: `!sell carp` or `!sell all`";
        }

        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        var fishToSell = string.Join(" ", args).ToLower();

        if (fishToSell == "all")
        {
            if (!userProfile.Fish.Any())
            {
                return "❌ You don't have any fish to sell!";
            }

            int totalGold = 0;
            var salesReport = "💰 **Sold all fish:**\n";

            foreach (var fishEntry in userProfile.Fish.ToList())
            {
                var fishData = _gameData.GetFishByName(fishEntry.Key);
                if (fishData != null)
                {
                    var value = fishData.BaseValue * fishEntry.Value;
                    totalGold += value;
                    salesReport += $"  {fishEntry.Key} x{fishEntry.Value}: {value} gold\n";
                }
            }

            userProfile.Fish.Clear();
            userProfile.Gold += totalGold;
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

            return salesReport + $"\n💰 **Total earned:** {totalGold} gold\n" +
                   $"🪙 **New balance:** {userProfile.Gold} gold";
        }
        else
        {
            // Find the fish in user's collection
            var fishKey = userProfile.Fish.Keys.FirstOrDefault(f => 
                f.Equals(fishToSell, StringComparison.OrdinalIgnoreCase));

            if (fishKey == null || userProfile.Fish[fishKey] <= 0)
            {
                return $"❌ You don't have any {fishToSell} to sell!";
            }

            var fishData = _gameData.GetFishByName(fishKey);
            if (fishData == null)
            {
                return "❌ Error finding fish data. Please try again.";
            }

            userProfile.Fish[fishKey]--;
            if (userProfile.Fish[fishKey] <= 0)
            {
                userProfile.Fish.Remove(fishKey);
            }

            userProfile.Gold += fishData.BaseValue;
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

            return $"💰 Sold 1x {fishKey} for {fishData.BaseValue} gold!\n" +
                   $"🪙 New balance: {userProfile.Gold} gold";
        }
    }

    private async Task<string> HandleBuyCommand(ulong userId, string[] args)
    {
        if (args.Length == 0)
        {
            return "❌ Please specify what to buy. Example: `!buy worms` or `!buy steel rod`";
        }

        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        var itemToBuy = string.Join(" ", args).ToLower();

        // Handle different purchasable items
        switch (itemToBuy)
        {
            case "worms" or "worm":
                return await BuyBait(userProfile, userId, "Worms", 2, 10);
                
            case "minnows" or "minnow":
                return await BuyBait(userProfile, userId, "Minnows", 4, 1);
                
            case "insects" or "insect":
                return await BuyBait(userProfile, userId, "Insects", 6, 1);
                
            case "trap bait":
                return await BuyBait(userProfile, userId, "Trap Bait", 1, 50);

            case "steel rod":
                return await BuyRod(userProfile, userId, "Steel Rod", 150, 2);

            case "pro rod":
                return await BuyRod(userProfile, userId, "Pro Rod", 300, 3);

            case "ice rod":
                return await BuyRod(userProfile, userId, "Ice Rod", 450, 4);

            case "lightning rod":
                return await BuyRod(userProfile, userId, "Lightning Rod", 600, 5);

            case "mystic rod":
                return await BuyRod(userProfile, userId, "Mystic Rod", 750, 6);
                
            case "basic trap":
                return await BuyTrap(userProfile, userId, "Basic Trap", 50);
                
            case "steel trap":
                return await BuyTrap(userProfile, userId, "Steel Trap", 150);
                
            case "pro trap":
                return await BuyTrap(userProfile, userId, "Pro Trap", 300);
                
            case "basic aquarium":
                return await BuyAquarium(userProfile, userId, "Basic Aquarium", 200, 10);
                
            case "large aquarium":
                return await BuyAquarium(userProfile, userId, "Large Aquarium", 500, 25);
                
            case "deluxe aquarium":
                return await BuyAquarium(userProfile, userId, "Deluxe Aquarium", 1000, 50);

            default:
                return $"❌ '{itemToBuy}' is not available for purchase. Check `!shop` for available items.";
        }
    }

    private async Task<string> BuyBait(UserProfile userProfile, ulong userId, string baitName, int price, int quantity)
    {
        if (userProfile.Gold < price)
        {
            return $"❌ You need {price} gold to buy {quantity}x {baitName}. You have {userProfile.Gold} gold.";
        }

        userProfile.Gold -= price;
        
        if (!userProfile.Inventory.ContainsKey(baitName))
            userProfile.Inventory[baitName] = 0;
        
        userProfile.Inventory[baitName] += quantity;
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"✅ Purchased {quantity}x {baitName} for {price} gold!\n" +
               $"🪙 Remaining gold: {userProfile.Gold}\n" +
               $"🎒 You now have {userProfile.Inventory[baitName]} {baitName}";
    }

    private async Task<string> BuyRod(UserProfile userProfile, ulong userId, string rodName, int price, int requiredLevel)
    {
        if (userProfile.Level < requiredLevel)
        {
            return $"❌ You need to be level {requiredLevel} to buy {rodName}. You are level {userProfile.Level}.";
        }

        if (userProfile.Gold < price)
        {
            return $"❌ You need {price} gold to buy {rodName}. You have {userProfile.Gold} gold.";
        }

        if (userProfile.Equipment.ContainsKey(rodName))
        {
            return $"❌ You already own {rodName}!";
        }

        userProfile.Gold -= price;
        userProfile.Equipment[rodName] = 1;
        userProfile.EquippedRod = rodName; // Auto-equip new rod
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        var rodData = _gameData.GetRodByName(rodName);
        var abilityText = !string.IsNullOrEmpty(rodData?.SpecialAbility) 
            ? $"\n⚡ Special Ability: {rodData.SpecialAbility}" 
            : "";

        return $"✅ Purchased and equipped {rodName} for {price} gold!\n" +
               $"🪙 Remaining gold: {userProfile.Gold}\n" +
               $"🎣 Success rate: {(rodData?.SuccessRate ?? 0.5) * 100:F0}%{abilityText}";
    }

    private async Task<string> BuyTrap(UserProfile userProfile, ulong userId, string trapName, int price)
    {
        if (userProfile.Gold < price)
        {
            return $"❌ You need {price} gold to buy {trapName}. You have {userProfile.Gold} gold.";
        }

        userProfile.Gold -= price;
        
        if (!userProfile.Traps.ContainsKey(trapName))
            userProfile.Traps[trapName] = 0;
        
        userProfile.Traps[trapName]++;
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        var trapDetails = trapName switch
        {
            "Basic Trap" => "20 bait capacity, 5% catch rate/hour",
            "Steel Trap" => "50 bait capacity, 8% catch rate/hour", 
            "Pro Trap" => "100 bait capacity, 12% catch rate/hour",
            _ => "Unknown specifications"
        };

        return $"✅ Purchased {trapName} for {price} gold!\n" +
               $"🪙 Remaining gold: {userProfile.Gold}\n" +
               $"🪤 Trap specs: {trapDetails}\n" +
               $"📦 You now own {userProfile.Traps[trapName]} {trapName}(s)\n\n" +
               $"💡 Use `!traps place {trapName.ToLower()}` to deploy it!";
    }

    private async Task<string> BuyAquarium(UserProfile userProfile, ulong userId, string aquariumName, int price, int capacity)
    {
        if (userProfile.Gold < price)
        {
            return $"❌ You need {price} gold to buy {aquariumName}. You have {userProfile.Gold} gold.";
        }

        if (userProfile.Aquariums.Any())
        {
            return $"❌ You can only have one aquarium at a time. Upgrade your current one instead!";
        }

        userProfile.Gold -= price;
        
        var newAquarium = new Aquarium
        {
            Name = aquariumName,
            WaterQuality = 100,
            Temperature = 75,
            LastCleaned = DateTime.UtcNow,
            LastMaintenance = DateTime.UtcNow
        };
        
        userProfile.Aquariums.Add(newAquarium);
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"✅ Purchased {aquariumName} for {price} gold!\n" +
               $"🪙 Remaining gold: {userProfile.Gold}\n" +
               $"🐠 Capacity: {capacity} fish\n" +
               $"💧 Water quality: 100% (pristine)\n" +
               $"🌡️ Temperature: 75% (optimal)\n\n" +
               $"💡 Use `!aquarium add <fish_name>` to add fish from your collection!";
    }

    private async Task<string> HandleDigCommand(ulong userId)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        
        // Check cooldown (10 minutes between digs)
        if (userProfile.LastActive != DateTime.MinValue)
        {
            var timeSinceLastDig = DateTime.UtcNow - userProfile.LastActive;
            if (timeSinceLastDig.TotalMinutes < 10)
            {
                var remaining = TimeSpan.FromMinutes(10) - timeSinceLastDig;
                return $"⏰ You need to wait {remaining.Minutes}m {remaining.Seconds}s before digging again.";
            }
        }
        
        userProfile.LastActive = DateTime.UtcNow;
        
        // Random chance to find worms (70% success rate)
        var random = new Random();
        var success = random.NextDouble() < 0.7;
        
        if (success)
        {
            var wormsFound = random.Next(3, 8); // 3-7 worms
            
            if (!userProfile.Inventory.ContainsKey("Worms"))
                userProfile.Inventory["Worms"] = 0;
            
            userProfile.Inventory["Worms"] += wormsFound;
            
            // Auto-equip worms if no bait equipped
            if (string.IsNullOrEmpty(userProfile.EquippedBait))
            {
                userProfile.EquippedBait = "Worms";
            }
            
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
            
            return $"🐛 **Success!** You found {wormsFound} worms while digging!\n\n" +
                   $"🎒 You now have {userProfile.Inventory["Worms"]} worms total\n" +
                   $"⏰ You can dig again in 10 minutes\n\n" +
                   "💡 Digging is a free way to get basic bait when you're low on gold!";
        }
        else
        {
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
            
            return $"🕳️ **No luck!** You dug around but didn't find any worms.\n\n" +
                   $"⏰ You can try again in 10 minutes\n\n" +
                   "💡 Tip: Digging has a 70% success rate for finding 3-7 worms.";
        }
    }

    private async Task<string> HandleTrapsCommand(ulong userId, string[] args)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        
        if (args.Length == 0)
        {
            return $"🪤 **Fish Traps System**\n\n" +
                   $"📍 Current Area: {userProfile.Area}\n" +
                   $"🪤 Owned Traps: {userProfile.Traps.Sum(t => t.Value)}\n\n" +
                   "**Trap Commands:**\n" +
                   "• `!traps place <trap_name>` - Place a trap in current area\n" +
                   "• `!traps check` - Check all your active traps\n" +
                   "• `!traps collect` - Collect fish from ready traps\n" +
                   "• `!traps status` - Show trap statistics\n\n" +
                   "**Available Traps:**\n" +
                   "• Basic Trap: 50 🪙 (holds 20 bait, 5% catch rate/hour)\n" +
                   "• Steel Trap: 150 🪙 (holds 50 bait, 8% catch rate/hour)\n" +
                   "• Pro Trap: 300 🪙 (holds 100 bait, 12% catch rate/hour)\n\n" +
                   "💡 Traps are a passive way to catch fish while you're away!";
        }

        var subCommand = args[0].ToLower();
        
        return subCommand switch
        {
            "place" => await HandleTrapPlace(userProfile, userId, args.Skip(1).ToArray()),
            "check" => await HandleTrapCheck(userProfile, userId),
            "collect" => await HandleTrapCollect(userProfile, userId),
            "status" => await HandleTrapStatus(userProfile, userId),
            _ => "❌ Unknown trap command. Use `!traps` to see available commands."
        };
    }

    private async Task<string> HandleTrapPlace(UserProfile userProfile, ulong userId, string[] args)
    {
        if (args.Length == 0)
        {
            return "❌ Please specify which trap to place. Example: `!traps place basic trap`";
        }

        var trapName = string.Join(" ", args).ToLower() switch
        {
            "basic" or "basic trap" => "Basic Trap",
            "steel" or "steel trap" => "Steel Trap", 
            "pro" or "pro trap" => "Pro Trap",
            _ => null
        };

        if (trapName == null)
        {
            return "❌ Invalid trap name. Available: basic trap, steel trap, pro trap";
        }

        // Check if user owns this trap
        if (!userProfile.Traps.ContainsKey(trapName) || userProfile.Traps[trapName] <= 0)
        {
            return $"❌ You don't own any {trapName}. Visit the shop to buy one!";
        }

        // Check if user has trap bait
        if (!userProfile.Inventory.ContainsKey("Trap Bait") || userProfile.Inventory["Trap Bait"] <= 0)
        {
            return "❌ You need Trap Bait to use fish traps. Buy some from the shop!";
        }

        // Initialize placed traps if not exists
        if (!userProfile.Equipment.ContainsKey("PlacedTraps"))
        {
            userProfile.Equipment["PlacedTraps"] = 0;
        }

        // Place the trap
        userProfile.Traps[trapName]--;
        userProfile.Equipment["PlacedTraps"]++;

        // Calculate bait capacity and use it
        var baitCapacity = trapName switch
        {
            "Basic Trap" => 20,
            "Steel Trap" => 50,
            "Pro Trap" => 100,
            _ => 20
        };

        var baitToUse = Math.Min(baitCapacity, userProfile.Inventory["Trap Bait"]);
        userProfile.Inventory["Trap Bait"] -= baitToUse;

        // Store trap placement data 
        var trapKey = $"trap_{userProfile.Area}_{DateTime.UtcNow.Ticks}";
        userProfile.Equipment[trapKey] = baitToUse; // Store bait amount
        userProfile.Equipment[$"{trapKey}_type"] = trapName switch
        {
            "Basic Trap" => 1,
            "Steel Trap" => 2,
            "Pro Trap" => 3,
            _ => 1
        };
        userProfile.Equipment[$"{trapKey}_placed"] = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"✅ **{trapName} placed successfully!**\n\n" +
               $"📍 Location: {userProfile.Area}\n" +
               $"🪱 Bait loaded: {baitToUse}\n" +
               $"⏰ Placed at: {DateTime.UtcNow:HH:mm UTC}\n\n" +
               $"🎣 The trap will passively catch fish over time!\n" +
               $"Use `!traps check` to monitor progress.";
    }

    private async Task<string> HandleTrapCheck(UserProfile userProfile, ulong userId)
    {
        var placedTraps = userProfile.Equipment
            .Where(kv => kv.Key.StartsWith("trap_") && !kv.Key.Contains("_type") && !kv.Key.Contains("_placed"))
            .ToList();

        if (!placedTraps.Any())
        {
            return "🪤 You don't have any active traps placed.\n\n" +
                   "💡 Use `!traps place <trap_name>` to deploy a trap!";
        }

        var result = "🪤 **Active Traps Status:**\n\n";
        
        foreach (var trap in placedTraps)
        {
            var trapKey = trap.Key;
            var baitRemaining = trap.Value;
            var trapTypeKey = $"{trapKey}_type";
            var trapPlacedKey = $"{trapKey}_placed";

            if (!userProfile.Equipment.ContainsKey(trapTypeKey) || !userProfile.Equipment.ContainsKey(trapPlacedKey))
                continue;

            var trapType = userProfile.Equipment[trapTypeKey] switch
            {
                1 => "Basic Trap",
                2 => "Steel Trap", 
                3 => "Pro Trap",
                _ => "Unknown Trap"
            };

            var placedTime = DateTimeOffset.FromUnixTimeSeconds(userProfile.Equipment[trapPlacedKey]).DateTime;
            var timeElapsed = DateTime.UtcNow - placedTime;
            var location = trapKey.Split('_')[1];

            // Calculate potential catches
            var catchRate = userProfile.Equipment[trapTypeKey] switch
            {
                1 => 0.05, // 5% per hour
                2 => 0.08, // 8% per hour  
                3 => 0.12, // 12% per hour
                _ => 0.05
            };

            var hoursElapsed = timeElapsed.TotalHours;
            var potentialCatches = (int)(hoursElapsed * catchRate * baitRemaining);

            result += $"🎣 **{trapType}** in {location}\n" +
                     $"   ⏰ Running for: {timeElapsed.Hours}h {timeElapsed.Minutes}m\n" +
                     $"   🪱 Bait: {baitRemaining}\n" +
                     $"   🐟 Potential catches: ~{potentialCatches}\n\n";
        }

        result += "💡 Use `!traps collect` to gather fish from all traps!";
        return result;
    }

    private async Task<string> HandleTrapCollect(UserProfile userProfile, ulong userId)
    {
        var placedTraps = userProfile.Equipment
            .Where(kv => kv.Key.StartsWith("trap_") && !kv.Key.Contains("_type") && !kv.Key.Contains("_placed"))
            .ToList();

        if (!placedTraps.Any())
        {
            return "🪤 You don't have any active traps to collect from.\n\n" +
                   "💡 Use `!traps place <trap_name>` to deploy a trap!";
        }

        var totalFishCaught = 0;
        var totalGoldEarned = 0;
        var collectionReport = "🎣 **Trap Collection Results:**\n\n";
        var random = new Random();
        var currentArea = _gameData.GetArea(userProfile.Area);

        foreach (var trap in placedTraps.ToList()) // ToList to avoid modification during iteration
        {
            var trapKey = trap.Key;
            var baitRemaining = trap.Value;
            var trapTypeKey = $"{trapKey}_type";
            var trapPlacedKey = $"{trapKey}_placed";

            if (!userProfile.Equipment.ContainsKey(trapTypeKey) || !userProfile.Equipment.ContainsKey(trapPlacedKey))
                continue;

            var trapType = userProfile.Equipment[trapTypeKey];
            var placedTime = DateTimeOffset.FromUnixTimeSeconds(userProfile.Equipment[trapPlacedKey]).DateTime;
            var timeElapsed = DateTime.UtcNow - placedTime;
            var location = trapKey.Split('_')[1];

            // Calculate actual catches
            var catchRate = trapType switch
            {
                1 => 0.05, // 5% per hour
                2 => 0.08, // 8% per hour
                3 => 0.12, // 12% per hour
                _ => 0.05
            };

            var hoursElapsed = timeElapsed.TotalHours;
            var baseCatches = hoursElapsed * catchRate;
            var actualCatches = 0;

            // Roll for each potential catch
            for (int i = 0; i < baitRemaining; i++)
            {
                if (random.NextDouble() < baseCatches / baitRemaining)
                {
                    actualCatches++;
                }
            }

            if (actualCatches > 0)
            {
                // Generate fish from trap location area
                var trapArea = _gameData.GetAreaByName(location) ?? currentArea;
                for (int i = 0; i < actualCatches; i++)
                {
                    var fishName = trapArea.Fish[random.Next(trapArea.Fish.Count)];
                    var fishData = _gameData.GetFishByName(fishName);
                    
                    if (fishData != null)
                    {
                        // Add to inventory
                        if (!userProfile.Fish.ContainsKey(fishName))
                            userProfile.Fish[fishName] = 0;
                        userProfile.Fish[fishName]++;
                        
                        // Calculate value (traps give slightly less value)
                        var value = (int)(fishData.BaseValue * 0.8); // 80% of normal value
                        totalGoldEarned += value;
                    }
                }
                totalFishCaught += actualCatches;
            }

            var trapTypeName = trapType switch
            {
                1 => "Basic Trap",
                2 => "Steel Trap",
                3 => "Pro Trap", 
                _ => "Unknown Trap"
            };

            collectionReport += $"🪤 {trapTypeName} ({location}): {actualCatches} fish\n";

            // Remove the trap (it's been collected)
            userProfile.Equipment.Remove(trapKey);
            userProfile.Equipment.Remove(trapTypeKey);
            userProfile.Equipment.Remove(trapPlacedKey);
            userProfile.Equipment["PlacedTraps"]--;
        }

        userProfile.Gold += totalGoldEarned;
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        collectionReport += $"\n🎣 **Total Results:**\n" +
                           $"🐟 Fish caught: {totalFishCaught}\n" +
                           $"💰 Gold earned: {totalGoldEarned}\n" +
                           $"🪙 New balance: {userProfile.Gold}\n\n" +
                           "✅ All traps have been collected and need to be placed again.";

        return collectionReport;
    }

    private async Task<string> HandleTrapStatus(UserProfile userProfile, ulong userId)
    {
        var activeTrapCount = userProfile.Equipment.ContainsKey("PlacedTraps") ? 
            userProfile.Equipment["PlacedTraps"] : 0;

        return $"📊 **Trap Statistics:**\n\n" +
               $"🪤 Active traps: {activeTrapCount}\n" +
               $"📦 Owned traps: {userProfile.Traps.Sum(t => t.Value)}\n" +
               $"🪱 Trap bait: {(userProfile.Inventory.ContainsKey("Trap Bait") ? userProfile.Inventory["Trap Bait"] : 0)}\n\n" +
               "**Trap Efficiency:**\n" +
               "• Basic Trap: 5% catch rate per hour\n" +
               "• Steel Trap: 8% catch rate per hour\n" +
               "• Pro Trap: 12% catch rate per hour\n\n" +
               "💡 Traps give 80% of normal fish value but work passively!";
    }

    private async Task<string> HandleAquariumCommand(ulong userId, string[] args)
    {
        var userProfile = await _userManager.GetOrCreateUserAsync(userId.ToString());
        
        if (userProfile.Aquariums == null || !userProfile.Aquariums.Any())
        {
            return $"🐠 **Aquarium System**\n\n" +
                   $"You don't have any aquariums yet!\n\n" +
                   "**Get Started:**\n" +
                   "• `!buy basic aquarium` - Purchase your first aquarium (200 🪙)\n\n" +
                   "**Aquarium Features:**\n" +
                   "• Store and display your favorite fish\n" +
                   "• Fish can breed and grow if kept happy\n" +
                   "• Requires regular maintenance (feeding, cleaning)\n" +
                   "• Decorate with items to boost fish happiness\n" +
                   "• Name your fish and watch them thrive!\n\n" +
                   "💡 Happy fish in aquariums can breed rare variants!";
        }
        
        // Process aquarium maintenance first
        await ProcessAquariumMaintenance(userProfile, userId);
        
        if (args.Length == 0)
        {
            // Show current aquarium status
            return await ShowAquariumStatus(userProfile);
        }

        var subCommand = args[0].ToLower();
        
        return subCommand switch
        {
            "feed" => await HandleAquariumFeed(userProfile, userId),
            "clean" => await HandleAquariumClean(userProfile, userId),
            "add" => await HandleAquariumAdd(userProfile, userId, args.Skip(1).ToArray()),
            "remove" => await HandleAquariumRemove(userProfile, userId, args.Skip(1).ToArray()),
            "view" or "list" => await HandleAquariumView(userProfile),
            "name" => await HandleAquariumName(userProfile, userId, args.Skip(1).ToArray()),
            "status" => await ShowAquariumStatus(userProfile),
            _ => "❌ Unknown aquarium command. Available: feed, clean, add, remove, view, name, status"
        };
    }

    private async Task ProcessAquariumMaintenance(UserProfile userProfile, ulong userId)
    {
        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return;

        var timeSinceLastMaintenance = DateTime.UtcNow - aquarium.LastMaintenance;
        var hoursSince = timeSinceLastMaintenance.TotalHours;

        if (hoursSince > 0)
        {
            // Degrade water quality over time (1% per hour)
            aquarium.WaterQuality = Math.Max(0, aquarium.WaterQuality - (int)(hoursSince * 1));
            
            // Fish get hungrier over time (2% per hour)
            foreach (var fish in aquarium.Fish)
            {
                fish.Hunger = Math.Min(100, fish.Hunger + (int)(hoursSince * 2));
                
                // Happiness decreases if hungry or poor water quality
                if (fish.Hunger > 70 || aquarium.WaterQuality < 50)
                {
                    fish.Happiness = Math.Max(0, fish.Happiness - (int)(hoursSince * 3));
                }
                else if (fish.Hunger < 30 && aquarium.WaterQuality > 80)
                {
                    fish.Happiness = Math.Min(100, fish.Happiness + (int)(hoursSince * 1));
                }
            }

            aquarium.LastMaintenance = DateTime.UtcNow;
            await _userManager.UpdateUserAsync(userId.ToString(), userProfile);
        }
    }

    private async Task<string> ShowAquariumStatus(UserProfile userProfile)
    {
        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";
        
        var fishCount = aquarium.Fish?.Count ?? 0;
        var capacity = aquarium.Name switch
        {
            "Basic Aquarium" => 10,
            "Large Aquarium" => 25, 
            "Deluxe Aquarium" => 50,
            _ => 10
        };

        var avgHappiness = CalculateAverageHappiness(aquarium);
        var avgHunger = aquarium.Fish.Any() ? (int)aquarium.Fish.Average(f => f.Hunger) : 0;

        var status = $"🐠 **{aquarium.Name}**\n\n" +
                    $"🏠 Fish: {fishCount}/{capacity}\n" +
                    $"💧 Water Quality: {GetStatusIcon(aquarium.WaterQuality)} {aquarium.WaterQuality}%\n" +
                    $"😊 Average Happiness: {GetStatusIcon(avgHappiness)} {avgHappiness}%\n" +
                    $"🍽️ Average Hunger: {GetHungerIcon(avgHunger)} {avgHunger}%\n\n";

        if (fishCount > 0)
        {
            status += "🐟 **Recent Fish:**\n";
            foreach (var fish in aquarium.Fish.Take(3))
            {
                var displayName = !string.IsNullOrEmpty(fish.CustomName) ? fish.CustomName : fish.Name;
                status += $"  • {displayName} - {GetStatusIcon(fish.Happiness)} {fish.Happiness}% happy\n";
            }
            if (fishCount > 3)
            {
                status += $"  ... and {fishCount - 3} more\n";
            }
            status += "\n";
        }

        status += "**Commands:**\n" +
                 "• `!aquarium feed` - Feed all fish\n" +
                 "• `!aquarium clean` - Clean the water\n" +
                 "• `!aquarium add <fish_name>` - Add fish from inventory\n" +
                 "• `!aquarium view` - View all fish details\n\n";

        // Maintenance alerts
        if (aquarium.WaterQuality < 50)
        {
            status += "⚠️ **Alert:** Water quality is poor! Use `!aquarium clean`\n";
        }
        if (avgHunger > 70)
        {
            status += "⚠️ **Alert:** Fish are hungry! Use `!aquarium feed`\n";
        }
        if (avgHappiness > 80 && fishCount >= 2)
        {
            status += "💝 **Breeding possible!** Happy fish may produce offspring!\n";
        }

        return status;
    }

    private string GetStatusIcon(int value)
    {
        return value switch
        {
            >= 80 => "🟢",
            >= 60 => "🟡", 
            >= 40 => "🟠",
            _ => "🔴"
        };
    }

    private string GetHungerIcon(int hunger)
    {
        return hunger switch
        {
            <= 30 => "🟢", // Well fed
            <= 60 => "🟡", // Getting hungry
            <= 80 => "🟠", // Hungry
            _ => "🔴" // Very hungry
        };
    }

    private async Task<string> HandleAquariumFeed(UserProfile userProfile, ulong userId)
    {
        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";
        
        if (!aquarium.Fish.Any())
        {
            return "🐠 Your aquarium doesn't have any fish to feed!";
        }

        // Check if user has fish food (use worms as fish food)
        if (!userProfile.Inventory.ContainsKey("Worms") || userProfile.Inventory["Worms"] <= 0)
        {
            return "❌ You don't have any fish food! Use worms to feed your fish.\n\n" +
                   "💡 Buy worms from the shop or use `!dig` to find free ones.";
        }

        // Feed fish (costs 1 worm per 2 fish)
        var foodNeeded = Math.Max(1, aquarium.Fish.Count / 2);
        if (userProfile.Inventory["Worms"] < foodNeeded)
        {
            return $"❌ You need {foodNeeded} worms to feed all your fish. You have {userProfile.Inventory["Worms"]}.";
        }

        userProfile.Inventory["Worms"] -= foodNeeded;

        // Reset hunger and boost happiness
        foreach (var fish in aquarium.Fish)
        {
            fish.Hunger = 0;
            fish.Happiness = Math.Min(100, fish.Happiness + 10);
            fish.LastFed = DateTime.UtcNow;
        }

        aquarium.LastFed = DateTime.UtcNow;
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"🍽️ **Fed all fish!**\n\n" +
               $"🪱 Food used: {foodNeeded} worms\n" +
               $"😊 All fish happiness increased by 10%\n" +
               $"🪱 Worms remaining: {userProfile.Inventory["Worms"]}\n\n" +
               "💡 Well-fed fish are happier and more likely to breed!";
    }

    private async Task<string> HandleAquariumClean(UserProfile userProfile, ulong userId)
    {
        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";

        if (aquarium.WaterQuality >= 90)
        {
            return "💧 Your aquarium water is already very clean! (90%+)";
        }

        // Cleaning costs gold
        var cleaningCost = 5;
        if (userProfile.Gold < cleaningCost)
        {
            return $"❌ You need {cleaningCost} gold to clean the aquarium. You have {userProfile.Gold} gold.";
        }

        userProfile.Gold -= cleaningCost;
        aquarium.WaterQuality = 100;
        aquarium.LastCleaned = DateTime.UtcNow;

        // Happy fish from clean water
        foreach (var fish in aquarium.Fish)
        {
            fish.Happiness = Math.Min(100, fish.Happiness + 5);
        }

        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"💧 **Aquarium cleaned!**\n\n" +
               $"💰 Cost: {cleaningCost} gold\n" +
               $"💧 Water quality: 100% (pristine)\n" +
               $"😊 All fish happiness increased by 5%\n" +
               $"🪙 Gold remaining: {userProfile.Gold}\n\n" +
               "✨ Your fish love the clean water!";
    }

    private async Task<string> HandleAquariumAdd(UserProfile userProfile, ulong userId, string[] args)
    {
        if (args.Length == 0)
        {
            return "❌ Please specify which fish to add. Example: `!aquarium add carp`";
        }

        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";

        var capacity = aquarium.Name switch
        {
            "Basic Aquarium" => 10,
            "Large Aquarium" => 25,
            "Deluxe Aquarium" => 50,
            _ => 10
        };

        if (aquarium.Fish.Count >= capacity)
        {
            return $"❌ Your aquarium is full! ({aquarium.Fish.Count}/{capacity})\n\n" +
                   "💡 Consider upgrading to a larger aquarium.";
        }

        var fishName = string.Join(" ", args);
        var fishKey = userProfile.Fish.Keys.FirstOrDefault(f => 
            f.Equals(fishName, StringComparison.OrdinalIgnoreCase));

        if (fishKey == null || userProfile.Fish[fishKey] <= 0)
        {
            return $"❌ You don't have any {fishName} to add!\n\n" +
                   "💡 Catch fish first using `!fish` command.";
        }

        // Remove from inventory and add to aquarium
        userProfile.Fish[fishKey]--;
        if (userProfile.Fish[fishKey] <= 0)
        {
            userProfile.Fish.Remove(fishKey);
        }

        var newAquariumFish = new AquariumFish
        {
            Name = fishKey,
            Happiness = 70, // Starting happiness
            Hunger = 20,    // Slightly hungry
            AddedAt = DateTime.UtcNow,
            Size = "Normal"
        };

        aquarium.Fish.Add(newAquariumFish);
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"🐠 **Added {fishKey} to aquarium!**\n\n" +
               $"🏠 Aquarium space: {aquarium.Fish.Count}/{capacity}\n" +
               $"😊 Starting happiness: 70%\n" +
               $"📅 Added on: {DateTime.UtcNow:MM/dd HH:mm}\n\n" +
               "💡 Use `!aquarium name {fishKey}` to give it a custom name!";
    }

    private async Task<string> HandleAquariumView(UserProfile userProfile)
    {
        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";

        if (!aquarium.Fish.Any())
        {
            return "🐠 Your aquarium is empty!\n\n" +
                   "💡 Use `!aquarium add <fish_name>` to add fish from your collection.";
        }

        var result = $"🐠 **All Fish in {aquarium.Name}:**\n\n";
        
        for (int i = 0; i < aquarium.Fish.Count; i++)
        {
            var fish = aquarium.Fish[i];
            var displayName = !string.IsNullOrEmpty(fish.CustomName) ? 
                $"{fish.CustomName} ({fish.Name})" : fish.Name;
            
            var age = DateTime.UtcNow - fish.AddedAt;
            var ageText = age.TotalDays >= 1 ? $"{age.Days}d" : $"{age.Hours}h";
            
            result += $"{i + 1}. **{displayName}**\n" +
                     $"   😊 Happiness: {GetStatusIcon(fish.Happiness)} {fish.Happiness}%\n" +
                     $"   🍽️ Hunger: {GetHungerIcon(fish.Hunger)} {fish.Hunger}%\n" +
                     $"   📏 Size: {fish.Size}\n" +
                     $"   📅 Age: {ageText}\n\n";
        }

        result += $"**Total: {aquarium.Fish.Count} fish**\n\n" +
                 "💡 Use `!aquarium name <number> <new_name>` to rename fish!";

        return result;
    }

    private async Task<string> HandleAquariumName(UserProfile userProfile, ulong userId, string[] args)
    {
        if (args.Length < 2)
        {
            return "❌ Usage: `!aquarium name <fish_number> <new_name>`\n\n" +
                   "Example: `!aquarium name 1 Goldie`\n" +
                   "Use `!aquarium view` to see fish numbers.";
        }

        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";

        if (!int.TryParse(args[0], out int fishIndex) || fishIndex < 1 || fishIndex > aquarium.Fish.Count)
        {
            return $"❌ Invalid fish number. Use a number between 1 and {aquarium.Fish.Count}.";
        }

        var newName = string.Join(" ", args.Skip(1));
        if (newName.Length > 20)
        {
            return "❌ Fish name must be 20 characters or less.";
        }

        var fish = aquarium.Fish[fishIndex - 1];
        var oldName = !string.IsNullOrEmpty(fish.CustomName) ? fish.CustomName : fish.Name;
        fish.CustomName = newName;

        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"✅ **Fish renamed!**\n\n" +
               $"Old name: {oldName}\n" +
               $"New name: {newName}\n\n" +
               "🐠 Your fish loves its new name!";
    }

    private async Task<string> HandleAquariumRemove(UserProfile userProfile, ulong userId, string[] args)
    {
        if (args.Length == 0)
        {
            return "❌ Usage: `!aquarium remove <fish_number>`\n\n" +
                   "Use `!aquarium view` to see fish numbers.";
        }

        var aquarium = userProfile.Aquariums.FirstOrDefault();
        if (aquarium == null) return "❌ No aquarium found.";

        if (!int.TryParse(args[0], out int fishIndex) || fishIndex < 1 || fishIndex > aquarium.Fish.Count)
        {
            return $"❌ Invalid fish number. Use a number between 1 and {aquarium.Fish.Count}.";
        }

        var fish = aquarium.Fish[fishIndex - 1];
        var displayName = !string.IsNullOrEmpty(fish.CustomName) ? 
            $"{fish.CustomName} ({fish.Name})" : fish.Name;
        
        // Return fish to inventory
        if (!userProfile.Fish.ContainsKey(fish.Name))
            userProfile.Fish[fish.Name] = 0;
        userProfile.Fish[fish.Name]++;

        aquarium.Fish.RemoveAt(fishIndex - 1);
        await _userManager.UpdateUserAsync(userId.ToString(), userProfile);

        return $"🐠 **Removed {displayName} from aquarium!**\n\n" +
               $"The fish has been returned to your collection.\n" +
               $"🏠 Aquarium space: {aquarium.Fish.Count}/{(aquarium.Name switch { "Basic Aquarium" => 10, "Large Aquarium" => 25, "Deluxe Aquarium" => 50, _ => 10 })}";
    }

    private int CalculateAverageHappiness(Aquarium aquarium)
    {
        if (aquarium.Fish == null || !aquarium.Fish.Any())
            return 100;
        
        var avgHappiness = aquarium.Fish.Average(f => f.Happiness);
        return (int)Math.Round(avgHappiness);
    }

    private async Task<string> HandleAreasCommand()
    {
        var areas = _gameData.GetAreas();
        var result = "🗺️ **All Fishing Areas**\n\n";

        foreach (var area in areas)
        {
            result += $"🌊 **{area.Name}** (Level {area.Level}, Difficulty {area.Difficulty})\n" +
                     $"📖 {area.Description}\n" +
                     $"🐟 Fish: {string.Join(", ", area.Fish)}\n\n";
        }

        return result;
    }

    private async Task<string> HandleHelpCommand()
    {
        return "🎣 **ZPT Fishing Bot Commands**\n\n" +
               "**Basic Commands:**\n" +
               "`!start` or `!play` - Start your fishing adventure\n" +
               "`!fish` - Cast your line and try to catch fish\n" +
               "`!inventory` or `!inv` - View your items and fish\n" +
               "`!shop` - Visit the fishing shop\n" +
               "`!move` - View available fishing areas\n" +
               "`!moveto <area>` - Travel to a specific area\n" +
               "`!areas` - See detailed area information\n\n" +
               "**Trading Commands:**\n" +
               "`!sell <fish>` - Sell a specific fish for gold\n" +
               "`!sell all` - Sell all your fish\n" +
               "`!buy <item>` - Purchase equipment from the shop\n\n" +
               "**Advanced Commands:**\n" +
               "`!dig` - Search for free worms (10min cooldown)\n" +
               "`!traps` - Manage your fish traps\n" +
               "`!aquarium` - View and manage your aquariums\n\n" +
               "`!help` - Show this help message\n\n" +
               "**Tips:**\n" +
               "• Start at the Lake for easier fish\n" +
               "• Upgrade your rod for better success rates\n" +
               "• Different areas have different fish species\n" +
               "• Sell fish for gold to buy better equipment\n" +
               "• Higher level areas have rarer fish but are harder\n" +
               "• Use `!dig` for free worms when you're broke\n" +
               "• Fish traps provide passive income\n" +
               "• Aquariums let fish breed and grow\n\n" +
               "Happy fishing! 🎣";
    }

    private static int CalculateLevel(int experience)
    {
        // Simple level calculation: 100 XP per level
        return Math.Max(1, experience / 100 + 1);
    }
}