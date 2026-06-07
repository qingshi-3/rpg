# Battle Preparation Drag Deployment UX

Status: Archived

## Relationship Metadata

- Requirement Id: `REQ-2026-06-07-battle-prep-drag-deployment-ux`
- Parent Proposal: none
- Supersedes: none
- Superseded By: none
- Amends: `2026-05-17-presentation-ui-layout-architecture`, `2026-05-23-battle-plan-state-machine`
- Amended By: none
- Affected Authority Documents:
  - `gameplay-design/details/combat-command/README.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/world-battle-entry-architecture.md`
- Related Implementation Proposals: pending

## Current Design

The accepted direction already requires battle preparation to create one plan per selected hero company before battle start. The current documents describe a company-by-company preparation loop with hero placement, corps placement, objective-zone selection, engagement-rule selection, and plan confirmation.

The current UI architecture still permits too much panel-shaped presentation: battle-preparation content may be hosted as a main left workspace, objective selection may read as a modal tactical overview, and roster/panel wording does not yet forbid long text explanations or action-list style UI. This leaves room for a text-heavy "paper panel" implementation that provides entry points but weak player experience.

## Expected Design

Battle preparation becomes a map-first drag deployment flow:

```text
compact hero-company roster / drag source
-> drag hero portrait into battlefield
-> preview full hero-led company formation
-> validate full formation placement
-> drop legal formation
-> select objective from a tactical thumbnail
-> select engagement rule from compact current-company controls
-> repeat until all required companies are complete
-> start battle
```

The compact roster is only a switcher and drag source. It shows portrait, name, and status marker. It must not become a long information panel.

During drag, the UI should get out of the way. Persistent HUD controls slide offscreen while deployment-zone highlights, formation preview, and legality feedback remain visible. Invalid placements render the whole formation preview red and may show a short local reason.

Objective selection uses a compact tactical thumbnail for the current company. Objective choices must be marker-backed target regions from the active map. Engagement rule selection belongs to compact current-company controls, not a text-heavy action list.

Application remains the authority for plan draft validation. Presentation previews the result but must not create a second battle snapshot, a second unit pool, or hidden objective cells.

## Non-Goals

- Redesigning large-world UI.
- Changing combat Runtime movement, target, or damage authority.
- Adding live battle command UI.
- Creating new objective-zone authoring semantics beyond the existing semantic marker architecture.
- Replacing the accepted hero-led light RTS battle identity.

## Acceptance Criteria

- A player can drag a hero company portrait from a compact roster and see the full company formation preview.
- Legal and illegal deployment locations are visually distinguishable during drag; illegal placement is red at the formation level.
- Dropping on a valid deployment location commits the company formation through Application-owned draft state.
- Persistent HUD controls move away during drag and return after release.
- The current company can choose a marker-backed objective from a tactical thumbnail.
- The current company can choose an engagement rule without reading a long text panel.
- Start battle remains blocked until every required player company has valid formation placement, objective-zone selection, and engagement rule selection.
