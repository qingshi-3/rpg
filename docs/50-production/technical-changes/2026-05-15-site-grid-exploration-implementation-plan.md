# Site Grid Exploration Discrete Realtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do not commit unless the user explicitly asks for a commit.

**Goal:** Build the first `WorldSite` exploration loop as a pausable discrete realtime simulation with player movement, fixed-route patrol movement, fixed alert radius detection, deterministic interest-point actions, and battle handoff.

**Architecture:** Keep runtime authority in `WorldSiteState.Exploration` and authored structure in `WorldSiteDefinition`. `WorldSiteExplorationService` owns deterministic state mutation and tick progression; `WorldSiteRoot` only projects state, captures input intent, and calls the service. Battle remains separate: exploration builds `BattleStartRequest` context but does not touch `TurnSystem` or battle AP.

**Tech Stack:** Godot 4 C#, existing `BattleGridMap`, `MovementRangeFinder`, `WorldSiteRoot`, `BattleSessionHandoff`, regression console tests under `tests/BattleHitFeedbackRegression`.

---

## Documentation Impact

`Medium`: this implements existing gameplay and technical contracts for site grid exploration. Keep authoritative design in:

- `docs/20-game-design/strategic-map/strategic-world-site-grid-exploration.md`
- `docs/30-technical-design/world/strategic-world-site-grid-exploration.md`

If implementation needs to change a contract, update those focused documents in the same change set. Do not add implementation progress or temporary inventory to `AGENTS.md`.

## File Map

- Modify: `src/Domain/World/WorldSiteExplorationState.cs`
  - Add persistent simulation state: party exploration AP, pause flag, pending path, patrol states, active alert trigger.
- Create: `src/Domain/World/SiteExplorationPatrolState.cs`
  - Persist patrol actor position, route index, AP, and removed state.
- Modify: `src/Definitions/World/WorldSiteDefinition.cs`
  - Add authored `ExplorationPatrols`.
- Create: `src/Definitions/World/SiteExplorationPatrolDefinition.cs`
  - Define fixed patrol route, alert radius, AP regen, move cost, and initial active flag.
- Modify: `src/Application/World/WorldSiteExplorationService.cs`
  - Add path intent storage, `AdvanceTick`, patrol movement, alert-radius detection, and richer battle request context.
- Modify: `src/Application/Battle/BattleStartRequest.cs`
  - Add explicit exploration context fields instead of encoding all context only in `ObjectiveIds`.
- Modify: `src/Application/World/StrategicWorldV1DefinitionFactory.cs`
  - Add mock-but-long-term-shaped exploration points and patrol route for the first site slice.
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Replace immediate exploration teleport with move intent + tick progression; render party and patrol markers; pause on alert.
- Modify: `tests/BattleHitFeedbackRegression/Program.cs`
  - Add regression coverage for tick movement, patrol AP, alert radius, pause behavior, and battle request context.
- Optional Modify: `docs/60-qa/testcases/strategic-world-v1.md`
  - Add manual acceptance only after implementation exists and UX is testable.

---

## Task 1: Persist Exploration Simulation State

**Files:**
- Modify: `src/Domain/World/WorldSiteExplorationState.cs`
- Create: `src/Domain/World/SiteExplorationPatrolState.cs`

- [ ] **Step 1: Extend `WorldSiteExplorationState` with long-term simulation fields**

Add fields that are valid for both mock content and authored content:

```csharp
using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class WorldSiteExplorationState
{
    public int CurrentCellX { get; set; }
    public int CurrentCellY { get; set; }
    public int CurrentCellHeight { get; set; }
    public int PartyActionPoints { get; set; }
    public bool IsSimulationPaused { get; set; } = true;
    public string PauseReason { get; set; } = "";
    public int AlertLevel { get; set; }
    public string ActiveAlertPatrolId { get; set; } = "";
    public string PendingInteractionPointId { get; set; } = "";
    public List<string> PendingPathCellKeys { get; set; } = new();
    public List<string> RevealedCellKeys { get; set; } = new();
    public List<string> VisitedCellKeys { get; set; } = new();
    public List<string> RevealedPointIds { get; set; } = new();
    public List<string> ResolvedPointIds { get; set; } = new();
    public List<SiteExplorationPatrolState> PatrolUnits { get; set; } = new();
}
```

- [ ] **Step 2: Add patrol state type**

Create `src/Domain/World/SiteExplorationPatrolState.cs`:

