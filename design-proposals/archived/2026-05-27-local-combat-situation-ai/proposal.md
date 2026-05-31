# Local Combat Situation AI Proposal

Status: Archived

Requirement Id: battle-ai-local-combat-situation-v1

Parent Proposal: none

Supersedes: none

Superseded By: none

Amends: none

Amended By: none

Affected Authority Documents:

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

Related Implementation Proposals:

- `gameplay-alignment/implementation-proposals/archived/2026-05-28-local-combat-situation-ai.md`

## Problem

Current battle AI has plan-driven objective movement and target-specific attack-slot movement, but it lacks an explicit middle layer for local fights that have already started. When one part of the line is engaged, nearby units can still behave as if their only choices are objective movement, direct target pursuit, or hold. This creates poor combat feel: enemies may wait beside an active fight instead of reinforcing, occupying an attack lane, or taking a support position.

The next slice must move from navigation demo behavior toward playable battle behavior. The goal is not global per-unit optimal pathing. The goal is readable tactical behavior: units notice nearby active combat, join within their command scope, avoid overcrowding attack positions, and preserve defensive leash rules.

## Current Design

Current authority already says:

- battle plans and engagement rules constrain automatic behavior;
- tactical AI selects intent but Runtime validates movement, occupancy, reservations, damage, and events;
- LimboAI is a decision surface, not combat truth;
- objective-zone movement should not become global full-map attack-position search;
- pathfinding runs at Runtime action decision boundaries, not render frames.

Current design does not yet describe a durable Runtime-owned tactical fact for local combat situations, support positions, join decisions, or event-driven tactical-fact caching.

## Expected Design

Introduce a Runtime-owned, temporary tactical fact named `LocalCombatSituation`.

Player-facing meaning: when a fight starts in an area, nearby units that satisfy the local combat predicates below should understand that this is an active local fight. They should either attack, move into an open attack position, move into a named support position, hold near the fight, or return to their objective/defense area when the fight is no longer relevant.

Runtime-facing meaning: `LocalCombatSituation` is not a scene node, not a persistent object, and not a second combat authority. It is a cached tactical observation built from Runtime truth:

- recent attack and damage events;
- actor anchors, target ids, HP, phases, and command facts;
- objective-zone and engagement-rule facts;
- navigation topology, dynamic occupancy, and reservations;
- valid attack slots and support slots around engaged targets.

First-slice design must avoid empty terms. "Relevant", "eligible", "pressure", and "support" are not free-form labels; they must resolve through executable rules.

## Design Principles

- Do not run global attack-position search for every unit every tick.
- Do not let behavior-tree blackboards own HP, position, target locks, occupancy, reservations, battle results, or world state.
- Do not make objective-zone movement blind to active nearby fights.
- Do not let defenders chase indefinitely outside their accepted objective, hold, or defense area.
- Do not make support units idle when direct attack slots are full but named support positions are available.
- Do not introduce another runtime authority parallel to existing Runtime events and navigation validation.

## Runtime Tactical Facts

Runtime should target three decision layers:

```text
BattleTacticalSnapshot
-> LocalCombatSituation
-> ActorDecisionFacts
```

`BattleTacticalSnapshot` is a target architecture layer for a lightweight tactical index, not a required first-slice full recomputation of the battle:

- living actors by faction;
- recent attack/damage pairs;
- rough spatial buckets;
- active objective-zone references;
- tactical version counters.

`LocalCombatSituation` is scoped around one active local fight:

- stable situation id;
- center cell and scan radius;
- participants and nearby actors that satisfy the join predicates;
- primary hostile anchors;
- open attack slots;
- occupied attack slots;
- support slots;
- simple local imbalance facts, such as attacker count, defender count, open attack-slot count, and support-slot count;
- defensive leash or objective boundary;
- dirty reason and last built runtime time.

`ActorDecisionFacts` is assembled only when an actor reaches a Runtime decision boundary. It answers narrow behavior-tree questions:

- is a target already in range;
- is there a nearby local fight that satisfies the executable local combat rules;
- can the actor join within command scope;
- is an attack slot open;
- is a support slot open;
- is the actor outside leash;
- should it continue objective movement, join combat, hold support, return, or retreat.

## Executable Local Combat Rules

A local combat situation exists when at least one of these predicates is true:

- `RecentDamage`: a living actor damaged or was damaged by a hostile actor within the last 1.5 Runtime seconds.
- `RecentAttackIntent`: a living actor emitted or accepted an attack action against a hostile actor within the last 1.5 Runtime seconds.
- `CloseThreat`: hostile corps anchors are within local threat distance, defined as `max(attacker range, defender range) + 2`, and at least one side has an active target, attack recovery, or wait-for-charge state.
- `BlockedByFight`: the actor's objective or retained-target route has its next practical movement corridor occupied by hostile or engaged actors.

