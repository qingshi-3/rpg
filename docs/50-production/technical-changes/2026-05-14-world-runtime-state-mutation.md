# World Runtime State Mutation

## Background And Goal

The strategic world should keep mutable run state resident in memory while the run is active. Clicking a `WorldSite` in the strategic map should read prepared state and metadata instead of rebuilding heavy runtime information, and returning from battle must write actual world changes back into that resident state.

This change focuses on unit count mutation after site battles. It does not introduce a database or a new persistence layer.

## Contract

- `WorldSiteState` remains the authority for mutable site state: garrison counts, facilities, control state, threats, and unit placements.
- `BattleResult.ForceResults` carries force-level survival counts from battle runtime back to world runtime.
- `WorldBattleResultApplier` uses force results when present and keeps legacy full-force behavior only as compatibility fallback.
- `WorldGarrisonMutationService` owns garrison add/remove count semantics for application-layer world services.

## Non-Goals

- No SQLite or external data store in this slice.
- No full map-object, NPC, tile-damage, or facility-damage delta model yet.
- No battle flow, AP system, or TurnSystem architecture change.

## Follow-Up

The next runtime-state slice should separate resident site state from derived presentation caches more explicitly: authored definitions, resident run state, prepared deployment cache, and disposable scene nodes should have distinct owners and lifetime rules.

## Acceptance Checks

- Assault victory transfers only surviving attacker units into the captured site garrison.
- Defense victory removes only defeated defending garrison units.
- Existing battle-result paths without force results continue to resolve through compatibility behavior.
- World garrison count changes route through a shared mutation service instead of duplicated local helper methods.
