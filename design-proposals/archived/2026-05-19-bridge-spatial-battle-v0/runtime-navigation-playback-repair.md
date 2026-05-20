# Battle Runtime Navigation Playback Repair Implementation Plan

Status: Legacy misplaced implementation plan; not executable under the current workflow.
Workflow Role: Source material for a future `docs/50-production/technical-changes/` implementation proposal after the originating design requirement is merged and archived.
Originating Requirements:
- `REQ-BRIDGE-SPATIAL-BATTLE-V0`
- `REQ-BNAV-TOPOLOGY-DECOUPLING`
Related Implementation Proposals: None yet.

> **For future agentic workers:** Do not execute this file directly while it remains under `design-proposals/active/`. After it is converted into a focused technical-change proposal, use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement the accepted plan task-by-task.

**Goal:** Repair the V0 battle loop so runtime movement, attack legality, navigation, and playback describe the same square-grid realtime battle instead of three loosely patched models.

**Architecture:** Runtime remains the only combat truth. Runtime resolves battle ticks from immutable tick-start facts, emits semantic events, and never depends on Presentation timing. Map topology compilation owns static walkable topology and authored height links before Runtime starts. Runtime navigation consumes the compiled topology and owns actor footprint legality, dynamic occupancy, and same-tick reservations. Presentation only interpolates runtime cells, plays animations, and applies visual feedback at the event impact moment.

**Tech Stack:** Godot C#, `Rpg.Runtime.Battle`, `Rpg.Runtime.Battle.Navigation`, `Rpg.Runtime.Battle.Events`, `Rpg.Presentation.World.Sites`, `Rpg.Presentation.Battle.Entities`, existing regression console projects under `tests/`.

---

## Status

This is a repair plan, not an implemented contract.

Current movement authority lives in `system-design/battle-runtime-architecture.md` and `system-design/battle-navigation-topology-architecture.md`. This legacy misplaced plan predates those focused authority documents and must be converted before implementation.

## Authority Boundary

This plan implements the accepted hero-led light RTS direction:

- Runtime owns actor grid position, movement legality, target selection, attack legality, damage, event stream, outcome, and reportable facts.
- Map topology compilation owns walkable surface selection, same-level topology edges, and authored height connections.
- Runtime navigation owns actor footprint legality over compiled topology, dynamic occupancy, and same-tick reservations.
- Presentation owns interpolation, facing, animations, health bars, hit feedback, highlights, pause inspection UI, and logs for visual playback.
- Presentation must not move a unit into range, apply damage early, correct paths, or invent fallback combat truth outside runtime events.
- LimboAI may choose typed action requests later, but it must not own movement, occupancy, damage, settlement, or battle outcome.

## Current Failure Summary

The current implementation has these root problems:

1. `BattleRuntimeSession` processes each tick by `ActorId` order and mutates state immediately. This creates deterministic initiative artifacts and makes "realtime" behave like automated turns.
2. Runtime emits a completed event stream, while Presentation tries to replay it concurrently by actor lane. Because runtime resolution is sequential but playback is parallel, visual timing can contradict runtime state.
3. Initial placement uses footprint centers, but movement playback resolves only anchor cell centers. Larger units can visually jump after the first movement.
4. Damage and defeat are applied before attack impact feedback. Health bars and death presentation can lead the visible swing.
5. Playback waits for broad target-lane progress, including target attack animations, not only target movement needed for position correctness. This can reintroduce stalls.
6. Pathfinder terrain legality checks anchor cells but not every footprint-covered cell. Large units can plan through water, walls, or missing surfaces.
7. Generated diagonal movement edges allow corner cutting around blocked terrain.
8. Runtime has a `MotionState` field, but no real actor state machine. Movement is set to `Moving` and then immediately back to `Anchored` inside one action call, so there is no persistent in-transit state, no path invalidation state, and no explicit decision boundary after a target moves.
9. Existing tests are too source-shape heavy. They do not prove the observed "attack before arrival", "stuck after one hit", "stale target path", or "large footprint path through invalid terrain" failures.

## Repair Invariants

The implementation must preserve these invariants:

