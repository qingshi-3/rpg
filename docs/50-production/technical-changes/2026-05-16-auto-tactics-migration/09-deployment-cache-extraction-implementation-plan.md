# Deployment Cache Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract `WorldSiteRoot` deployment cache construction into a focused, testable owner without changing battle entry, site placement, or world writeback behavior.

**Architecture:** Add a pure deployment cache builder under `Rpg.Application.World` that reads `BattleGridMap` and produces `WorldSiteDeploymentCell` candidates keyed by `WorldSiteAttackDirection`. `WorldSiteRoot` keeps only cache rebuild orchestration and logging. `WorldSiteState.UnitPlacements` remains the only site-local deployment authority; the cache remains a runtime candidate list.

**Tech Stack:** Godot 4.5 C#, .NET 8, existing console regression test projects under `tests/`, `BattleGridMap` / `GridCellSurface`, `WorldSiteDeploymentCell`, `WorldSiteAttackDirection`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/03-deployment-cache-extraction.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/08-subagent-handoff.md`
- `docs/60-qa/testcases/auto-tactics-migration.md`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Application/World/WorldSiteDeploymentCell.cs`
- `src/Domain/Battle/Grid/BattleGridMap.cs`
- `src/Domain/Battle/Grid/GridCellSurface.cs`
- `src/Domain/World/WorldSiteAttackDirection.cs`

## Scope Boundaries

- Do not build auto battle runtime in this plan.
- Do not change `WorldSiteState.UnitPlacements` semantics.
- Do not change `WorldSiteDeploymentService` behavior.
- This slice did not remove legacy battle flow; final cleanup is tracked separately.
- Do not start the Godot editor. Use static checks and focused .NET commands.
- Do not stage unrelated dirty worktree changes.

## File Structure

- Create `src/Domain/Battle/Grid/BattleGridTerrainQueries.cs`
  - Pure terrain-tag helpers for `GridCell` and `GridCellSurface`.
- Create `src/Application/World/WorldSiteRuntimeDeploymentCache.cs`
  - Immutable cache value with `SiteId`, `CandidateSurfaceCount`, `CandidatesByDirection`, and `GetCandidates`.
- Create `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`
  - Candidate filtering, direction ordering, terrain copying, water tagging, and empty-cache handling.
- Modify `src/Presentation/Battle/Rules/BattleRuleQueries.cs`
  - Delegate water checks to `BattleGridTerrainQueries` so water tagging has one pure owner.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Delete the private cache class and direction ordering method.
  - Add a builder field.
  - Replace cache construction with builder call.
  - Keep existing root behavior and logs.
- Create `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
  - Focused console regression test project.
- Create `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Direction ordering, water flag, invalid-surface exclusion, empty-cache behavior, and root delegation checks.

## Task 1: Add Failing Deployment Cache Regression Tests

**Files:**
- Create: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
- Create: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Create the regression test project file**

Create `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\rpg.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create failing behavior tests for the extracted builder**

Create `tests/WorldSiteDeploymentCacheRegression/Program.cs`:

