# Deployment Target Evaluator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract site deployment target validation and move execution out of `WorldSiteRoot` into a focused owner, while leaving UI drag orchestration in the root.

**Architecture:** Add `WorldSiteDeploymentTargetEvaluator` under `Rpg.Application.World`. It reads `BattleGridMap`, `WorldSiteState`, `WorldSiteDefinition`, and a placement water-capability callback, then delegates final authority checks and writes to `WorldSiteDeploymentService`. `WorldSiteRoot` continues to translate mouse input into grid cells and update UI, but no longer owns blocked/water/occupancy validation logic.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleGridMap`, `WorldSiteState`, `WorldSiteDefinition`, `WorldSiteDeploymentService`, `WorldSiteRuntimeDeploymentCacheBuilder`, existing console regression project `tests/WorldSiteDeploymentCacheRegression`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/02-worldsite-runtime-split.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/03-deployment-cache-extraction.md`
- `src/Application/World/WorldSiteDeploymentService.cs`
- `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Scope Boundaries

- Do not extract full drag input handling in this slice.
- Do not move `Node2D`, HUD, overlay, or entity positioning logic out of `WorldSiteRoot`.
- Do not change `WorldSiteState.UnitPlacements` semantics.
- Do not change battle entry or auto battle runtime.
- Do not add AP, `TurnSystem`, or manual battle command behavior.

## File Structure

- Create `src/Application/World/WorldSiteDeploymentTargetEvaluator.cs`
  - Owns deployment cell validation and final placement move/write through `WorldSiteDeploymentService`.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Add evaluator field.
  - Replace `TryEvaluateSiteDeploymentTarget` body to delegate after mouse-grid resolution.
  - Remove root-owned `CanPlaceSiteDeploymentOnGridCell`.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Add evaluator behavior tests and source-level root delegation checks.

## Task 1: Add Failing Evaluator Tests

**Files:**
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add evaluator test run lines**

Add these `Run(...)` calls after the existing deployment cache tests:

```csharp
Run("deployment target evaluator rejects blocked water and occupied cells", DeploymentTargetEvaluatorRejectsBlockedWaterAndOccupiedCells);
Run("deployment target evaluator moves placement through deployment service", DeploymentTargetEvaluatorMovesPlacementThroughDeploymentService);
Run("world site root delegates deployment target validation", WorldSiteRootDelegatesDeploymentTargetValidation);
```

- [ ] **Step 2: Add evaluator behavior tests**

Add these tests before `WorldSiteRootDelegatesDeploymentCacheConstruction`:

```csharp
static void DeploymentTargetEvaluatorRejectsBlockedWaterAndOccupiedCells()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "plain");
    AddWalkableSurface(grid, 1, 0, terrainTag: "water");
    AddNonWalkableFoundation(grid, 2, 0, terrainTag: "blocked");

    WorldSiteState site = BuildDeploymentSite();
    site.UnitPlacements.Add(new WorldSiteUnitPlacement { PlacementId = "unit:1", UnitTypeId = "militia", CellX = 0, CellY = 0 });
    site.UnitPlacements.Add(new WorldSiteUnitPlacement { PlacementId = "unit:2", UnitTypeId = "militia", CellX = 3, CellY = 0 });

    WorldSiteDeploymentTargetEvaluator evaluator = new();

    AssertTrue(
        !evaluator.CanMoveToGridCell(grid, site, new WorldSiteDefinition(), "unit:1", new Vector2I(2, 0), CanPlacementEnterWater, out string blockedReason),
        "blocked cell should reject placement move");
    AssertEqual("placement_cell_blocked", blockedReason, "blocked reason");

    AssertTrue(
        !evaluator.CanMoveToGridCell(grid, site, new WorldSiteDefinition(), "unit:1", new Vector2I(1, 0), CanPlacementEnterWater, out string waterReason),
        "water should reject non-water placement move");
    AssertEqual("placement_cell_water", waterReason, "water reason");

    AssertTrue(
        !evaluator.CanMoveToGridCell(grid, site, new WorldSiteDefinition(), "unit:1", new Vector2I(3, 0), CanPlacementEnterWater, out string occupiedReason),
        "occupied cell should reject placement move");
    AssertEqual("placement_cell_occupied", occupiedReason, "occupied reason");
}