- A basic attack can be emitted only from an anchored runtime position that is in range at decision time.
- One actor cannot both move and basic attack in the same runtime tick.
- Damage for same-tick basic attacks is resolved from tick-start anchored positions and applied as one deterministic batch.
- A target moving in the same tick cannot make an out-of-range attacker attack early. The attacker may attack only on a later tick after both sides have a committed anchored state.
- Same-tick damage events should be ordered before same-tick movement events when the damage was resolved from tick-start positions.
- Runtime event fields must contain enough spatial facts for Presentation, Report, and diagnostics to explain where the attack was legal.
- Every candidate committed footprint must be static-terrain legal, unoccupied by other living actors, and unreserved.
- Projected future occupancy is a soft path cost, not a hard terrain blocker.
- Diagonal movement is legal only when the diagonal target and the relevant orthogonal side anchors are legal for that actor footprint.
- Presentation movement resolves the same footprint center used during deployment and initial placement.
- Health and defeat feedback are applied at attack impact, not at animation start.
- Every living actor has an explicit runtime state machine. A unit may keep target intent, command posture, cooldown, and current movement reservation, but it must not keep an authoritative long path to a stale target.
- Pathfinding is recalculated from current runtime actor state at every decision boundary: movement completion, command change, target cell change, target defeat, reservation rejection, or path invalidation. This means every runtime simulation tick that can make a new movement decision replans from current positions.
- Replanning is a runtime simulation concern, not a Godot render-frame concern. Presentation frames interpolate the last accepted runtime movement; they do not choose a new path.
- Movement intent is not fixed for the whole battle. The same unit may switch between move-to-point, attack-move, chase-actor, engage, hold-area, retreat, and reacquire-target modes during one battle.
- Every movement-intent switch is an explicit state-machine transition and invalidates cached path data unless the new intent proves the same command, target, destination, actor anchor, target anchor, graph version, and dynamic revision.

## Task 0: Simplify Battle Navigation Authoring

**Files:**

- Modify: battle-map and TileSet authoring assumptions used by the bridge battle proposal scope
- Modify: proposal-scope navigation contracts that still assume tile custom-data movement semantics

- [ ] **Step 1: Reduce battle TileSet responsibility to walkable-only**

Battle pathfinding authoring must not require TileSet custom data such as `MoveCost`, `CanStandOn`, `TerrainTag`, or `IsObstacle` for combat navigation legality. Battle navigation consumes only whether each covered cell is statically walkable.

- [ ] **Step 2: Keep height topology outside TileSet movement semantics**

Same-height neighbor generation and explicit height connections are map-topology compiler behavior. Runtime receives the resulting topology data. They are not replaced by per-tile movement cost, terrain tags, or obstacle metadata.

- [ ] **Step 3: Use uniform traversal cost on walkable cells**

This repair removes authored movement-cost dependence from the battle pathfinding contract. Traversal cost is uniform across statically walkable cells, with only projected dynamic occupancy adding soft route cost.

- [ ] **Step 4: Verify authoring boundary**

The bridge battle must remain playable with walkable-only battle navigation authoring plus authored height topology. Complex TileSet navigation custom data is out of scope for this repair.

## File Structure

Expected file responsibilities:

| File | Responsibility |
|---|---|
| `src/Runtime/Battle/BattleRuntimeSession.cs` | Session lifecycle, battle loop, termination, event stream ownership. It should delegate tick resolution instead of owning all movement and damage details inline. |
| `src/Runtime/Battle/BattleRuntimeTickResolver.cs` | New runtime-only helper that builds tick-start facts, advances actor state machines, builds action proposals, resolves deterministic attack batches, movement reservations, state mutations, and emitted events. |
| `src/Runtime/Battle/BattleRuntimeActorStateMachine.cs` | New runtime-only owner of per-actor phase transitions such as anchored decision, moving, attacking, cooldown, interrupted, and defeated. |
| `src/Runtime/Battle/BattleRuntimeActorPhase.cs` | New runtime-only enum or equivalent value object for stable actor phases. It replaces ad-hoc `MotionState` toggling as the combat decision gate. |
| `src/Runtime/Battle/BattleMovementIntentKind.cs` | New runtime-only enum for movement strategy modes such as move-to-point, attack-move, chase-actor, engage, hold-area, retreat, and reacquire-target. |
| `src/Runtime/Battle/BattleMovementIntentState.cs` | New runtime-only per-actor state for current movement intent, destination, target id, posture, and intent revision. |
| `src/Runtime/Battle/BattleMovementStrategy.cs` | New runtime-only strategy selector that maps the actor's movement intent to path target selection, target acquisition, and invalidation rules. |
| `src/Runtime/Battle/BattleRuntimePathIntent.cs` | New runtime-only DTO for command-scoped target intent and optional non-authoritative path cache metadata. It must store invalidation keys if any cache is used. |
| `src/Runtime/Battle/BattleRuntimeActionProposal.cs` | New runtime-only DTO for one actor's tick proposal: hold, wait, move, or attack. |
| `src/Runtime/Battle/Events/BattleEvent.cs` | Add attack spatial context fields needed by playback and report diagnostics. |
| `src/Application/Battle/Navigation/BattleNavigationTopologyCompiler.cs` | Compiles final walkable topology nodes, same-level edges, and authored height-link edges before Runtime starts. |
| `src/Application/Battle/Navigation/BattleNavigationTopology.cs` | Pure topology data contract consumed by Runtime pathfinding. |
| `src/Runtime/Battle/Navigation/BattleNavigationGraph.cs` | Runtime view over compiled topology plus actor footprint static legality. |
| `src/Runtime/Battle/Navigation/BattlePathStepRules.cs` | Shared step legality: static footprint legality plus immediate dynamic reservation rules. |
| `src/Runtime/Battle/Navigation/BattlePathfinder.cs` | A* next-step selection using static footprint legality and dynamic soft cost. |
| `src/Runtime/Battle/Navigation/BattleMovementReservationMap.cs` | Immediate same-tick reservation of full candidate footprints. |
| `src/Runtime/Battle/Navigation/BattleDynamicOccupancy.cs` | Tick-start occupied footprint cells and soft projected occupancy counts. |
| `src/Presentation/Battle/Entities/BattleUnitRoot.cs` | Movement interpolation, attack animation timing, impact callback, defeat presentation, footprint-center movement points. |
| `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs` | Runtime event playback orchestration and dependency wiring. |
| `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs` | Movement and damage event presentation; damage must apply at impact timing. |
| `src/Presentation/World/Sites/BattleRuntimePlaybackPlanner.cs` | Actor-lane grouping and movement-only target-position dependencies. |
| `tests/TargetBattleArchitectureRegression/` | Runtime behavior regressions for tick resolution, attack legality, navigation, footprints, and diagnostics. |
| `tests/BattleHitFeedbackRegression/` | Playback planner and health/defeat timing regressions. |

