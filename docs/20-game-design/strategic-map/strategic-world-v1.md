# Strategic World V1

Strategic World V1 is the foundation slice for the large-map and `WorldSite` loop. It is not the final complete form of strategic map, people management, strategic-location operation, and hero-led battle.

## Reading Path

1. `../../30-technical-design/world/strategic-world-v1-data-model.md`: definitions, runtime state, resources, facilities, sites, and threats.
2. `strategic-world-v1-actions-and-tick.md`: world actions, resources, production, `WorldTick`, and raid pressure.
3. `../../30-technical-design/world/strategic-world-v1-battle-contract.md`: world-to-battle request/result handoff.
4. `strategic-world-v1-ui-flow.md`: strategic map UI, site panel, action preview, and feedback.
5. `../../30-technical-design/world/strategic-world-v1-implementation.md`: implementation phases and acceptance gates.
6. `../../60-qa/testcases/strategic-world-v1.md`: manual and regression checks.

## Role In Current Direction

V1 should support:

```text
large map state and movement
-> enterable WorldSite
-> site operation / garrison / facility state
-> battle request
-> battle result writeback
```

The future battle runtime target is hero-led light RTS. V1 should preserve request/result writeback and site-local placement authority while the battle runtime changes.

## Core Goals

- The player can read changing strategic-map state.
- The player can enter a `WorldSite` local map.
- A `WorldSite` can preserve facilities, garrison, memory, threats, and damage.
- World armies can move and trigger site attack/defense or field contact.
- Battle entry is expressed as `BattleStartRequest`.
- Battle result is consumed as `BattleResult`.
- World, site, army, threat, facility, and resource state can change after result writeback.

## Non-Goals

V1 is not:

- a full city builder;
- a full open-world exploration game;
- a full 4X;
- a pure strategic conquest game;
- a complete officer-social simulation;
- a complete auto battle system.

## Player Experience Loop

```text
inspect strategic map
-> choose site / army / threat
-> move, attack, defend, scout, assign, build, or wait
-> enter WorldSite or launch battle when relevant
-> receive result and changed world state
-> choose next strategic action
```

## WorldSite Requirements

`WorldSite` should be concrete enough to support the new product direction:

- authored local map;
- facility slots;
- garrison and unit placement;
- deployment cells and entrances;
- local resources and memory;
- battle-relevant state snapshot;
- result writeback after battle or auto-resolution.

It should not degrade into only a right-side menu or a disposable battle scene.

## Battle Integration Requirements

V1 battle integration must preserve:

- `WorldBattleRequestBuilder`;
- `BattleStartRequest`;
- `BattleSessionHandoff`;
- `BattleResult`;
- `WorldBattleResultApplier`;
- `WorldSiteState.UnitPlacements` as site-local deployment authority.

The battle runtime behind the handoff is transitional. Do not restore legacy manual/AP battle behavior or treat pure auto battle playback as product identity; future-facing work should align with hero-led light RTS authority.

## First Acceptance Bar

- Player can occupy Bonefield from the strategic map.
- Bonefield becomes a persistent player-held `WorldSite`.
- Graveyard Raid can threaten Bonefield.
- Defense can launch through the battle handoff.
- Battle or auto-resolution can update ownership, damage, garrison, threat, and resources.
- Returning to strategic map shows the changed state.
