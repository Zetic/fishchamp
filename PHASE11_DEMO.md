# PHASE 11: World Events & Dynamic Encounters - Demo Guide

This document demonstrates the new World Events & Dynamic Encounters functionality implemented in PHASE 11.

## New Features Overview

### 1. Multi-Phase Events
Events can now progress through multiple phases automatically:
- **Dark Skies Event** progresses through: Storm Gathering → Storm Peak → Calm After Storm
- Each phase has unique fish, effects, and duration
- Phase transitions happen automatically based on time

### 2. Dynamic Event Types
New event types that alter core game mechanics:
- **Fishing Frenzy**: +25% bite rate, all rods work anywhere
- **Flood Season**: Dock fishing disabled, boat fishing required
- **Volcanic Unrest**: Lava zones opened, heat-proof rods required
- **Celestial Drift**: Cosmic fish appear in all zones

### 3. World Boss Encounters
Cooperative boss fights requiring multiple players:
- **The Abyssal King**: Requires 3+ players, 1500 HP
- Shared combat commands: `/boss strike`, `/boss hook`, `/boss weaken`
- Damage tracking and leaderboards
- Special rewards for participants

### 4. Enhanced Event Commands

#### Basic Event Commands
- `/event list` - Shows active and upcoming events with phase info
- `/event info <event_id>` - Detailed event info including current phase and effects
- `/event join <event_id>` - Join an event to participate
- `/event progress` - View your progress in active events
- `/event rewards <event_id>` - View and claim event rewards

#### New PHASE 11 Commands
- `/event bosses` - List active world boss encounters
- `/event trigger <type>` - Trigger special events (frenzy, storm, flood, volcano, cosmic, boss)
- `/boss join <boss_id>` - Join a world boss fight
- `/boss strike <boss_id>` - Perform powerful strike attack (45-65 damage)
- `/boss hook <boss_id>` - Use fishing hook as weapon (25-40 damage)
- `/boss weaken <boss_id>` - Weaken boss defenses (15-30 damage)
- `/boss status <boss_id>` - Check boss fight status and leaderboards

## Demo Scenarios

### Scenario 1: Triggering a Fishing Frenzy Event
1. Use `/event trigger frenzy` to start a 6-hour fishing frenzy
2. Check `/event list` to see the active event with effects
3. Go fishing and notice improved bite rates (+25% bonus)
4. Event shows "Event bonus active!" message when fishing

### Scenario 2: Multi-Phase Storm Event
1. Use `/event trigger storm` to start the Dark Skies event
2. Use `/event info <event_id>` to see current phase info
3. Watch as the event progresses through phases:
   - Phase 1 (4h): Storm Gathering - Eerie fish spawn
   - Phase 2 (4h): Storm Peak - Lightning strikes, storm fish
   - Phase 3 (4h): Calm Waters - Bonus rewards
4. Different fish and effects are available in each phase

### Scenario 3: World Boss Encounter
1. Use `/event trigger boss` to spawn The Abyssal King
2. Use `/boss join <boss_id>` to join the fight (need 3+ players)
3. Use combat commands to attack:
   - `/boss strike <boss_id>` for heavy damage
   - `/boss hook <boss_id>` for medium damage  
   - `/boss weaken <boss_id>` for light damage
4. Check `/boss status <boss_id>` to see damage leaderboard
5. Boss is defeated when health reaches 0, rewards distributed

### Scenario 4: Dynamic Event Effects
1. Trigger different event types and notice their effects:
   - **Flood Season**: Dock fishing becomes disabled
   - **Volcanic Unrest**: New lava zones require heat-proof rods
   - **Celestial Drift**: Cosmic fish appear everywhere

## Integration with Fishing System

The new events seamlessly integrate with the existing fishing mechanics:

- **Bite Rate Modifiers**: Events can increase/decrease fish bite chances
- **Event Fish Spawning**: Special fish only appear during specific events/phases
- **Area Restrictions**: Some events disable certain fishing areas
- **Equipment Requirements**: Some events require special gear

## Technical Implementation

### Key Models Added:
- `EventPhase` - Individual phases within events
- `PhaseObjective` - Objectives for phase progression
- `WorldBossEvent` - Cooperative boss encounters
- `EventType` - Different categories of events (Seasonal, Dynamic, WorldBoss, etc.)

### Enhanced Services:
- `EventService` now handles phase progression and world boss timeouts
- `FishingInteractionGroup` checks for active event modifiers
- Event effects are applied dynamically during fishing

### New Commands:
- `WorldBossCommandGroup` - Complete boss battle system
- Enhanced `EventCommandGroup` - Shows phase info and effects

This implementation provides a solid foundation for the complete PHASE 11 feature set while maintaining compatibility with existing systems.