# Continuous RTS Movement Cadence

Status: Archived

## Requirement Id

REQ-2026-05-22-continuous-rts-movement

## Parent Proposal

None

## Supersedes

None

## Superseded By

None

## Amends

None

## Amended By

None

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

## Related Implementation Proposals

- Pending implementation workstream under `gameplay-alignment/implementation-proposals/`.

## Current Architecture

The accepted battle architecture already targets hero-led light RTS combat, map topology, flow-field-assisted movement planning, actor phases, and Presentation-owned visual interpolation.

However, movement cadence is still action-bound: a ready actor plans and commits one neighboring cell as a complete movement action, then waits `MoveStepSeconds` before the next decision boundary. Presentation receives one movement event per committed neighbor cell and may briefly return to idle between cells.

That model is deterministic and readable, but it produces visible step cadence and does not match mature RTS movement, where simulation time advances at a fixed cadence and moving agents continue advancing while path direction is refreshed from current facts.

## Expected Architecture

Runtime keeps square-grid topology as combat authority, but uses fixed simulation ticks for live battle advancement. Moving actors maintain continuous movement state across ticks and cross cell boundaries as their speed accumulates enough progress. Flow-field and attack-slot data provide the current direction or next anchor target; action locks still prevent attacking, casting, or replanning while the actor is in an incompatible phase.

Presentation follows Runtime movement state and visualizes continuous motion. It must not create alternate combat truth, but it also must not treat each cell as a complete animation state. Move animation remains active while the actor is still pursuing a runtime movement intent and returns to idle only when Runtime stops, attacks, holds, is interrupted, or battle playback completes.

## Acceptance

- Live battle Runtime advances on a fixed simulation cadence instead of waiting for each actor action boundary as the outer clock.
- Movement can remain grid-authored and deterministic while visual motion is continuous across neighboring cells.
- Flow-field/path data remain runtime-owned decision support and are not called from Presentation frames.
- Attack, recovery, cast, hold, interruption, and defeat phases still lock actor decisions until their runtime completion condition.
- Presentation receives enough movement events to animate continuous motion without inventing pathfinding truth.
