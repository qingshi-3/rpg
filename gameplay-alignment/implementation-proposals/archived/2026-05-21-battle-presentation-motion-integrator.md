# Battle Presentation Motion Integrator Implementation Proposal

Status: Archived - accepted
Created: 2026-05-21
Accepted: 2026-05-21
Implemented: 2026-05-21
Verified: 2026-05-21

Originating Design Proposal: None
Requirement Id: None
Related Implementation Proposal:
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-movement-performance.md`

Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/presentation-ui-layout-architecture.md`

## Goal

Make live battle unit movement feel like one continuous visual motion stream instead of many short per-step tweens, without changing Runtime movement rules, path legality, action timing, damage, settlement, or target choice.

## Boundary

- Runtime remains the authority for committed grid cells and semantic battle events.
- Presentation may buffer and smooth visual movement, but must not invent movement, combat, or pathfinding facts.
- `GridOccupantComponent` may still update to the Runtime-authoritative target cell when a movement event is observed.
- The visual node position should be driven by a per-actor motion lane instead of creating one Godot `Tween` per movement event.

## Scope

- Replace live movement visual interpolation in `BattleUnitRoot` with actor-local motion lanes advanced from `_Process(delta)`.
- Queue consecutive movement event points into the actor lane so adjacent steps are stitched into one continuous visual path.
- Preserve render sort and facing updates at segment boundaries.
- Keep damage timing dependent on same-tick movement delays.
- Keep low-noise monitor counters, but reinterpret active movement count as active visual motion lanes.

## Non-Goals

- No Runtime navigation or combat rule changes.
- No movement speed, attack speed, attack range, footprint, command, or settlement changes.
- No Presentation pathfinding authority.
- No broad animation system rewrite.
- No Godot editor-side scene restructuring.

## Expected Implementation

- Add a private `MovementLane` model in `BattleUnitRoot` that stores queued visual points, surface points, elapsed segment time, per-step duration, return-to-idle policy, and last applied segment index.
- `MoveEntityTo` should enqueue points into the lane and start/continue move animation/audio without creating a movement tween.
- `_Process(delta)` advances all active lanes, lerps the entity between queued points, updates facing and render sort when crossing segment boundaries, and completes idle transition when the lane drains.
- Existing fallback behavior remains for missing footprint position resolution or zero-duration movement.
- Add a small Presentation-only smoothing buffer so the visual lane is less likely to empty during short Runtime spikes. This buffer does not write back to Runtime movement cadence.

## Tests

Run:

```powershell
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Update presentation regression tests to assert:

- live movement uses a motion lane / `_Process` runner;
- live movement no longer depends on creating a movement tween per step;
- active movement monitor remains available.

## Manual QA

Run the same many-unit battle and inspect:

- movement appears as continuous unit motion, not low-sample per-cell stepping;
- `Battle/MovementTweensCreated` should no longer climb with every movement event;
- `Battle/MovementTweensInterrupted` remains zero;
- hit feedback and damage numbers still wait for relevant movement.

## Acceptance Evidence

Implemented:

- `BattleUnitRoot` now stores actor-local `MovementLane` instances and advances them in `_Process(delta)`.
- Movement events enqueue visual segments into the lane instead of creating a Godot movement `Tween`.
- Existing `GridOccupantComponent` updates still happen from Runtime movement events before visual interpolation.
- Render sort and facing update at segment boundaries.
- Superseded by `gameplay-alignment/implementation-proposals/archived/2026-05-21-battle-live-movement-queueing.md`: visual movement duration now stays equal to Runtime movement action duration, so `VisualMoveSmoothingSeconds` no longer adds per-step interpolation time.
- `ObserveRuntimeMovementEvent` returns the visual movement duration so same-tick damage waits against the authoritative movement action duration.
- `scenes/world/sites/WorldSiteRoot.tscn` tunes `UnitMoveDuration` from `0.16` to `0.27`, slowing movement by about 40% for easier visual inspection and a calmer live battle cadence.

Verification:

- Red before implementation: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed on `battle runtime live movement uses actor motion lane`.
- Passed after implementation: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

Manual QA:

- User reran the same live movement scenario after the motion lane and slower movement tuning and reported the result was close enough to archive.
- If this feel is revisited later, tune `VisualMoveSmoothingSeconds` and `UnitMoveDuration` as Presentation/cadence parameters before changing Runtime movement rules.

## Stop Conditions

Stop and return to design discussion if smoothing requires Presentation to create pathfinding truth, delay Runtime settlement facts, or change movement/combat rules.
