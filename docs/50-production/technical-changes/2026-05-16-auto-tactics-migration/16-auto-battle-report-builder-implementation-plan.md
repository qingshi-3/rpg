# Auto Battle Report Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert auto battle simulation events and `BattleResult.ForceResults` into a testable report model that can later feed playback UI and final battle reports.

**Architecture:** Add `AutoBattleReportBuilder` and report DTOs under `Rpg.Application.Battle.Auto`. The builder reads only `AutoBattleSimulationResult`, `AutoBattleEvent`, and `BattleResult`; it summarizes outcome, force survival/losses, contribution counters, concise event feed rows, and a top failure reason for defeat. It does not render UI, localize display strings, mutate world state, or inspect scene nodes.

**Tech Stack:** Godot 4.5 C#, .NET 8, `AutoBattleSimulationResult`, `AutoBattleEvent`, `BattleResult`, console regression project under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/14-auto-battle-runtime-skeleton-implementation-plan.md`
- `src/Application/Battle/Auto/AutoBattleSimulation.cs`
- `src/Application/Battle/Auto/AutoBattleEvent.cs`
- `src/Application/Battle/BattleResult.cs`
- `tests/AutoBattleRuntimeRegression/Program.cs`

## Scope Boundaries

- Do not create Godot UI, scenes, themes, HUD controls, playback widgets, or final report panels.
- Do not localize player-facing text in this slice. Use stable summary/failure keys so later UI can provide Chinese copy.
- Do not infer casualties from presentation nodes. Casualty facts come from `BattleResult.ForceResults`.
- Do not mutate `StrategicWorldState`, `WorldSiteState`, deployment state, or `BattleSessionHandoff`.
- Do not add dependencies on `WorldSiteRoot`, `BattleTurnController`, AP, `TurnSystem`, or manual command flow.

## File Structure

- Create `src/Application/Battle/Auto/AutoBattleReport.cs`
  - Root report model: outcome, totals, failure reason, force reports, and concise event feed.
- Create `src/Application/Battle/Auto/AutoBattleForceReport.cs`
  - Per-force survival/loss and contribution counters.
- Create `src/Application/Battle/Auto/AutoBattleReportEvent.cs`
  - Concise event feed row with stable summary key and references.
- Create `src/Application/Battle/Auto/AutoBattleReportBuilder.cs`
  - Builds reports from simulation result and force/event facts.
- Modify `tests/AutoBattleRuntimeRegression/Program.cs`
  - Add report builder tests and source guards.

## Task 1: Add Failing Report Builder Tests

**Files:**
- Modify: `tests/AutoBattleRuntimeRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add:

```csharp
Run("auto battle report builder summarizes victory force results and event feed", AutoBattleReportBuilderSummarizesVictoryForceResultsAndEventFeed);
Run("auto battle report builder explains player defeat", AutoBattleReportBuilderExplainsPlayerDefeat);
```

- [ ] **Step 2: Add victory report test**

The test should:

- run the existing two-player-vs-one-enemy deterministic simulation;
- call `new AutoBattleReportBuilder().Build(simulation)`;
- assert `Outcome == BattleOutcome.Victory`;
- assert totals are `InitialUnitCount = 3`, `SurvivedUnitCount = 2`, `DefeatedUnitCount = 1`;
- assert the player force report has `InitialCount = 2`, `SurvivedCount = 2`, `DefeatedCount = 0`, `DamageDealt > 0`, and `AttackCount > 0`;
- assert the enemy force report has `InitialCount = 1`, `SurvivedCount = 0`, `DefeatedCount = 1`;
- assert the event feed contains `battle_started`, `unit_deployed`, `attack_resolved`, `unit_defeated`, and `battle_ended`;
- assert the event feed does not contain movement spam keys.

- [ ] **Step 3: Add defeat report test**

The test should:

- run the existing one-player-vs-two-enemy deterministic simulation;
- build the report;
- assert `Outcome == BattleOutcome.Defeat`;
- assert `TopFailureReason == "player_force_eliminated"`;
- assert the player force report has `SurvivedCount = 0` and `DefeatedCount = 1`;
- assert the enemy force report has `SurvivedCount = 1` and `DefeatedCount = 1`.

- [ ] **Step 4: Extend source guard**

Require auto battle source to avoid `WorldSiteRoot`, `StrategicWorldState`, `BattleTurnController`, `TurnSystem`, `ActionPoint`, `BattleActionMenu`, and `Godot.Control`.

- [ ] **Step 5: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `AutoBattleReportBuilder` and report DTOs do not exist yet.

## Task 2: Implement Report DTOs

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleReport.cs`
- Create: `src/Application/Battle/Auto/AutoBattleForceReport.cs`
- Create: `src/Application/Battle/Auto/AutoBattleReportEvent.cs`

- [ ] **Step 1: Add DTOs**

Use mutable lists internally through simple properties so console tests and future UI adapters can read the same model:

```csharp
public sealed class AutoBattleReport
{
    public BattleOutcome Outcome { get; set; } = BattleOutcome.None;
    public int InitialUnitCount { get; set; }
    public int SurvivedUnitCount { get; set; }
    public int DefeatedUnitCount { get; set; }
    public string TopFailureReason { get; set; } = "";
    public List<AutoBattleForceReport> ForceReports { get; set; } = new();
    public List<AutoBattleReportEvent> EventFeed { get; set; } = new();
}
```

- [ ] **Step 2: Run focused test**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile still fails because `AutoBattleReportBuilder` is missing.

## Task 3: Implement `AutoBattleReportBuilder`

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleReportBuilder.cs`

- [ ] **Step 1: Add builder**

Expose:

```csharp
public AutoBattleReport Build(AutoBattleSimulationResult simulationResult)
```

Behavior:

- throw `ArgumentNullException` if `simulationResult` is null;
- copy `Outcome` from `simulationResult.BattleResult.Outcome`;
- build one force report per `BattleResult.ForceResults`;
- sum totals from force results;
- count attacks and damage from `AutoBattleEventKind.AttackResolved` grouped by actor force id;
- count defeated units from `AutoBattleEventKind.UnitDefeated` grouped by defeated force id;
- include event feed rows only for battle start, unit spawn, attack resolved, unit defeated, and battle end;
- map feed summary keys to `battle_started`, `unit_deployed`, `attack_resolved`, `unit_defeated`, and `battle_ended`;
- set `TopFailureReason = "player_force_eliminated"` when outcome is `Defeat` and all non-enemy surviving units are zero; otherwise keep it empty.

- [ ] **Step 2: Run focused test**

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

- Report facts come from runtime events and `BattleResult.ForceResults`.
- Report builder does not inspect scene nodes or mutate world state.
- Event feed is concise and excludes movement spam.
- Failure reason is a stable key, not final player-facing copy.
- No UI, AP, `TurnSystem`, `WorldSiteRoot`, or manual command dependency was introduced.
