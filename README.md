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

## Game Features

### Areas System
- **Starter Lake**: Perfect for beginners with common fish
- **Mystic Lake**: Mysterious waters with rare fish (unlockable)

### Fishing System
- Simple RNG-based fishing with 70% success rate
- Different fish species in different areas
- Experience points and leveling system

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