```csharp
using Godot;
using Rpg.Application.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-world-site-deployment-cache-tests"));

Run("deployment cache orders directions and preserves water", DeploymentCacheOrdersDirectionsAndPreservesWater);
Run("deployment cache excludes invalid surfaces and keeps only top surface", DeploymentCacheExcludesInvalidSurfacesAndKeepsTopSurface);
Run("deployment cache handles missing grid as empty cache", DeploymentCacheHandlesMissingGridAsEmptyCache);
Run("deployment cache falls back to any direction", DeploymentCacheFallsBackToAnyDirection);

static void DeploymentCacheOrdersDirectionsAndPreservesWater()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 1, 1, terrainTag: "center");
    AddWalkableSurface(grid, 1, 0, terrainTag: "north");
    AddWalkableSurface(grid, 1, 2, terrainTag: "south");
    AddWalkableSurface(grid, 0, 1, terrainTag: "west");
    AddWalkableSurface(grid, 2, 1, terrainTag: "east");
    AddWalkableSurface(grid, 3, 1, terrainTag: "water");

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);

    AssertEqual(new Vector2I(1, 0), cache.GetCandidates(WorldSiteAttackDirection.North)[0].Cell, "north should start from lowest y");
    AssertEqual(new Vector2I(1, 2), cache.GetCandidates(WorldSiteAttackDirection.South)[0].Cell, "south should start from highest y");
    AssertEqual(new Vector2I(0, 1), cache.GetCandidates(WorldSiteAttackDirection.West)[0].Cell, "west should start from lowest x");
    AssertEqual(new Vector2I(3, 1), cache.GetCandidates(WorldSiteAttackDirection.East)[0].Cell, "east should start from highest x");
    AssertEqual(new Vector2I(1, 1), cache.GetCandidates(WorldSiteAttackDirection.Any)[0].Cell, "any should start near center");

    WorldSiteDeploymentCell water = cache.GetCandidates(WorldSiteAttackDirection.Any)
        .Single(item => item.Cell == new Vector2I(3, 1));
    AssertTrue(water.IsWater, "water terrain candidate should preserve IsWater");
    AssertEqual("water", water.TerrainTag, "water terrain tag should be preserved");
}

static void DeploymentCacheExcludesInvalidSurfacesAndKeepsTopSurface()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 4, 4, height: 0, terrainTag: "lower");
    AddWalkableSurface(grid, 4, 4, height: 2, terrainTag: "upper");
    AddNonWalkableFoundation(grid, 5, 5, terrainTag: "blocked");
    AddFoundationWithoutWalkability(grid, 6, 6, terrainTag: "zero_cost");

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    IReadOnlyList<WorldSiteDeploymentCell> any = cache.GetCandidates(WorldSiteAttackDirection.Any);

    AssertTrue(any.Any(item => item.Cell == new Vector2I(4, 4) && item.Height == 2), "top walkable surface should be included");
    AssertTrue(!any.Any(item => item.Cell == new Vector2I(4, 4) && item.Height == 0), "non-top surface should be excluded");
    AssertTrue(!any.Any(item => item.Cell == new Vector2I(5, 5)), "non-walkable surface should be excluded");
    AssertTrue(!any.Any(item => item.Cell == new Vector2I(6, 6)), "foundation without positive move cost should be excluded");
}

static void DeploymentCacheHandlesMissingGridAsEmptyCache()
{
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("missing_grid_site", null);

    AssertEqual("missing_grid_site", cache.SiteId, "empty cache should keep site id");
    AssertEqual(0, cache.CandidateSurfaceCount, "missing grid should have zero candidate surfaces");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.Any).Count, "any should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.North).Count, "north should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.South).Count, "south should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.West).Count, "west should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.East).Count, "east should be empty");
}

static void DeploymentCacheFallsBackToAnyDirection()
{
    WorldSiteDeploymentCell fallback = new(new Vector2I(7, 8), 1, "fallback", IsWater: false);
    WorldSiteRuntimeDeploymentCache cache = new(
        "site_under_test",
        candidateSurfaceCount: 1,
        new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>
        {
            [WorldSiteAttackDirection.Any] = new[] { fallback }
        });

    AssertEqual(fallback, cache.GetCandidates(WorldSiteAttackDirection.North)[0], "missing direction should fall back to any candidates");
}

static GridCellSurface AddWalkableSurface(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "plain")
{
    GridCellSurface surface = grid.GetOrCreateSurface(new GridPosition(x, y), height);
    surface.AddLayer(new GridCellLayerData(
        "foundation",
        LayerRole.Foundation,
        height,
        affectsWalkability: true,
        affectsLineOfSight: false,
        isHeightTransitionLayer: false,
        isVisualOnly: false,
        walkable: true,
        moveCost: 1,
        canStandOn: true,
        isObstacle: false,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}

static GridCellSurface AddNonWalkableFoundation(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "blocked")
{
    GridCellSurface surface = grid.GetOrCreateSurface(new GridPosition(x, y), height);
    surface.AddLayer(new GridCellLayerData(
        "foundation",
        LayerRole.Foundation,
        height,
        affectsWalkability: true,
        affectsLineOfSight: false,
        isHeightTransitionLayer: false,
        isVisualOnly: false,
        walkable: false,
        moveCost: 0,
        canStandOn: false,
        isObstacle: true,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}

static GridCellSurface AddFoundationWithoutWalkability(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "zero_cost")
{
    GridCellSurface surface = grid.GetOrCreateSurface(new GridPosition(x, y), height);
    surface.AddLayer(new GridCellLayerData(
        "foundation",
        LayerRole.Foundation,
        height,
        affectsWalkability: false,
        affectsLineOfSight: false,
        isHeightTransitionLayer: false,
        isVisualOnly: false,
        walkable: true,
        moveCost: 0,
        canStandOn: false,
        isObstacle: false,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}

static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        Environment.ExitCode = 1;
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}
```

