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

## Failure Degradation

- Target lost: choose a valid target inside command scope; otherwise hold or regroup.
- Path unreachable: try local reroute through Runtime navigation; otherwise stop advance and emit a failure candidate.
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
- Runtime navigation result observations;
- previous typed action result observations.

## Outputs

- typed action requests such as hold, move, attack, cast, protect, regroup, or retreat;
- AI failure candidates for Runtime events;
- observations for later behavior-tree ticks.

## Acceptance

This architecture is acceptable when:

- AI can keep battles moving without high-frequency player micro;
- explicit player command and command scope remain the highest tactical constraint;
- LimboAI can choose intent without mutating movement, damage, settlement, or world state;
- Runtime remains the final validator and event source for movement, attack, damage, outcome, and failure facts.
