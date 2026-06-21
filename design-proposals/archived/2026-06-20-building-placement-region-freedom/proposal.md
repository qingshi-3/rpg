# Building Placement Region Freedom

Status: Archived

## Relationship Metadata

- Requirement Id: STRAT-OPS-001-AMEND-REGION-FREEDOM
- Parent Proposal: `design-proposals/archived/2026-06-20-strategic-operation-foundation/`
- Supersedes: none
- Superseded By: none
- Amends: `design-proposals/archived/2026-06-20-strategic-operation-foundation/`
- Amended By: none
- Affected Authority Documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/semantic-map-marker-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-20-strategic-operation-foundation-loop.md`

## Current Design

The accepted Strategic Operation foundation design still treats construction regions as category-compatible regions. Building definitions carry categories, construction-region definitions carry allowed categories, and placement legality rejects a building when its category does not match the selected region.

This makes authored map regions act like menu slots with spatial shape. It also causes presentation and Strategic Management to fail when marker-backed region cells and definition-backed region cells diverge.

## Expected Design

Construction regions define where city buildings may be placed. They constrain footprint, bounds, overlap, ownership/control, resources, and explicit building eligibility, but they do not restrict building category.

Building categories remain useful for UI grouping, city capability derivation, corps muster, and later support systems. They are not placement-region legality.

Terrain, tile, or local map context may later affect production/support efficiency through a focused economy/capability model. Efficiency modifiers must not be implemented as hidden category bans.

## Acceptance

User confirmed on 2026-06-20: buildable regions should only limit where construction is allowed; specific building choice belongs to the player; different terrain or tiles may influence resource efficiency instead of forbidding building categories.
