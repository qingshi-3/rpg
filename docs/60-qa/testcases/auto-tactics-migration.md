# Legacy Manual Runtime Retirement Checks

## Purpose

These checks protect the cleanup that retired the old manual tactical battle runtime. They are historical migration guardrails, not the current product battle identity.

Current gameplay authority lives in `gameplay-design/`. Detailed first-migration workstreams remain as historical records in `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/`.

## Battle Identity Checks

- Confirm new battle work follows hero-led light RTS authority before expanding runtime or UI.
- Confirm backend auto-resolve/report code is treated as infrastructure, not as pure no-command product identity.
- Confirm new work does not add TFT-like shop rolls, fair-board economy rounds, or synergy drafting as the main loop.
- Confirm new work does not restore AP, `TurnSystem`, manual action-menu control, command routers, or player-phase battle HUDs.

## Deployment Cache Extraction

- Build a site deployment cache from a grid with north, south, east, west, center, and water cells.
- Confirm direction ordering keeps side-specific entry behavior:
  - north starts from lowest Y;
  - south starts from highest Y;
  - west starts from lowest X;
  - east starts from highest X;
  - any starts near the map center.
- Confirm water cells remain marked as water so non-water units can filter them out.
- Confirm non-top or non-walkable surfaces do not become deployment candidates.
- Confirm surfaces with `MoveCost <= 0` do not become deployment candidates.
- Confirm a null or missing active grid builds an empty deployment cache and does not create hidden fallback placement authority.

## Migration Smoke Checks

- Enter a `WorldSite` without active battle and confirm facility slots and garrison placements still render.
- Start assault, defense raid, and field-intercept style battle requests and confirm unit placements still come from `WorldSiteState.UnitPlacements` or a service that writes there before request launch.
- Finish a battle and confirm `BattleResult.ForceResults` still writes surviving counts back to world state.
- Confirm no new feature depends on adding more logic to `WorldSiteRoot` when a focused runtime owner can own it.

## Backend Auto-Resolve Guard Checks

- Run `dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj` and confirm the pure C# runtime spawns from `BattleStartRequest`, emits structured events, resolves deterministic victory/defeat, and returns `BattleResult.ForceResults`.
- Confirm `AutoBattleSessionRunner` can complete an active `BattleSessionHandoff` with the simulation's full `BattleResult.ForceResults`, and that failed simulations leave the active handoff unresolved instead of fabricating fallback results.
- Confirm `AutoBattleReportBuilder` summarizes outcome, force survival/losses, contribution counters, a concise event feed, and a defeat reason key without reading scene nodes or mutating world state.
- Confirm `AutoBattleRuntimeController` can start an active handoff, expose a report, advance/pause/resume/speed/skip its visible event feed, and report failed starts without consuming the handoff.
- Confirm `WorldSiteAutoBattleAdapter` resolves an active WorldSite battle handoff through auto battle without depending on the retired manual activation path.
- Confirm `AutoBattleReportSummaryFormatter` turns victory, defeat reason, and contribution facts into concise Chinese notice text without depending on Godot UI or world-state mutation.
- Confirm `WorldSiteRoot` appends that summary to the existing site management notice after world writeback without exposing a dead manual/auto toggle.
- Confirm stale manual battle authority docs are deleted rather than kept as deprecated routes.
- Confirm no active scene dependency points at retired manual battle code.
- Start a supported auto battle from a `BattleStartRequest`.
- Confirm one hero and one corps spawn from site-authoritative placement data.
- Confirm backend auto-resolve units move, acquire targets, attack, and resolve outcome without scene or world-state mutation.
- Confirm one hero skill trigger is visible in playback and recorded in the report.
- Pause, resume, speed up, and skip the battle; confirm the final report remains coherent.
- Confirm `BattleResult.ForceResults` is present for player and enemy forces.
- Confirm `WorldBattleResultApplier` applies the result without the battle runtime mutating `StrategicWorldState` directly.
