# Code Refactor Design

Status: Accepted On 2026-05-17

## Authority

This refactor design implements the accepted expected architecture in:

- `expected/system-design/hero-led-light-rts-system-architecture.md`

The expected architecture is proposal-scoped authority after user design acceptance on 2026-05-17. This document does not change gameplay rules. If implementation reveals a required architecture change, pause and update the expected architecture for user acceptance before continuing.

## Current Code Reading

The project already has a useful vertical shape:

```text
src/Definitions
src/Domain
src/Application
src/Presentation
src/Infrastructure
```

Keep that shape. Do not start with a directory-wide rewrite.

Current battle and strategic code also has several old-model anchors:

| Area | Current Shape | Refactor Meaning |
|---|---|---|
| Battle request/result | `BattleStartRequest`, `BattleForceRequest`, `BattleResult`, `BattleForceResult` model force counts and garrison/army sources. | Treat as legacy-compatible input/output. It is not the final `BattleStartSnapshot` / `BattleOutcomeResult` contract. |
| Handoff | `BattleSessionHandoff` is a static active-request bridge between world and battle. | Replace with an explicit battle session service after the new snapshot/result contracts exist. |
| Auto battle | `Application/Battle/Auto` simulates unit-count battles and builds `AutoBattleReport`. | Keep only as a removable migration adapter or test oracle. It must not become the target Runtime authority. |
| World state | `StrategicWorldState`, `WorldSiteState`, `WorldArmyState`, `GarrisonState` are the current long-term strategic state. | Reuse strategic location/resource pieces, but introduce `BattleGroupState`, `HeroState`, and `CorpsState` instead of extending garrison counts into the new core identity. |
| Presentation battle | `Presentation/Battle` owns Godot entities, grid reading, action visuals, cues, damage numbers, and unit factories. | Reuse scene/presentation pieces as adapters. Rules, settlement, and report truth move to Application/Runtime contracts. |
| Tests | Console regression projects already guard auto battle, deployment, world movement, intel, and presentation behavior. | Add contract-style tests before changing core flows. Existing tests should remain green during migration unless an accepted plan replaces a behavior. |

## Refactor Strategy

Use an incremental strangler refactor:

```text
add target contracts and tests
-> build adapters from old WorldSite/WorldArmy/Garrison data
-> run one new battle-group vertical slice
-> move UI and scene handoff onto the new service
-> retire old request/result/auto-battle authority after replacement
```

Rejected alternatives:

- Big-bang rewrite: too much scene, save, and battle state risk at once.
- Rename old `WorldArmy/Garrison/BattleForce` into new names: preserves the wrong model under better names.
- Grow `AutoBattle` into target Runtime: conflicts with the accepted light-RTS command/runtime architecture.

## Target Module Map

Add target modules beside existing code. Old modules stay available until their replacement path is working.

| Layer | Target Paths | Responsibility |
|---|---|---|
| Definitions | `src/Definitions/Heroes`, `src/Definitions/Corps`, `src/Definitions/Equipment`, existing `src/Definitions/Battle/Abilities` | Godot `Resource` definitions for hero, corps, equipment, ability/effect content, tags, and text identity. |
| Domain | `src/Domain/Heroes`, `src/Domain/Corps`, `src/Domain/BattleGroups`, existing `src/Domain/World` | Saveable long-term state and invariants. No Godot scene nodes, runtime target locks, cooldown frames, or settlement computation. |
| Application | `src/Application/BattleGroups`, `src/Application/Battle/Snapshots`, `src/Application/Battle/Commands`, `src/Application/Battle/Settlement`, `src/Application/Battle/Reports` | Use-case orchestration: create/station/sortie battle groups, build snapshots, validate commands, settle results, generate reports. |
| Runtime | `src/Runtime/Battle` | Discardable battle execution state: actors, commands, cooldowns, tactical AI, event stream, termination reason. No direct `StrategicWorldState` mutation. |
| Presentation | existing `src/Presentation/Battle`, existing `src/Presentation/World` | Scene nodes, HUD, input, animation, feedback, and report display. UI submits requests and displays facts; it does not own rules or settlement. |
| Infrastructure | existing `src/Infrastructure`, save/load support | Resource loading, logging, IDs, random seed, save serialization, diagnostics, test fixtures. No gameplay decisions. |

Do not rename `WorldSite` globally in the first phase. It can remain the technical strategic-location abstraction while new battle-group state and contracts are introduced.

## First Target Contracts

The first implementation plan should introduce only the minimal contracts needed for one vertical slice.

### Long-Term State

```text
HeroState
CorpsState
BattleGroupState
EquipmentInstance / EquipmentAssignment
```

Rules:

- `BattleGroupState` is the selectable/sortie identity: one hero plus one main corps.
- `CorpsState.CorpsStrength` uses the accepted 0-100 range.
- Visible soldiers are never saved as independent long-term units.
- Existing `WorldSiteState` and resources remain strategic-location state; they do not own hero/corps growth.

### Snapshot And Result

```text
BattleStartSnapshot
BattleGroupSnapshot
LocationBattleContext
BattleEvent
BattleEventStream
BattleOutcomeResult
SettlementPlan
StateDeltaSet
BattleReportRecord
```

Rules:

- Snapshot builders read Domain and Definitions through Application services.
- Runtime receives snapshots and emits events/results.
- Settlement writes long-term state only from complete results with explicit termination reasons.
- Report reads the same events/results as Settlement.

### Commands

```text
CommandRequest
CommandValidationResult
RuntimeOrder
CommandEvent
```

Rules:

- Command channels are explicit: hero, corps, combined.
- UI may run basic availability hints, but Application owns ownership/channel validation.
- Runtime owns target/reachability acceptance and execution state.
- Runtime rejections enter the event stream when they explain battle state.

## First Vertical Slice

The first code refactor should prove this path:

```text
definitions + long-term state
-> create/station one BattleGroupState
-> build BattleStartSnapshot
-> run a minimal Runtime session with hero/corps actors
-> emit BattleEventStream + BattleOutcomeResult
-> build SettlementPlan and StateDeltaSet
-> build BattleReportRecord from the same facts
```

Recommended sample scope:

- 1 city / strategic location;
- 1 target site;
- 3 hero definitions;
- 3 corps definitions;
- 1 equipment sample set;
- 1 minimal battle map context;
- movement, attack, hold, retreat, and one minimal hero skill hook;
- basic report sections: outcome, contribution, corps strength loss, command facts, rewards, failure candidates.

## Adapter Rules

Adapters are allowed only at boundaries:

| Adapter | Purpose | Delete Condition |
|---|---|---|
| `WorldArmy/Garrison -> BattleGroupState` migration adapter | Seed first demo battle groups from current strategic data. | Removed when save/content authoring natively creates battle groups. |
| `BattleStartRequest -> BattleStartSnapshot` adapter | Let old launchers exercise the new snapshot path while UI migration is incomplete. | Removed when scene launch uses the new battle session service directly. |
| `BattleOutcomeResult -> BattleResult` adapter | Keep existing world-result appliers alive during settlement migration. | Removed when `SettlementPlan` / `StateDeltaSet` replaces old appliers. |
| `AutoBattle` adapter | Optional deterministic fallback for tests only. | Removed when Runtime skeleton produces target events/results for the covered scenarios. |

Adapters must log explicit conversion decisions and missing mappings. They must not become second authorities.

## Migration Boundaries

Preserve:

- existing console regression test style;
- authored `.tscn`, `.tres`, `.gdshader`, and reusable scene resources;
- `WorldSite` as a technical abstraction for strategic locations;
- existing presentation components that only render or animate state.

Do not preserve as target authority:

- `BattleForceRequest` as the main battle identity;
- `GarrisonState.Count` as long-term corps identity;
- static `BattleSessionHandoff` as final session ownership;
- `AutoBattle` report as final report truth;
- UI action executors as battle-rule authority.

## Test And Verification Design

Add a new regression project:

```text
tests/TargetBattleArchitectureRegression
```

First tests should guard:

- Runtime source files do not reference `StrategicWorldState`, `WorldSiteRoot`, Godot UI controls, or save services.
- Domain state files do not reference Godot scene nodes or Runtime classes.
- Snapshot builders copy required facts and do not pass live Domain objects into Runtime.
- Settlement rejects incomplete results and mismatched snapshot/result IDs.
- Report and Settlement consume the same event IDs.
- Command validation distinguishes UI hint rejection, Application rejection, and Runtime rejection.
- `CorpsStrength` remains clamped to 0-100 and visible soldier count is derived, not saved.

Use low-concurrency verification:

```text
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Run broader existing regression projects after each migration phase, not after every small file edit.

## First-Phase Acceptance

The first refactor phase is acceptable when:

- target definitions, domain state, snapshot, command, runtime event/result, settlement, and report skeletons exist;
- one battle group can be created, stationed, snapshotted, resolved through the new runtime skeleton, settled, and reported;
- Runtime cannot mutate long-term state directly;
- Presentation cannot compute settlement/report truth;
- old `AutoBattle` and old `BattleStartRequest/BattleResult` paths are clearly marked as migration adapters or still-isolated legacy paths;
- existing regression tests remain green or any intentional break has an accepted replacement test and migration note.

## Next Planning Step

After user review of this refactor design, create a detailed implementation plan with task-sized steps. The plan should start with boundary tests, then introduce target contracts, then add adapters, then wire the first vertical slice.
