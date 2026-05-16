# Auto Battle Runtime Controller Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a minimal application-layer `AutoBattleRuntimeController` that starts an active auto battle handoff, builds the report, and exposes playback-style controls over the generated event feed.

**Architecture:** Keep this slice below Godot UI and scene presentation. The controller composes `AutoBattleSessionRunner` and `AutoBattleReportBuilder`; it runs the current pure simulation through `BattleSessionHandoff`, stores the simulation/report output, and owns a deterministic playback cursor for start, pause, resume, speed, skip, completion, and failed-start state. It does not instantiate units, animate scenes, mutate world state, or replace `WorldSiteRoot` yet.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleSessionHandoff`, `AutoBattleSessionRunner`, `AutoBattleReportBuilder`, console regression project under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/15-auto-battle-session-runner-implementation-plan.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/16-auto-battle-report-builder-implementation-plan.md`
- `src/Application/Battle/Auto/AutoBattleSessionRunner.cs`
- `src/Application/Battle/Auto/AutoBattleReportBuilder.cs`
- `tests/AutoBattleRuntimeRegression/Program.cs`

## Scope Boundaries

- Do not wire the controller into `WorldSiteRoot`, `StrategicWorldRoot`, battle scenes, HUD, camera, animation, or authored UI resources in this slice.
- Do not create a Godot `Node`, `.tscn`, theme, or UI control.
- Do not introduce incremental combat simulation or scene entity movement yet. Playback cursor advances over already generated runtime events.
- Do not mutate `StrategicWorldState`, `WorldSiteState`, deployment state, or result writeback.
- Do not add dependencies on `BattleTurnController`, AP, `TurnSystem`, `BattleActionMenu`, or manual command flow.

## File Structure

- Create `src/Application/Battle/Auto/AutoBattleRuntimePhase.cs`
  - Runtime controller phase enum: idle, playing, paused, completed, failed.
- Create `src/Application/Battle/Auto/AutoBattleRuntimeControllerConfig.cs`
  - Playback tuning such as seconds per report event.
- Create `src/Application/Battle/Auto/AutoBattleRuntimeController.cs`
  - Starts active battle, holds simulation/report output, reveals report events through playback cursor, handles pause/resume/speed/skip.
- Modify `tests/AutoBattleRuntimeRegression/Program.cs`
  - Add controller tests and source guards.
- Modify documentation routes:
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`
  - `docs/60-qa/testcases/auto-tactics-migration.md`

## Task 1: Add Failing Controller Tests

**Files:**
- Modify: `tests/AutoBattleRuntimeRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add:

```csharp
Run("auto battle runtime controller starts active battle and reveals report events", AutoBattleRuntimeControllerStartsActiveBattleAndRevealsReportEvents);
Run("auto battle runtime controller pause speed and skip control playback cursor", AutoBattleRuntimeControllerPauseSpeedAndSkipControlPlaybackCursor);
Run("auto battle runtime controller reports failed start without consuming handoff", AutoBattleRuntimeControllerReportsFailedStartWithoutConsumingHandoff);
```

- [ ] **Step 2: Add start/playback test**

The test should:

- begin a valid active `BattleSessionHandoff` request;
- create an `AutoBattleRuntimeController` using deterministic config;
- call `StartActiveBattle(out string failureReason)`;
- assert phase is `Playing`, report is present, simulation is present, failure reason is empty, and `BattleSessionHandoff.HasActiveLaunch` is false;
- call `AdvancePlayback(1.0)` with `SecondsPerReportEvent = 0.5`;
- assert at least two feed events are visible;
- call `AdvancePlayback(99.0)` and assert phase becomes `Completed` and all report feed events are visible.

- [ ] **Step 3: Add pause/speed/skip test**

The test should:

- start a valid controller;
- advance `0.5` seconds and record visible event count;
- pause and advance `10.0` seconds, asserting visible count does not change;
- resume, set speed to `2.0`, advance `0.5` seconds, and assert visible count increases by at least two events;
- call `SkipToEnd()` and assert phase is `Completed` and all report feed events are visible.

- [ ] **Step 4: Add failed-start test**

The test should:

- begin a request with missing preferred placement;
- call `StartActiveBattle(out failureReason)`;
- assert it returns false, phase is `Failed`, `FailureReason` contains `auto_battle_missing_preferred_placement`, report is null, and active handoff remains unresolved;
- cancel the handoff at the end.

- [ ] **Step 5: Extend source guard**

Require auto battle source to avoid `WorldSiteRoot`, `StrategicWorldState`, `BattleTurnController`, `TurnSystem`, `ActionPoint`, `BattleActionMenu`, `Godot.Control`, and `Node`.

- [ ] **Step 6: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `AutoBattleRuntimeController`, `AutoBattleRuntimeControllerConfig`, and `AutoBattleRuntimePhase` do not exist yet.

## Task 2: Implement Runtime Controller DTOs

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleRuntimePhase.cs`
- Create: `src/Application/Battle/Auto/AutoBattleRuntimeControllerConfig.cs`

- [ ] **Step 1: Add enum and config**

`AutoBattleRuntimePhase` values:

- `Idle`
- `Playing`
- `Paused`
- `Completed`
- `Failed`

`AutoBattleRuntimeControllerConfig`:

- `SecondsPerReportEvent`, default `0.5`, clamped by controller to a positive value.

- [ ] **Step 2: Run focused test**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile still fails because `AutoBattleRuntimeController` is missing.

## Task 3: Implement `AutoBattleRuntimeController`

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleRuntimeController.cs`

- [ ] **Step 1: Add constructor and state**

Constructor dependencies:

```csharp
public AutoBattleRuntimeController(
    AutoBattleSessionRunner sessionRunner = null,
    AutoBattleReportBuilder reportBuilder = null,
    AutoBattleRuntimeControllerConfig config = null)
```

Expose read-only state:

- `Phase`
- `PlaybackSpeed`
- `FailureReason`
- `SimulationResult`
- `Report`
- `VisibleEventCount`
- `VisibleEventFeed`

- [ ] **Step 2: Add `StartActiveBattle(out string failureReason)`**

Behavior:

- reset prior state;
- call `sessionRunner.TryRunActiveBattle`;
- on failure: set phase `Failed`, copy failure reason, keep report/simulation null, return false;
- on success: build report, set phase `Playing`, set visible event count `0`, return true.

- [ ] **Step 3: Add playback controls**

Methods:

- `Pause()`: `Playing -> Paused`;
- `Resume()`: `Paused -> Playing`;
- `SetPlaybackSpeed(double speed)`: clamp to at least `0.1`;
- `AdvancePlayback(double deltaSeconds)`: reveal events only while `Playing`; reveal `floor(accumulator / SecondsPerReportEvent)` events; mark `Completed` when all feed events are visible;
- `SkipToEnd()`: reveal all events and set phase `Completed` if report exists.

- [ ] **Step 4: Run focused test**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: all auto battle runtime regression tests pass.

## Task 4: Verification

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Expected:

- all regression projects exit `0`;
- main project build exits `0`;
- known Godot source generator warning may appear in console test projects and does not block this slice.

## Self-Review Checklist

- Controller composes runner and report builder instead of duplicating simulation or report logic.
- Playback controls operate on report event feed only.
- Failed start does not consume or cancel active handoff.
- No Godot node/UI, world-state, AP, `TurnSystem`, or manual command dependency was introduced.
- Legacy battle flow remains unchanged.
