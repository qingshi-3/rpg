# Battle Plan State Machine Proposal

Status: Archived
Created: 2026-05-23

## Relationship Metadata

| Field | Value |
|---|---|
| Requirement Id | REQ-BATTLE-PLAN-STATE-MACHINE-2026-05-23 |
| Parent Proposal | None |
| Supersedes | None |
| Superseded By | None |
| Amends | None |
| Amended By | None |
| Affected Authority Documents | `gameplay-design/content-systems-long-term-design.md`; `gameplay-design/details/combat-command/README.md`; `system-design/hero-led-light-rts-system-architecture.md`; `system-design/world-battle-entry-architecture.md`; `system-design/presentation-ui-layout-architecture.md`; `system-design/battle-command-architecture.md`; `system-design/battle-runtime-architecture.md`; `system-design/battle-navigation-topology-architecture.md`; `system-design/battle-ai-boundary-architecture.md`; `system-design/semantic-map-marker-architecture.md` |
| Related Implementation Proposals | TBD after design acceptance and authority merge |

## Problem

The current movement performance issue exposes a design gap, not only a local optimization gap.

The accepted runtime and navigation documents already require explicit actor phases, continuous movement, sticky target ownership, and low-noise path diagnostics. However, battle entry still does not define a mature player-facing operation that produces a battle-group plan before runtime starts.

Without a battle-group plan, default movement is forced to derive too much intent from local target acquisition. That creates both product and technical problems:

- product: units look as if they are constantly searching for the globally best enemy or attack slot instead of following the player's battle plan;
- gameplay: the player cannot express the expected commander-level decision of where each hero-led corps should advance and how it should behave on contact;
- architecture: target acquisition, attack-slot scoring, objective movement, and tactical posture are easy to collapse into one high-frequency movement decision;
- performance: moving actors can repeatedly trigger expensive enemy/attack-position scoring while stationary attack phases are smooth.

## Current Design Summary

Current accepted authority supports:

- hero-led light RTS with `battle group = 1 hero + 1 main corps`;
- hero, corps, and combined command channels;
- pre-battle deployment zones;
- runtime actor phases and continuous grid-authoritative movement;
- sticky target acquisition as a recent movement-performance correction;
- semantic markers for deployment zones, chokepoints, lanes, reserve points, flank routes, ranged points, and defend points.

Current authority does not yet define:

- a battle-preparation flow where the player selects each hero-led battle group, deploys hero and corps, chooses a configured objective area, then selects engagement rules before battle start;
- a persisted battle-preparation plan DTO carried into `BattleStartSnapshot`;
- objective-zone semantics as first-class map data;
- battle-group state-machine states derived from the plan;
- engagement rules such as move-first, attack-first, fire-on-the-move, hold, retreat-first, or protect-hero as explicit state-transition policy;
- navigation separation between objective-region advance, local perception, target lock, attack-slot approach, and fallback to the objective path.

## Expected Design

Introduce a `BattleGroupPlan` concept created during battle preparation and consumed by battle runtime.

The player-facing preparation loop becomes:

```text
select hero battle group
-> deploy the hero
-> deploy that hero's corps units
-> zoom out to the authored tactical map
-> choose one configured objective area for that battle group
-> choose engagement rules
-> repeat for remaining battle groups
-> confirm and start battle
```

The plan records the initial intent for each battle group:

| Plan Field | Meaning |
|---|---|
| `BattleGroupId` | The hero-led battle group being planned. |
| `HeroDeployment` | Hero starting cell or deployment marker selection. |
| `CorpsDeployment` | Formation slots or unit placements for the visible corps. |
| `ObjectiveZoneId` | Authored target region selected by the player. |
| `EngagementRule` | State-machine policy such as fire-on-the-move, move-first, attack-first, hold, retreat-first, or protect-hero. |
| `InitialFormation` | Formation identity or lane alignment for the first runtime slice. |

Runtime uses the plan as command-scoped intent, not as a full scripted path. The battle-group state machine advances from objective movement into local combat through explicit transitions:

```text
Deploying
-> AdvancingToObjective
-> SensingContact
-> TargetLocked
-> MovingToAttackSlot
-> Attacking
-> RegroupingOrReturningToObjective
-> Retreating / Routed / Defeated
```

Navigation becomes layered:

1. **Objective-region movement:** choose next movement toward the selected objective zone through authored lanes, chokepoints, and region hints.
2. **Local perception:** detect enemies within command/posture range; perception can interrupt objective advance only through the active engagement rule.
3. **Target lock:** retain a valid target until death, invalidation, command override, excessive pursuit distance, or a higher-priority rule triggers reacquisition.
4. **Attack-slot approach:** build or reuse target-specific attack-slot pathing only when a locked target requires a legal attack anchor.
5. **Fallback:** if the target is lost or unreachable, return to the objective route, hold, or retreat according to the rule.

This keeps mature RTS behavior: the player gives a plan, units advance coherently, local combat is automatic and readable, and expensive global target/attack-slot scoring is not the default movement loop.

## Implementation Direction After Acceptance

Implementation should happen only after the expected authority copies are accepted, merged, and archived. A focused implementation proposal should then define the code/resource work in phases:

1. Add data contracts for `BattleGroupPlan`, `ObjectiveZone`, and `EngagementRule`.
2. Update battle-preparation UI to produce plans without becoming state authority.
3. Add semantic marker extraction for objective zones and route hints.
4. Add battle-group runtime state-machine state and diagnostics.
5. Route target acquisition through engagement rules and local perception.
6. Change movement planning to prefer objective-zone advance plus retained target pursuit.
7. Add regression tests for plan handoff, state transitions, target retention, and bounded flow-field builds.

## Non-Goals

- No high-frequency individual soldier micro.
- No freeform physics navigation; square-grid runtime authority remains.
- No pure post-deployment auto-battle without player command authority.
- No second UI-owned unit pool or battle-start snapshot.
- No per-frame target/attack-slot rescoring as a default behavior.
- No direct authority-document edits before acceptance.

## Acceptance Criteria

This proposal is acceptable when the expected copies clearly define:

- the battle-preparation operation from hero selection through objective and rule selection;
- how objective zones are authored and carried into battle start;
- how engagement rules drive state-machine transitions;
- how battle-group plans interact with hero/corps/combined command channels;
- how runtime movement avoids global per-unit target scoring during ordinary advance;
- how navigation separates objective-region movement from local attack-slot approach;
- how UI remains Presentation-owned and submits plan requests instead of owning battle truth.

## Merge Record

Accepted, merged into authority documents, and archived on 2026-05-23.
