# Battle Deployment Preparer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract WorldSite-to-`BattleStartRequest` deployment preparation out of `WorldSiteRoot`.

**Architecture:** Add `WorldSiteBattleDeploymentPreparer` under `Rpg.Application.World`. It prepares battle placements from `WorldSiteState.UnitPlacements`, deployment cache candidates, entrance metadata, and terrain reconciliation, while `WorldSiteRoot` only resolves scene/runtime context and calls the preparer.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleStartRequest`, `BattleForceRequest`, `WorldSiteState`, `WorldSiteDefinition`, `WorldSiteRuntimeDeploymentCache`, `WorldSiteDeploymentService`, `WorldSiteDeploymentTerrainReconciler`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/02-worldsite-runtime-split.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/11-deployment-terrain-reconciler-implementation-plan.md`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Application/World/WorldSiteDeploymentService.cs`
- `src/Application/World/WorldSiteDeploymentTerrainReconciler.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- `tests/BattleHitFeedbackRegression/Program.cs`

## Scope Boundaries

- Do not start auto battle runtime in this slice.
- Do not change scene loading, map loading, entity instantiation, HUD, or battle playback.
- Do not mutate `StrategicWorldState` inside the preparer.
- Do not change `WorldSiteState.UnitPlacements` semantics.
- Do not add AP, `TurnSystem`, or manual battle command behavior.

## File Structure

- Create `src/Application/World/WorldSiteBattleDeploymentPreparer.cs`
  - Owns non-scene battle deployment preparation from site state into request forces.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Add preparer field.
  - Replace root-owned force placement preparation with a single preparer call.
  - Delete root-owned battle deployment preparation helpers.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Add behavior tests and root source delegation checks.
- Modify `tests/BattleHitFeedbackRegression/Program.cs`
  - Move the known-entrance source check from `WorldSiteRoot` to `WorldSiteBattleDeploymentPreparer`.

## Task 1: Add Failing Preparer Tests

**Files:**
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- Modify: `tests/BattleHitFeedbackRegression/Program.cs`

- [ ] **Step 1: Add using**

Add to `tests/WorldSiteDeploymentCacheRegression/Program.cs`:

```csharp
using Rpg.Application.Battle;
```

- [ ] **Step 2: Add test run lines**

Add after terrain reconciler tests:

```csharp
Run("battle deployment preparer creates site placements and force preferred placements", BattleDeploymentPreparerCreatesSitePlacementsAndForcePreferredPlacements);
Run("battle deployment preparer uses known entrance before desired approach direction", BattleDeploymentPreparerUsesKnownEntranceBeforeDesiredApproachDirection);
Run("world site root delegates battle deployment preparation", WorldSiteRootDelegatesBattleDeploymentPreparation);
```

- [ ] **Step 3: Add behavior tests**

Add before source-level tests:

```csharp
static void BattleDeploymentPreparerCreatesSitePlacementsAndForcePreferredPlacements()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "west");
    AddWalkableSurface(grid, 2, 0, terrainTag: "east");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    WorldSiteDefinition definition = new() { Id = "site_under_test" };
    BattleStartRequest request = new()
    {
        TargetSiteId = site.SiteId,
        BattleKind = BattleKind.AssaultSite,
        AttackDirection = WorldSiteAttackDirection.East,
        AttackerFactionId = "player"
    };
    BattleForceRequest playerForce = new()
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "militia",
        Count = 1,
        FactionId = "player"
    };
    BattleForceRequest enemyForce = new()
    {
        ForceId = "enemy_force",
        SourceKind = "ThreatArmy",
        SourceId = "threat_1",
        UnitDefinitionId = "militia",
        Count = 1,
        FactionId = "enemy"
    };
    request.PlayerForces.Add(playerForce);
    request.EnemyForces.Add(enemyForce);

    bool prepared = new WorldSiteBattleDeploymentPreparer().Prepare(
        request,
        site,
        definition,
        cache,
        grid,
        CanForceEnterWater,
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(prepared, $"deployment preparation should succeed failure={failureReason}");
    AssertEqual(2, site.UnitPlacements.Count, "created placement count");
    AssertEqual(1, playerForce.PreferredPlacements.Count, "player preferred placement count");
    AssertEqual(1, enemyForce.PreferredPlacements.Count, "enemy preferred placement count");
    AssertEqual(2, playerForce.PreferredPlacements[0].CellX, "attacker should use east candidate");
    AssertEqual(0, enemyForce.PreferredPlacements[0].CellX, "defender should use west candidate");
}

static void BattleDeploymentPreparerUsesKnownEntranceBeforeDesiredApproachDirection()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "west");
    AddWalkableSurface(grid, 2, 0, terrainTag: "east");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    BattleStartRequest request = new()
    {
        TargetSiteId = site.SiteId,
        BattleKind = BattleKind.AssaultSite,
        AttackDirection = WorldSiteAttackDirection.East,
        AttackerFactionId = "player"
    };
    request.AvailableEntrances.Add(new BattleEntranceRequest
    {
        EntranceId = "known_west_gate",
        Direction = WorldSiteAttackDirection.West,
        FactionId = "player"
    });
    BattleForceRequest playerForce = new()
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "militia",
        Count = 1,
        FactionId = "player"
    };
    request.PlayerForces.Add(playerForce);

    bool prepared = new WorldSiteBattleDeploymentPreparer().Prepare(
        request,
        site,
        new WorldSiteDefinition { Id = "site_under_test" },
        cache,
        grid,
        CanForceEnterWater,
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(prepared, $"known entrance deployment should succeed failure={failureReason}");
    AssertEqual("known_west_gate", playerForce.PreferredEntranceId, "force preferred entrance should use known entrance");
    AssertEqual(0, playerForce.PreferredPlacements[0].CellX, "known west entrance should override east desired direction");
}
```

