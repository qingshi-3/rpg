# Exploration World

The world layer uses a hub-and-dungeon structure with local free movement.

It is not an open-world simulation. Towns, dungeons, and rooms are authored locations connected by data-driven entrances.

## Direction

- Players move freely inside the current location.
- NPCs, story beats, entrances, and encounters are authored content.
- Town and Dungeon are sibling `WorldLocation` types.
- Locations do not directly load or hold each other.
- `WorldRoot` and `WorldFlow` own location switching.
- Battle remains a separate runtime flow.

World flow follows the project dependency rules in `docs/design/systems/project-architecture.md`: upper-level flow code interacts with `WorldLocation` abstractions, requests, results, and definitions, not concrete town or dungeon implementations.

## Scene Architecture

```text
WorldRoot.tscn
├─ LocationRoot
│  └─ active WorldLocation scene
├─ Player
├─ Camera2D
├─ WorldUI
└─ TransitionOverlay
```

Current sample scenes:

- `scenes/world/WorldRoot.tscn`
- `scenes/world/locations/WorldLocationBase.tscn`
- `scenes/world/locations/TownVillage01.tscn`
- `scenes/world/locations/DungeonGraveyard01.tscn`

Current sample registry:

- `assets/world/locations/world_location_registry.tres`

## Location Contract

Every `WorldLocation` scene should expose these child roots:

- `SpawnPoints`: contains `WorldSpawnPoint` nodes.
- `Entrances`: contains `WorldEntrance` nodes.
- `NPCs`: contains local NPC content.
- `Interactables`: contains local interactable content.
- `EncounterTriggers`: contains battle or event triggers.

This mirrors the battle-side idea of a stable runtime shell plus swappable concrete content scenes.

Concrete town, dungeon, and room scenes should inherit from `WorldLocationBase.tscn` when practical, so common child roots and lifecycle wiring stay consistent.

## Switching Rule

`WorldEntrance` emits only data:

- `target_location_id`
- `target_spawn_id`

`WorldRoot` receives that request, looks up `WorldLocationRegistry`, unloads the active location, loads the target scene, and moves the player to the target spawn point.

Town and Dungeon scenes must not directly reference or instantiate each other.

## Battle Handoff

`WorldEncounterTrigger` currently emits an `encounter_id` and `WorldRoot` logs it.

Future battle handoff should use:

```text
WorldEncounterTrigger
  -> BattleStartRequest
  -> BattleRoot
  -> BattleResult
  -> WorldRoot / WorldState
```

The world layer may choose the battle, party, reward, and return point. It must not modify Battle flow, AP, or TurnSystem.

## Phase 1 Scope

- One town sample.
- One dungeon sample.
- Data-driven entrance switching.
- Placeholder NPC and encounter trigger nodes.
- Local free movement with a simple player controller.

Out of scope:

- Quest system.
- Dialogue system.
- Shop/rest UI.
- Save/load world state.
- Battle result application.
- Open-world simulation or emergent NPC behavior.
