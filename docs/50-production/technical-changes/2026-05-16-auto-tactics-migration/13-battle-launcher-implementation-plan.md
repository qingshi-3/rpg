# Battle Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract battle handoff begin/activation failure rollback out of `WorldSiteRoot`.

**Architecture:** Add `WorldSiteBattleLauncher` under `Rpg.Application.World`. It owns battle launch rollback snapshots, `BattleSessionHandoff.BeginBattle`, activation failure cancellation, and site-mode/exploration rollback; `WorldSiteRoot` supplies scene callbacks for `ApplyBattleStartRequest`, `ActivateBattleRuntime`, and presentation cleanup.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleStartRequest`, `BattleSessionHandoff`, `WorldSiteState`, `WorldSiteModeTransitionService`, existing console regression projects.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/02-worldsite-runtime-split.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/12-battle-deployment-preparer-implementation-plan.md`
- `docs/30-technical-design/world/strategic-world-v1-battle-contract.md`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Application/Battle/BattleSessionHandoff.cs`
- `src/Application/World/WorldSiteModeTransitionService.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- `tests/WorldSiteIntelRegression/Program.cs`

## Scope Boundaries

- Do not build `AutoBattleRuntimeController` in this slice.
- Do not move battle entity instantiation, HUD, camera, or turn/runtime internals.
- Do not change exploration request construction.
- Do not mutate `StrategicWorldState` outside restoring the target `WorldSiteState` after a failed launch.
- Do not add AP, `TurnSystem`, or manual command behavior.

## File Structure

- Create `src/Application/World/WorldSiteBattleLauncher.cs`
  - Owns rollback snapshot DTO, handoff begin, failed activation cancellation, site rollback, and low-noise launch logs.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Add launcher field.
  - Replace direct `BattleSessionHandoff.BeginBattle` + `ActivateBattleRuntime` + `RollbackSiteBattleLaunch` call sequences with one launcher call.
  - Delete root-owned `SiteBattleLaunchRollback`, `CaptureSiteBattleLaunchRollback`, `ApplyModeTransitionRollbackEvent`, and `RollbackSiteBattleLaunch`.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Add behavior tests and root source delegation checks.
- Modify `tests/WorldSiteIntelRegression/Program.cs`
  - Update StartsBattle source check to require `WorldSiteBattleLauncher`.

## Task 1: Add Failing Launcher Tests

**Files:**
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- Modify: `tests/WorldSiteIntelRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add after battle deployment preparer tests:

```csharp
Run("battle launcher cancels handoff and restores site on activation failure", BattleLauncherCancelsHandoffAndRestoresSiteOnActivationFailure);
Run("world site root delegates battle launch handoff", WorldSiteRootDelegatesBattleLaunchHandoff);
```

- [ ] **Step 2: Add behavior test**

```csharp
static void BattleLauncherCancelsHandoffAndRestoresSiteOnActivationFailure()
{
    BattleSessionHandoff.CancelBattle();
    StrategicWorldState state = new() { WorldTick = 7 };
    WorldSiteState site = BuildDeploymentSite();
    site.SiteMode = WorldSiteMode.Alert;
    site.Exploration = new WorldSiteExplorationState
    {
        IsSimulationPaused = true,
        PauseReason = "exploration_ready",
        ActiveAlertPatrolId = "patrol_a"
    };
    site.Exploration.PendingPathCellKeys.Add("1:2:0");
    state.SiteStates[site.SiteId] = site;

    WorldSiteBattleLauncher launcher = new();
    WorldSiteBattleLaunchRollback rollback = launcher.CaptureRollback(site);
    site.SiteMode = WorldSiteMode.Wartime;
    site.Exploration.IsSimulationPaused = false;
    site.Exploration.PauseReason = "exploration_battle";
    site.Exploration.ActiveAlertPatrolId = "patrol_b";
    site.Exploration.PendingPathCellKeys.Clear();

    int applyCalls = 0;
    int cleanupCalls = 0;
    int runtimeDisableCalls = 0;
    WorldSiteBattleLaunchResult result = launcher.BeginAndActivate(
        state,
        new BattleStartRequest { RequestId = "request_under_test", TargetSiteId = site.SiteId },
        rollback,
        () => applyCalls++,
        () => false,
        () => "activation_blocked",
        () => cleanupCalls++,
        () => cleanupCalls++,
        enabled =>
        {
            if (!enabled)
            {
                runtimeDisableCalls++;
            }
        });

    AssertTrue(!result.Success, "failed activation should return failure");
    AssertEqual("activation_blocked", result.FailureReason, "failure reason");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "failed launch should cancel active handoff");
    AssertEqual(1, applyCalls, "apply start request call count");
    AssertEqual(2, cleanupCalls, "cleanup callbacks should run during rollback");
    AssertEqual(1, runtimeDisableCalls, "runtime should be disabled during rollback");
    AssertEqual(WorldSiteMode.Alert, site.SiteMode, "site mode should be restored");
    AssertTrue(site.Exploration.IsSimulationPaused, "exploration pause should be restored");
    AssertEqual("exploration_ready", site.Exploration.PauseReason, "exploration pause reason should be restored");
    AssertEqual("patrol_a", site.Exploration.ActiveAlertPatrolId, "active alert patrol should be restored");
    AssertEqual("1:2:0", site.Exploration.PendingPathCellKeys[0], "pending path should be restored");
}
```

- [ ] **Step 3: Add source delegation test**

```csharp
static void WorldSiteRootDelegatesBattleLaunchHandoff()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_battleLauncher.BeginAndActivate", StringComparison.Ordinal),
        "WorldSiteRoot should delegate battle handoff and failed activation rollback");
    AssertTrue(
        rootSource.Contains("_battleLauncher.CaptureRollback", StringComparison.Ordinal),
        "WorldSiteRoot should capture battle launch rollback through WorldSiteBattleLauncher");
    AssertTrue(
        !rootSource.Contains("private sealed class SiteBattleLaunchRollback", StringComparison.Ordinal),
        "WorldSiteRoot should not own battle launch rollback DTO");
    AssertTrue(
        !rootSource.Contains("private void RollbackSiteBattleLaunch", StringComparison.Ordinal),
        "WorldSiteRoot should not own battle launch rollback");
    AssertTrue(
        !rootSource.Contains("private static void ApplyModeTransitionRollbackEvent", StringComparison.Ordinal),
        "WorldSiteRoot should not own mode transition rollback extraction");
}
```

- [ ] **Step 4: Update WorldSiteIntelRegression source check**

In `WorldSiteRootExecutesStartsBattleExplorationActionsThroughBattleHandoff`, replace direct checks for `BattleSessionHandoff.BeginBattle`, `ActivateBattleRuntime`, `RollbackSiteBattleLaunch`, and `BattleSessionHandoff.CancelBattle` with `_battleLauncher.BeginAndActivate` and `_battleLauncher.CaptureRollback`.

- [ ] **Step 5: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: FAIL at compile time because `WorldSiteBattleLauncher` is missing.

## Task 2: Implement `WorldSiteBattleLauncher`

**Files:**
- Create: `src/Application/World/WorldSiteBattleLauncher.cs`

- [ ] **Step 1: Create launcher**

Create `WorldSiteBattleLaunchRollback`, `WorldSiteBattleLaunchResult`, and `WorldSiteBattleLauncher` with:

- `CaptureRollback(WorldSiteState site)`;
- `ApplyModeTransitionRollbackEvent(WorldSiteBattleLaunchRollback rollback, IReadOnlyCollection<GameEvent> events)`;
- `BeginAndActivate(StrategicWorldState state, BattleStartRequest request, WorldSiteBattleLaunchRollback rollback, Action applyBattleStartRequest, Func<bool> activateBattleRuntime, Func<string> getBlockedReason, Action clearBattleEntities, Action clearBattleCorpsRuntime, Action<bool> setBattleRuntimeEnabled)`;
- `Rollback(...)` as a private helper using `WorldSiteModeTransitionService.RestoreMode`.

- [ ] **Step 2: Run focused regression**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: launcher behavior passes; root source delegation still fails.

## Task 3: Wire `WorldSiteRoot`

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`

