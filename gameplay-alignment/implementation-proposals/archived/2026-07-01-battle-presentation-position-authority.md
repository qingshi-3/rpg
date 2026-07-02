# Battle Presentation Position Authority

Status: Archived By User Request - Automated Verification Passed, Manual QA Pending

## Origin

- Requirement: BPPA-001
- Design Proposal: None. This implements existing accepted Runtime and Presentation authority.
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/hero-led-light-rts-system-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`

## Scope

Fix battle unit movement presentation so a live movement event does not promote the target cell into the unit's current Presentation surface before the visual lane reaches that cell.

The focused behavior is:

- Runtime remains the only combat grid authority.
- Presentation movement lanes may queue future visual segments.
- `GridOccupantComponent.SurfacePosition` in Presentation represents the current visually committed surface, not a queued future target.
- Anchored skill presentation may stop movement, but its snap point must come from the visually committed surface or current visual position, not from a queued future target.
- Segment completion updates the Presentation occupant surface and render sort at the same time the visible unit reaches the segment end.

## Non-Goals

- Do not change Runtime movement, pathfinding, occupancy, reservations, AI, skill duration, or skill interruption rules.
- Do not replace the existing actor-local movement lane with Godot tweens.
- Do not add skill balance changes or new skill configuration behavior.
- Do not change battle preparation placement semantics.

## Touched Systems

- Battle Runtime live Presentation observer, only through existing movement event consumption if needed.
- Battle unit movement presentation in `BattleUnitRoot`.
- Battle hit feedback regression tests as source-structure guards for this presentation contract.

## GodotPrompter Skills

- `using-godot-prompter`
- `godot-debugging`
- `godot-testing`
- `csharp-godot`
- `state-machine`

## Tests

- Add a RED regression test proving `MoveEntityTo` does not call `gridOccupant.SetSurfacePosition(targetPosition)` when a movement lane is queued.
- Guard that movement lane segment completion commits `segment.ToSurface` to `GridOccupantComponent`.
- Guard that movement cancellation resolves a snap surface through a helper that does not blindly use the queued final target.
- Run `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` after the focused regression suite passes.
- Run `git diff --check`.

## Diagnostics

No new high-frequency runtime diagnostics are required. Existing movement and teleport logs are sufficient for this bug because the Runtime event stream already proved the grid sequence was coherent. If future manual QA still sees visual jumps, add a low-noise presentation log only at movement interruption boundaries.

## Manual QA

Replay the sustained hero skill case:

- Start a battle with the cavalry captain hero.
- Cast `first_slice_skill_thunder_spiral_break`.
- Watch the actor from skill release through the next movement steps.
- Expected: the actor may hold visually during the anchored cast and then continue from one visible position; it must not alternate between two cells.

## Acceptance

- Presentation no longer maintains two competing current-position authorities for a moving unit.
- Queued movement targets remain inside the movement lane until visually reached.
- Skill movement cancellation cannot snap to a future queued movement target.
- Existing movement, teleport, skill, damage, and presentation regression tests pass.

## Verification Evidence

- Archived by user request on 2026-07-01. Manual QA remains recorded as pending historical evidence rather than active queue work.
- RED observed before implementation:
  - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - Failed on `battle runtime movement keeps surface position at visual commit`, proving current movement presentation promoted queued target surfaces before visual commit.
- Automated verification after implementation:
  - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Manual QA:
  - Pending user replay of the sustained hero skill case.
