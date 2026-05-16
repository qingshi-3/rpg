# Deployment Terrain Reconciler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract deployment terrain validation, placement height sync, and terrain-driven relocation out of `WorldSiteRoot`.

**Architecture:** Add `WorldSiteDeploymentTerrainReconciler` under `Rpg.Application.World`. It reads `BattleGridMap`, `WorldSiteRuntimeDeploymentCache`, `WorldSiteState`, and `WorldSiteDefinition`, then updates only `WorldSiteState.UnitPlacements` rows that need height sync or relocation. `WorldSiteRoot` remains the scene shell that ensures the cache exists and passes the unit water-capability resolver.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleGridMap`, `WorldSiteRuntimeDeploymentCache`, `WorldSiteDeploymentService`, existing console regression project `tests/WorldSiteDeploymentCacheRegression`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/02-worldsite-runtime-split.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/10-deployment-target-evaluator-implementation-plan.md`
- `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`
- `src/Application/World/WorldSiteDeploymentTargetEvaluator.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Scope Boundaries

- Do not extract mouse input, drag entity movement, HUD notices, or overlay behavior.
- Do not change `WorldSiteState.UnitPlacements` semantics.
- Do not build auto battle runtime or playback UI in this slice.
- Do not add AP, `TurnSystem`, or manual battle command behavior.

## File Structure

- Create `src/Application/World/WorldSiteDeploymentTerrainReconciler.cs`
  - Owns placement surface lookup, water/blocked validation, height synchronization, and relocation candidate choice.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Add reconciler field.
  - Delegate `EnsureSitePlacementsRespectTerrain`, `CanUsePlacement`, `TryRelocatePlacementForTerrain`, and `GetPlacementTerrainTag`.
  - Remove root-owned placement surface and relocation candidate helpers.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Add reconciler behavior tests and source-level root delegation checks.

## Task 1: Add Failing Reconciler Tests

**Files:**
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add after the deployment target evaluator tests:

```csharp
Run("deployment terrain reconciler syncs placement height", DeploymentTerrainReconcilerSyncsPlacementHeight);
Run("deployment terrain reconciler relocates blocked non-water placement", DeploymentTerrainReconcilerRelocatesBlockedNonWaterPlacement);
Run("world site root delegates deployment terrain reconciliation", WorldSiteRootDelegatesDeploymentTerrainReconciliation);
```

- [ ] **Step 2: Add behavior tests**

Add before source-level tests:

```csharp
static void DeploymentTerrainReconcilerSyncsPlacementHeight()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 2, 0, height: 2, terrainTag: "upper_plain");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    WorldSiteUnitPlacement placement = new() { PlacementId = "unit:1", UnitTypeId = "militia", CellX = 2, CellY = 0, CellHeight = 0 };
    site.UnitPlacements.Add(placement);

    WorldSiteDeploymentTerrainReconcileResult result = new WorldSiteDeploymentTerrainReconciler()
        .Reconcile(grid, cache, site, new WorldSiteDefinition(), CanPlacementEnterWater);

    AssertTrue(result.Success, $"height sync should succeed reason={result.LastFailureReason}");
    AssertEqual(1, result.HeightSynced, "height sync count");
    AssertEqual(2, placement.CellHeight, "placement height should sync to top surface");
}

static void DeploymentTerrainReconcilerRelocatesBlockedNonWaterPlacement()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "water");
    AddWalkableSurface(grid, 1, 0, terrainTag: "plain");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    WorldSiteDefinition definition = BuildDefaultZoneDefinition(new Vector2I(1, 0));
    WorldSiteUnitPlacement placement = new()
    {
        PlacementId = "unit:1",
        UnitTypeId = "militia",
        CellX = 0,
        CellY = 0,
        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId
    };
    site.UnitPlacements.Add(placement);

    WorldSiteDeploymentTerrainReconcileResult result = new WorldSiteDeploymentTerrainReconciler()
        .Reconcile(grid, cache, site, definition, CanPlacementEnterWater);

    AssertTrue(result.Success, $"relocation should succeed reason={result.LastFailureReason}");
    AssertEqual(1, result.Relocated, "relocation count");
    AssertEqual(1, placement.CellX, "relocated placement x");
    AssertEqual(0, placement.CellY, "relocated placement y");
}
```

- [ ] **Step 3: Add source delegation test**

```csharp
static void WorldSiteRootDelegatesDeploymentTerrainReconciliation()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_deploymentTerrainReconciler.Reconcile", StringComparison.Ordinal),
        "WorldSiteRoot should delegate placement terrain reconciliation");
    AssertTrue(
        rootSource.Contains("_deploymentTerrainReconciler.TryRelocatePlacementForTerrain", StringComparison.Ordinal),
        "WorldSiteRoot should delegate terrain relocation");
    AssertTrue(
        !rootSource.Contains("private bool CanUsePlacementSurface", StringComparison.Ordinal),
        "WorldSiteRoot should not own placement surface validation");
    AssertTrue(
        !rootSource.Contains("private IReadOnlyList<WorldSiteDeploymentCell> BuildRelocationCandidates", StringComparison.Ordinal),
        "WorldSiteRoot should not own relocation candidate ordering");
    AssertTrue(
        !rootSource.Contains("private bool TryGetPlacementSurface", StringComparison.Ordinal),
        "WorldSiteRoot should not own placement surface lookup");
}
```

- [ ] **Step 4: Add definition helper**

```csharp
static WorldSiteDefinition BuildDefaultZoneDefinition(params Vector2I[] cells)
{
    WorldSiteDefinition definition = new()
    {
        Id = "site_under_test",
        DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId
    };
    SiteDeploymentZoneDefinition zone = new()
    {
        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison
    };
    zone.Cells.AddRange(cells);
    definition.DeploymentZones.Add(zone);
    return definition;
}
```

- [ ] **Step 5: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: FAIL at compile time with missing `WorldSiteDeploymentTerrainReconciler` / `WorldSiteDeploymentTerrainReconcileResult`.

## Task 2: Implement `WorldSiteDeploymentTerrainReconciler`

**Files:**
- Create: `src/Application/World/WorldSiteDeploymentTerrainReconciler.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Create reconciler and result**

