# Auto Battle Session Runner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an application-layer runner that can resolve an active `BattleSessionHandoff` request through `AutoBattleSimulation` and complete the handoff with the simulation's full `BattleResult`.

**Architecture:** Keep scene roots and legacy battle flow unchanged. Add `AutoBattleSessionRunner` under `Rpg.Application.Battle.Auto`; it peeks the active handoff request, runs the pure simulation, and asks `BattleSessionHandoff` to store the exact structured result. Extend `BattleSessionHandoff` with a full-result completion overload so `ForceResults` are preserved instead of being rebuilt from outcome only.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleSessionHandoff`, `BattleStartRequest`, `BattleResult`, `AutoBattleSimulation`, console regression project under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/14-auto-battle-runtime-skeleton-implementation-plan.md`
- `docs/30-technical-design/world/strategic-world-v1-battle-contract.md`
- `src/Application/Battle/BattleSessionHandoff.cs`
- `src/Application/Battle/Auto/AutoBattleSimulation.cs`
- `tests/AutoBattleRuntimeRegression/Program.cs`

## Scope Boundaries

- Do not wire `AutoBattleSessionRunner` into `WorldSiteRoot`, `StrategicWorldRoot`, battle scenes, HUD, camera, animation, or playback UI in this slice.
- Do not remove or change the legacy `BattleSessionHandoff.CompleteBattle(BattleOutcome)` path.
- Do not mutate `StrategicWorldState`, `WorldSiteState`, or deployment state.
- Do not add AP, `TurnSystem`, manual command, or `BattleActionMenu` behavior.
- Do not fabricate fallback `ForceResults`. If the simulation cannot run, leave the active handoff request active and report a failure reason to the caller.

## File Structure

- Modify `src/Application/Battle/BattleSessionHandoff.cs`
  - Add `CompleteBattle(BattleResult battleResult)` that stores the full runtime result and keeps the existing outcome-only overload intact.
- Create `src/Application/Battle/Auto/AutoBattleSessionRunner.cs`
  - Owns active handoff lookup, simulation invocation, failed-run reporting, and full-result completion.
- Modify `tests/AutoBattleRuntimeRegression/Program.cs`
  - Add session runner tests and source guards.
- Modify documentation routes:
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
  - `docs/60-qa/testcases/auto-tactics-migration.md`

## Task 1: Add Failing Session Runner Tests

**Files:**
- Modify: `tests/AutoBattleRuntimeRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add after existing auto battle runtime tests:

```csharp
Run("auto battle session runner completes active handoff with force results", AutoBattleSessionRunnerCompletesActiveHandoffWithForceResults);
Run("auto battle session runner leaves active handoff when simulation cannot run", AutoBattleSessionRunnerLeavesActiveHandoffWhenSimulationCannotRun);
```

- [ ] **Step 2: Add success test**

The success test should:

- call `BattleSessionHandoff.CancelBattle()` first;
- build a valid request with preferred placements;
- call `BattleSessionHandoff.BeginBattle(request)`;
- call `new AutoBattleSessionRunner(new AutoBattleSimulation(config)).TryRunActiveBattle(out AutoBattleSimulationResult simulation, out string failureReason)`;
- assert success is `true`;
- assert `BattleSessionHandoff.HasActiveLaunch` is `false`;
- consume `BattleSessionHandoff.TryConsumeLastBattleResult(out BattleStartRequest consumedRequest, out BattleResult consumedResult)`;
- assert the consumed request is the original request;
- assert `consumedResult` is the simulation result and includes player/enemy `ForceResults`.

- [ ] **Step 3: Add failed-run test**

The failed-run test should:

- begin an active battle request with a force count greater than its preferred placement count;
- call the runner;
- assert success is `false`;
- assert `failureReason` contains `auto_battle_missing_preferred_placement`;
- assert `BattleSessionHandoff.HasActiveLaunch` remains `true`;
- assert no completed battle result can be consumed;
- cancel the battle at the end to avoid leaking static handoff state into later tests.

- [ ] **Step 4: Extend source guard**

Require the auto runtime source to avoid `WorldSiteRoot`, `StrategicWorldState`, `BattleTurnController`, `TurnSystem`, and `ActionPoint`.

- [ ] **Step 5: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `AutoBattleSessionRunner` and `BattleSessionHandoff.CompleteBattle(BattleResult)` do not exist yet.

## Task 2: Add Full-Result Handoff Completion

**Files:**
- Modify: `src/Application/Battle/BattleSessionHandoff.cs`

- [ ] **Step 1: Add overload**

Add:

```csharp
public static BattleSessionResult CompleteBattle(BattleResult battleResult)
```

Behavior:

- return `null` if there is no active request or `battleResult` is null;
- use the active request as `_lastRequest`;
- normalize missing `RequestId`, `ContextId`, and `BattleKind` from the active request;
- store `battleResult` directly in `_lastBattleResult`;
- create `_lastResult` from active request context, encounter, return scene path, and `battleResult.Outcome`;
- clear `_activeRequest`.

- [ ] **Step 2: Run focused test**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile still fails because `AutoBattleSessionRunner` is missing.

## Task 3: Implement `AutoBattleSessionRunner`

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleSessionRunner.cs`

- [ ] **Step 1: Add runner**

The runner should expose:

```csharp
public bool TryRunActiveBattle(
    out AutoBattleSimulationResult simulationResult,
    out string failureReason)
```

Behavior:

- if there is no active handoff request, return `false` and `failureReason = "no_active_battle_request"`;
- run the configured `AutoBattleSimulation`;
- call `BattleSessionHandoff.CompleteBattle(simulationResult.BattleResult)`;
- return `true` only if handoff completion returns a non-null `BattleSessionResult`;
- catch simulation exceptions, set `failureReason` to the exception message, and leave the active handoff request active.

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

- `AutoBattleSessionRunner` is application-layer only.
- Full `BattleResult.ForceResults` survive handoff consumption.
- Failed simulation does not cancel or complete an active handoff.
- Legacy `CompleteBattle(BattleOutcome)` still works for existing manual battle flow.
- No `WorldSiteRoot`, UI, AP, `TurnSystem`, or strategic-world mutation dependency was introduced.
