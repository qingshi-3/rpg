# WorldSite Runtime Split

## Purpose

This document defines how to shrink `WorldSiteRoot` without losing the strategic-world, site operation, deployment, exploration, and battle handoff behavior that already exists.

`WorldSiteRoot` is currently the shared shell for site management, deployment, exploration, battle entry, entity placement, and post-battle reconciliation. It should become a composition root that binds scene nodes and delegates to focused owners.

## Architecture Judgment

System: `WorldSite`, with boundaries to `Strategic World`, `Auto Battle`, `UI`, and `Content Pipeline`.

Existing authority to preserve:

- `WorldSiteState` owns persistent site state.
- `WorldSiteState.UnitPlacements` owns site-local deployment rows.
- `WorldBattleRequestBuilder` creates `BattleStartRequest`.
- `WorldBattleResultApplier` applies `BattleResult`.
- `WorldSiteRoot` currently projects state and should not remain a domain owner.

## Target Owners

| Owner | Reads | Writes | Does Not Own |
|---|---|---|---|
| `WorldSiteMapRuntime` | Authored site scene, `BattleGridMap`, render anchors | Runtime map references only | Facilities, deployment, battle rules |
| `WorldSiteRuntimeDeploymentCacheBuilder` | Active grid, direction, terrain tags | Candidate cache value | `WorldSiteState.UnitPlacements` |
| `WorldSiteManagementPresenter` | `WorldSiteState`, `WorldSiteDefinition`, resources, threats | UI projection only | Facility business rules |
| `WorldSiteDeploymentController` | `WorldSiteState.UnitPlacements`, cache, definitions, input | Unit placement rows through service APIs | Battle simulation |
| `WorldSiteExplorationController` | Exploration state, grid, patrol definitions | Exploration state through service APIs | Battle turns or AP |
| `WorldSiteBattleLauncher` | Site state, request builder, placement service | Handoff request and logs | Battle runtime or result application |
| `AutoBattleRuntimeController` | `BattleStartRequest`, runtime entities, grid | Runtime battle state and `BattleResult` | World persistence |
| `AutoBattleReportBuilder` | Battle events and result | Readable report DTO/UI model | Strategic writeback |

## Extraction Order

1. Extract deployment cache construction first.
2. Extract deployment selection and drag/drop validation.
3. Extract management panel presentation.
4. Extract exploration movement, patrol alert, and encounter launch.
5. Extract battle launch validation and request preparation.
6. Add auto battle runtime and report owners.
7. Remove legacy manual battle ownership from `WorldSiteRoot` after replacement coverage exists.

The first implementation of step 2 should start with deployment target validation and move writes, not full input/HUD extraction. Plans: `10-deployment-target-evaluator-implementation-plan.md`, then `11-deployment-terrain-reconciler-implementation-plan.md` for placement terrain reconciliation, then `12-battle-deployment-preparer-implementation-plan.md` for battle-entry deployment preparation, then `13-battle-launcher-implementation-plan.md` for battle handoff and rollback.

Auto battle scene entry begins with `18-worldsite-auto-battle-adapter-implementation-plan.md`: `WorldSiteRoot` gains only an opt-in adapter call and keeps the legacy manual activation path intact until playback UI and presentation ownership are ready.

## Rules For Each Extraction

- Move one responsibility at a time.
- Keep public behavior unchanged until the target owner is verified.
- Do not copy private helper logic into multiple places.
- If an extracted owner needs data currently hidden inside `WorldSiteRoot`, pass it through a focused dependency or value object.
- Runtime caches are not persistent state.
- A service may validate and write `WorldSiteState.UnitPlacements`; UI code may not become an alternate authority.

## Logging

Keep low-noise logs for state transitions and failures:

- cache built;
- placement prepared, rejected, relocated, or moved;
- battle request accepted or rejected;
- auto battle started, paused, skipped, completed;
- report built;
- result applied.

Do not log per-frame movement or every tick.

## Acceptance

- `WorldSiteRoot` mostly wires dependencies and delegates.
- Each extracted owner can be read without loading the full root file.
- No extracted owner directly mutates world state outside its contract.
- Deployment, exploration, management, and battle launch can be tested or smoke-checked independently.
