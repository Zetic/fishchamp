🎮 Game Structure Overview
A multiplayer, socially-driven fishing game with exploration, farming, crafting, and customization, all controlled via Discord.

🧱 Core Systems & Modules

1. Player System
- Profiles: Track name, inventory, currency, current area, house, boat, aquarium, etc.
- Persistence: JSON for early development, scalable to PostgreSQL.

2. World & Map System
- Areas: Each area has a name, description, fishing spots, farming spots, and unlock criteria.
- Navigable Maps: Use message buttons or select menus to navigate between zones.
- Example: Area: Mystic Lake → spots: Land Dock, Deep Waters, Hidden Farm Patch.

3. Fishing System
- Fishing Spots: Water or land-based.
- Minigame Mechanics:
    - Time-based reaction (e.g., "Click 🐟 when it appears in 2s!")
    - Quick combo input (e.g., “🎣 tap sequence: 🔼🔽⬅️➡️”)
    - RNG + skill modifiers.

4. Passive Fishing (Fish Traps)
- Deploy in certain areas, catch fish after real-time delay.
- Discord timers with reminders: “Your fish trap caught something!”

5. Aquariums
- Customizable tanks, store and withdraw fish as well as decoration
- Fish happiness for breeding.
- Maintenance required (e.g., clean algae, maintain temperature).
- Breeding system (compatible species + mood level).
- Decorations give fish happiness

6. Farming System
- Spots per Area: Tilled soil for planting.
- Crops: Used in bait crafting or cooking.
- Digging for Worms: Emoji mini-games to “search” for live bait.

7. Cooking System
- Recipe system: Combine crops into meals.
- Buffs: Enhance fishing minigame or fish attraction.

8. Boats
- Unlock deeper water fishing areas.
- Collect crafting materials (wood, coral, ores).
- Upgrade system: Speed, cargo, durability.

9. Housing & Plots
- Buyable land in some areas.
- Build customizable houses:
     - Buildable and expandable with materials from boats
     - Using crafting materials for objects in house
     - Decoration slots
     - Interactable Storage
     - Etc..

10. Shops
- Per-area shops.
- Currency: Fish Coins, special tokens for rare items.
- Categories: Rods, Bait, Aquariums, Boats, Seeds, Decorations.

11. Crafting
- Combine materials for furniture, traps, bait, decorations.

💬 Social Interactions
- Fishing Together: Share a spot, compete or collaborate.
- Trading: Fish, rods, decorations.
- Guilds: Fishing clubs for tournaments.
- Leaderboards: Biggest catch, most unique fish, richest player.

🛠️ Technical Stack
Language: C#
Discord Library: Remora.Discord
Persistence: JSON initially, upgrade to PostgreSQL
Scheduling: native task queues
Concurrency: Use stateful bot architecture
