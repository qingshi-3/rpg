# Square-Grid Realtime Combat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current automatic chess-like runtime with square-grid anchored realtime combat for the first battle slice.

**Architecture:** Runtime remains the combat authority and uses square-grid anchored cells, reservations, actor targets, and attack events. Presentation consumes runtime movement/damage events for interpolation and feedback; authored Godot collision shapes support selection and hover only.

**Tech Stack:** Godot 4 C#, `.tscn` authored resources, custom console regression projects, `dotnet build rpg.sln -maxcpucount:2 -v:minimal`.

**Performance Constraints:** The first slice must avoid per-actor full-map pathfinding and Godot physics callbacks as rule authority. Runtime work should be bounded by active actor count and fixed neighbor candidates; event count remains bounded by runtime tick limit and actor count.

---

## File Structure

- Modify `src/Runtime/Battle/BattleRuntimeActor.cs`: add anchored-cell state, optional reservation, target id, attack range, and motion state.
- Create `src/Runtime/Battle/BattleRuntimeActorMotionState.cs`: runtime-only actor state enum.
- Modify `src/Runtime/Battle/Events/BattleEvent.cs`: add authoritative movement cell fields for playback.
- Modify `src/Runtime/Battle/BattleRuntimeSession.cs`: replace Manhattan one-axis advance with 8-neighbor anchored-cell state machine.
- Create `src/Definitions/Battle/Abilities/AbilityTargetMode.cs`, `AbilityDirectionMode.cs`, and `AbilityAreaShape.cs`: data contract extension points.
- Modify `src/Definitions/Battle/Abilities/AbilityDefinition.cs`: export target, direction, and area shape metadata with safe defaults.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`: consume runtime movement cells directly and stop creating separate visual approach truth before damage playback.
- Modify `scenes/battle/entities/units/BattleUnitBase.tscn`: add authored `CollisionShape2D` for interaction and debug.
- Modify `tests/TargetBattleArchitectureRegression/Program.cs`: cover 8-neighbor square-grid movement, anchored attacks, and movement event cells.
- Modify `tests/TargetBattleArchitectureRegression/Program.cs`: guard against runtime dependencies on Godot physics and presentation pathfinding.
- Modify `tests/BattleHitFeedbackRegression/Program.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs`: cover battle unit scene collision authoring and ability spatial defaults.
- Modify `src/Definitions/Battle/BattleUnitDefinition.cs`: export footprint width and height with `1x1` defaults.
- Modify `src/Application/Battle/BattleForceRequest.cs` and `src/Application/Battle/Snapshots/BattleGroupSnapshot.cs`: carry footprint width and height from request to snapshot.
- Create `src/Application/Battle/BattleFootprintCells.cs`: share bounded top-left-anchor footprint cell enumeration for deployment previews and validation.
- Modify `src/Runtime/Battle/BattleRuntimeActor.cs` and `src/Runtime/Battle/BattleRuntimeSession.cs`: evaluate occupancy, reservations, and range by rectangular footprints.
- Modify `src/Presentation/Battle/Entities/BattleUnitFactory.cs`: derive a uniform footprint visual scale from the largest footprint side and a tuning coefficient, without stretching sprites by raw footprint width and height.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.SiteInteraction.cs`: show and validate pre-battle deployment drag previews by resizing the existing hover selection frame to all footprint-covered cells, snapping dragged sprites to the footprint center, and moving the anchor only after the pointer crosses the footprint-center half-cell threshold.

## Task 1: Runtime Regression Tests

**Files:**
- Modify: `tests/TargetBattleArchitectureRegression/Program.cs`

- [ ] **Step 1: Add test registrations**

Add these runs after `runtime auto battle resolves opposing factions from actor state`:

```csharp
Run("runtime uses 8-neighbor square-grid movement", RuntimeUsesEightNeighborSquareGridMovement);
Run("runtime diagonal adjacency is in basic attack range", RuntimeDiagonalAdjacencyIsInBasicAttackRange);
Run("runtime movement events carry authoritative cells", RuntimeMovementEventsCarryAuthoritativeCells);
Run("runtime square-grid combat avoids physics and full-map pathfinding authority", RuntimeSquareGridCombatAvoidsPhysicsAndFullMapPathfindingAuthority);
```

- [ ] **Step 2: Add the failing tests**