- [ ] **Step 3: Run the regression project to verify it fails before implementation**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: FAIL at compile time with errors like:

```text
error CS0246: The type or namespace name 'WorldSiteRuntimeDeploymentCache' could not be found
error CS0246: The type or namespace name 'WorldSiteRuntimeDeploymentCacheBuilder' could not be found
```

- [ ] **Step 4: Commit the failing regression harness if this execution workflow uses commits**

```powershell
git add tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj tests/WorldSiteDeploymentCacheRegression/Program.cs
git commit -m "test: cover world site deployment cache construction"
```

If commits are not part of the active execution request, skip the commit command and list these files in the task handoff.

## Task 2: Implement Pure Deployment Cache Builder

**Files:**
- Create: `src/Domain/Battle/Grid/BattleGridTerrainQueries.cs`
- Create: `src/Application/World/WorldSiteRuntimeDeploymentCache.cs`
- Create: `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`
- Modify: `src/Presentation/Battle/Rules/BattleRuleQueries.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Create pure terrain-tag queries**

Create `src/Domain/Battle/Grid/BattleGridTerrainQueries.cs`:

```csharp
using System.Linq;

namespace Rpg.Domain.Battle.Grid;

public static class BattleGridTerrainQueries
{
    private const string WaterTerrainTag = "water";

    public static bool IsWater(GridCell cell)
    {
        return cell != null &&
               (string.Equals(cell.TerrainTag, WaterTerrainTag, System.StringComparison.OrdinalIgnoreCase) ||
                cell.TerrainTags.Contains(WaterTerrainTag, System.StringComparer.OrdinalIgnoreCase));
    }

