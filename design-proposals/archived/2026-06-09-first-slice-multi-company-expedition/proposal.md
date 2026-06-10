# First Slice Multi-Company Expedition Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: `REQ-2026-06-09-first-slice-multi-company-expedition`
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: `2026-06-07-first-slice-hero-skill-content-expansion` implementation assumptions about exactly one selected company per expedition
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/vertical-slices/first-playable-slice.md`
  - `system-design/world-battle-entry-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-09-first-slice-expedition-capacity.md`

## Current Design

The first playable slice currently requires the player to choose exactly one hero company for Bonefield and deploy exactly that company into the battle. The battle-entry architecture requires every required player battle group in the request to be placed, assigned an objective zone, and assigned an engagement rule before battle launch.

## Expected Design

The first slice should allow a Sanguo Qunying-style expedition where one strategic expedition army can carry multiple hero companies. Battle preparation lists the carried hero companies as deployable companies. The player may deploy any subset of carried companies, but at least one player hero company must be deployed, assigned an objective zone, and assigned an engagement rule before battle launch.

Undeployed carried companies remain in reserve for the first slice. They do not spawn in Runtime, do not participate in battle, do not suffer battle casualties, and cannot be called in as reinforcements in this implementation slice.

## Acceptance

Accepted by the user on 2026-06-09 with the explicit scope that deployment is optional per carried company, at least one company is required, and undeployed companies remain reserve-only.