An actor may consider joining a local combat situation only when all of these predicates are true:

- `DecisionReady`: the actor is at a Runtime anchored decision boundary.
- `InCommandScope`: the situation satisfies the actor's engagement rule table below.
- `ReachableSoon`: an attack slot or support slot can be reached by the next committed step, or by a bounded support search of 2-3 legal steps.
- `NotLockedElsewhere`: the actor is not inside a minimum join, return, retreat, attack recovery, cast, or command-interrupt lock.
- `BudgetAllowsJoin`: the battle group or side has not exceeded the local join budget for this situation.

If any predicate fails, the AI must emit a diagnostic reason such as `local_combat_out_of_scope`, `local_combat_no_reachable_slot`, `local_combat_join_budget_full`, or `local_combat_locked_elsewhere`.

## Engagement Rule Table

| Engagement Rule | Join Local Combat | Ignore Local Combat | Return / Stop Condition |
|---|---|---|---|
| Move-first | Join only if the fight blocks the objective route, threatens the actor or its company, is within 2 cells of the planned corridor, or includes the retained target. | Ignore distant incidental fights that do not affect the planned advance. | Return to objective after 1.5s without local damage/threat, or when the route is clear. |
| Attack-first | Join relevant fights within local perception and command scope, prioritizing open attack slots. | Ignore fights outside leash or impossible to reach soon. | Continue pursuit until target invalid, leash breaks, or command changes. |
| Fire-on-the-move | Join only for quick attacks or support that does not materially reverse objective progress. | Ignore support moves that increase objective route cost without opening an attack soon. | Resume objective after attack/recovery or after support slot no longer leads to an attack. |
| Hold | Join only inside held area or defense leash. | Ignore and do not chase targets outside leash. | Return to held area when hostile leaves leash for 1.5s or no local damage occurs for 1.5s. |
| Protect-hero | Join threats inside the hero protect radius, prioritizing actors attacking or pathing toward the hero. | Ignore fights that pull the corps beyond protect distance unless explicitly commanded. | Return to hero/protect anchor when threat clears. |
| Retreat-first | Join only if a hostile blocks the retreat path or is already attacking the retreating actor. | Ignore ordinary fights. | Continue retreat once path clears. |

## Role Minimums

First slice should not make all units use identical local-combat behavior. If corps class metadata is unavailable in Runtime, use existing combat facts as fallback: melee actors use shield/infantry-like behavior, actors with range greater than 1 use archer-like behavior, and future high-mobility actors may opt into cavalry-like behavior.

Minimum role behavior:

- Shield / infantry: prefer front-line attack slots, chokepoint blocking slots, and support slots directly behind or beside the front line. They should not choose side support that opens the line behind them.
- Archer / ranged: prefer valid firing attack slots within range. If no firing slot is available, hold a support position that preserves distance and does not block front-line movement.
- Cavalry / mobile: join only when a side or rear approach is reachable inside command scope. If the fight is in a chokepoint with no flank route, hold as reserve instead of clogging the front.

These role rules are intentionally minimal. Full class tactics, charge behavior, healing, morale, and formation systems are out of first-slice scope.

## Battle-Group Budget And Locks

Local combat response must preserve the hero-company identity. It must not pull individual visible soldiers away from the company as independent long-term units.

First-slice budget rules:

- One battle group may have at most one active local combat assignment at a time.
- A battle group already assigned to a local fight does not switch to another situation unless the target dies, the command changes, the leash breaks, or the current situation has been inactive for 1.5s.
- A local situation should not attract every nearby group. It should accept joiners up to its open attack slots plus a small support allowance.
- Objective or protect tasks keep at least one active group committed unless an explicit command or direct threat overrides them.

Anti-jitter locks:

- `JoinLocalCombat` decision lock: at least 0.6s or until the next movement boundary completes.
- `ReturnToObjective` decision lock: at least 0.5s unless attacked.
- Slot claim timeout: 0.4s if the actor cannot reserve progress toward the claimed slot.
- De-aggro timer: 1.5s without local damage, attack intent, or close threat before a hold/return decision.

These are starting values. Implementation may tune them, but the names and rationale must stay visible near the constants.

## Refresh And Invalidation

Local tactical facts should be event-driven and lazily rebuilt.

Full local-combat rebuild is triggered by:

- battle start;
- `DamageApplied`;
- target defeat;
- movement completion into or out of threat range;
- command, objective, or engagement-rule change;
- navigation/path failure that changes local movement feasibility;
- new large unit batch, summon, reinforcement, or deployment event;
- cache TTL expiration.

