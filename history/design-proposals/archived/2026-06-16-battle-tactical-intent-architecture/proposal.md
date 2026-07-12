# Battle Tactical Intent Architecture Proposal

Status: Archived

## Relationship Metadata

| Field | Value |
|---|---|
| Requirement Id | BTIA-001 |
| Parent Proposal | None |
| Supersedes | None |
| Superseded By | None |
| Amends | `battle-ai-boundary-architecture.md`, `battle-group-tactical-region-architecture.md`, `battle-runtime-architecture.md`, `battle-navigation-topology-architecture.md`, `content-systems-long-term-design.md`, `combat-command/README.md` |
| Amended By | None |
| Affected Authority Documents | `gameplay-design/content-systems-long-term-design.md`; `gameplay-design/details/combat-command/README.md`; `system-design/README.md`; `system-design/hero-led-light-rts-system-architecture.md`; `system-design/battle-tactical-intent-architecture.md`; `system-design/battle-runtime-architecture.md`; `system-design/battle-navigation-topology-architecture.md`; `system-design/battle-ai-boundary-architecture.md`; `system-design/battle-group-tactical-region-architecture.md` |
| Related Implementation Proposals | `gameplay-alignment/implementation-proposals/2026-06-16-enemy-tactical-intent-first-slice.md` |

## Current Architecture

The accepted architecture already separates player command, enemy policy, region-directed movement, local combat, Runtime validation, and Presentation interpolation. However, enemy behavior is still described mainly as enemy policy selecting fixed or temporary target regions. That leaves business semantics such as siege defense, active defense, offense, and temporary player clusters close to the movement target selection path.

This has two practical weaknesses:

- battle scenario semantics can be mistaken for AI intent, such as treating a siege defense as always holding a point;
- volatile runtime observations, especially player clusters, can become ordinary non-engaged movement targets and make enemy movement retarget too often.

## Expected Architecture

Introduce `Battle Tactical Intent` as a focused domain boundary.

The new architecture separates:

```text
battle target objects = tactical nouns
AI intent plans = verbs and constraints
tactical capabilities = reusable target selection, stickiness, leash, fallback, engagement, and movement-goal generation
scenario/adventure/business defaults = input only
```

Battle scenarios provide target objects and default intent, but explicit enemy group intent can override those defaults. A siege-defense battle may contain defenders that hold a gate, sally out, harass, protect a structure, retreat, or assault depending on configuration.

The first implementation slice connects enemy-controlled groups only. Player-commanded movement remains unchanged until a later accepted migration.

## Acceptance

This design is accepted when the expected documents define:

- target objects as the shared tactical noun layer;
- AI intent plans as configurable group-level input;
- scenario defaults as fallback, not hard behavior binding;
- temporary clusters as volatile tactical observations unless intent explicitly authorizes them;
- enemy-first implementation scope with player movement excluded;
- Runtime, Navigation, AI, and tactical-region documents routing to the new boundary.