- [ ] **Step 1: Add launcher field**

Near world services:

```csharp
private readonly WorldSiteBattleLauncher _battleLauncher = new();
```

- [ ] **Step 2: Replace launch call sequences**

Replace each local sequence:

```csharp
BattleSessionHandoff.BeginBattle(request);
ApplyBattleStartRequest();
if (!ActivateBattleRuntime())
{
    BattleSessionHandoff.CancelBattle();
    RollbackSiteBattleLaunch(rollback, request, _battleStartBlockedReason);
    ...
}
```

with:

```csharp
WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
    StrategicWorldRuntime.State,
    request,
    rollback,
    ApplyBattleStartRequest,
    ActivateBattleRuntime,
    () => _battleStartBlockedReason,
    ClearBattleEntities,
    ClearBattleCorpsRuntime,
    enabled => SetBattleRuntimeEnabled(enabled));
if (!launch.Success)
{
    ...
}
```

Use the existing local failure notice and log text, but use `launch.FailureReason`.

- [ ] **Step 3: Replace rollback capture and mode-event extraction**

Use:

```csharp
WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(site);
_battleLauncher.ApplyModeTransitionRollbackEvent(rollback, result.Events);
```

- [ ] **Step 4: Delete root-owned rollback types/helpers**

Delete:

- nested `SiteBattleLaunchRollback`;
- `CaptureSiteBattleLaunchRollback`;
- `ApplyModeTransitionRollbackEvent`;
- `RollbackSiteBattleLaunch`.

- [ ] **Step 5: Run focused regression**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: all focused tests pass.

## Task 4: Verification

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
rg -n "SiteBattleLaunchRollback|RollbackSiteBattleLaunch|ApplyModeTransitionRollbackEvent" src/Presentation/World/Sites/WorldSiteRoot.cs
rg -n "WorldSiteBattleLauncher" src/Application/World src/Presentation/World/Sites/WorldSiteRoot.cs tests/WorldSiteDeploymentCacheRegression/Program.cs tests/WorldSiteIntelRegression/Program.cs
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj
dotnet build-server shutdown
```

Expected:

- focused regression exits `0`;
- main build has `0 Error(s)`;
- first source check has no output;
- second source check finds application, root, and tests;
- broader WorldSite and battle feedback regressions exit `0`.

## Self-Review Checklist

- `WorldSiteRoot` still owns scene callbacks and runtime presentation.
- `WorldSiteBattleLauncher` owns handoff begin, activation failure cancel, and state rollback.
- Battle runtime still does not mutate `StrategicWorldState`.
- No auto battle runtime, AP, `TurnSystem`, or manual command changes were introduced.
