# Battle Live Movement Queueing Implementation Proposal

Status: Archived - accepted
Created: 2026-05-21
Accepted: 2026-05-21
Implemented: 2026-05-21
Verified: 2026-05-21

Originating Design Proposal: None
Related Implementation Proposals:
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-movement-performance.md`
- `gameplay-alignment/implementation-proposals/archived/2026-05-21-battle-presentation-motion-integrator.md`
- `gameplay-alignment/implementation-proposals/archived/2026-05-21-battle-footprint-navigation-attack-slots.md`

Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/presentation-ui-layout-architecture.md`

## Goal

Reduce visible live-battle movement stutter after strict tick-start occupancy blocking, without allowing same-tick released-footprint entry and without letting Presentation create movement truth.

## Boundary

- Runtime remains authoritative for committed cells, occupancy, reservations, action timing, attack legality, damage, and settlement facts.
- Presentation may queue and smooth visual movement events that Runtime already emitted.
- Tick-start occupied cells remain hard blockers for the whole Runtime tick.
- This work must not shorten Runtime occupancy protection in a way that recreates visual clipping.

## Scope

1. Change live Presentation event observation so movement events enqueue into the actor motion lane immediately.
2. Split actor-local presentation dependencies into action tails and movement tails.
3. Make incoming damage depend on the attacker's action tail and the target's movement tail, not the target's unrelated attack backlog.
4. Keep visual movement duration equal to Runtime movement action duration; smoothing must not add per-step visual time.
5. Preserve same-tick movement delay before dependent damage through explicit movement-tail dependencies.
6. Add regression coverage that live movement enqueueing is no longer delayed by the same actor's visual action tail.
7. Expose extra low-noise Godot monitor bindings for diagnosing battle movement stutter without changing Runtime or animation timing.

## Non-Goals

- No same-tick released-footprint entry.
- No Runtime pathfinding, target choice, attack range, damage, or settlement changes.
- No Presentation-side pathfinding or inferred movement.
- No new Godot scene/resource structure.

## Tests

Run after implementation:

```powershell
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Acceptance

- Consecutive Runtime movement events can feed an actor's `MovementLane` before the previous visual segment has fully drained.
- Later attack/damage presentation for that actor still waits behind the queued visual movement duration.
- Incoming damage waits for target movement completion but does not wait for target attack backlog.
- Visual movement duration does not accumulate extra per-step smoothing time over Runtime action duration.
- Existing strict footprint reservation regressions continue to pass.

## Acceptance Evidence

Implemented:

- Added `BattleRuntimeLivePresentationState.TrackActorMovement`.
- Added `BattleRuntimeLivePresentationState.TrackActorDamage`.
- Added `BattlePresentationTimeline` as the pure-code presentation timing model for actor action cursors and actor movement cursors.
- Added Presentation observation counters: `Battle/PresentationObserveMsLast`, `Battle/PresentationObserveMsMax`, and `Battle/PresentationObserveCount`.
- Added Godot official monitor mirrors under custom monitor ids so battle metrics and engine metrics can be watched together in Debugger > Monitors:
  - `Godot/Fps`
  - `Godot/FrameMs`
  - `Godot/PhysicsMs`
  - `Godot/Nodes`
  - `Godot/Resources`
  - `Godot/OrphanNodes`
  - `Godot/DrawCalls`
  - `Godot/ObjectsInFrame`
  - `Godot/Primitives`
  - `Godot/StaticMemoryMiB`
  - `Godot/StaticMemoryMaxMiB`
  - `Godot/MessageBufferMaxMiB`
- Live movement events now call `ObserveRuntimeMovementEvent` immediately, enqueueing Runtime movement facts into `BattleUnitRoot`'s `MovementLane` without waiting for the previous visual movement tail.
- Actor movement completion is tracked separately from actor action backlog. Later actions for the same actor still serialize against action tails, while incoming hits on a target depend only on that target's movement tail.
- Damage events are observed through `TrackActorDamage`, which waits for the attacker's action tail and the target's movement tail before starting attack and impact feedback.
- `BattleUnitRoot.ResolveVisualMoveStepDurationSeconds` now returns the Runtime movement action duration instead of adding `VisualMoveSmoothingSeconds` every step. This prevents cumulative visual drift from Runtime timing.

Red/green evidence:

- Red: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed on `battle runtime live movement queues before actor visual tail waits` before implementation.
- Green: the same suite passed after adding `TrackActorMovement` and updating the live movement observer.
- Follow-up red: the same suite failed on `battle runtime visual movement keeps runtime action duration`, proving visual movement still accumulated smoothing time over Runtime action duration.
- Follow-up green: the same suite passed after `ResolveVisualMoveStepDurationSeconds` stopped adding per-step smoothing time.
- Timeline red/green: `battle presentation timeline separates movement completion from action backlog` and `battle presentation timeline waits target movement but not target attack backlog` lock the separation between movement completion, action backlog, and incoming damage dependencies.
- Monitor red/green: `battle runtime registers Godot performance monitors` failed until Presentation observe counters and Godot official monitor mirrors were registered through `Performance.AddCustomMonitor`.

Verification:

- Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`