## Task 1: Lock Failing Behavior Regressions First

**Files:**

- Modify: `tests/TargetBattleArchitectureRegression/Program.cs`
- Modify: `tests/TargetBattleArchitectureRegression/TargetBattleAttackCadenceRegressionCases.cs`
- Modify: `tests/TargetBattleArchitectureRegression/TargetBattleNavigationRegressionCases.cs`
- Modify: `tests/TargetBattleArchitectureRegression/TargetBattleFootprintRegressionCases.cs`
- Modify: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.RuntimePlayback.cs`

- [ ] **Step 1: Add actor-id fairness regression**

Add a case named `RuntimeAdjacentOpponentsResolveSameTickAttacksWithoutActorIdInitiative`.

Scenario:

```text
player corps at (0,0), enemy corps at (1,0)
both have AttackRange=1, AttackSpeed high enough to attack on tick 0, MaxHitPoints=20, AttackDamage=10
```

Expected:

```text
two DamageApplied events exist at RuntimeTick=0
one from player to enemy
one from enemy to player
both actors end with 10 HP if no later attacks have resolved yet
```

The assertion must not depend on event source order except that both damage events share the same runtime tick.

- [ ] **Step 2: Add no-move-and-attack-same-tick regression**

Add a case named `RuntimeMoverCannotAttackUntilAnchoredOnLaterTick`.

Scenario:

```text
player corps at (0,0), enemy corps at (3,0)
both are 1x1, AttackRange=1
player must move before contact
```

Expected:

```text
for each actor id, no RuntimeTick contains both MovementCompleted and DamageApplied
the first DamageApplied tick for an actor is greater than that actor's last approach MovementCompleted tick
```

- [ ] **Step 3: Add footprint static terrain regression**

Add a case named `RuntimeLargeFootprintCannotPlanThroughMissingCoveredSurface`.

Scenario:

```text
player corps footprint 2x1 at anchor (0,0)
enemy at (4,0)
authored surfaces exist for (0,0), (1,0), (1,1), (2,1), (3,1), (4,0)
surface (2,0) is deliberately missing
```

Expected:

```text
no MovementCompleted event moves the 2x1 player anchor to (1,0), because covered cell (2,0) is missing
if an alternate legal route exists through y=1, the first movement chooses that route
```

- [ ] **Step 4: Add diagonal corner-cut regression**

Add a case named `RuntimeDiagonalMoveRequiresOrthogonalSideClearance`.

Scenario:

```text
player at (0,0), enemy at (2,2)
authored surfaces include (0,0), (1,1), (2,2)
surfaces (1,0) and (0,1) are missing
```

Expected:

```text
runtime must not emit a first move from (0,0) to (1,1)
navigation diagnostic should report no reachable attack anchor unless another legal route is authored
```

- [ ] **Step 5: Add playback dependency regression**

Add a case named `RuntimePlaybackDamageWaitsForTargetMovementButNotTargetAttackBacklog`.

Scenario:

```text
target actor has DamageApplied events at ticks 10 and 11
target actor has MovementCompleted at tick 12
attacker has DamageApplied against target at tick 13
```

Expected:

```text
dependency for attacker damage requires target tick 12, not 11 due only to attacks
if target has only prior DamageApplied events and no prior MovementCompleted events, there is no target-lane dependency
```

- [ ] **Step 6: Add stale-target-path regression**

Add a case named `RuntimeReplansNextStepAfterTargetCellChanges`.

Scenario:

```text
player corps starts at (0,1)
enemy corps starts at (4,1)
authored surfaces form two routes: lower route through y=1 and upper route through y=0
the enemy moves from (4,1) to (4,0) before the player reaches attack range
the player's next movement decision happens after that enemy movement is committed
```

Expected:

```text
the player's next MovementCompleted event is chosen against the enemy's current anchor (4,0), not the original anchor (4,1)
if both routes are legal, the first player step after the enemy moves must reduce the path estimate toward (4,0)
the test must fail if the player continues following a cached route whose target key is still (4,1)
```

Use a scripted runtime AI executor or a tick-resolver harness so the test can force the target movement and then inspect the next player movement decision. Do not test this through Presentation playback.

- [ ] **Step 7: Add movement-intent switch regression**

Add a case named `RuntimeActorInvalidatesPathWhenMovementIntentSwitches`.

Scenario:

```text
player corps starts with a MoveToPoint intent toward a rally cell
enemy enters detection or command scope before the player reaches the rally cell
the same player corps switches to ChaseActor or Engage intent
the enemy then moves to a different anchor before the player's next decision
```

Expected:

```text
the player actor records an intent revision change when switching from MoveToPoint to ChaseActor or Engage
the old rally-point path is not reused after the switch
the next movement proposal uses the current enemy anchor and current movement intent
if the enemy moves again, the chase path invalidates again
```

This test protects the common RTS flow where a selected unit starts moving to a clicked point, sees or is ordered onto an enemy, then changes behavior inside the same battle.

- [ ] **Step 8: Run tests and confirm failures before implementation**

Run:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
```

