# Battle Runtime Spike Diagnostics Implementation Proposal

Status: Implemented - pending manual QA
Created: 2026-05-21

Originating Design Proposal: None
Related Implementation Proposals:
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-movement-performance.md`
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-live-movement-queueing.md`

Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

## Goal

Diagnose live battle stutter where `Battle/RuntimeAdvanceMsLast` spikes during movement-heavy combat, without changing movement legality, target choice, animation timing, or Presentation authority.

## Boundary

- Runtime remains authoritative for movement, target choice, reservations, damage, and action timing.
- Presentation remains an observer of Runtime events and must not create movement or pathfinding facts.
- Same-tick released footprint cells remain blocked until a later Runtime decision boundary.
- Diagnostics must stay low-noise and cheap enough to leave enabled during manual Godot runs.

## Scope

1. Add Runtime sub-stage counters for flow-field build, combat-slot scan, target scoring, movement proposal resolution, and runtime tick-at-max.
2. Add movement-cadence counters for movement events per advance, reservation rejections, actors holding after reservation failure, decision-ready actors with no movement event, and max observed movement-event gap.
3. Expose the new counters through the existing Godot custom monitor registry.
4. Add an automatic `BattleRuntimeSpike` summary at the Runtime advance boundary so manual Godot runs do not require reading several monitor values at once.
5. Keep tests focused on counter availability, deterministic headless counter updates, and automatic spike-summary output.

## Non-Goals

- No pathfinding optimization in this slice.
- No change to attack slot rules, target selection semantics, reservation legality, or same-tick occupancy rules.
- No Presentation-side smoothing or timing change.
- No per-node or per-edge logging.

## Tests

Run after implementation:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Manual QA

During the same battle scene that showed `RuntimeAdvanceMsLast=264`, watch these monitor groups together:

- `Battle/RuntimeAdvanceMsLast`, `Battle/RuntimeAdvanceMsMax`, `Battle/RuntimeAdvanceTickAtMax`
- `Battle/FlowFieldBuildMsLast`, `Battle/CombatSlotScanMsLast`, `Battle/TargetScoringMsLast`, `Battle/MovementResolveMsLast`
- `Battle/MovementEventsLastAdvance`, `Battle/ReservationRejectedLastAdvance`, `Battle/ActorsReadyNoMoveLastAdvance`, `Battle/MovementEventGapMsMax`

The runtime also writes a self-contained `BattleRuntimeSpike` warning line when an advance crosses the spike threshold. That line includes total Runtime time plus target scoring, flow-field build, combat-slot scan, movement resolve, movement event, reservation rejection, and ready-without-move fields.

## Acceptance

- A manual Godot run can distinguish a true computation spike from movement cadence stalls.
- Headless regressions prove the new counters are updated in a deterministic movement-heavy scenario.
- Existing Runtime, Navigation, Presentation timing, and settlement tests still pass.

## Implementation Evidence

Implemented:

- Added Runtime sub-stage counters:
  - `FlowFieldBuildElapsedTicks`
  - `CombatSlotScanElapsedTicks`
  - `TargetScoringElapsedTicks`
  - `MovementResolveElapsedTicks`
  - `RuntimeAdvanceTickAtMax`
- Added movement-cadence counters:
  - `MovementEventsLastAdvance`
  - `ReservationRejectedCount`
  - `ReservationRejectedLastAdvance`
  - `HoldDueReservationCount`
  - `ActorsReadyNoMoveLastAdvance`
  - `MaxMovementEventGapMicroseconds`
- Exposed matching Godot custom monitors under `Battle/*`, including:
  - `Battle/FlowFieldBuildMsLast`
  - `Battle/CombatSlotScanMsLast`
  - `Battle/TargetScoringMsLast`
  - `Battle/MovementResolveMsLast`
  - `Battle/RuntimeAdvanceTickAtMax`
  - `Battle/MovementEventsLastAdvance`
  - `Battle/ReservationRejectedLastAdvance`
  - `Battle/ActorsReadyNoMoveLastAdvance`
  - `Battle/MovementEventGapMsMax`
- Added automatic `BattleRuntimeSpike` warning logs at both live `AdvanceNextTick()` and headless autonomous Runtime advance boundaries.
- Default spike threshold is `50 ms`. `RPG_BATTLE_RUNTIME_SPIKE_DIAGNOSTIC_MS` can override it for focused diagnosis; `0` logs every measured advance and is intended for tests or temporary local diagnosis.

Verification:

- Red before implementation:
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed on missing `FlowFieldBuildElapsedTicks`.
  - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed because the monitor registry did not expose the new Runtime spike counters.
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed on missing `BattleRuntimeSpike` automatic summary before the spike logger was implemented.
- Green after implementation:
  - Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed: `dotnet build-server shutdown`

Manual QA remains pending. Use the monitor values above to decide whether the next fix should optimize Runtime computation or movement cadence.