Create `src/Application/World/WorldSiteDeploymentTerrainReconciler.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteDeploymentTerrainReconcileResult
{
    public bool Success { get; init; } = true;
    public int Relocated { get; init; }
    public int HeightSynced { get; init; }
    public int Invalid { get; init; }
    public string LastFailureReason { get; init; } = "";
}
```

Then add `WorldSiteDeploymentTerrainReconciler` that mirrors the existing root behavior: missing site/definition fails; missing grid succeeds without mutation; valid placements sync height; invalid placements try zone candidates first, then cache candidates; occupied cells are skipped; failures are logged.

- [ ] **Step 2: Run tests and verify behavior passes while source check fails**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: reconciler behavior tests pass; `world site root delegates deployment terrain reconciliation` still fails.

## Task 3: Wire `WorldSiteRoot` To Reconciler

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add reconciler field**

Near deployment fields add:

```csharp
private readonly WorldSiteDeploymentTerrainReconciler _deploymentTerrainReconciler = new();
```

- [ ] **Step 2: Delegate terrain reconciliation**

Replace the body of `EnsureSitePlacementsRespectTerrain` after cache rebuild with:

```csharp
WorldSiteDeploymentTerrainReconcileResult result = _deploymentTerrainReconciler.Reconcile(
    _activeGridMap,
    _deploymentCache,
    site,
    definition,
    ResolvePlacementCanEnterWater);
return result.Success;
```

- [ ] **Step 3: Delegate placement checks and relocation used by preferred placement application**

Keep the small root wrappers:

```csharp
private bool CanUsePlacement(WorldSiteUnitPlacement placement, bool canEnterWater)
{
    return _deploymentTerrainReconciler.CanUsePlacement(_activeGridMap, placement, canEnterWater, out _);
}

private bool TryRelocatePlacementForTerrain(
    WorldSiteState site,
    WorldSiteDefinition definition,
    WorldSiteUnitPlacement placement,
    bool canEnterWater,
    out string failureReason)
{
    return _deploymentTerrainReconciler.TryRelocatePlacementForTerrain(
        _activeGridMap,
        _deploymentCache,
        site,
        definition,
        placement,
        canEnterWater,
        out failureReason);
}

private string GetPlacementTerrainTag(WorldSiteUnitPlacement placement)
{
    return _deploymentTerrainReconciler.GetPlacementTerrainTag(_activeGridMap, _deploymentCache, placement);
}
```

- [ ] **Step 4: Delete root-owned helpers**

Delete these from `WorldSiteRoot`:

- `TrySyncPlacementSurfaceHeight`
- `CanUsePlacementSurface`
- `BuildRelocationCandidates`
- `AddZoneRelocationCandidates`
- `TryGetPlacementSurface`
- `TryGetDeploymentCellForPlacement`
- `IsDeploymentCandidateOccupied`

- [ ] **Step 5: Run focused regression tests**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: all deployment cache, target evaluator, and terrain reconciler tests pass.

## Task 4: Verification

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
rg -n "CanUsePlacementSurface|BuildRelocationCandidates|TryGetPlacementSurface|IsDeploymentCandidateOccupied" src/Presentation/World/Sites/WorldSiteRoot.cs
rg -n "WorldSiteDeploymentTerrainReconciler" src/Application/World src/Presentation/World/Sites/WorldSiteRoot.cs tests/WorldSiteDeploymentCacheRegression/Program.cs
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
dotnet build-server shutdown
```

Expected:

- focused regression exits `0`;
- main build has `0 Error(s)`;
- first `rg` has no output;
- second `rg` finds the reconciler in application, root, and tests;
- broader WorldSite regression exits `0`.

## Self-Review Checklist

- `WorldSiteRoot` still owns scene orchestration only.
- Terrain validation, height sync, relocation candidate ordering, and relocation writes are delegated.
- `WorldSiteState.UnitPlacements` remains the only persistent placement authority.
- No auto battle runtime, AP, `TurnSystem`, or manual command changes were introduced.
