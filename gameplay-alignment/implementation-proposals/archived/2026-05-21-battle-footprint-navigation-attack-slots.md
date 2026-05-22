# Battle Footprint Navigation And Attack Slots Implementation Proposal

Status: Archived - accepted
Created: 2026-05-21
Accepted: 2026-05-21
Implemented: 2026-05-21
Verified: 2026-05-21

Originating Design Proposal:
- `design-proposals/archived/2026-05-21-battle-footprint-navigation-attack-slots`

Requirement Id: `REQ-BATTLE-FOOTPRINT-NAV-ATTACK-SLOTS-2026-05-21`

Authority Documents:
- `gameplay-design/details/combat-command/README.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

Related Implementation Proposals:
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-movement-performance.md`
- `gameplay-alignment/implementation-proposals/archived/2026-05-21-battle-presentation-motion-integrator.md`

## Goal

Align Runtime movement, reservations, attack-slot selection, and regression tests with the accepted full-footprint navigation and attack-slot authority.

## Architecture Boundary

- Runtime remains the authority for actor anchors, full-footprint legality, dynamic occupancy, same-tick reservations, target choice, pathfinding, attack range, damage, and emitted semantic events.
- Map topology compilation remains the authority for static topology nodes and edges. Runtime must not parse TileMapLayer, water, bridge, land, or raw authoring concepts.
- Presentation remains limited to visual interpolation, selection, hover, animation, audio, and feedback. Presentation must not create pathfinding, movement, or attack truth.
- This work implements the accepted square-grid anchored model; it must not introduce freeform physics movement, NavigationServer authority, AP turns, lane-only combat, or a second combat runtime.

## Current Evidence

Current code already contains several pieces of the accepted direction:

- `BattleActorFootprint` expands actor anchors into covered cells and calculates footprint-to-footprint gaps.
- `BattleNavigationGraph.CanPlaceFootprint` rejects anchors whose covered cells are missing from topology.
- `BattleDynamicOccupancy` stores tick-start occupants per covered cell.
- `BattleMovementReservationMap.TryReserveMove` rejects tick-start occupied covered cells and same-tick reservations for committed movement.
- `BattleCombatSlotAllocator.FindSlots` generates target-local attack and support slots from `BattleActorFootprint.GetGap`.
- `BattleFlowField.GoalSlots` feeds attack-opportunity scoring without duplicate slot scans.

Known verification gaps:

- Same-tick released cells must remain blocked for immediate committed movement, but this rule needs an explicit named regression case.
- Large-target surround behavior is implied by combat-slot generation but lacks a direct regression that two smaller attackers can use distinct valid attack slots against a larger target.
- Fastest reachable attack-slot target choice exists in targeting code but needs a regression tied to footprint-valid slots rather than nearest actor anchor.
- Existing tests cover missing covered terrain and footprint occupancy, but implementation comments should be checked against the accepted tick-start occupancy boundary.

## Scope

1. Add focused regression coverage that same-tick released cells remain blocked until a later decision boundary.
2. Add focused regression coverage for multi-attacker surround behavior against a larger target footprint.
3. Add focused regression coverage that ordinary assault target choice favors the fastest reachable footprint-valid attack slot.
4. Repair Runtime code only if those tests expose a mismatch with accepted authority.
5. Update nearby implementation comments where wording still implies anchor-only legality or ambiguous same-tick release rules.

## Non-Goals

- No gameplay rule changes beyond the accepted authority documents.
- No design or architecture document changes unless implementation reveals a design conflict.
- No Presentation smoothing, animation, audio, or UI changes.
- No movement speed, attack speed, damage, range, footprint-size cap, or balance changes.
- No broad pathfinding rewrite or new navigation authority.
- No raw map authoring parsing in Runtime.
- No implementation from an active design proposal.

## Touched Systems

Likely test files:
- `tests/TargetBattleArchitectureRegression/Program.cs`
- `tests/TargetBattleArchitectureRegression/TargetBattleCongestionRegressionCases.cs`
- `tests/TargetBattleArchitectureRegression/TargetBattleFootprintRegressionCases.cs`
- `tests/TargetBattleArchitectureRegression/TargetBattleMultiUnitNavigationRegressionCases.cs`

Likely Runtime files if tests expose gaps:
- `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- `src/Runtime/Battle/Navigation/BattleActorFootprint.cs`
- `src/Runtime/Battle/Navigation/BattleMovementReservationMap.cs`
- `src/Runtime/Battle/Navigation/BattleCombatSlotAllocator.cs`
- `src/Runtime/Battle/Navigation/BattleCrowdMovementPlanner.cs`
- `src/Runtime/Battle/Navigation/BattlePathStepRules.cs`

## Implementation Plan

### Phase 1: Baseline And Red Tests

- Run the existing target battle architecture regression suite before edits:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
```

