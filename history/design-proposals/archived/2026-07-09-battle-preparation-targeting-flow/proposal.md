# Battle Preparation Targeting Flow Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: UI-BATTLE-PREP-TARGET-001
- Parent Proposal: `design-proposals/archived/2026-07-07-battle-preparation-click-beacon-flow/`
- Supersedes: None
- Superseded By: None
- Amends: `design-proposals/archived/2026-07-07-battle-preparation-click-beacon-flow/`
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/details/combat-command/README.md`
  - `gameplay-design/vertical-slices/first-playable-slice.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-07-09-battle-preparation-targeting-flow.md`

## Current Design

Battle preparation already uses a map-first deployment flow: the player clicks a compact roster row, places the selected battle group's formation, then chooses an initial destination beacon before launch. Current authority still says the destination step is guided by a non-blocking top prompt and confirmed by right-clicking a reachable destination cell. Presentation also allows a bottom battle-preparation plan bar that shows current group and objective state.

This leaves a weak feedback gap after placement. The major HUD docks are suppressed, the formation appears on the map, and the next required action is not visually self-evident. The bottom plan bar also carries little useful work because the decisive interaction is on the map.

## Expected Design

Battle preparation should treat destination selection as a focused map operation.

After a battle group is placed, Presentation immediately enters `DestinationTargeting` for that group. Persistent battle-preparation UI remains suppressed. Instead of relying on a text prompt, the map overlay draws a curved directional guide from the placed battle group toward the pointer. The guide uses a card-targeting style: thin at the source, wider through the middle, thin near the target, with an arrowhead at the end. The system cursor is hidden while this targeting guide is active, and the existing battle-grid hover remains visible so the player can still identify the target cell.

In this state, left-clicking a reachable destination cell confirms the initial destination beacon. Invalid or unreachable cells produce local map feedback and keep targeting active. Cancel returns to the battle-preparation roster without committing a destination.

The bottom battle-preparation plan bar is removed from the target UI. Battle launch is represented by one lower-right start-battle button that appears only outside map-operation suppression states. The button is disabled until launch readiness is satisfied, including at least one deployed participating battle group and required initial destination beacon facts.

Live battle and tactical-pause destination beacon commands keep using the accepted runtime command payload. Their exact input gesture remains Presentation-owned and can differ from the preparation-only left-click targeting state without changing Runtime command authority.

## Acceptance Criteria

- Battle preparation no longer depends on a bottom plan bar for current group, objective, or launch state.
- A lower-right start-battle button is the only persistent launch control outside map-operation states.
- The start-battle button is disabled with a player-readable reason until launch readiness is satisfied.
- Clicking a roster row enters formation-follow placement; left-clicking a legal placement commits the formation.
- After placement, Presentation immediately enters destination targeting for that battle group.
- Destination targeting suppresses persistent HUD, hides the system cursor, keeps grid hover visible, and draws a curved arrow guide from the placed group to the pointer.
- Left-clicking a reachable cell during destination targeting creates or moves the selected group initial destination beacon.
- Invalid destination clicks stay local to Presentation/Application feedback and do not create Runtime events.
- Cancel exits destination targeting without committing a new destination and restores the previous battle-preparation HUD state.
- The targeting guide is a subsystem-owned battle map overlay, not a generic tooltip or a gameplay authority.

## Non-Goals

- Do not redesign the full project UI theme-routing taxonomy.
- Do not change live battle Runtime movement, beacon flow-field semantics, settlement, or report attribution.
- Do not add objective-region or engagement-rule selection back into battle preparation.
- Do not make the curved targeting guide own destination legality or pathfinding truth.
- Do not implement code, scenes, or resources directly from this design proposal; use the related implementation proposal.