```csharp
static void RuntimeUsesEightNeighborSquareGridMovement()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_diagonal_move", 80, 80, enemyCellX: 2, enemyCellY: 2);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

    BattleEvent firstMove = result.EventStream.Events
        .FirstOrDefault(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == "force_player:1");

    AssertTrue(firstMove != null, "player corps should move before diagonal contact");
    AssertEqual(0, firstMove.FromGridX, "first diagonal move from x");
    AssertEqual(0, firstMove.FromGridY, "first diagonal move from y");
    AssertEqual(1, firstMove.ToGridX, "first diagonal move to x");
    AssertEqual(1, firstMove.ToGridY, "first diagonal move to y");
}

static void RuntimeDiagonalAdjacencyIsInBasicAttackRange()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_diagonal_attack", 80, 80, enemyCellX: 1, enemyCellY: 1);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

    BattleEvent[] combatEvents = result.EventStream.Events
        .Where(item => item.Kind is BattleEventKind.MovementCompleted or BattleEventKind.DamageApplied)
        .ToArray();

    AssertTrue(combatEvents.Length > 0, "diagonal adjacency should produce combat events");
    AssertEqual(BattleEventKind.DamageApplied, combatEvents[0].Kind, "diagonal adjacent units should attack before moving");
}

static void RuntimeMovementEventsCarryAuthoritativeCells()
{
    BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_move_cells", 80, 80, enemyCellX: 3, enemyCellY: 0);
    BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

    BattleEvent move = result.EventStream.Events.FirstOrDefault(item => item.Kind == BattleEventKind.MovementCompleted);
    AssertTrue(move != null, "runtime should emit movement");
    AssertTrue(move.HasMovementCells, "movement event should mark authoritative cells");
    AssertTrue(move.FromGridX != move.ToGridX || move.FromGridY != move.ToGridY, "movement event should carry a changed destination");
}

static void RuntimeSquareGridCombatAvoidsPhysicsAndFullMapPathfindingAuthority()
{
    string source = CombinedSource("src", "Runtime", "Battle");
    AssertTrue(!source.Contains("Area2D", StringComparison.Ordinal), "runtime combat must not depend on Godot Area2D authority");
    AssertTrue(!source.Contains("CollisionShape2D", StringComparison.Ordinal), "runtime combat must not depend on Godot collision shapes");
    AssertTrue(!source.Contains("MovementRangeFinder", StringComparison.Ordinal), "runtime combat must not run full-map pathfinding per actor in this slice");
}
```

- [ ] **Step 3: Update the helper signature**

Change `BuildOpposedSnapshot` to:

```csharp
static BattleStartSnapshot BuildOpposedSnapshot(
    string battleId,
    int playerStrength,
    int enemyStrength,
    int enemyCellX = 6,
    int enemyCellY = 0)
```

Then use `CellX = enemyCellX` and `CellY = enemyCellY` for the enemy group.

- [ ] **Step 4: Run the focused regression and verify it fails**

Run: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`

Expected before implementation: failures mention missing `FromGridX`, `ToGridX`, `HasMovementCells`, or diagonal movement expectations.

## Task 2: Runtime Anchored-Cell State

**Files:**
- Create: `src/Runtime/Battle/BattleRuntimeActorMotionState.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeActor.cs`
- Modify: `src/Runtime/Battle/Events/BattleEvent.cs`

- [ ] **Step 1: Add the motion enum**

```csharp
namespace Rpg.Runtime.Battle;

public enum BattleRuntimeActorMotionState
{
    Anchored = 0,
    Moving = 1,
    Attacking = 2,
    Casting = 3,
    Defeated = 4
}
```

- [ ] **Step 2: Extend runtime actor state**

Add properties to `BattleRuntimeActor`:

```csharp
public BattleRuntimeActorMotionState MotionState { get; set; } = BattleRuntimeActorMotionState.Anchored;
public string TargetActorId { get; set; } = "";
public bool HasReservedGridCell { get; set; }
public int ReservedGridX { get; set; }
public int ReservedGridY { get; set; }
public int ReservedGridHeight { get; set; }
public int AttackRange { get; set; } = 1;
```

- [ ] **Step 3: Extend battle movement events**

Add properties to `BattleEvent`:

```csharp
public bool HasMovementCells { get; set; }
public int FromGridX { get; set; }
public int FromGridY { get; set; }
public int FromGridHeight { get; set; }
public int ToGridX { get; set; }
public int ToGridY { get; set; }
public int ToGridHeight { get; set; }
```

- [ ] **Step 4: Run the focused regression and verify compilation advances to behavior failures**

Run: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`