Expected before implementation:

```text
new regressions fail for actor-id initiative, move+attack timing, stale target pathing, movement-intent switching, footprint terrain legality, diagonal corner cutting, or playback dependency breadth
existing unrelated tests should still compile
```

## Task 2: Introduce Actor State Machine And Replace ActorId-Sequential Mutation

**Files:**

- Create: `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- Create: `src/Runtime/Battle/BattleRuntimeActorStateMachine.cs`
- Create: `src/Runtime/Battle/BattleRuntimeActorPhase.cs`
- Create: `src/Runtime/Battle/BattleMovementIntentKind.cs`
- Create: `src/Runtime/Battle/BattleMovementIntentState.cs`
- Create: `src/Runtime/Battle/BattleMovementStrategy.cs`
- Create: `src/Runtime/Battle/BattleRuntimePathIntent.cs`
- Create: `src/Runtime/Battle/BattleRuntimeActionProposal.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeSession.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeActor.cs`
- Modify: `src/Runtime/Battle/Events/BattleEvent.cs`
- Modify: `tests/TargetBattleArchitectureRegression/TargetBattleAttackCadenceRegressionCases.cs`

- [ ] **Step 1: Introduce actor phases**

Create `BattleRuntimeActorPhase` with these first-slice phases:

```csharp
internal enum BattleRuntimeActorPhase
{
    AnchoredDecision = 0,
    Moving = 1,
    AttackWindup = 2,
    AttackRecovery = 3,
    WaitingForCharge = 4,
    Holding = 5,
    Defeated = 6
}
```

`BattleRuntimeActorMotionState` may remain as a compatibility field only if existing code still reads it, but runtime decisions must use the new phase. Do not keep two equal authorities for movement state.

- [ ] **Step 2: Introduce path intent with invalidation keys**

Create `BattleMovementIntentKind`:

```csharp
internal enum BattleMovementIntentKind
{
    None = 0,
    MoveToPoint = 1,
    AttackMove = 2,
    ChaseActor = 3,
    EngageActor = 4,
    HoldArea = 5,
    Retreat = 6,
    ReacquireTarget = 7
}
```

Create `BattleMovementIntentState`:

```csharp
internal sealed class BattleMovementIntentState
{
    public BattleMovementIntentKind Kind { get; set; }
    public string CommandId { get; set; } = "";
    public string TargetActorId { get; set; } = "";
    public bool HasDestination { get; set; }
    public BattleGridCoord Destination { get; set; }
    public int IntentRevision { get; set; }
}
```

Intent semantics:

| Intent | Path target | Target acquisition | Switches when |
|---|---|---|---|
| `MoveToPoint` | destination cell or assigned slot | none unless command rules allow interruption | destination reached, command changes, enemy engagement overrides it |
| `AttackMove` | destination until enemy enters command scope | acquire valid enemy, then switch to `ChaseActor` or `EngageActor` | target found, destination reached, command changes |
| `ChaseActor` | current target actor anchor or attack anchor near it | keep target id while valid | target anchor changes, target dies, target unreachable, command changes |
| `EngageActor` | attack anchor around current target | keep target id while attack remains legal or nearly legal | target exits range, target dies, cooldown/state changes |
| `HoldArea` | current/defend area anchors | acquire enemies only inside hold scope | actor pulled outside hold scope, command changes |
| `Retreat` | retreat destination or exit anchor | avoid new engagement unless blocked | retreat completes, blocked, command changes |
| `ReacquireTarget` | no cached path | choose next valid target inside command scope | target chosen or no target exists |

Every switch increments `IntentRevision`. Any cached `BattleRuntimePathIntent` whose revision differs is invalid.

- [ ] **Step 3: Introduce movement strategy selector**

Create `BattleMovementStrategy` to map current intent to the next path query:

```csharp
internal sealed class BattleMovementStrategy
{
    public BattleRuntimeActionProposal BuildProposal(
        BattleRuntimeActor actor,
        BattleMovementIntentState intent,
        BattleRuntimeTickFacts facts,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations);
}
```

Rules:

- Strategy selects the navigation target shape: point cell, current target actor, attack anchor, hold area, or retreat anchor.
- Strategy does not mutate HP, grid coordinates, settlement, or presentation.
- Strategy may switch intent only through the actor state machine or a narrow transition helper that increments `IntentRevision`.
- Strategy must not keep a long path as authority. It may keep only a next-step cache guarded by command id, intent kind, intent revision, actor anchor, target anchor or destination, graph version, and dynamic revision.

- [ ] **Step 4: Introduce path intent with invalidation keys**

Create `BattleRuntimePathIntent`:

```csharp
internal sealed class BattleRuntimePathIntent
{
    public string CommandId { get; init; } = "";
    public BattleMovementIntentKind MovementIntentKind { get; init; }
    public int MovementIntentRevision { get; init; }
    public string TargetActorId { get; init; } = "";
    public bool HasDestination { get; init; }
    public BattleGridCoord Destination { get; init; }
    public BattleGridCoord LastPlannedActorAnchor { get; init; }
    public BattleGridCoord LastPlannedTargetAnchor { get; init; }
    public int StaticGraphVersion { get; init; }
    public int DynamicRevision { get; init; }
    public BattleGridCoord NextAnchor { get; init; }
    public bool HasNextAnchor { get; init; }
}
```

V0 may choose not to cache `NextAnchor` at all. If it does cache, the cache is valid only while command id, movement intent kind, movement intent revision, target actor id, destination, actor anchor, target anchor, static graph version, and dynamic revision all match. A changed target cell or movement mode invalidates the cached path immediately.

- [ ] **Step 5: Introduce actor state machine**

Create `BattleRuntimeActorStateMachine` with methods equivalent to:

```csharp
internal sealed class BattleRuntimeActorStateMachine
{
    public BattleRuntimeActorPhase GetPhase(BattleRuntimeActor actor);
    public bool CanDecide(BattleRuntimeActor actor);
    public bool CanStartMove(BattleRuntimeActor actor);
    public bool CanStartAttack(BattleRuntimeActor actor);
    public void EnterMoving(BattleRuntimeActor actor, BattleGridCoord from, BattleGridCoord to, int tick);
    public void CompleteMove(BattleRuntimeActor actor, BattleGridCoord to, int tick);
    public void EnterAttackWindup(BattleRuntimeActor actor, string targetActorId, int tick);
    public void CompleteAttack(BattleRuntimeActor actor, int tick);
    public void EnterWaitingForCharge(BattleRuntimeActor actor, int tick);
    public void EnterHolding(BattleRuntimeActor actor, string reason, int tick);
    public void EnterDefeated(BattleRuntimeActor actor, int tick);
}
```

Rules:

- `Moving` persists at least until the movement completion boundary. It must not be set and cleared inside the same helper before the rest of runtime can observe it.
- `CanStartAttack` is false while `Moving`, `AttackWindup`, `AttackRecovery`, or `Defeated`.
- `CanStartMove` is false while `Moving`, `AttackWindup`, `AttackRecovery`, or `Defeated`.
- `AnchoredDecision` is the only normal phase that may request a new path step.
- Target intent survives normal movement completion, but cached path data does not survive a target anchor change.
- Movement-intent switches are state-machine transitions, not incidental field writes. A switch from `MoveToPoint` to `ChaseActor`, from `ChaseActor` to `EngageActor`, or from any combat intent to `Retreat` must increment intent revision and invalidate cached path data.

- [ ] **Step 6: Introduce action proposals**

`BattleRuntimeActionProposal` must be runtime-only and contain:

```csharp
internal sealed class BattleRuntimeActionProposal
{
    public BattleRuntimeAiActionRequest Request { get; init; }
    public BattleRuntimeActor Actor { get; init; }
    public BattleRuntimeActor Target { get; init; }
    public BattleRuntimeAiActionKind Kind => Request?.Kind ?? BattleRuntimeAiActionKind.Hold;
    public BattleGridCoord ActorStart { get; init; }
    public BattleGridCoord TargetStart { get; init; }
    public BattleGridCoord MoveTo { get; init; }
    public bool HasMoveTo { get; init; }
    public string FailureReason { get; init; } = "";
}
```

Do not expose this type outside `Rpg.Runtime.Battle`.

- [ ] **Step 7: Build tick-start facts before any mutation**

`BattleRuntimeTickResolver` must snapshot every living corps actor's start cell, footprint, HP, attack charge, target id, and command id at the start of the tick.

Decision facts passed to `_aiExecutor.ChooseAction` must come from tick-start actor and target facts. They must not read positions already mutated by another actor earlier in the same tick.

- [ ] **Step 8: Replan only at runtime decision boundaries**

For every actor whose phase is `AnchoredDecision`, rebuild its movement decision from current runtime state:

```text
current actor anchor
current target anchor
current command id/posture
current movement intent kind and revision
current static graph
current tick-start occupancy
current same-tick reservation map
```

Do not reuse a path whose movement intent kind/revision differs from the actor's current intent, whose destination differs from the current destination, or whose `LastPlannedTargetAnchor` differs from the target's current anchor. If the target moved from a lower route to an upper route, the actor's next proposal must be calculated toward the upper-route target anchor.

This is not a render-frame pathfinder. Godot `_Process` or animation frames must not call A*. Runtime simulation ticks or state-machine transition boundaries call A*.

- [ ] **Step 9: Split proposal, attack, and movement phases**

Tick resolution order:

```text
build tick-start occupancy
advance movement-intent transitions that are forced by command, target validity, or defeat
build one proposal per living corps actor through BattleMovementStrategy
resolve attack proposals from tick-start anchored positions
apply all attack damage as one deterministic batch
discard movement proposals from actors defeated by same-tick damage
discard movement proposals whose target was defeated by same-tick damage
resolve movement proposals with same-tick reservations
apply successful movement mutations and emit MovementCompleted events
recharge actors that did not successfully attack
log action results
```

Rules:

- An actor with an attack proposal never moves in the same tick.
- An actor with a movement proposal never attacks in the same tick.
- An attack proposal is legal only if `BattleActorFootprint.GetGap(actorStartFootprint, targetStartFootprint) <= AttackRange`.
- Damage totals are calculated before any HP mutation is applied.
- Same-tick attacks may defeat each other.
- Event ordering inside one tick is deterministic: damage events first, movement events second, each group ordered by actor id only for log stability.
- Actors in `Moving`, `AttackWindup`, `AttackRecovery`, or `Defeated` do not build new proposals until their state machine reaches `AnchoredDecision`.

- [ ] **Step 10: Add attack spatial context to events**

Add fields to `BattleEvent`:

```csharp
public bool HasActorCells { get; set; }
public int ActorGridX { get; set; }
public int ActorGridY { get; set; }
public int ActorGridHeight { get; set; }
public bool HasTargetCells { get; set; }
public int TargetGridX { get; set; }
public int TargetGridY { get; set; }
public int TargetGridHeight { get; set; }
```

For `DamageApplied`, populate actor and target cells from the same tick-start positions used for legality. For `MovementCompleted`, keep using `FromGrid*` and `ToGrid*`.

- [ ] **Step 11: Reduce `BattleRuntimeSession` ownership**

`BattleRuntimeSession.ResolveAutonomousCombat` should keep:

```text
for tick loop
termination check
navigation graph creation
event stream ownership
call to BattleRuntimeTickResolver.ResolveTick(...)
```

Move actor phase transitions, movement-intent transitions, strategy selection, proposal construction, path invalidation, action application, reservation checks, damage batching, and per-action logging into `BattleRuntimeTickResolver`, `BattleMovementStrategy`, and `BattleRuntimeActorStateMachine`.

- [ ] **Step 12: Verify**

Run:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
```