- [ ] **Step 4: Add source delegation test and helper**

Add source test:

```csharp
static void WorldSiteRootDelegatesBattleDeploymentPreparation()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_battleDeploymentPreparer.Prepare", StringComparison.Ordinal),
        "WorldSiteRoot should delegate battle deployment preparation");
    AssertTrue(
        !rootSource.Contains("private bool EnsureForceWorldSitePlacement", StringComparison.Ordinal),
        "WorldSiteRoot should not own force placement preparation");
    AssertTrue(
        !rootSource.Contains("private bool ApplyPreferredPlacementsFromWorldSite", StringComparison.Ordinal),
        "WorldSiteRoot should not own force preferred placement projection");
    AssertTrue(
        !rootSource.Contains("private static BattleEntranceRequest ResolveForceEntrance", StringComparison.Ordinal),
        "WorldSiteRoot should not own force entrance resolution");
}
```

Add helper:

```csharp
static bool CanForceEnterWater(BattleForceRequest force)
{
    return string.Equals(force?.UnitDefinitionId, "boat", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 5: Update known entrance broad source test**

In `tests/BattleHitFeedbackRegression/Program.cs`, update `WorldSiteDeploymentUsesKnownEntrancesBeforeDesiredApproachDirection` to read `src/Application/World/WorldSiteBattleDeploymentPreparer.cs` and extract `private static BattleEntranceRequest ResolveForceEntrance`. Keep the assertions that require `return candidates.FirstOrDefault();` and reject the legacy desired-direction reuse.

- [ ] **Step 6: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: FAIL at compile time because `WorldSiteBattleDeploymentPreparer` is missing.

## Task 2: Implement `WorldSiteBattleDeploymentPreparer`

**Files:**
- Create: `src/Application/World/WorldSiteBattleDeploymentPreparer.cs`

- [ ] **Step 1: Create preparer**

Create `src/Application/World/WorldSiteBattleDeploymentPreparer.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;
```

Implement `WorldSiteBattleDeploymentPreparer.Prepare(...)` with this contract:

- validate request/site/definition/cache;
- call `WorldSiteDeploymentService.EnsureGarrisonPlacements`;
- call `WorldSiteDeploymentTerrainReconciler.Reconcile`;
- create missing battle placements for player and enemy forces through `WorldSiteDeploymentService.EnsureBattlePlacementsForForce`;
- populate each force's `PreferredPlacements` from `WorldSiteState.UnitPlacements`;
- use available entrance candidates before falling back to desired approach direction;
- log failed force preparation, invalid terrain, missing placement count, and final preparation summary.

- [ ] **Step 2: Run focused regression**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: behavior tests pass; root source delegation test still fails.

## Task 3: Wire `WorldSiteRoot`

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add preparer field**

Near deployment fields:

```csharp
private readonly WorldSiteBattleDeploymentPreparer _battleDeploymentPreparer = new();
```

- [ ] **Step 2: Replace root deployment preparation body**

In `EnsureBattleRequestSiteDeployments`, keep scene/world-context validation and cache rebuild, then replace garrison/force/preferred placement logic with:

```csharp
return _battleDeploymentPreparer.Prepare(
    request,
    site,
    siteDefinition,
    _deploymentCache,
    _activeGridMap,
    ResolveForceCanEnterWater,
    ResolvePlacementCanEnterWater,
    out _);
```

- [ ] **Step 3: Delete root-owned battle deployment helpers**

Delete:

- `EnsureForceWorldSitePlacement`
- `ApplyPreferredPlacementsFromWorldSite`
- `ResolveWorldSitePlacementsForForce`
- `IsResidentGarrisonForceForSite`
- `IsResidentWorldSiteForceForSite`
- `IsResidentSitePlacementForce`
- `IsResidentPlayerArmySiteForce`
- `ResolveForceDeploymentDirection`
- `IsAttackingForce`
- `GetOppositeDirection`
- `ResolveForceEntrance`
- `IsEntranceForForce`

Keep `ResolveForceSourceKind`, `ResolveForceSourceId`, `CanUseDeploymentCell`, and `IsPlayerArmySitePlacement` if still used by corps/exploration code.

- [ ] **Step 4: Run focused regression**

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
rg -n "EnsureForceWorldSitePlacement|ApplyPreferredPlacementsFromWorldSite|private static BattleEntranceRequest ResolveForceEntrance|private static bool IsAttackingForce" src/Presentation/World/Sites/WorldSiteRoot.cs
rg -n "WorldSiteBattleDeploymentPreparer" src/Application/World src/Presentation/World/Sites/WorldSiteRoot.cs tests/WorldSiteDeploymentCacheRegression/Program.cs tests/BattleHitFeedbackRegression/Program.cs
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

- `WorldSiteRoot` resolves context and delegates battle deployment preparation.
- `WorldSiteBattleDeploymentPreparer` does not mutate `StrategicWorldState`.
- `WorldSiteDeploymentService` remains the write authority for created placement rows.
- `WorldSiteDeploymentTerrainReconciler` remains the owner for terrain validation and relocation.
- No auto battle runtime, AP, `TurnSystem`, or manual command changes were introduced.