Light dirty marking is triggered by:

- movement started;
- reservation rejection;
- attack recovery completion;
- repeated hold near an active fight.

Recommended first-slice tunables:

```text
BattleTacticalSnapshot refresh: optional in V1; if present, important event or 0.25s TTL
LocalCombatSituation TTL: 0.20s
Local scan radius: 6 cells
Support search depth: 2-3 steps
Max local-combat rebuilds per runtime advance: 4
Max local situations evaluated per actor decision: 2
```

Runtime may adjust these through named constants or configuration, but comments must preserve the tuning rationale.

V1 should prefer simple local lazy queries at actor decision boundaries over building a complex global cache. Add broader tactical indexing only when tests or profiling prove the local query is insufficient.

## Behavior Tree Boundary

LimboAI should select tactical intent, not execute combat truth.

Behavior tree decisions should be shaped like:

```text
Root
-> locked/defeated/no-decision guard
-> explicit command branch
-> target in range: attack or wait for charge
-> local fight that satisfies join predicates: attack slot, support slot, hold support, or return
-> objective movement
-> hold/regroup fallback
```

Behavior tree output is a typed Runtime action request. It may include:

- actor id;
- target actor id;
- local combat situation id;
- desired approach mode;
- fallback reason.

Initial approach modes:

```text
AttackSlotFirst
SupportSlotFirst
HoldSupportPosition
ReturnToObjective
```

Runtime then validates topology, footprint, occupancy, reservations, action locks, attack legality, and event emission.

Behavior tree decisions must also return reason strings for diagnostics, for example:

```text
join_recent_damage
join_blocks_objective_route
join_protect_hero_threat
hold_support_attack_slots_full
return_objective_threat_clear
reject_outside_leash
reject_join_budget_full
reject_no_reachable_slot
```

## Navigation And Slots

Attack slots remain positions where an actor can legally attack the target from its footprint and range.

Support slots are not arbitrary waiting cells. A support slot must:

- be statically legal for the actor footprint;
- not block an occupied attack slot;
- respect current occupancy and reservations for the committed next step;
- improve the actor's path toward a named attack or support role compared with the actor's current position;
- remain inside the actor's command scope, defense leash, or objective allowance;
- be eligible for deterministic reservation so multiple actors do not pile onto the same slot.

Support slots are a tactical fallback when all direct attack slots are occupied, blocked, unsafe, or lower priority than maintaining local formation.

V1 support slots should be limited to visible, explainable staging roles:

- `MeleeQueue`: a second-line slot that can enter an attack slot within 1-2 legal steps when it opens.
- `LineHold`: a slot that prevents the local line from opening behind an engaged ally or at a chokepoint.
- `RangedHold`: a slot for ranged units that keeps firing distance or preserves a future firing lane without blocking melee.

Do not implement generic abstract support scoring in V1.

## First Slice Acceptance

The first implementation slice is acceptable when:

- an enemy with an objective plan can still join an active nearby local fight when its engagement rule allows it;
- open attack slots are preferred over support slots;
- full attack slots do not cause nearby support units to idle if valid support slots exist;
- defensive units stop pursuit outside their leash or accepted objective/hold area;
- multiple units do not reserve the same attack or support position;
- LimboAI and the C# Runtime executor expose the same decision branches through a narrow facade;
- Runtime remains the only authority for movement, reservations, damage, defeat, and emitted battle events;
- diagnostics explain whether a unit joined combat, held support, returned to objective, or failed due to pathing/reservation/leash.

Recommended first-slice scenarios:

- Narrow bridge fight: front line engages, the second melee unit takes an open attack slot, the third unit holds a non-blocking support position.
- Full attack slots: a nearby unit does not idle forever, does not reserve a duplicate destination, and fills a slot after it opens.
- Hold leash: defenders reinforce inside leash, stop chasing outside leash, and return to the held area.
- Move-first column: distant incidental combat does not pull units away, but combat blocking the planned route causes a short local response.
- Protect hero: a threat to the hero pulls a corps unit that satisfies the protect-hero join predicates into local response, then the unit returns after the threat clears.
- Path failure diagnostics: narrow-path failures report occupancy, reservation, leash, or no-route reasons without jitter.

## Non-Goals

- No per-frame behavior tree execution.
- No full battle tactical rebuild every Runtime tick.
- No freeform physics or Godot collision authority for combat.
- No individual-soldier long-term AI state.
- No global best-target scan as the default for every moving unit.
- No complex morale, formation, reinforcement director, or multi-front battle director in this first slice.
- No generic pressure-score tactical director in the first slice.
- No complex local-situation merge/split identity inheritance in the first slice.