Expected:

```text
actor-id fairness regression passes
no-move-and-attack-same-tick regression passes
stale-target-path regression passes
movement-intent switch regression passes
existing attack cadence and command tests still pass
```

## Task 3: Repair Static Navigation, Footprint Legality, And Diagonal Movement

**Files:**

- Modify: `src/Runtime/Battle/Navigation/BattleNavigationGraph.cs`
- Modify: `src/Runtime/Battle/Navigation/BattlePathStepRules.cs`
- Modify: `src/Runtime/Battle/Navigation/BattlePathfinder.cs`
- Modify: `src/Runtime/Battle/Navigation/BattlePathCostPolicy.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleMovementReservationMap.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleDynamicOccupancy.cs`
- Modify: `tests/TargetBattleArchitectureRegression/TargetBattleNavigationRegressionCases.cs`
- Modify: `tests/TargetBattleArchitectureRegression/TargetBattleFootprintRegressionCases.cs`

- [ ] **Step 1: Add static footprint legality**

`BattleNavigationGraph` must expose:

```csharp
public bool ContainsFootprint(BattleRuntimeActor actor, BattleGridCoord anchor)
```

It returns true only when every `BattleActorFootprint.Enumerate(actor, anchor)` cell is present in the authored surface graph or inside legacy bounds.