- Add a same-tick occupancy regression where a front actor moves and a rear actor is not allowed to enter the front actor's released footprint until a later decision boundary.
- Add a large-target surround regression where two `1x1` attackers can occupy distinct legal attack slots around a `2x2` target and both produce damage without occupying the same covered cell.
- Add a fastest attack-slot target regression where the nearest enemy actor is not the fastest reachable valid attack slot, and the actor chooses the target whose valid attack slot can be reached sooner.
- Run the target battle architecture regression suite and capture failures as the implementation checklist.

### Phase 2: Minimal Runtime Alignment

Only if Phase 1 tests fail against accepted authority:

- Adjust reservation logic so same-tick released cells are not available to later movers in the same tick.
- Preserve duplicate destination rejection and opposite-edge swap rejection.
- Keep graph placement checks footprint-aware through `BattleNavigationGraph.CanPlaceFootprint`.
- Keep attack-slot generation footprint-to-footprint and target-local.
- Keep target choice based on fastest reachable valid attack slot under ordinary assault when no explicit AI override exists.

### Phase 3: Comments And Diagnostics

- Update nearby comments in Runtime movement and reservation code to say tick-start occupied covered cells remain hard blockers for immediate movement.
- Keep diagnostics low-noise and actor/path scoped. Do not log per frame or per search node.
- If a new failure reason is needed, prefer a specific reason such as `reservation_rejected`, `no_reachable_attack_slot`, or `footprint_static_illegal`.

## Tests

Run after implementation:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

The target battle architecture suite is the primary gate because this work changes Runtime navigation and combat legality. The hit-feedback suite guards Presentation playback assumptions around movement and attack event timing.

## Manual QA

Use a battle with a large target and several small corps moving at once.

Check:

- small units can naturally surround a larger target when terrain and reservations allow it;
- same-tick follow movement feels smooth and does not create duplicate occupied cells;
- units do not pass through missing terrain or blocked diagonal corners;
- units do not attack while in transit;
- damage numbers and defeat feedback still align with attack events;
- battle outcome and report facts remain consistent with Runtime events.

## Acceptance Evidence

Implemented regression coverage:

- `runtime same-tick follow cannot enter released footprint`
- `runtime smaller units can surround large target attack slots`
- `runtime target choice uses reachable footprint attack slots`
- `runtime support below vertical engagement flanks to open attack slot`

Runtime changes:

- Updated Runtime movement reservation so same-tick released cells remain blocked for immediate committed movement; `TryReserveMoveAllowingReleasedCells` and its released-cell tracking path were removed.
- Updated `BattleCrowdMovementPlanner` so movement gradients prefer executable open attack slots instead of letting occupied attack slots pull support units directly behind engaged allies.
- Updated assault target scoring to use the same open-attack-slot dynamic flow, so target choice and movement both ask "which target can this actor actually attack soonest?"
- Added `BattleFlowFieldBuilder.BuildFromGoalSlots` so Runtime can rebuild a dynamic movement gradient toward the open subset of accepted combat slots without changing static topology authority or rescanning combat slots.
- Added `BattleFlowFieldBuilder.PreferOpenAttackSlots` as the shared mechanism used by both target scoring and movement planning.

Red/green evidence:

- First run after adding tests failed on `runtime same-tick follow cannot enter released footprint`, proving the previous Runtime path allowed later movers to enter same-tick released footprints and could produce visual clipping.
- After removing released-cell reservation bypasses, old chain-follow regressions were updated to the new contract: rear actors wait instead of entering tick-start occupied cells during the same tick.
- An earlier run after adding surround tests failed on `runtime smaller units can surround large target attack slots` because the test observed only the first runtime tick, before attack charge was ready. The test was corrected to run the minimal battle and assert that both distinct attackers eventually damage the `2x2` target.
- The same first run also failed the oversized-file guard because the added test registrations pushed `tests/TargetBattleArchitectureRegression/Program.cs` to `1003` lines. Registration formatting was tightened back to the accepted `1000`-line ceiling.
- A follow-up user-reported vertical engagement case failed before the Runtime fix: support approaching from below moved to the occupied attack lane behind an engaged ally instead of flanking to an open side slot. After the flow-field goal filtering fix, the regression passed.
- The existing target-choice regression was strengthened so the nearby target's closest attack slot is occupied by a friendly actor; the actor now selects the target with the fastest executable open attack slot, not the target whose occupied slot has the cheapest raw flow cost.

Verification:

- Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`

Manual QA:

- Deferred. This slice added Runtime regression coverage and did not change Runtime behavior or Presentation movement. A later playable QA pass should still inspect large-target surround readability in-scene.

## Stop Conditions

Stop implementation and return to design proposal flow if this work requires:

- changing the accepted tick-start occupancy blocking rule;
- changing attack range away from shortest footprint-to-footprint square-grid distance;
- letting Presentation create movement or combat truth;
- introducing a second navigation or combat authority;
- changing settlement or report facts to hide movement or attack-slot issues.
