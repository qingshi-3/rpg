# Battle Map Operation HUD Suppression Proposal

Status: Accepted

## Relationship Metadata

- Requirement Id: UI-BATTLE-HUD-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends:
  - `design-proposals/archived/2026-07-07-battle-preparation-click-beacon-flow/`
  - `design-proposals/archived/2026-07-05-ui-hover-presentation-contract/`
- Amended By: None
- Affected Authority Documents:
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-07-08-battle-map-operation-hud-suppression.md`

## Current Architecture

The accepted Presentation UI architecture already treats battle maps as fullscreen world content and says battle-preparation placement can move persistent HUD out of the battlefield view. The current authority still allows several runtime and pause-time controls to remain visible while the player is clicking battlefield cells. In practice this means bottom bars, side docks, command panels, or pause details can obscure units or map cells exactly when the player needs to choose a destination, skill target, or deployment placement.

The implementation also has separate handling for preparation drag retreat, runtime skill target picking, destination beacon input, and tactical-pause command UI. Pointer gates can prevent accidental clicks through HUD, but they do not solve the player problem: the obscured map cell remains inaccessible while the player is performing a map operation.

## Expected Architecture

Map-targeting state is a hard HUD suppression state. While the player is doing any operation whose next meaningful input is a battlefield cell, actor, or destination, all screen-space panels that could cover the battlefield must hide or retract and ignore mouse input. This applies to battle-preparation formation placement, preparation destination beacons, live or paused runtime destination beacons, hero skill target selection, and future map-targeted command flows.

Only map-owned feedback may remain during the operation: world-space highlights, formation previews, destination markers, target rings, cursor-local prompts, and very small mouse-ignored prompts. When the player confirms, cancels, backs out, changes selection, or leaves the mode, Presentation restores the previous HUD layer such as compact runtime summary, tactical-pause details, or battle-preparation roster.

The first implementation slice should reset only the battle HUD surfaces involved in this interaction path. It should not finalize the global UI theme taxonomy or redesign unrelated strategic, city, or recruitment UI.

## Non-Goals

- No battle Runtime command, damage, movement, cooldown, settlement, or AI rule changes.
- No new persistence or strategic-management state.
- No global UI theme-routing taxonomy decision.
- No full radial command implementation requirement in this slice.
- No requirement to remove map-space overlays such as deployment zones, skill range, target cells, destination beacons, or formation previews.

## Acceptance Criteria

- `system-design/presentation-ui-layout-architecture.md` defines map-targeting as a hard HUD suppression state.
- Battle-preparation placement and destination-beacon selection suppress blocking screen-space HUD until commit or cancel.
- Runtime hero skill targeting suppresses runtime summary and command panels until submit or cancel.
- Runtime destination beacon selection has an explicit map-operation state, not only a pointer gate around visible HUD.
- Tactical pause can show detail panels, but any map click operation entered from pause suppresses those panels and restores them afterward.
- The first implementation proposal names the Godot UI, HUD, input, C#, and testing skills used.