Expected after state additions but before runtime logic: tests compile, then fail on diagonal movement or event-cell behavior.

## Task 3: Square-Grid Runtime State Machine

**Files:**
- Modify: `src/Runtime/Battle/BattleRuntimeSession.cs`

- [ ] **Step 1: Initialize actor attack range and anchored state**

When building corps actors, set:

```csharp
MotionState = BattleRuntimeActorMotionState.Anchored,
AttackRange = EngagementRange,
TargetActorId = ""
```

- [ ] **Step 2: Replace Manhattan distance with square-grid range**

Replace the current `GetManhattanDistance` helper with:

```csharp
private static int GetSquareGridDistance(BattleRuntimeActor first, BattleRuntimeActor second)
{
    return System.Math.Max(
        System.Math.Abs((first?.GridX ?? 0) - (second?.GridX ?? 0)),
        System.Math.Abs((first?.GridY ?? 0) - (second?.GridY ?? 0)));
}
```

Use this helper for target ordering and attack range checks.

- [ ] **Step 3: Replace one-axis movement with bounded 8-neighbor reservation**

Add a movement helper shaped like this:

```csharp
private static bool TryAdvanceOneSquareGridStep(
    BattleRuntimeState state,
    BattleRuntimeActor actor,
    BattleRuntimeActor target,
    System.Collections.Generic.ISet<(int X, int Y)> occupiedCells,
    System.Collections.Generic.ISet<(int X, int Y)> reservedCells,
    out (int FromX, int FromY, int FromHeight, int ToX, int ToY, int ToHeight) move)
{
    move = default;
    if (state?.Actors == null || actor == null || target == null)
    {
        return false;
    }

    int stepX = System.Math.Sign(target.GridX - actor.GridX);
    int stepY = System.Math.Sign(target.GridY - actor.GridY);
    var candidates = new[]
    {
        (X: actor.GridX + stepX, Y: actor.GridY + stepY),
        (X: actor.GridX + stepX, Y: actor.GridY),
        (X: actor.GridX, Y: actor.GridY + stepY)
    };

    for (int index = 0; index < candidates.Length; index++)
    {
        (int x, int y) = candidates[index];
        if ((x == actor.GridX && y == actor.GridY) ||
            occupiedCells.Contains((x, y)) ||
            reservedCells.Contains((x, y)))
        {
            continue;
        }

        move = (actor.GridX, actor.GridY, actor.GridHeight, x, y, actor.GridHeight);
        actor.HasReservedGridCell = true;
        actor.ReservedGridX = x;
        actor.ReservedGridY = y;
        actor.ReservedGridHeight = actor.GridHeight;
        actor.MotionState = BattleRuntimeActorMotionState.Moving;
        reservedCells.Add((x, y));
        actor.GridX = x;
        actor.GridY = y;
        actor.Position = actor.GridX;
        actor.HasReservedGridCell = false;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        return true;
    }

    return false;
}
```

- [ ] **Step 4: Emit movement cells from Runtime**

When movement succeeds, emit `MovementCompleted` with:

```csharp
HasMovementCells = true,
FromGridX = move.FromX,
FromGridY = move.FromY,
FromGridHeight = move.FromHeight,
ToGridX = move.ToX,
ToGridY = move.ToY,
ToGridHeight = move.ToHeight
```

- [ ] **Step 5: Keep attacks anchored**

After a successful movement, `continue` so that the same actor cannot move and attack in the same update pass. Only emit `DamageApplied` from the branch where `GetSquareGridDistance(actor, target) <= actor.AttackRange`.

- [ ] **Step 6: Run the focused regression and verify it passes**

Run: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`

Expected: all TargetBattleArchitectureRegression cases pass.

## Task 4: Ability Spatial Contract Defaults

**Files:**
- Create: `src/Definitions/Battle/Abilities/AbilityTargetMode.cs`
- Create: `src/Definitions/Battle/Abilities/AbilityDirectionMode.cs`
- Create: `src/Definitions/Battle/Abilities/AbilityAreaShape.cs`
- Modify: `src/Definitions/Battle/Abilities/AbilityDefinition.cs`
- Modify: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs`
- Modify: `tests/BattleHitFeedbackRegression/Program.cs`

- [ ] **Step 1: Add ability contract enums**

```csharp
namespace Rpg.Definitions.Battle.Abilities;

public enum AbilityTargetMode
{
    UnitTarget = 0,
    CellTarget = 1,
    DirectionTarget = 2,
    SelfCentered = 3
}
```

