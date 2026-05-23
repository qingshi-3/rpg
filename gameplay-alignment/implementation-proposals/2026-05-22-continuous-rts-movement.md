# Continuous RTS Movement Implementation Proposal

Status: Implemented - pending manual QA
Created: 2026-05-22
Implemented: 2026-05-22

Originating Design Proposal:
- `design-proposals/active/2026-05-22-continuous-rts-movement`

Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

## Goal

Move live battle movement from action-bound one-cell cadence toward fixed-clock RTS movement while preserving runtime authority, grid topology, attack locks, reservations, diagnostics, and reportable battle facts.

## Scope

1. Advance presentation-backed runtime on a stable simulation cadence instead of waiting directly for each actor's next action-ready timestamp.
2. Add runtime support for fixed-time slices while preserving current action-lock semantics for movement, attack recovery, holding, and defeated phases.
3. Keep movement events runtime-authored and presentation-only interpolation.
4. Prevent Presentation from returning moving actors to idle between contiguous runtime movement segments.
5. Add or update regression tests for fixed-clock advancement and continuous movement presentation behavior.

## Non-Goals

- No Presentation-side pathfinding.
- No freeform physics authority; square-grid topology remains combat truth.
- No campaign settlement or report rewrite.
- No high-frequency individual soldier micro controls.

## Verification

Run after implementation:

```powershell
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Implementation Evidence

- Added `BattleActionTimingPolicy.DefaultSimulationTickSeconds` as the shared live simulation cadence.
- Added `BattleRuntimeSessionController.AdvanceFixedTick` so presentation-backed runtime resolves tick-zero at runtime time 0, then advances by fixed clock slices without jumping straight to the next actor-ready boundary.
- Added runtime actor movement target/progress state. Movement start now records from/to cells, duration, start time, and progress without immediately committing the actor anchor.
- Updated `BattleRuntimeActorStateMachine` so moving actors advance progress during runtime ticks and commit the target cell only when the movement boundary is reached.
- Updated dynamic occupancy to include an in-progress mover's reserved target footprint so other actors cannot path into the target cell while the mover is between cells.
- Split runtime movement events into `MovementStarted` for visual movement launch and `MovementCompleted` for committed runtime cell boundaries.
- Added sticky target acquisition for default assault movement: actors still pick the fastest attack opportunity when acquiring a target and still snap to immediate attack opportunities, but they retain a live target while marching instead of rescoring all enemies and rebuilding flow fields every movement boundary.
- Captured occupancy before movement-boundary commits, so cells released by a movement completion are not available to other actors in the same resolver pass.
- Excluded actors that complete movement in the current resolver pass from starting a new attack or movement decision in that same runtime tick.
- Updated live site battle runtime to call `AdvanceFixedTick(tickSeconds)` and wait the same fixed interval.
- Kept `AdvanceNextTick` for headless/action-bound regression behavior while the continuous movement runtime is phased in.
- Added a movement-idle grace window in `BattleUnitRoot` so consecutive runtime movement events do not flash idle between cells.
- Updated regression assertions for fixed live tick cadence, actor-local movement locks, deferred runtime cell commit during in-progress movement, and MovementStarted/MovementCompleted ordering.

## Verification Evidence

- Passed: `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`

Known warnings during test runs:

- Existing Godot source-generator warning in test projects: `Property 'GodotProjectDir' is null or empty.`
- Existing nullable warnings in `TargetBattleArchitectureRegression`.

## Residual QA

Manual Godot playback should verify that multiple units no longer visibly pulse idle between movement cells and that attack recovery still reads as a deliberate pause.
