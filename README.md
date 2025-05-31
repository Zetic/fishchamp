# FishChamp Discord Bot

A Discord fishing game bot built with C# and Remora.Discord, featuring modular commands, exploration, and persistent player data.

## Phase 0: Foundational Setup ✅

The bot now includes:

### Iteration 0.1 - Remora Setup ✅
- ✅ C# project with .NET 8.0 and Remora.Discord
- ✅ Basic bot scaffolding with dependency injection
- ✅ Configuration management for bot token and database

### Iteration 0.2 - Command Router & Module System ✅
- ✅ Modular command system with separate modules:
  - **FishingModule**: `/fishing cast`, `/fishing profile`, `/fish`
  - **MapModule**: `/map current`, `/map travel`, `/map areas`
  - **InventoryModule**: `/inventory view`, `/inventory fish`
- ✅ Abstract command handling with proper error handling

### Iteration 0.3 - Persistence Layer ✅
- ✅ JSON-based repository pattern for easy database swapping
- ✅ Data models: `PlayerProfile`, `Inventory`, `AreaState`
- ✅ Repository interfaces and implementations
- ✅ Automatic default area initialization

## Phase 2: Unique Fish & Fishing Expansion ✅

### Iteration 2.1 – Fish Abilities & Traits ✅
- ✅ Added fish traits: evasive, slippery, magnetic, camouflage
- ✅ Added rods with counter-abilities: SharpHook, Precision, FishFinder, Lure
- ✅ Each trait affects fishing mechanics (escape chance, catch difficulty)

### Iteration 2.2 – Fishing Minigames ✅
- ✅ Added quickCast parameter for optional minigame functionality
- ✅ Created interaction models for button-based fishing

### Iteration 2.3 – Fish Weight & Size ✅
- ✅ Generate random weight based on size and rarity for each fish
- ✅ Added biggest fish tracking per species in player profiles
- ✅ Added leaderboard for biggest catches 

### Iteration 2.4 – Fish Lore & Catalog ✅
- ✅ Added FishDex command to show discovered species and info
- ✅ Show detailed fish stats including size, weight, and traits

### Iteration 2.5 – Multiplayer Fishing ✅
- ✅ Allow players to fish at the same spot
- ✅ Added cooperative bonuses for fishing together

## Phase 3: Passive Fishing & Traps ✅

### Iteration 3.1 – Deployable Fish Traps ✅
- ✅ DeployTrap command with configurable timer (1-24 hours)
- ✅ CheckTrap command that yields caught fish after duration
- ✅ Passive fishing mechanics with realistic catch rates
- ✅ Bait compatibility and efficiency bonuses
- ✅ Shop integration with purchasable trap items

### Iteration 3.2 – Trap Crafting & Bait ✅
- ✅ Different trap types (Basic, Shallow, Deep, Reinforced)
- ✅ Trap durability system with wear over time
- ✅ Crafting system for creating advanced traps
- ✅ Material requirements and crafting recipes
- ✅ Trap repair system using materials
- ✅ Enhanced bait types with specialized bonuses

## Setup Instructions

### Prerequisites
- .NET 8.0 SDK
- Discord Bot Token

### Configuration
1. Clone the repository
2. Copy `appsettings.json` and update the Discord token:
```json
{
  "Discord": {
    "Token": "YOUR_BOT_TOKEN_HERE"
  }
}
```

### Running the Bot
```bash
cd src/FishChamp
dotnet run
```

## Available Commands

### Fishing Commands
- `/fish` - Quick fishing at your current fishing spot
- `/fishing cast` - Cast your fishing line at your current spot (requires being at a fishing spot)
- `/fishing profile` - View your fishing profile and stats
- `/fishing fishdex` - View your discovered fish species catalog
- `/fishing fish-together` - Start or join a multiplayer fishing session
- `/fishing leaderboard` - View the biggest fish leaderboard

### Map Commands  
- `/map current` - View your current area and fishing spots
- `/map goto <fishing spot>` - Go to a specific fishing spot in your area
- `/map travel <area>` - Travel to a connected area
- `/map areas` - List all available areas

### Inventory Commands
- `/inventory view` - View your complete inventory
- `/inventory fish` - View only your fish collection

### Trap Commands (NEW - PHASE 3)
- `/trap deploy <hours> [bait]` - Deploy a fish trap for passive fishing (1-24 hours)
- `/trap check` - Check your traps and collect caught fish
- `/trap list` - View your trap deployment history
- `/trap info [type]` - Get information about trap types and properties
- `/trap repair [trap_id]` - Repair damaged traps using materials

### Crafting Commands (NEW - PHASE 3)
- `/craft trap <type>` - Craft traps using materials (basic, shallow, deep, reinforced)
- `/craft recipes` - View all available trap crafting recipes

### Shop Commands
- `/shop browse` - Browse items available in your current area
- `/shop buy <item> [quantity]` - Purchase items from the shop

## Game Features

### Fishing System
- Interactive fishing minigames with timing-based mechanics
- Different fish species in different areas with unique traits
- Fish rarity system (common, uncommon, rare, epic, legendary)
- Rod and bait equipment system with upgrade mechanics
- Experience points and leveling system
- Multiplayer fishing sessions for cooperative play

### Passive Fishing System (NEW - PHASE 3)
- **Fish Traps**: Deploy traps for automated fishing over time
- **Trap Types**: Basic, Shallow Water, Deep Water, and Reinforced traps
- **Trap Durability**: Traps wear out over time and need repair/replacement
- **Bait Compatibility**: Use different baits to improve trap efficiency
- **Crafting System**: Create advanced traps using materials
- **Repair System**: Restore damaged traps with materials

### Areas System
- **Starter Lake**: Perfect for beginners with common fish
- **Mystic Lake**: Mysterious waters with rare fish (unlockable)
- Area-specific shops with unique items and trap materials

### Persistence
- Player profiles with levels, experience, and currency
- Inventory management with fish collection
- Area states with unlockable content

## Architecture

### Modular Design
- Clean separation of concerns with modules
- Repository pattern for data persistence
- Dependency injection for loose coupling

### Extensible Framework
- Easy to add new command modules
- Swappable persistence layer (JSON → PostgreSQL)
- Configurable game mechanics

## Next Steps

Future iterations will add:
- Interactive fishing minigames
- Fish traps and passive fishing
- Aquarium management
- Farming and crafting systems
- Social features and trading