- [ ] **Step 2: Apply footprint legality to every projected A* neighbor**

`BattlePathStepRules.CanUseAnchor` must reject a neighbor when:

```text
graph.ContainsFootprint(actor, neighbor) == false
```

This check applies to both immediate and projected route cells. Dynamic occupancy remains hard only for the immediate committed step.

- [ ] **Step 3: Prevent generated diagonal corner cutting**

For a generated diagonal neighbor from `(x,y,h)` to `(x+dx,y+dy,h)`, where both `dx` and `dy` are non-zero:

```text
side A anchor = (x + dx, y, h)
side B anchor = (x, y + dy, h)
```

The diagonal is legal only if:

```text
ContainsFootprint(actor, diagonal target)
ContainsFootprint(actor, side A anchor)
ContainsFootprint(actor, side B anchor)
```

For authored height connections, keep explicit connections authoritative. Do not block a height transition solely because same-height side anchors are absent.

- [ ] **Step 4: Keep dynamic occupancy layered**

Immediate move:

```text
full candidate footprint must be free of other tick-start occupants
full candidate footprint must be unreserved
opposite-edge swap must be rejected
```

Projected future route:

```text
full candidate footprint must be static legal
other occupied cells add cost through BattlePathCostPolicy
other occupied cells do not make the route statically illegal
```

