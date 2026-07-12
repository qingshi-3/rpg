# Battle AI Boundary Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by keeping player input medium-frequency while preserving Runtime as combat truth.

## Responsibility

Tactical AI owns continuous battle decisions inside player intent:

- target selection inside command scope;
- path-following intent and local movement requests;
- command-scoped target retention;
- protect, pursuit, retreat, hold, regroup, and fallback decisions;
- degradation when a command cannot be fully executed;
- behavior-tree decision surface boundaries where LimboAI is used.

## Does Not Own

Tactical AI does not own:

- movement legality, topology, occupancy, reservations, or pathfinding truth;
- HP, damage, defeat, settlement, or battle outcome authority;
- long-term Domain state;
- final report generation;
- Presentation-only animation state.

## Player Intent Precedence

```text
explicit player command
-> active command constraints
-> battle group tactical posture
-> autonomous local behavior
-> safe fallback
```

Player command owns:

- main battle-group target;
- hero, corps, and combined command channel;
- tactical intent such as attack, defend, protect, retreat, regroup, or hold;
- hero skill cast intent and target;
- whether a new command overrides current autonomous behavior.

Tactical AI owns:

- battle-group plan execution inside the active objective zone and engagement rule;
- ordinary attack target choice;
- without explicit AI override, ordinary assault follows the active battle-group plan, senses local enemies inside that scope, then chooses the enemy and footprint-valid attack slot that best fits the engagement rule;
- local combat response: when a nearby fight is active inside command scope, AI may request joining that fight, occupying an attack slot, occupying a support slot, holding support, or returning to objective;
- command-scoped target retention, so movement does not jitter between incidental nearest enemies while rerouting;
- protect, pursuit, retreat, and hold-position choice;
- group spacing and formation maintenance where implemented;
- degradation when a command cannot be fully executed.

## Plan-Driven Autonomy

Tactical AI should not use global per-unit attack-position search as the default way to decide where moving units go.

The normal decision order is:

```text
active player command or battle-group plan
-> selected objective zone
-> engagement rule
-> local perception
-> retained target validation
-> request objective movement, target pursuit, attack-slot approach, hold, regroup, protect, or retreat
-> Runtime validation
```

The engagement rule controls when local perception interrupts objective movement. For example, move-first ignores distant incidental enemies, attack-first promotes sensed enemies quickly, hold drops targets that leave the held area, and protect-hero constrains target choice by threats to the hero.

Target reacquisition is allowed when the retained target dies, becomes invalid, violates the active scope, or the engagement rule demands a higher-priority response. It is not allowed merely because another render frame elapsed or because another enemy could be globally scored as slightly better.

## Local Combat Situation Decisions

AI may consume Runtime-built `LocalCombatSituation` facts for the owning battle group. A local combat situation is a temporary, group-owned tactical observation that identifies an active nearby fight inside a bounded local combat region, its participating actors, open attack slots, occupied attack slots, support slots, local imbalance facts, and command-scope boundaries. It is not a global tactical director or a whole-map target search cache.

Local combat terms must be executable:

- A fight is active when recent damage, recent attack intent, close threat, or objective-route blocking is present.
- A fight is relevant to an actor only when the actor is decision-ready, inside command scope and the owning group local combat region, able to reach an attack or support slot soon, not locked into another decision, and allowed by battle-group budget.
- Support positions are named staging roles, such as melee queue, line hold, or ranged hold. They are not generic waiting cells.
- Generic pressure scoring is not a first-slice behavior authority. Use explicit facts such as open attack-slot count, occupied attack-slot count, nearby friendly count, nearby hostile count, leash status, route-blocking status, and perception-coverage weight inside the configured local region cap.

Behavior trees use these facts to choose intent:

```text
target already in range
-> attack or wait for charge
nearby relevant local fight
-> join through attack slot, support slot, hold support, or return
objective still relevant
-> advance objective
fallback
-> hold or regroup
```

Local combat decisions must preserve player intent precedence:

| Engagement Rule | Local Combat Behavior |
|---|---|
| Move-first | Joins only if the fight blocks the objective route, threatens the actor/company, lies near the planned corridor, or includes the retained target. Distant incidental fights are ignored. |
| Attack-first | Joins relevant local fights inside perception and command scope, preferring open attack slots. |
| Fire-on-the-move | Joins only for short attacks or support moves that do not materially reverse objective progress. |
| Hold | Joins only inside held area or leash, then returns when the threat leaves. |
| Protect-hero | Joins threats inside the hero protect radius and returns after the threat clears. |
| Retreat-first | Joins only to clear a retreat blocker or immediate attacker. |

Local response should use anti-jitter locks: minimum join time, return lock, slot-claim timeout, and de-aggro timer. These locks prevent actors from oscillating between joining, returning, holding, and retargeting.

Behavior trees output typed intent such as attack, advance to target, advance to objective, join local combat, hold support, return to objective, retreat, or regroup. Runtime remains responsible for validating whether a requested attack or movement is legal.

