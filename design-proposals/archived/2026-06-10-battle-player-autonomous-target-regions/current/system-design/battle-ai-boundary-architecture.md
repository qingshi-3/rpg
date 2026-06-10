# Battle AI Boundary Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by keeping player input medium-frequency while preserving Runtime as combat truth.

## Responsibility

Tactical AI owns continuous battle decisions inside player intent:

- target selection inside command scope;
- local movement intent and bounded neighbor-step requests;
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
- combat-zone response: when a relevant global combat zone is active inside command scope, AI may request moving toward that zone, joining that fight, occupying an attack slot, occupying a support slot, queueing, holding support, regrouping, or returning to objective;
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
-> battle-group commander state
-> retained target validation
-> request objective movement, target pursuit, attack-slot approach, hold, regroup, protect, or retreat
-> Runtime validation
```

The engagement rule controls when local perception interrupts objective movement. For example, move-first ignores distant incidental enemies, attack-first promotes sensed enemies quickly, hold drops targets that leave the held area, and protect-hero constrains target choice by threats to the hero.

Target reacquisition is allowed when the retained target dies, becomes invalid, violates the active scope, or the engagement rule demands a higher-priority response. It is not allowed merely because another render frame elapsed or because another enemy could be globally scored as slightly better.

## Combat Zone And Local Combat Decisions

AI may consume Runtime-built global `CombatZone` facts and commander-owned `GroupActionZone` facts. A combat zone identifies an active or imminent fight from all living units and clustered contact/perception/attack facts. A group action zone identifies what one commander group is trying to do in response. Neither fact is a movement, damage, or settlement authority.

A local combat situation is a temporary tactical observation scoped to a selected combat zone and the consuming commander group. It identifies participating actors, open attack slots, occupied attack slots, support slots, queue or regroup options, local imbalance facts, and command-scope boundaries. It is not a whole-map target search cache.

Local combat terms must be executable:

- A fight is active when recent damage, recent attack intent, close threat, or objective-route blocking is present.
- A fight is relevant to an actor only when the actor is decision-ready, inside command scope and the owning group local combat region, able to reach an attack or support slot soon, not locked into another decision, and allowed by battle-group budget.
- Support positions are named staging roles, such as melee queue, line hold, or ranged hold. They are not generic waiting cells.
- Reachability terms distinguish static slot reachability from executable next-step reachability. A statically reachable attack slot can still be unavailable this tick when the actor footprint, occupied cells, or reservations block every improving first step.
- Generic pressure scoring is not a first-slice behavior authority. Use explicit facts such as open attack-slot count, occupied attack-slot count, nearby friendly count, nearby hostile count, leash status, route-blocking status, and perception-coverage weight inside the configured local region cap.

Behavior trees use these facts to choose intent:

```text
target already in range
-> attack or wait for charge
relevant combat zone outside current unit position
-> move toward that combat zone through group action-zone movement
inside relevant combat zone
-> join through attack slot, support slot, queue, regroup, hold support, or return
blocked attack entry
-> named support, queue, line hold, flank, regroup, or explicit failure
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

Behavior trees output typed intent such as attack, advance to target, advance to objective, join local combat, hold support, return to objective, retreat, or regroup. Runtime remains responsible for validating whether a requested attack or movement is legal. First-slice movement requests name the target, region, attack slot, support slot, or hold role; they do not request or own flow-field construction.

AI may provide or consume coarse route intent such as objective entrance, lane, gate, chokepoint, portal, or group route anchor when those facts are already part of the accepted battle plan, static route topology, or group action zone. AI does not own route topology construction, portal search, obstacle following, movement legality, or final pathing truth. Runtime movement steering owns the local `SeekGoal`, `FollowObstacle`, `RejoinSeek`, `QueueOrHold`, and `StuckRecovery` mechanics after AI has selected the tactical intent.

Behavior-tree and C# executor decisions must emit low-noise reason codes for diagnostics, such as `join_recent_damage`, `join_blocks_objective_route`, `hold_support_attack_slots_full`, `return_objective_threat_clear`, `reject_outside_leash`, `reject_join_budget_full`, or `reject_no_reachable_slot`.

## Skill Release Decision Boundary

Skill release decisions belong to the actor behavior layer inside player command scope. UI may show availability hints and submit command intent, but it does not decide final release timing or apply effects.

Behavior trees or C# actor-decision logic may request `cast` when:

- an accepted skill order exists or autonomous skill policy explicitly allows the skill;
- the actor is at a valid release or interrupt boundary;
- the skill's default or explicit interrupt traits allow canceling the current action phase;
- cost, cooldown, limited use, caster state, and target state pass the execution precheck;
- the requested release still respects the active command channel and battle-group scope.

The default precheck treats active skills conservatively:

- a skill may interrupt basic attack windup before damage impact;
- a skill waits through basic attack recovery after damage impact unless an explicit trait cancels recovery;
- a skill cannot interrupt another active skill unless an explicit trait allows it;
- a targeted skill fails if the locked target is dead, invalid, or untargetable at release time;
- a targeted skill does not fail merely because the locked target moved out of range after command acceptance.

Behavior-tree outputs remain typed intent. Runtime validates and starts the action, and the effect execution layer applies any resulting effect payloads.

## Tactical Area Ownership

Combat zones are global battlefield facts and are not owned by battle groups. Group action zones, target regions, temporary target regions, and engagement state are owned by battle groups. Runtime may expose global lookup caches, but those caches are not tactical authorities and must not mutate group intent.

Reusable AI services may build perception facts, combat-zone facts, group action-zone facts, target candidates, attack-slot candidates, support-slot candidates, queue/regroup candidates, and degradation reasons. Side policy chooses how to use those services:

- enemy offense and active defense may replan target regions automatically;
- enemy hold defense may switch the whole group to active assault after damage, attack, or perception trigger;
- player groups may reuse the same tactical solvers and may enter player-scoped engagement, but their target regions and posture are controlled by player commands or accepted battle plans.

Non-engaged movement is region-directed through the group's action zone. AI should not chase a moving actor as the ordinary out-of-combat movement target. Actor targets become valid when the group is inside or joining a selected combat zone, or when a command explicitly targets an actor.

## Bounded Local-Optimal Combat

Local combat optimization is scoped by a selected global combat zone and the consuming commander group's action zone. The combat zone is built from all living units, clustered contact/perception/attack facts, participant footprints, and configured hot-area padding. Performance budgets apply to zone splitting and local slot/search evaluation; they must not clip the fact bounds of a zone that already contains participant footprints and immediate join space.

Outside a selected combat zone, AI requests region movement toward a group action zone. Inside the selected combat zone, AI may choose the best available local combat target, open attack slot, support slot, queue role, regroup role, or fallback. Outside command scope, AI returns to group action-zone movement, command constraints, or safe fallback.

## Tactical Fact Refresh

AI tactical facts should not be rebuilt globally every Runtime tick. Runtime should use event-driven dirty marking, local lazy rebuilds, and bounded TTLs.

Rebuild combat-zone facts after important events such as battle start, first contact, engagement enter/exit, damage, target defeat, movement completion into or out of threat range, command changes, objective changes, major unit additions, and relevant path or reservation failures. Rebuild group action zones at battle start, command/objective changes, invalidation, and a fixed tick interval. Movement start may mark a local area dirty without forcing immediate full rebuild.

Combat-zone and group action-zone rebuilds must emit low-noise area snapshots that include zone bounds, deployment-zone bounds, group action-zone bounds, and living unit positions. These diagnostics are the primary manual-QA entry point for explaining battlefield distribution.

Behavior trees should run at Runtime actor decision boundaries, not every render frame and not every fixed simulation tick while an actor is moving, recovering, casting, defeated, or otherwise locked.

First implementation should prefer simple local situation queries at actor decision boundaries. A broader `BattleTacticalSnapshot` cache may be added only when profiling or tests show that local queries are insufficient.

## Failure Degradation

- Target lost: choose a valid target inside command scope; otherwise hold or regroup.
- Movement blocked by map-scale static topology: request or switch a group-scoped route hint from static route topology when available; actor execution then continues through Runtime local movement.
- Movement blocked by short local static topology: try Runtime local steering with obstacle following; otherwise stop advance, switch route hint, hold pressure, or emit a failure candidate.
- Movement blocked by living-unit occupancy or same-tick reservation: queue, support, hold pressure, or retry at the next movement boundary instead of treating the blocker as a static obstacle to route far around.
- Local combat full: try valid support positions before idle hold, while respecting leash, occupancy, and reservations.
- Attack entry blocked by current occupancy: try named support, queue, line-hold, flank, or regroup roles before reporting a terminal path failure.
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
- Static route topology and route hints are advisory AI inputs; they do not let AI or Presentation bypass Runtime movement validation.