    public static bool IsWater(GridCellSurface surface)
    {
        return surface != null &&
               (string.Equals(surface.TerrainTag, WaterTerrainTag, System.StringComparison.OrdinalIgnoreCase) ||
                surface.TerrainTags.Contains(WaterTerrainTag, System.StringComparer.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 2: Create the cache value object**

Create `src/Application/World/WorldSiteRuntimeDeploymentCache.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteRuntimeDeploymentCache
{
    public WorldSiteRuntimeDeploymentCache(
        string siteId,
        int candidateSurfaceCount,
        IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> candidatesByDirection)
    {
        SiteId = siteId ?? "";
        CandidateSurfaceCount = System.Math.Max(0, candidateSurfaceCount);
        CandidatesByDirection = (candidatesByDirection ?? new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>())
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? System.Array.Empty<WorldSiteDeploymentCell>());
    }

    public string SiteId { get; }
    public int CandidateSurfaceCount { get; }
    public IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> CandidatesByDirection { get; }

    public IReadOnlyList<WorldSiteDeploymentCell> GetCandidates(WorldSiteAttackDirection direction)
    {
        if (CandidatesByDirection.TryGetValue(direction, out IReadOnlyList<WorldSiteDeploymentCell> candidates) &&
            candidates.Count > 0)
        {
            return candidates;
        }

        return CandidatesByDirection.TryGetValue(WorldSiteAttackDirection.Any, out IReadOnlyList<WorldSiteDeploymentCell> anyCandidates)
            ? anyCandidates
            : System.Array.Empty<WorldSiteDeploymentCell>();
    }
}
```

- [ ] **Step 3: Create the deployment cache builder**

Create `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteRuntimeDeploymentCacheBuilder
{
    public static IReadOnlyList<WorldSiteAttackDirection> SupportedDirections { get; } = new[]
    {
        WorldSiteAttackDirection.Any,
        WorldSiteAttackDirection.North,
        WorldSiteAttackDirection.South,
        WorldSiteAttackDirection.West,
        WorldSiteAttackDirection.East
    };

    public WorldSiteRuntimeDeploymentCache Build(string siteId, BattleGridMap gridMap)
    {
        var candidatesByDirection = new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>();
        foreach (WorldSiteAttackDirection direction in SupportedDirections)
        {
            candidatesByDirection[direction] = System.Array.Empty<WorldSiteDeploymentCell>();
        }

        if (gridMap == null)
        {
            return new WorldSiteRuntimeDeploymentCache(siteId, candidateSurfaceCount: 0, candidatesByDirection);
        }

        GridCellSurface[] surfaces = gridMap.Surfaces.Values
            .Where(surface => IsDeploymentCandidateSurface(gridMap, surface))
            .ToArray();

        foreach (WorldSiteAttackDirection direction in SupportedDirections)
        {
            candidatesByDirection[direction] = surfaces.Length == 0
                ? System.Array.Empty<WorldSiteDeploymentCell>()
                : OrderDeploymentSurfaceCandidates(surfaces, direction)
                    .Select(surface => new WorldSiteDeploymentCell(
                        new Vector2I(surface.Position.X, surface.Position.Y),
                        surface.Height,
                        surface.TerrainTag ?? "",
                        BattleGridTerrainQueries.IsWater(surface)))
                    .ToArray();
        }

        return new WorldSiteRuntimeDeploymentCache(siteId, surfaces.Length, candidatesByDirection);
    }

    public static bool IsDeploymentCandidateSurface(BattleGridMap gridMap, GridCellSurface surface)
    {
        return gridMap != null &&
               surface is { IsWalkable: true, MoveCost: > 0 } &&
               gridMap.IsTopSurface(surface.SurfacePosition);
    }

    private static IEnumerable<GridCellSurface> OrderDeploymentSurfaceCandidates(
        IReadOnlyCollection<GridCellSurface> candidates,
        WorldSiteAttackDirection direction)
    {
        int minX = candidates.Min(surface => surface.Position.X);
        int maxX = candidates.Max(surface => surface.Position.X);
        int minY = candidates.Min(surface => surface.Position.Y);
        int maxY = candidates.Max(surface => surface.Position.Y);
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;

        return direction switch
        {
            WorldSiteAttackDirection.North => candidates
                .OrderBy(surface => surface.Position.Y)
                .ThenBy(surface => System.Math.Abs(surface.Position.X - centerX))
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.South => candidates
                .OrderByDescending(surface => surface.Position.Y)
                .ThenBy(surface => System.Math.Abs(surface.Position.X - centerX))
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.West => candidates
                .OrderBy(surface => surface.Position.X)
                .ThenBy(surface => System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.East => candidates
                .OrderByDescending(surface => surface.Position.X)
                .ThenBy(surface => System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Height),
            _ => candidates
                .OrderBy(surface => System.Math.Abs(surface.Position.X - centerX) + System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height)
        };
    }
}
```

- [ ] **Step 4: Delegate presentation water checks to the domain query**

Modify `src/Presentation/Battle/Rules/BattleRuleQueries.cs`:

```csharp
public static bool IsWater(GridCell cell)
{
    return BattleGridTerrainQueries.IsWater(cell);
}

public static bool IsWater(GridCellSurface surface)
{
    return BattleGridTerrainQueries.IsWater(surface);
}
```

Remove the private `WaterTerrainTag` constant from `BattleRuleQueries` after both methods delegate to `BattleGridTerrainQueries`.

- [ ] **Step 5: Run the deployment cache regression tests**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected:

```text
PASS deployment cache orders directions and preserves water
PASS deployment cache excludes invalid surfaces and keeps only top surface
PASS deployment cache handles missing grid as empty cache
PASS deployment cache falls back to any direction
```

- [ ] **Step 6: Commit the builder implementation if this execution workflow uses commits**

```powershell
git add src/Domain/Battle/Grid/BattleGridTerrainQueries.cs src/Application/World/WorldSiteRuntimeDeploymentCache.cs src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs src/Presentation/Battle/Rules/BattleRuleQueries.cs tests/WorldSiteDeploymentCacheRegression/Program.cs
git commit -m "feat: extract world site deployment cache builder"
```

If commits are not part of the active execution request, skip the commit command and list these files in the task handoff.

## Task 3: Wire `WorldSiteRoot` To The Extracted Builder

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- Test: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add source regression checks for root delegation**

Append this run line near the other `Run(...)` calls in `tests/WorldSiteDeploymentCacheRegression/Program.cs`:

```csharp
Run("world site root delegates deployment cache construction", WorldSiteRootDelegatesDeploymentCacheConstruction);
```

Append these helper methods before the assertion helpers:

```csharp
static void WorldSiteRootDelegatesDeploymentCacheConstruction()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        !rootSource.Contains("private sealed class WorldSiteRuntimeDeploymentCache", StringComparison.Ordinal),
        "WorldSiteRoot should not own the deployment cache value object");
    AssertTrue(
        !rootSource.Contains("OrderDeploymentSurfaceCandidates", StringComparison.Ordinal),
        "WorldSiteRoot should not own deployment candidate ordering");
    AssertTrue(
        rootSource.Contains("_deploymentCacheBuilder.Build", StringComparison.Ordinal),
        "WorldSiteRoot should build deployment cache through WorldSiteRuntimeDeploymentCacheBuilder");
    AssertTrue(
        rootSource.Contains("WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface", StringComparison.Ordinal),
        "WorldSiteRoot placement validation should reuse builder candidate filtering");
}

static string ProjectRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "rpg.csproj")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate project root from test output directory.");
}
```

- [ ] **Step 2: Run tests to verify the new source regression fails before root wiring**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: FAIL for `world site root delegates deployment cache construction`, with at least one message about `WorldSiteRoot` still owning cache or ordering.

- [ ] **Step 3: Remove the private cache class from `WorldSiteRoot`**

In `src/Presentation/World/Sites/WorldSiteRoot.cs`, delete the nested class currently shaped like:

```csharp
private sealed class WorldSiteRuntimeDeploymentCache
{
    public WorldSiteRuntimeDeploymentCache(
        string siteId,
        Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>> candidatesByDirection)
    {
        SiteId = siteId ?? "";
        CandidatesByDirection = candidatesByDirection ?? new Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>>();
    }

    public string SiteId { get; }
    public Dictionary<WorldSiteAttackDirection, List<WorldSiteDeploymentCell>> CandidatesByDirection { get; }

    public IReadOnlyList<WorldSiteDeploymentCell> GetCandidates(WorldSiteAttackDirection direction)
    {
        if (CandidatesByDirection.TryGetValue(direction, out List<WorldSiteDeploymentCell> candidates) &&
            candidates.Count > 0)
        {
            return candidates;
        }

        return CandidatesByDirection.TryGetValue(WorldSiteAttackDirection.Any, out List<WorldSiteDeploymentCell> anyCandidates)
            ? anyCandidates
            : System.Array.Empty<WorldSiteDeploymentCell>();
    }
}
```

- [ ] **Step 4: Add the builder field to `WorldSiteRoot`**

Near the existing deployment service field:

```csharp
private readonly WorldSiteDeploymentService _deploymentService = new();
```

add:

```csharp
private readonly WorldSiteRuntimeDeploymentCacheBuilder _deploymentCacheBuilder = new();
```

- [ ] **Step 5: Replace root cache construction with a builder call**

Replace `RebuildSiteDeploymentRuntimeCache` in `src/Presentation/World/Sites/WorldSiteRoot.cs` with:

```csharp
private void RebuildSiteDeploymentRuntimeCache(string siteId)
{
    _deploymentCache = _deploymentCacheBuilder.Build(siteId, _activeGridMap);
    if (_activeGridMap == null)
    {
        GameLog.Warn(nameof(WorldSiteRoot), $"Cannot build site deployment cache site={siteId} reason=grid_missing");
        return;
    }

    string counts = string.Join(
        " ",
        WorldSiteRuntimeDeploymentCacheBuilder.SupportedDirections
            .Select(direction => $"{direction}={_deploymentCache.GetCandidates(direction).Count}"));
    GameLog.Info(nameof(WorldSiteRoot), $"SiteDeploymentCacheBuilt site={siteId} surfaces={_deploymentCache.CandidateSurfaceCount} {counts}");
}
```

- [ ] **Step 6: Make root placement validation reuse the builder filter**

Replace the body of `IsDeploymentCandidateSurface` in `src/Presentation/World/Sites/WorldSiteRoot.cs` with:

```csharp
private bool IsDeploymentCandidateSurface(GridCellSurface surface)
{
    return WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface(_activeGridMap, surface);
}
```

This keeps root call sites stable while moving the filtering rule to the extracted owner.

- [ ] **Step 7: Delete root-owned direction ordering**

Delete `OrderDeploymentSurfaceCandidates` from `src/Presentation/World/Sites/WorldSiteRoot.cs`. After deletion, this search should return no matches in the root file:

```powershell
rg -n "OrderDeploymentSurfaceCandidates" src/Presentation/World/Sites/WorldSiteRoot.cs
```

Expected: no output.

- [ ] **Step 8: Run the deployment cache regression tests**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected:

```text
PASS deployment cache orders directions and preserves water
PASS deployment cache excludes invalid surfaces and keeps only top surface
PASS deployment cache handles missing grid as empty cache
PASS deployment cache falls back to any direction
PASS world site root delegates deployment cache construction
```

- [ ] **Step 9: Commit the root wiring if this execution workflow uses commits**

```powershell
git add src/Presentation/World/Sites/WorldSiteRoot.cs tests/WorldSiteDeploymentCacheRegression/Program.cs
git commit -m "refactor: delegate world site deployment cache construction"
```

If commits are not part of the active execution request, skip the commit command and list these files in the task handoff.

## Task 4: Verification And Handoff

**Files:**
- Verify: `src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs`
- Verify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Verify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Run the focused regression tests**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: all five `PASS` lines from Task 3 Step 8 and process exit code `0`.

- [ ] **Step 2: Build the main project with low concurrency**

Run:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If the project already has unrelated warnings, record the exact warning count and confirm there are `0 Error(s)`.

- [ ] **Step 3: Run targeted source checks**

Run:

```powershell
rg -n "private sealed class WorldSiteRuntimeDeploymentCache|OrderDeploymentSurfaceCandidates" src/Presentation/World/Sites/WorldSiteRoot.cs
rg -n "WorldSiteRuntimeDeploymentCacheBuilder" src/Application/World src/Presentation/World/Sites/WorldSiteRoot.cs tests/WorldSiteDeploymentCacheRegression/Program.cs
rg -n "BattleGridTerrainQueries.IsWater" src/Presentation/Battle/Rules/BattleRuleQueries.cs src/Application/World/WorldSiteRuntimeDeploymentCacheBuilder.cs
```

Expected:

- First command: no output.
- Second command: matches in the new builder, root, and regression tests.
- Third command: matches in `BattleRuleQueries.cs` and `WorldSiteRuntimeDeploymentCacheBuilder.cs`.

- [ ] **Step 4: Run broader existing regression coverage if time allows**

Run:

```powershell
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
```

Expected: process exit code `0`. This test project includes existing source-level checks around `WorldSiteRoot` and battle request behavior.

- [ ] **Step 5: Shut down idle build servers after verification**

Run:

```powershell
dotnet build-server shutdown
```

Expected: command completes without killing Godot/editor/user processes.

- [ ] **Step 6: Prepare handoff summary**

Include these items in the handoff:

- changed files;
- test commands run and exact pass/fail status;
- confirmation that `WorldSiteState.UnitPlacements` semantics were not changed;
- confirmation that auto battle runtime was not implemented in this slice;
- any unverified manual smoke checks from `docs/60-qa/testcases/auto-tactics-migration.md`.

## Self-Review Checklist For Implementer

- Deployment candidate ordering still matches the old root algorithm.
- Water tagging is preserved through `BattleGridTerrainQueries`.
- Missing grid produces an empty cache, not a second placement authority.
- `WorldSiteRoot` no longer owns the cache class or ordering algorithm.
- `WorldSiteDeploymentService` remains the writer for `WorldSiteState.UnitPlacements`.
- No new code references AP, `TurnSystem`, or manual battle commands.
- No code path lets battle runtime mutate `StrategicWorldState` or persist `WorldSiteState`.