```csharp
namespace Rpg.Domain.World;

public sealed class SiteExplorationPatrolState
{
    public string PatrolId { get; set; } = "";
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
    public int RouteIndex { get; set; }
    public int ActionPoints { get; set; }
    public bool IsRemoved { get; set; }
}
```

- [ ] **Step 3: Preserve backward compatibility for existing initialized states**

Do not add constructors that overwrite existing list instances. Existing default property initializers keep old saves and tests valid when deserialized or constructed without patrol state.

---

## Task 2: Add Authored Patrol Definitions

**Files:**
- Modify: `src/Definitions/World/WorldSiteDefinition.cs`
- Create: `src/Definitions/World/SiteExplorationPatrolDefinition.cs`

- [ ] **Step 1: Add patrol definitions to `WorldSiteDefinition`**

Add this property near `ExplorationPoints`:

```csharp
public List<SiteExplorationPatrolDefinition> ExplorationPatrols { get; set; } = new();
```

- [ ] **Step 2: Add patrol definition type**

Create `src/Definitions/World/SiteExplorationPatrolDefinition.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class SiteExplorationPatrolDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<SiteExplorationRouteCellDefinition> RouteCells { get; set; } = new();
    public int AlertRadiusCells { get; set; } = 2;
    public int ActionPointRegenPerTick { get; set; } = 1;
    public int MoveCostPerCell { get; set; } = 1;
    public bool InitiallyActive { get; set; } = true;
}
```

- [ ] **Step 3: Add route cell definition type in the same file**

Use a separate public type in the same file so route data can be reused by tooling later:

```csharp
namespace Rpg.Definitions.World;

public sealed class SiteExplorationRouteCellDefinition
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
}
```

If C# namespace declarations conflict, keep both classes under one file-scoped namespace declaration.

---

## Task 3: Add Pure Tick Progression to `WorldSiteExplorationService`

**Files:**
- Modify: `src/Application/World/WorldSiteExplorationService.cs`

- [ ] **Step 1: Add result models inside or near the service**

Add compact result types so presentation does not infer state transitions from mutated fields:

```csharp
public sealed class SiteExplorationTickResult
{
    public bool PartyMoved { get; set; }
    public bool PatrolMoved { get; set; }
    public bool Paused { get; set; }
    public string PauseReason { get; set; } = "";
    public string AlertPatrolId { get; set; } = "";
    public List<GridSurfacePosition> PartyPathStep { get; } = new();
    public List<SiteExplorationPatrolMove> PatrolMoves { get; } = new();
}

public sealed class SiteExplorationPatrolMove
{
    public string PatrolId { get; set; } = "";
    public GridSurfacePosition From { get; set; }
    public GridSurfacePosition To { get; set; }
}
```

- [ ] **Step 2: Replace immediate movement authority with move intent helper**

Keep `TryMoveParty` for compatibility if existing tests use it, but add a new method that stores a path instead of directly teleporting the party:

```csharp
public static bool TrySetPartyMoveIntent(
    WorldSiteExplorationState exploration,
    BattleGridMap gridMap,
    GridPosition destination,
    out IReadOnlyList<GridSurfacePosition> path,
    out string failureReason)
{
    path = Array.Empty<GridSurfacePosition>();
    failureReason = "";
    if (!TryBuildPath(exploration, gridMap, destination, out path, out failureReason))
    {
        return false;
    }

    exploration.PendingPathCellKeys.Clear();
    foreach (GridSurfacePosition cell in path.Skip(1))
    {
        exploration.PendingPathCellKeys.Add(ToCellKey(cell));
    }

    exploration.IsSimulationPaused = false;
    exploration.PauseReason = "";
    return true;
}
```

Move the shared path logic from `TryMoveParty` into `TryBuildPath(...)` so both old and new paths use the same authority.

- [ ] **Step 3: Add `AdvanceTick` signature**

Use integer AP for deterministic simulation:

```csharp
public static SiteExplorationTickResult AdvanceTick(
    WorldSiteExplorationState exploration,
    WorldSiteDefinition siteDefinition,
    BattleGridMap gridMap,
    int partyActionPointRegenPerTick = 1,
    int partyMoveCostPerCell = 1)
```

Rules inside `AdvanceTick`:

```text
if exploration/state/definition/grid missing -> return paused result with reason exploration_missing
if IsSimulationPaused -> return no-op result
add party AP
advance party one pending path cell if AP >= move cost
add patrol AP for each active patrol
advance each patrol one route cell if AP >= move cost
mark visited/revealed after party movement
check alert radius after all movement
pause on alert, arrival, invalid path cell, or missing patrol route
```