```csharp
namespace Rpg.Definitions.Battle.Abilities;

public enum AbilityDirectionMode
{
    FreeAngle = 0,
    EightWay = 1,
    FourWay = 2,
    ForwardArc = 3
}
```

```csharp
namespace Rpg.Definitions.Battle.Abilities;

public enum AbilityAreaShape
{
    SingleActor = 0,
    SingleCell = 1,
    Line = 2,
    Cone = 3,
    CircleRadius = 4,
    GridRadius = 5
}
```

- [ ] **Step 2: Export defaults on `AbilityDefinition`**

Add:

```csharp
[Export]
public AbilityTargetMode TargetMode { get; set; } = AbilityTargetMode.UnitTarget;

[Export]
public AbilityDirectionMode DirectionMode { get; set; } = AbilityDirectionMode.EightWay;

[Export]
public AbilityAreaShape AreaShape { get; set; } = AbilityAreaShape.SingleActor;
```

- [ ] **Step 3: Add the default contract test**

Add a test that constructs `new AbilityDefinition()` and asserts:

```csharp
AssertEqual(AbilityTargetMode.UnitTarget, ability.TargetMode, "basic ability target mode");
AssertEqual(AbilityDirectionMode.EightWay, ability.DirectionMode, "basic ability direction mode");
AssertEqual(AbilityAreaShape.SingleActor, ability.AreaShape, "basic ability area shape");
```

- [ ] **Step 4: Run the focused regression**

Run: `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`

Expected: all BattleHitFeedbackRegression cases pass.

## Task 5: Runtime-Driven Playback

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`

- [ ] **Step 1: Consume runtime movement cells**

In `PlayRuntimeMovementEventAsync`, remove target-driven path resolving and use `runtimeEvent.HasMovementCells` plus `ToGridX/ToGridY/ToGridHeight` to build:

```csharp
GridSurfacePosition nextStep = new(runtimeEvent.ToGridX, runtimeEvent.ToGridY, runtimeEvent.ToGridHeight);
_unitRoot.MoveEntityTo(actor, new[] { actorGrid.SurfacePosition, nextStep });
```

- [ ] **Step 2: Stop presentation from moving units into attack range before damage**

In `PlayRuntimeDamageEventAsync`, remove:

```csharp
await MoveRuntimeActorIntoVisualAttackRangeAsync(actor, target);
```

and remove the warning that checks visual adjacency before damage. Runtime has already decided the attack was valid.

- [ ] **Step 3: Delete the old visual approach helpers**

Remove `MoveRuntimeActorIntoVisualAttackRangeAsync`, `TryResolveNextCombatStep`, `TryResolveRuntimeVisualPathStep`, and `IsInRuntimeVisualAttackRange` from the playback partial.

- [ ] **Step 4: Run architecture and build checks**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal
dotnet build rpg.sln -maxcpucount:2 -v:minimal
```

Expected: both commands exit 0.

## Task 6: Authored Battle Unit Interaction Shape

**Files:**
- Modify: `scenes/battle/entities/units/BattleUnitBase.tscn`
- Modify: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs`
- Modify: `tests/BattleHitFeedbackRegression/Program.cs`

- [ ] **Step 1: Add a scene-text regression**

Assert that `BattleUnitBase.tscn` contains a `CollisionShape2D` named `InteractionShape` and an authored shape resource. The test should read the scene file as text and check for:

```text
[sub_resource type="CircleShape2D"
[node name="InteractionShape" type="CollisionShape2D" parent="."]
shape = SubResource(
```

- [ ] **Step 2: Author the shape in the scene**

Add a `CircleShape2D` sub-resource with a modest radius, then add:

```text
[node name="InteractionShape" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_interaction")
```

- [ ] **Step 3: Run presentation regression**

Run: `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`

Expected: all BattleHitFeedbackRegression cases pass.

## Task 7: Final Verification

**Files:**
- Verify all files touched by this plan.

- [ ] **Step 1: Run focused regressions**

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal
```

Expected: both commands exit 0.

- [ ] **Step 2: Run full build**

```powershell
dotnet build rpg.sln -maxcpucount:2 -v:minimal
```

Expected: build exits 0.

- [ ] **Step 3: Check diff whitespace**

```powershell
git diff --check
```

Expected: no output and exit 0.

- [ ] **Step 4: Clean build server**

```powershell
dotnet build-server shutdown
```

Expected: command exits 0.