- [ ] **Step 5: Improve navigation diagnostics**

When `path_not_found` is logged, include:

```text
start footprint legal
target anchor in graph
nearest reachable attack anchor
blocked diagonal or missing covered surface counts when available
```

Keep this low-noise: log once per actor-target-failure reason, not every frame or every search node.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
```

Expected:

```text
large-footprint terrain regression passes
diagonal corner-cut regression passes
existing congestion soft-cost tests still pass
existing authored height connection test still passes
```

## Task 4: Align Presentation Movement With Runtime Footprints

**Files:**

- Modify: `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroCorps.cs`
- Modify: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs`

- [ ] **Step 1: Replace movement cell resolver with footprint resolver**

Change the presentation resolver contract from:

```csharp
GridPosition -> global center of anchor cell
```

to:

```csharp
GridPosition + footprint size -> global center of covered footprint
```

`WorldSiteRoot` already has `TryGetFootprintCenterGlobalPosition`; `BattleUnitRoot` movement must use the same resolver for movement points and fallback placement.

- [ ] **Step 2: Keep 1x1 behavior unchanged**

For a `1x1` actor, the footprint center must equal the old cell center. Existing placement and movement tests for ordinary units must not change expected coordinates.

- [ ] **Step 3: Resolve every movement path point from current footprint**

`BattleUnitRoot.TryBuildMovementGlobalPath` must call the footprint resolver for every destination path point using the moving entity's `GridOccupantComponent.FootprintWidth` and `FootprintHeight`.

Do not use anchor cell global position as a fallback for moving entities with a footprint component.

- [ ] **Step 4: Verify visual diagnostics**

Movement logs should include:

```text
entity id
from surface cell
to surface cell
footprint width/height
from global
to global
```

This is one log per movement event, not per frame.

- [ ] **Step 5: Verify**

Run:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
```

Expected:

```text
presentation tests confirm BattleUnitRoot movement uses the footprint resolver
existing deployment footprint tests still pass
```

## Task 5: Repair Playback Timing And Remove Over-Broad Target Lane Waiting

**Files:**

- Modify: `src/Presentation/World/Sites/BattleRuntimePlaybackPlanner.cs`
- Modify: `src/Presentation/World/Sites/BattleRuntimeActorLaneProgress.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`
- Modify: `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- Modify: `src/Presentation/Battle/Entities/HealthComponent.cs`
- Modify: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.RuntimePlayback.cs`

- [ ] **Step 1: Narrow target-lane dependencies to movement**

`BuildDamageEventActorProgressDependencies` must depend only on target `MovementCompleted` events that happen before the damage event or same-tick before the damage event in source order.

Target `DamageApplied` events must not create a dependency for another actor's attack. A target's attack animation backlog is not required for target position correctness.

- [ ] **Step 2: Use runtime spatial context for debug validation**

When a `DamageApplied` event has `HasActorCells` and `HasTargetCells`, playback should be able to log:

```text
actor event cell
target event cell
current actor grid component cell
current target grid component cell
```

If the current grid component cell differs because a movement event has not played yet, this must be solved by movement dependency, not by teleporting or recalculating combat truth.

- [ ] **Step 3: Apply health at impact timing**

Refactor attack playback so:

```text
start attack animation
wait until configured impact delay
apply HealthComponent.ApplyDamage
spawn damage feedback
mark defeated if health is now zero
wait remaining attack animation duration
return lane duration
```

Do not apply `HealthComponent.ApplyDamage` before `PlayAttack`.

- [ ] **Step 4: Move death presentation after impact**

`MarkEntityDefeated` must be called only after impact damage has been applied and hit feedback has had a chance to start. Death presentation must not lead the attack swing.

- [ ] **Step 5: Keep unrelated movement independent**

Actor lanes should still run concurrently with `Task.WhenAll`. Removing broad target attack dependencies must not restore a global tick batch that waits for the slowest attack animation.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
```

Expected:

```text
movement-only dependency regression passes
health/death timing regression passes
actor-lane concurrency regression still passes
```

## Task 6: Manual Playtest And Log Acceptance

**Files:**

- Use existing scene: `scenes/world/sites/impl/demo_site.tscn`
- Use current battle entry flow that launches the demo battle site
- Inspect log: current `GameLog.CurrentLogPath`