- [ ] **Step 4: Implement party one-cell movement from pending path**

Parse the first key in `PendingPathCellKeys`, validate it is a top walkable surface, move one cell, and remove the key:

```csharp
if (exploration.PendingPathCellKeys.Count > 0 && exploration.PartyActionPoints >= partyMoveCostPerCell)
{
    exploration.PartyActionPoints -= partyMoveCostPerCell;
    if (!TryParseCellKey(exploration.PendingPathCellKeys[0], out GridSurfacePosition next) ||
        !gridMap.TryGetSurface(next, out GridCellSurface surface) ||
        !surface.IsWalkable ||
        !gridMap.IsTopSurface(next))
    {
        exploration.IsSimulationPaused = true;
        exploration.PauseReason = "exploration_path_invalid";
        result.Paused = true;
        result.PauseReason = exploration.PauseReason;
        return result;
    }

    GridSurfacePosition previous = new(exploration.CurrentCellX, exploration.CurrentCellY, exploration.CurrentCellHeight);
    exploration.CurrentCellX = next.X;
    exploration.CurrentCellY = next.Y;
    exploration.CurrentCellHeight = next.Height;
    exploration.PendingPathCellKeys.RemoveAt(0);
    MarkVisited(exploration, next);
    result.PartyMoved = true;
    result.PartyPathStep.Add(previous);
    result.PartyPathStep.Add(next);
}
```

- [ ] **Step 5: Implement fixed-route patrol movement**

For each active patrol state, find its definition by id. If missing, pause with `exploration_patrol_definition_missing`. For route length below 2, do not move but still allow detection. For valid routes:

```text
state.ActionPoints += definition.ActionPointRegenPerTick
if state.ActionPoints >= definition.MoveCostPerCell:
  state.ActionPoints -= definition.MoveCostPerCell
  nextIndex = (state.RouteIndex + 1) % definition.RouteCells.Count
  state cell = route[nextIndex]
  state.RouteIndex = nextIndex
```

Validate route cells against `BattleGridMap`; invalid route cells should pause with `exploration_patrol_route_invalid` instead of silently teleporting.

- [ ] **Step 6: Implement fixed alert radius detection**

Use Manhattan distance for the first version:

```csharp
private static int GetManhattanDistance(GridSurfacePosition a, GridSurfacePosition b)
{
    return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Height - b.Height);
}
```

When any active patrol is within `AlertRadiusCells`:

```text
exploration.IsSimulationPaused = true
exploration.PauseReason = exploration_alert_radius
exploration.ActiveAlertPatrolId = patrolId
result.Paused = true
result.AlertPatrolId = patrolId
```

- [ ] **Step 7: Add helper to initialize missing patrol states from definitions**

Add:

```csharp
public static void EnsurePatrolStates(WorldSiteExplorationState exploration, WorldSiteDefinition definition)
```

It should add state only for active definitions that are not already present and have at least one route cell. Initial position is `RouteCells[0]`, `RouteIndex = 0`, `ActionPoints = 0`, `IsRemoved = false`.

---

## Task 4: Make BattleStartRequest Carry Explicit Exploration Context

**Files:**
- Modify: `src/Application/Battle/BattleStartRequest.cs`
- Modify: `src/Application/World/WorldSiteExplorationService.cs`
- Modify: `tests/BattleHitFeedbackRegression/Program.cs`

- [ ] **Step 1: Add explicit fields to `BattleStartRequest`**

Add fields near `ObjectiveIds` or after site ids:

```csharp
public string ExplorationPointId { get; set; } = "";
public string ExplorationTriggerPatrolId { get; set; } = "";
public int ExplorationEntryCellX { get; set; }
public int ExplorationEntryCellY { get; set; }
public int ExplorationEntryCellHeight { get; set; }
public int ExplorationAlertLevel { get; set; }
```

- [ ] **Step 2: Extend `BuildExplorationBattleRequest` signature**

Change the method to accept an optional patrol trigger:

```csharp
public static BattleStartRequest BuildExplorationBattleRequest(
    string siteId,
    string pointId,
    string triggerPatrolId,
    GridSurfacePosition entryCell,
    int alertLevel,
    string returnScenePath,
    string siteScenePath)
```

Populate both explicit fields and existing `ObjectiveIds` for compatibility:

```csharp
request.ExplorationPointId = pointId ?? "";
request.ExplorationTriggerPatrolId = triggerPatrolId ?? "";
request.ExplorationEntryCellX = entryCell.X;
request.ExplorationEntryCellY = entryCell.Y;
request.ExplorationEntryCellHeight = entryCell.Height;
request.ExplorationAlertLevel = Math.Max(0, alertLevel);
request.ObjectiveIds.Add($"exploration_cell={entryCell.X}:{entryCell.Y}:{entryCell.Height}");
request.ObjectiveIds.Add($"exploration_alert={Math.Max(0, alertLevel)}");
if (!string.IsNullOrWhiteSpace(triggerPatrolId))
{
    request.ObjectiveIds.Add($"exploration_patrol={triggerPatrolId}");
}
```

- [ ] **Step 3: Update existing test call sites**

Existing tests that call `BuildExplorationBattleRequest` should pass `""` for `triggerPatrolId` unless they are testing patrol detection.

---

## Task 5: Add Mock Content Through Long-Term Definition Shapes

**Files:**
- Modify: `src/Application/World/StrategicWorldV1DefinitionFactory.cs`

- [ ] **Step 1: Add one patrol definition to the first exploration site**

Find the `WorldSiteDefinition` for the first target site, likely `StrategicWorldIds.SiteBonefield`. Add `ExplorationPatrols` using authored route cells that exist on the current site grid. If exact grid cells differ after map inspection, keep the route in definition shape and adjust only coordinates.

Use this shape:

```csharp
ExplorationPatrols =
{
    new SiteExplorationPatrolDefinition
    {
        Id = "bonefield_patrol_01",
        DisplayName = "骸骨巡逻队",
        AlertRadiusCells = 2,
        ActionPointRegenPerTick = 1,
        MoveCostPerCell = 2,
        InitiallyActive = true,
        RouteCells =
        {
            new SiteExplorationRouteCellDefinition { CellX = 3, CellY = 3, CellHeight = 0 },
            new SiteExplorationRouteCellDefinition { CellX = 5, CellY = 3, CellHeight = 0 },
            new SiteExplorationRouteCellDefinition { CellX = 5, CellY = 5, CellHeight = 0 },
            new SiteExplorationRouteCellDefinition { CellX = 3, CellY = 5, CellHeight = 0 }
        }
    }
}
```

- [ ] **Step 2: Keep mock data honest**

Do not create patrol nodes directly in `WorldSiteRoot` as the source of truth. `WorldSiteRoot` may create marker nodes, but only after reading `WorldSiteState.Exploration.PatrolUnits` and `WorldSiteDefinition.ExplorationPatrols`.

---

## Task 6: Integrate Tick Simulation Into `WorldSiteRoot`

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`

- [ ] **Step 1: Add runtime presentation fields**

Add fields near `_siteExplorationPartyMarker`:

```csharp
private readonly Dictionary<string, Node2D> _siteExplorationPatrolMarkers = new(System.StringComparer.Ordinal);
private double _siteExplorationTickAccumulator;
private const double SiteExplorationTickSeconds = 0.25;
```

- [ ] **Step 2: Change `_Process` non-battle branch to advance exploration ticks**

Inside `_Process`, when `_battleRuntimeEnabled` is false, call a new helper before `UpdateSiteMapEntities()`:

```csharp
if (!_battleRuntimeEnabled)
{
    AdvanceSiteExplorationPresentation(delta);
    UpdateSiteMapEntities();
    return;
}
```

- [ ] **Step 3: Replace immediate click movement with move intent**

In `TryHandleSiteExplorationInput`, replace `TryMoveParty(...)` with `TrySetPartyMoveIntent(...)`. The handler should set intent, show path preview if available, and let `_Process` ticks move the marker.

Expected behavior:

```text
click unreachable cell -> notice failure reason, no state mutation
click reachable cell -> pending path stored, simulation unpaused
arrival -> simulation pauses with reason exploration_arrived
```

- [ ] **Step 4: Add `AdvanceSiteExplorationPresentation` helper**

The helper should:

```text
return if current runtime mode is not Exploration
return if no active grid map or current site state/definition
accumulate delta
while accumulator >= SiteExplorationTickSeconds:
  call EnsurePatrolStates
  call WorldSiteExplorationService.AdvanceTick
  move party marker if result.PartyMoved
  refresh patrol markers if result.PatrolMoved
  if result.Paused, show site notice and break