Behavior-tree and C# executor decisions must emit low-noise reason codes for diagnostics, such as `join_recent_damage`, `join_blocks_objective_route`, `hold_support_attack_slots_full`, `return_objective_threat_clear`, `reject_outside_leash`, `reject_join_budget_full`, or `reject_no_reachable_slot`.


## Tactical Region Ownership

Target regions, temporary target regions, local combat regions, and engagement state are owned by battle groups. Runtime may expose global lookup caches keyed by battle-group id, but those caches are not tactical authorities and must not mutate group intent.

Reusable AI services may build perception facts, local combat regions, target candidates, attack-slot candidates, support-slot candidates, and degradation reasons. Side policy chooses how to use those services:

- enemy offense and active defense may replan target regions automatically;
- enemy hold defense may switch the whole group to active assault after damage, attack, or perception trigger;
- player groups may reuse the same tactical solvers, but their target regions and posture are controlled by player commands or accepted battle plans.

Non-engaged movement is region-directed. AI should not chase a moving actor as the ordinary out-of-combat movement target. Actor targets become valid when the group is engaged or when a command explicitly targets an actor.

## Bounded Local-Optimal Combat

Local combat optimization is scoped by a battle-group-owned local combat region. The region is built from the group's member perception coverage and capped by a performance-safe size limit. Cells perceived by multiple group members carry higher selection weight, allowing the chosen local region to favor overlapping group awareness without becoming a full-map tactical scan.

Inside the local region, AI may choose the best available local combat target, open attack slot, support slot, or fallback. Outside the local region, AI returns to region movement, command constraints, or safe fallback.

## Tactical Fact Refresh

AI tactical facts should not be rebuilt globally every Runtime tick. Runtime should use event-driven dirty marking, local lazy rebuilds, and bounded TTLs.

Rebuild local combat facts after important events such as battle start, damage, target defeat, movement completion into or out of threat range, command changes, objective changes, major unit additions, and relevant path or reservation failures. Movement start may mark a local area dirty without forcing immediate full rebuild.

Behavior trees should run at Runtime actor decision boundaries, not every render frame and not every fixed simulation tick while an actor is moving, recovering, casting, defeated, or otherwise locked.

First implementation should prefer simple local situation queries at actor decision boundaries. A broader `BattleTacticalSnapshot` cache may be added only when profiling or tests show that local queries are insufficient.

## Failure Degradation

- Target lost: choose a valid target inside command scope; otherwise hold or regroup.
- Path unreachable: try local reroute through Runtime navigation; otherwise stop advance and emit a failure candidate.
- Local combat full: try valid support positions before idle hold, while respecting leash, occupancy, and reservations.
- Protect target out of range: prioritize return/protect; if impossible, hold nearest safe position.
- Pursuit too deep: pursuit must obey retreat, protect, and area-hold constraints.
- Retreat blocked or late: emit explicit failure candidates for report attribution.

AI behavior must be attributable to a command, default posture, or safe fallback. Reports must not describe AI-driven outcomes as source-less randomness.

## LimboAI Decision Boundary

LimboAI is the authored behavior-tree surface for tactical AI decisions. It does not own combat truth.

The integration model is driver-first:

```text
LimboAI decision tick
-> C# Facade builds typed action request
-> Runtime validates and applies action
-> Runtime emits event/result observation
-> LimboAI consumes observation on a later tick
```

Layer rules:

- Behavior trees may select targets, choose tactical posture, and request move, attack, cast, hold, protect, retreat, or regroup decisions.
- Behavior trees must output intent or command DTOs through a narrow Runtime or Presentation Facade.
- C# Runtime and Application services still validate movement, pathing, occupancy, ability legality, damage, event emission, settlement, and world writeback.
- Blackboard values are per-tick decision context. They must not become save authority for HP, position, battle result, world state, or city/location state.
- Initial Godot C# integration uses GDScript LimboAI tasks calling C# Facade nodes, because direct C# custom task support is not the first stable boundary for the GDExtension install.
- LimboAI tasks may record local observations such as `last_action_status` or `failure_reason`, but those values are feedback for the next decision tick, not authoritative state.

First migration order:

```text
presentation BattleIntent planner equivalence
-> target battle runtime AI executor
-> optional strategic enemy intent planner
```

`BattleIntentResolver` remains the first-slice action-resolution authority until replaced by an accepted Runtime AI executor. Runtime migration must preserve `BattleEventStream`, outcome, navigation, footprint, and settlement contracts.

## Inputs

- accepted `RuntimeOrder`;
- accepted `BattleGroupPlan` values and active plan state;
- battle-group posture and command scope;
- Runtime actor facts and target facts;
- Runtime-built local combat situation facts;
- Runtime navigation result observations;
- previous typed action result observations.

## Outputs

- typed action requests such as hold, move, attack, join local combat, hold support, return to objective, cast, protect, regroup, or retreat;
- AI failure candidates for Runtime events;
- observations for later behavior-tree ticks.

## Acceptance

This architecture is acceptable when:

- AI can keep battles moving without high-frequency player micro;
- explicit player command and command scope remain the highest tactical constraint;
- LimboAI can choose intent without mutating movement, damage, settlement, or world state;
- Runtime remains the final validator and event source for movement, attack, damage, outcome, and failure facts.