- [ ] **Step 1: Run the demo battle five times**

Manual setup:

```text
use the demo site battle map
use at least two friendly corps and two enemy corps
include one route around water or missing walkable surfaces
include one 2x1 or 2x2 unit if the current sample content allows it
```

- [ ] **Step 2: Check attack-before-arrival**

Acceptance:

```text
no visible attack plays before attacker and target are adjacent/in range by their footprint cells
no DamageApplied log appears in the same runtime tick as that actor's MovementCompleted event
damage number and health bar update happen on impact, not when the swing starts
```

- [ ] **Step 3: Check stuck movement**

Acceptance:

```text
units across water choose a valid land route when one exists
units do not stand forever merely because the direct straight-line route crosses water
units do not repeatedly choose an illegal large-footprint anchor
when an enemy changes route or moves to another front, pursuing units replan toward the enemy's current anchor on the next decision boundary instead of following the enemy's original cell
when a unit switches from move-to-point to chase, engage, hold, or retreat, the old movement mode's path is invalidated and cannot drive the next step
support units may pause behind a blocked front, but they should either advance to a legal support cell or log a clear reason
```

- [ ] **Step 4: Check playback readability**

Acceptance:

```text
unrelated movers keep moving while another unit attacks
a target's own repeated attacks do not freeze all attackers trying to hit it
death feedback follows the hit that killed the unit
```

- [ ] **Step 5: Check return-to-world stability**

Acceptance:

```text
battle completion returns to the world viewport without camera drift
defeated enemy site state does not respawn immediately from stale runtime facts
```

This step guards previous regressions even though the main repair is runtime/navigation/playback.

## Task 7: Legacy Documentation Cleanup Notes

This section predates the current design-first workflow. When this repair is converted into `docs/50-production/technical-changes/`, keep only the implementation-status and verification parts that still apply.

**Potential files for the future technical-change proposal:**

- Modify: `design-proposals/archived/2026-05-19-bridge-spatial-battle-v0/proposal.md`
- Modify: `design-proposals/archived/2026-05-19-bridge-spatial-battle-v0/playable-slice-v0.md`
- Do not edit `system-design/` or `gameplay-design/` from this implementation plan. If implementation reveals missing or wrong design authority, return to the design proposal flow first.

- [ ] **Step 1: Update future technical-change status**

Only after automated tests and manual playtest pass, update the future technical-change proposal from intended/implemented ambiguity to a truthful status.

Required status language:

```text
Status: Implemented after runtime/navigation/playback repair.
```

If manual playtest still fails, set:

```text
Status: Intended contract; implementation repair still active.
```

- [ ] **Step 2: Keep relationship metadata current**

The bridge proposal must keep relationship metadata pointing to the future technical-change proposal after conversion. Do not copy the whole implementation plan into `proposal.md`.

- [ ] **Step 3: Return to design flow for authority changes**

Do not directly edit accepted authority documents from this file. Durable rule changes belong in a design proposal that updates:

```text
system-design/hero-led-light-rts-system-architecture.md
gameplay-design/details/combat-command/README.md
```

Archive design proposals only after accepted expected copies have been merged into authority documents.

## Final Verification Command Set

Run:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
dotnet build-server shutdown
```

Expected:

```text
all listed tests pass
build passes with 0 errors
git diff --check has no new whitespace errors except pre-existing CRLF warnings if unchanged
build server shuts down cleanly
```

## Acceptance Criteria

The repair is accepted when:

- adjacent opposing actors can attack in the same runtime tick without ActorId initiative deleting the later actor's action;
- each living actor has an explicit runtime state machine, and `Moving` is a persistent phase rather than a transient flag set and cleared inside one method call;
- pathfinding is recalculated from current runtime actor and target positions at movement decision boundaries, so a unit does not keep following the target's first-frame location after the target changes route;
- a unit can switch movement intent during one battle, and every switch has explicit transition rules, intent revision, and path invalidation;
- no actor emits movement and basic attack in the same runtime tick;
- out-of-range actors never attack from a future or visually stale position;
- large footprints cannot path through missing or blocked covered terrain cells;
- diagonal movement cannot cut across blocked corners unless an explicit authored connection allows that transition;
- movement playback uses footprint center positions consistently from placement through every movement event;
- health, damage number, and defeat presentation occur at attack impact timing;
- target-lane playback waits for target movement only, not target attack animation backlog;
- unrelated actor lanes keep moving while another unit attacks;
- manual demo battle no longer shows the reported "attack before arrival" or "hit once then stuck" failures under the tested setup;
- runtime diagnostics explain remaining path failures without per-frame spam.

## Non-Goals

- No full skill system.
- No flow-field or sector-portal pathfinding.
- No freeform physics RTS steering.
- No multiple units per grid cell.
- No broad command UI redesign.
- No LimboAI ownership expansion.
- No balance pass beyond values needed to reproduce and verify the repair.