```

- [ ] **Step 5: Render patrol markers from state**

Add helpers:

```text
RefreshSiteExplorationPatrolPresentation(siteState, siteDefinition)
EnsureSiteExplorationPatrolMarker(patrolState, patrolDefinition)
DrawSiteExplorationPatrolMarker(marker)
```

Marker visuals can be simple authored-code circles for the first slice, but they are presentation only. Use a different color from the player marker and include the patrol display name in hover/notice later if needed.

- [ ] **Step 6: Pause and request battle on alert**

When `AdvanceTick` returns `PauseReason == "exploration_alert_radius"`, show a clear Chinese notice:

```text
被骸骨巡逻队发现，准备进入遭遇战。
```

First implementation may immediately build and hand off battle if existing UX has no confirmation panel. If adding confirmation UI, it must still use `BattleStartRequest` and not modify battle internals.

---

## Task 7: Regression Tests for Long-Term Boundaries

**Files:**
- Modify: `tests/BattleHitFeedbackRegression/Program.cs`

- [ ] **Step 1: Add test registration**

Add registrations near existing exploration tests:

```csharp
Run("site exploration tick moves party by exploration AP", SiteExplorationTickMovesPartyByExplorationAp);
Run("site exploration tick moves patrol by route AP", SiteExplorationTickMovesPatrolByRouteAp);
Run("site exploration alert radius pauses simulation", SiteExplorationAlertRadiusPausesSimulation);
Run("exploration battle request carries patrol trigger", ExplorationBattleRequestCarriesPatrolTrigger);
```

- [ ] **Step 2: Add party tick test**

Build a small `BattleGridMap` using existing test helpers in the file. The test should:

```text
create exploration at 0:0:0
set destination 2:0:0
call TrySetPartyMoveIntent
call AdvanceTick once with regen 1 and cost 1
assert current cell moved to 1:0:0
assert battle AP/TurnSystem are not referenced by the service API
```

- [ ] **Step 3: Add patrol route test**

Create a `WorldSiteDefinition` with one patrol route from `3:0:0` to `4:0:0`, initialize state, call `EnsurePatrolStates`, call `AdvanceTick`, and assert patrol cell becomes `4:0:0` only when AP covers move cost.

- [ ] **Step 4: Add alert radius test**

Set player at `2:0:0`, patrol at `4:0:0`, alert radius `2`. Call `AdvanceTick` and assert:

```text
result.Paused == true
result.AlertPatrolId == patrol id
exploration.IsSimulationPaused == true
exploration.PauseReason == exploration_alert_radius
```

- [ ] **Step 5: Add battle request patrol context test**

Call `BuildExplorationBattleRequest` with `triggerPatrolId = "bonefield_patrol_01"` and assert explicit fields are populated:

```text
request.ExplorationTriggerPatrolId == "bonefield_patrol_01"
request.ExplorationEntryCellX/Y/Height match input
request.ExplorationAlertLevel matches input
request.ObjectiveIds contains exploration_patrol=bonefield_patrol_01
```

---

## Task 8: Manual QA Route After Implementation

**Files:**
- Optional Modify: `docs/60-qa/testcases/strategic-world-v1.md`

- [ ] **Step 1: Add manual checks only after the feature is visible in-game**

Add a concise section for:

```text
进入埋骨地探索态。
点击远处可达格。
观察玩家 marker 按 tick 移动。
观察巡逻 marker 按固定路线移动。
进入警戒半径后探索暂停并显示被发现原因。
确认进入战斗后 Battle HUD 出现。
战斗结束后探索/经营状态按结果回写。
```

Do not add QA claims before the implementation actually exposes the behavior.

---

## Validation Commands

Run only when the user asks for validation or when executing this plan under an explicit validation phase:

```powershell
dotnet build D:\godot\rpg\tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -maxcpucount:2 -v:minimal
```

Expected result:

```text
Build succeeded.
```

If a broader project build is requested, use low concurrency:

```powershell
dotnet build <solution-or-project> -maxcpucount:2 -v:minimal
```

---

## Non-Goals

- Do not add combat AP to exploration.
- Do not call `BattleTurnController` from exploration simulation.
- Do not use physics collision as movement authority.
- Do not create patrol state only in scene nodes.
- Do not implement pursuit/search AI, vision cones, hearing, or random stealth checks in this slice.
- Do not silently fall back to teleporting when a path, patrol route, or alert context is invalid.
