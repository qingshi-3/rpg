# WorldSite Auto Battle Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a thin WorldSite-side adapter that resolves an active battle handoff through `AutoBattleRuntimeController`.

**Architecture:** Add `WorldSiteAutoBattleAdapter` under `Rpg.Application.World`. The adapter owns the handoff/controller boundary for WorldSite callers: it starts the active auto battle controller, consumes the completed `BattleResult`, and returns the request/result/report bundle. `WorldSiteRoot` delegates auto activation to the adapter.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleSessionHandoff`, `AutoBattleRuntimeController`, `AutoBattleReport`, `WorldSiteRoot`, console regression projects under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/02-worldsite-runtime-split.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/17-auto-battle-runtime-controller-implementation-plan.md`
- `src/Application/Battle/Auto/AutoBattleRuntimeController.cs`
- `src/Application/World/WorldSiteBattleLauncher.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/AutoBattleRuntimeRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Scope Boundaries

- Do not make auto battle the default scene behavior in this slice. `UseAutoBattleRuntime` defaults to `false`.
- Do not build HUD, playback UI, scene animation, or report presentation.
- Do not restore retired manual battle command behavior as part of this adapter.
- Do not mutate `StrategicWorldState` inside the adapter. World result application remains in `WorldSiteRoot` through `WorldBattleResultApplier`.
- Do not fabricate fallback results. If the controller fails, return failure and preserve the active handoff state for launcher rollback or caller handling.

## File Structure

- Create `src/Application/World/WorldSiteAutoBattleAdapter.cs`
  - Owns the active handoff to auto battle controller boundary and returns request/result/report.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Add exported `UseAutoBattleRuntime` flag, adapter field, and auto activation branch inside `ActivateBattleRuntime`.
  - Keep old manual activation code unchanged for the default path.
- Modify `tests/AutoBattleRuntimeRegression/Program.cs`
  - Add adapter behavior tests.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Add source guard that `WorldSiteRoot` exposes optional auto battle activation through the adapter.

## Task 1: Add Failing Adapter Tests

**Files:**
- Modify: `tests/AutoBattleRuntimeRegression/Program.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add adapter behavior tests**

Add to `AutoBattleRuntimeRegression`:

```csharp
Run("world site auto battle adapter resolves active handoff result and report", WorldSiteAutoBattleAdapterResolvesActiveHandoffResultAndReport);
Run("world site auto battle adapter preserves active handoff on failure", WorldSiteAutoBattleAdapterPreservesActiveHandoffOnFailure);
```

Success behavior:

- begin a valid `BattleSessionHandoff` request;
- call `WorldSiteAutoBattleAdapter.TryResolveActiveBattle(out WorldSiteAutoBattleResolveResult result)`;
- assert success, request identity, `BattleResult.ForceResults`, report outcome, and no active handoff remains;
- assert `BattleSessionHandoff.TryConsumeLastBattleResult` returns false after adapter consumption.

Failure behavior:

- begin a request with missing preferred placements;
- call the adapter;
- assert failure reason contains `auto_battle_missing_preferred_placement`;
- assert active handoff remains true and no request/result/report is returned;
- cancel the handoff at the end.

- [ ] **Step 2: Add WorldSiteRoot source guard**

Add to `WorldSiteDeploymentCacheRegression`:

```csharp
Run("world site root exposes optional auto battle activation through adapter", WorldSiteRootExposesOptionalAutoBattleActivationThroughAdapter);
```

The source guard should assert:

- root contains `UseAutoBattleRuntime`;
- root contains `_autoBattleAdapter.TryResolveActiveBattle`;
- root contains `ActivateAutoBattleRuntime`;
- root still contains `_turnController?.StartBattle()` for legacy manual activation;
- root does not instantiate `new AutoBattleRuntimeController`.

- [ ] **Step 3: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `WorldSiteAutoBattleAdapter` and `WorldSiteAutoBattleResolveResult` do not exist.

## Task 2: Implement `WorldSiteAutoBattleAdapter`

**Files:**
- Create: `src/Application/World/WorldSiteAutoBattleAdapter.cs`

- [ ] **Step 1: Add result DTO and adapter**

`WorldSiteAutoBattleResolveResult` should expose:

- `Success`
- `FailureReason`
- `BattleStartRequest Request`
- `BattleResult BattleResult`
- `AutoBattleReport Report`
- `AutoBattleRuntimeController RuntimeController`

`WorldSiteAutoBattleAdapter.TryResolveActiveBattle(out WorldSiteAutoBattleResolveResult result)` should:

- call the injected/default `AutoBattleRuntimeController.StartActiveBattle`;
- return failure with controller failure reason if the controller fails;
- consume `BattleSessionHandoff.TryConsumeLastBattleResult` after success;
- return success only when request, result, and report are present;
- not mutate strategic/world-site state.

- [ ] **Step 2: Run focused test**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: auto adapter behavior passes; WorldSiteRoot source guard still fails until root is wired.

## Task 3: Wire Optional Auto Activation In `WorldSiteRoot`

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`

- [ ] **Step 1: Add flag and fields**

Add:

```csharp
[Export]
public bool UseAutoBattleRuntime { get; set; }

private readonly WorldSiteAutoBattleAdapter _autoBattleAdapter = new();
private AutoBattleReport _lastAutoBattleReport;
```

- [ ] **Step 2: Branch in `ActivateBattleRuntime`**

At the top of `ActivateBattleRuntime`, after blocked reason handling:

```csharp
if (UseAutoBattleRuntime)
{
    return ActivateAutoBattleRuntime();
}
```

Manual activation should still call `PlaceBattleEntitiesOnGrid`, `RegisterBattleEntities`, `SetBattleRuntimeEnabled(true)`, and `_turnController?.StartBattle()`.

- [ ] **Step 3: Add `ActivateAutoBattleRuntime`**

Behavior:

- call `_autoBattleAdapter.TryResolveActiveBattle`;
- on failure: set `_battleStartBlockedReason`, disable battle runtime, log, return false;
- on success: store `_lastAutoBattleReport`, apply result to world through existing `ApplyBattleResultToWorld`, reconcile site placements with an empty live snapshot list, clear battle scene entities/corps runtime, switch back to non-battle UI using the request return scene path, log report basics, return true.

- [ ] **Step 4: Run focused source guard**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: source guard passes.

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

- Default manual battle scene path remains unchanged.
- Auto activation is opt-in through `UseAutoBattleRuntime`.
- `WorldSiteRoot` delegates controller details to `WorldSiteAutoBattleAdapter`.
- Auto result writeback still uses existing `ApplyBattleResultToWorld`.
- This adapter slice did not handle UI, HUD, scene animation, or manual command deletion; final cleanup is tracked separately.
