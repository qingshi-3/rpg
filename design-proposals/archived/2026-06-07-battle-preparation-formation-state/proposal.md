# Battle Preparation Formation State

Status: Archived
Requirement Id: battle-preparation-formation-state
Parent Proposal: `design-proposals/archived/2026-06-07-battle-preparation-drag-deployment-ux/`
Supersedes: None
Superseded By: None
Amends: `design-proposals/archived/2026-06-07-battle-preparation-drag-deployment-ux/`
Amended By: None
Affected Authority Documents:
- `gameplay-design/content-systems-long-term-design.md`
- `system-design/world-battle-entry-architecture.md`
- `system-design/presentation-ui-layout-architecture.md`
Related Implementation Proposals:
- `gameplay-alignment/implementation-proposals/2026-06-07-battle-preparation-company-deployment-ui.md`

## Current Design

Accepted battle preparation is map-first and company-based, but current authority does not explicitly define formation state. It describes deployment, objective choice, and engagement rule, while leaving unclear whether formation is a long-term company preference, a one-battle plan value, or a transient drag parameter.

The current formation planner validates member footprints, but the design documents do not explicitly state that formation auto-adaptation must preserve non-overlapping member footprints.

## Expected Design

Hero companies gain a two-layer formation model:

- strategic management owns a long-term default formation preference for the hero company;
- battle preparation owns the selected formation for the current battle plan.

When a battle-preparation plan is created, the selected formation initializes from the company's default formation. Drag deployment uses the selected formation. The player may change the selected formation before or after placing the company; changing it after placement attempts to recompute the formation around the current deployment anchor. If the new formation cannot fit, the previous valid placement remains intact.

Formation auto-adaptation is allowed to avoid deployment dead ends in narrow or irregular deployment zones. Adaptation may compress spacing, change row depth, or use a fallback column shape, but it must preserve member footprint legality. No two members may overlap, and every member's full footprint must stay inside deployable cells.

## Acceptance

The user accepted this direction in conversation on 2026-06-07 with the requirement to implement it and fix a drag-time overlap bug for 2x1 units.