static void DeploymentTargetEvaluatorMovesPlacementThroughDeploymentService()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "plain");
    AddWalkableSurface(grid, 1, 0, terrainTag: "plain");

    WorldSiteState site = BuildDeploymentSite();
    WorldSiteUnitPlacement placement = new() { PlacementId = "unit:1", UnitTypeId = "militia", CellX = 0, CellY = 0 };
    site.UnitPlacements.Add(placement);

    WorldSiteDeploymentTargetEvaluator evaluator = new();
    bool moved = evaluator.TryMoveToGridCell(
        grid,
        site,
        new WorldSiteDefinition(),
        "unit:1",
        new Vector2I(1, 0),
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(moved, $"valid placement move should succeed failure={failureReason}");
    AssertEqual(1, placement.CellX, "placement x should update through deployment service");
    AssertEqual(0, placement.CellY, "placement y should update through deployment service");
}
```

- [ ] **Step 3: Add source delegation test**

Add this test before helper methods:

```csharp
static void WorldSiteRootDelegatesDeploymentTargetValidation()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_deploymentTargetEvaluator.CanMoveToGridCell", StringComparison.Ordinal),
        "WorldSiteRoot should delegate placement target validation to WorldSiteDeploymentTargetEvaluator");
    AssertTrue(
        rootSource.Contains("_deploymentTargetEvaluator.TryMoveToGridCell", StringComparison.Ordinal),
        "WorldSiteRoot should delegate placement movement writes to WorldSiteDeploymentTargetEvaluator");
    AssertTrue(
        !rootSource.Contains("private bool CanPlaceSiteDeploymentOnGridCell", StringComparison.Ordinal),
        "WorldSiteRoot should not own placement grid-cell validation");
}
```

- [ ] **Step 4: Add helper methods**

Add these helpers near the other test helpers:

```csharp
static WorldSiteState BuildDeploymentSite()
{
    return new WorldSiteState
    {
        SiteId = "site_under_test",
        OwnerFactionId = "player"
    };
}

