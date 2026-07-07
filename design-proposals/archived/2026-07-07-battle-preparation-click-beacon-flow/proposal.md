# Battle Preparation Click Beacon Flow

Status: Archived

## Relationship Metadata

- Requirement Id: BATTLE-BEACON-COMMAND-002
- Parent Proposal: `design-proposals/archived/2026-07-07-battle-beacon-flowfield-command/`
- Supersedes: None
- Superseded By: None
- Amends: `design-proposals/archived/2026-07-07-battle-beacon-flowfield-command/`
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/details/combat-command/README.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/battle-runtime-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-07-07-battle-beacon-flowfield-command.md`

## Current Design

The accepted beacon-flow design removed mandatory pre-battle objective-region and engagement-rule selection, but its preparation interaction still describes a drag-first deployment flow. It also describes destination beacons primarily as live-battle or tactical-pause commands after the battle clock starts.

That leaves two problems for the intended player experience:

- deployment still depends on drag-and-drop instead of a click-to-place formation preview;
- the player is reminded to choose a destination only after battle start, so the initial movement intent is not set during preparation.

## Expected Design

Battle preparation uses a click-to-place flow:

1. The player clicks a battle group in the compact preparation roster.
2. The full default formation follows the mouse as a placement preview.
3. The player clicks a legal deployment position to place the group.
4. A non-blocking top-center prompt tells the player to right-click a destination.
5. The player right-clicks a reachable destination cell during preparation to set the group's initial destination beacon.

Multi-select remains supported. Multiple selected deployed groups may share one destination beacon, both during preparation and after battle start. Live battle and tactical pause continue to use the same right-click beacon command behavior accepted by the parent proposal.

The preparation beacon is not a Presentation-side movement path. It is an initial command fact passed into Runtime at battle launch. Runtime still owns beacon validation, flow-field cache use, local neighbor commits, occupancy, reservations, local combat, and movement events.

## Acceptance

The user corrected the parent flow on 2026-07-07 and explicitly requested the click-to-place deployment flow plus preparation-time beacon setup.
