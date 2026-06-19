# Player Tactical Intent Migration Proposal

Status: Archived

## Relationship Metadata

| Field | Value |
|---|---|
| Requirement Id | BTI-PLAYER-MIGRATION-001 |
| Parent Proposal | `design-proposals/archived/2026-06-16-battle-tactical-intent-architecture/` |
| Supersedes | None |
| Superseded By | None |
| Amends | `design-proposals/archived/2026-06-16-battle-tactical-intent-architecture/` |
| Amended By | None |
| Affected Authority Documents | `system-design/battle-tactical-intent-architecture.md`; `system-design/battle-ai-boundary-architecture.md`; `system-design/battle-group-tactical-region-architecture.md`; `system-design/battle-command-architecture.md` |
| Related Implementation Proposals | `gameplay-alignment/implementation-proposals/2026-06-16-player-tactical-intent-migration.md` |

## Requirement

Player-commanded battle-group movement must migrate into the same Tactical Intent domain used by enemy movement. Battle preparation and deployment remain upstream intent-input workflows; they do not own Runtime movement target selection, target stability, retarget policy, group action-zone generation, or pathing.

## Current Architecture

The accepted Tactical Intent architecture currently states that the first slice applies to enemy-controlled battle groups only. Player-commanded movement remains under the existing player command and battle-plan path.

The implementation has already started seeding player command regions into tactical state, but player movement can still use the older objective-anchor movement path as a separate runtime authority.

## Expected Architecture

Tactical Intent becomes side-neutral Runtime architecture. Enemy configuration, scenario defaults, accepted player battle plans, accepted runtime player commands, and player-scoped fallback can all produce tactical intent plans with explicit source policy.

Player-sourced intent has priority over autonomous fallback and enemy policy. Enemy-sourced intent remains configurable and independent from scenario type. Both sides consume the same selected target object, selected region, group action zone, region movement, and local-combat execution path.

`BattleGroupPlan` remains a battle-preparation input DTO. After battle entry, it must be converted into player-sourced tactical intent and must not remain a separate player-only movement authority.

## Non-Goals

- Do not redesign deployment UI or battle-preparation interaction.
- Do not change Presentation interpolation or animation.
- Do not replace Runtime movement legality, occupancy, reservations, topology, or path validation.
- Do not add campaign-persistent strategic AI.

## Acceptance Criteria

- Authority documents no longer say player movement is outside Tactical Intent migration scope.
- Deployment and battle preparation are documented as tactical intent input producers, not Runtime movement owners.
- Player and enemy non-engaged movement are documented as consumers of the same tactical intent and region movement path.
- The implementation proposal can remove the old player-only objective movement authority without a design conflict.
