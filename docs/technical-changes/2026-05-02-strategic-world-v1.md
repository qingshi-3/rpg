# 2026-05-02 Strategic World V1

## Background

The project direction has converged from isolated battle demos and world prototypes into:

```text
strategic world map
+ persistent scene states
+ minimal settlement operation
+ battle handoff/result writeback
+ enemy raid pressure
```

The stable V1 design entry is `docs/design/world/strategic-world-v1.md`.

Detailed implementation references:

- `docs/design/world/strategic-world-v1-data-model.md`
- `docs/design/world/strategic-world-v1-actions-and-tick.md`
- `docs/design/world/strategic-world-v1-battle-contract.md`
- `docs/design/world/strategic-world-v1-ui-flow.md`
- `docs/design/world/strategic-world-v1-implementation.md`
- `docs/testcases/strategic-world-v1.md`

## Goal

Implement the first playable strategic-world slice:

```text
Player Camp, Bonefield, and Graveyard as site nodes on a strategic map surface
```

The player operates three resources and three facility types:

- `population`
- `economy`
- `stone`
- `barracks`
- `mine`
- `defense_tower`

The slice must prove that world operation changes battle conditions and battle results change world state.

## Non-goals

- Full city building.
- Full open world.
- Full 4X AI.
- Deep emotion-system integration.
- Large content production.
- Battle flow, AP, or TurnSystem rewrites.

## Affected Areas

Expected new or changed areas:

```text
src/Definitions/World/
src/Domain/World/
src/Application/World/
src/Application/Battle/
src/Presentation/World/
docs/design/world/
docs/roadmap/
docs/testcases/
```

Existing battle runtime should remain structurally intact. World-to-battle integration should use request/result contracts.

## Implementation Plan

1. Add generic world resources, facilities, scenes, actions, and threat definitions.
2. Add `StrategicWorldState` with scene states, resources, facility instances, garrison, threats, and world tick.
3. Add generic world action resolution for build, train, occupy, and tick production.
4. Add a strategic map presentation for Player Camp, Bonefield, and Graveyard.
5. Upgrade battle handoff from `contextId + encounterId` toward `BattleStartRequest`.
6. Add result application from battle back into `StrategicWorldState`.
7. Add facility-based battle modifiers for defense tower and garrison.
8. Add Graveyard-to-Bonefield Raid threat countdown.
9. Add save/load for strategic world state.
10. Add test cases for world action, battle writeback, raid progression, and persistence.

## Migration Notes

Earlier prototypes have been superseded by the Strategic World V1 route. The active scene structure is now:

```text
scenes/world/StrategicWorldRoot.tscn
scenes/world/sites/WorldSiteRoot.tscn
scenes/world/sites/impl/
scenes/world/site_interactions/
```

| Current code | Treatment |
|---|---|
| Earlier campaign map, overmap, and free-exploration prototypes | Removed from current main-flow scenes and scripts |
| Earlier campaign runtime state | Replaced by `StrategicWorldState` |
| `BattleSessionHandoff` | Extend short term, replace with request/result contract long term |
| `BattleRoot` | Renamed/replaced by `WorldSiteRoot`; battle subsystems keep `Battle*` naming where they are tactical runtime components |

## 2026-05-02 End-of-Day Status

Implemented first playable vertical slice pieces:

- Added V1 strategic state, definitions, action resolver, world tick, threat service, save/load service, and runtime singleton.
- Added `StrategicWorldRoot` for the first strategic map slice: Player Camp, Bonefield, and Graveyard.
- Added `BattleStartRequest` / `BattleResult` handoff and battle result writeback to `StrategicWorldState`.
- Added site mode states: `Peacetime`, `Alert`, `Wartime`, `Aftermath`.
- Renamed the tactical scene shell from `BattleRoot` to `WorldSiteRoot`.
- Battle end now stays inside `WorldSiteRoot` and switches to site operation mode instead of immediately returning to the strategic map.
- First site operation mode is implemented as in-scene UI:
  - top resource bar,
  - return-to-map button,
  - right-side operation panel,
  - facility status list,
  - generic action buttons using `WorldActionViewModel`.
- Site operation mode can execute current V1 actions such as mine activation, defense tower construction, militia training/assignment, wait tick, and threat handling when available.

Known limitations:

- Facility buildings do not yet have authored site interaction entities. When real site maps are built, use `scenes/world/site_interactions/` entities bound to stable slot ids instead of UI buttons.
- Non-battle site operation is functionally in-scene, not yet a rich base-management presentation.
- Large-world AI is still only the V1 tick-based Graveyard Raid pressure.
- Persistent tactical map object memory, such as killed NPCs or destroyed map objects, is not yet modeled beyond strategic site/facility/garrison/threat state.

## Architecture Rules

- Resources use dictionary-style resource ids, not fixed fields.
- Facilities use definition + instance, not hardcoded building methods.
- World owns scene state; Battle consumes start data and returns result data.
- Battle modifiers are generated from world state but resolved by battle runtime.
- Enemy raids modify world state or trigger battle; they do not operate battle nodes directly.

## Risks

- Historical world prototypes must not be restored as main-flow architecture; doing so would return world logic to a location-button prototype.
- If facility behavior is hardcoded, future resources and buildings will require repeated rewrites.
- If battle result remains only victory/defeat, persistent scenes cannot work.
- If enemy raids are real-time instead of tick-based, pressure will become hard to tune.

## Acceptance Checks

- The player can see population, economy, and stone.
- The player can build or activate barracks, mine, and defense tower through generic actions.
- Bonefield can be occupied and kept as a persistent scene.
- Mine produces stone after world ticks.
- Graveyard can generate a raid against Bonefield.
- Defense tower and garrison change raid or defense-battle outcome.
- Battle victory and failure both write back different world states.
- Save/load restores resource, facility, ownership, and threat state.