static bool CanPlacementEnterWater(WorldSiteUnitPlacement placement)
{
    return string.Equals(placement?.UnitTypeId, "boat", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 5: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: FAIL at compile time with missing `WorldSiteDeploymentTargetEvaluator`.

## Task 2: Implement `WorldSiteDeploymentTargetEvaluator`

**Files:**
- Create: `src/Application/World/WorldSiteDeploymentTargetEvaluator.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Create evaluator**

Create `src/Application/World/WorldSiteDeploymentTargetEvaluator.cs`:

```csharp
using System;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteDeploymentTargetEvaluator
{
    private readonly WorldSiteDeploymentService _deploymentService;

    public WorldSiteDeploymentTargetEvaluator(WorldSiteDeploymentService deploymentService = null)
    {
        _deploymentService = deploymentService ?? new WorldSiteDeploymentService();
    }

    public bool CanMoveToGridCell(
        BattleGridMap gridMap,
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        Func<WorldSiteUnitPlacement, bool> canEnterWater,
        out string failureReason)
    {
        if (!CanPlaceOnGridCell(gridMap, site, placementId, cell, canEnterWater, out failureReason))
        {
            return false;
        }

        return _deploymentService.CanMovePlacement(site, definition, placementId, cell, out failureReason);
    }

    public bool TryMoveToGridCell(
        BattleGridMap gridMap,
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        Func<WorldSiteUnitPlacement, bool> canEnterWater,
        out string failureReason)
    {
        if (!CanPlaceOnGridCell(gridMap, site, placementId, cell, canEnterWater, out failureReason))
        {
            return false;
        }

        return _deploymentService.TryMovePlacement(site, definition, placementId, cell, out failureReason);
    }

    public bool CanPlaceOnGridCell(
        BattleGridMap gridMap,
        WorldSiteState site,
        string placementId,
        Vector2I cell,
        Func<WorldSiteUnitPlacement, bool> canEnterWater,
        out string failureReason)
    {
        failureReason = "";
        if (gridMap == null)
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        GridPosition position = new(cell.X, cell.Y);
        if (!gridMap.TryGetTopSurface(position, out GridCellSurface surface) ||
            !WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface(gridMap, surface))
        {
            failureReason = "placement_cell_blocked";
            return false;
        }

        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        bool canUseWater = canEnterWater?.Invoke(placement) == true;
        if (!canUseWater && BattleGridTerrainQueries.IsWater(surface))
        {
            failureReason = "placement_cell_water";
            return false;
        }

        return true;
    }
}
```

- [ ] **Step 2: Run tests and verify evaluator behavior passes while root source check still fails**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: evaluator behavior tests pass; `world site root delegates deployment target validation` still fails.

## Task 3: Wire `WorldSiteRoot` To Evaluator

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add evaluator field**

Near deployment service fields in `WorldSiteRoot`:

```csharp
private readonly WorldSiteDeploymentService _deploymentService = new();
private readonly WorldSiteRuntimeDeploymentCacheBuilder _deploymentCacheBuilder = new();
```

add:

```csharp
private readonly WorldSiteDeploymentTargetEvaluator _deploymentTargetEvaluator = new();
```

- [ ] **Step 2: Replace target validation delegation**

Replace the body after mouse grid resolution inside `TryEvaluateSiteDeploymentTarget` with:

```csharp
return _deploymentTargetEvaluator.CanMoveToGridCell(
    _activeGridMap,
    site,
    definition,
    placementId,
    new Vector2I(gridPosition.X, gridPosition.Y),
    ResolvePlacementCanEnterWater,
    out failureReason);
```

- [ ] **Step 3: Replace move execution delegation**

In `HandleSiteDeploymentDragInput`, replace `_deploymentService.TryMovePlacement(...)` with:

```csharp
_deploymentTargetEvaluator.TryMoveToGridCell(
    _activeGridMap,
    site,
    definition,
    placementId,
    new Vector2I(gridPosition.X, gridPosition.Y),
    ResolvePlacementCanEnterWater,
    out failureReason)
```

- [ ] **Step 4: Delete root-owned cell validator**

Delete `CanPlaceSiteDeploymentOnGridCell` from `WorldSiteRoot`.

- [ ] **Step 5: Run focused regression tests**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: all deployment cache and deployment target evaluator tests pass.

## Task 4: Verification

**Files:**
- Verify: `src/Application/World/WorldSiteDeploymentTargetEvaluator.cs`
- Verify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Verify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Run focused regression**

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: all tests pass, with any existing Godot source generator warning recorded separately.

- [ ] **Step 2: Build main project**

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 3: Source checks**

```powershell
rg -n "CanPlaceSiteDeploymentOnGridCell|BattleRuleQueries.IsWater\\(surface\\)" src/Presentation/World/Sites/WorldSiteRoot.cs
rg -n "WorldSiteDeploymentTargetEvaluator" src/Application/World src/Presentation/World/Sites/WorldSiteRoot.cs tests/WorldSiteDeploymentCacheRegression/Program.cs
```

Expected:

- First command: no output.
- Second command: matches in evaluator, root, and focused tests.

- [ ] **Step 4: Run broader WorldSite regression**

```powershell
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
```

Expected: process exit code `0`.

- [ ] **Step 5: Shut down idle build servers**

```powershell
dotnet build-server shutdown
```

Expected: command completes without killing Godot/editor/user processes.

## Self-Review Checklist

- `WorldSiteRoot` still owns UI drag flow only.
- Deployment target validation and write attempt are delegated to `WorldSiteDeploymentTargetEvaluator`.
- `WorldSiteDeploymentService` remains the authority for placement writes.
- Water validation uses `BattleGridTerrainQueries`.
- No auto battle runtime, AP, `TurnSystem`, or battle command changes were introduced.
