# Battle Runtime Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `hero-led-light-rts-system-architecture.md` and the accepted hero-led light RTS direction.

## Responsibility

Battle Runtime owns live battle truth after Application creates a battle snapshot:

- runtime actors and battle-group roots;
- actor HP, cooldown, current target, command state, temporary effects, and battle-only state;
- actor state-machine phases and state transitions;
- semantic battle events and battle outcome results;
- runtime failure semantics before settlement.

## Does Not Own

Runtime does not own:

- long-term Domain state or campaign writeback;
- Godot scene node truth, animation timing, UI selection, or visual interpolation;
- map authoring, TileMapLayer parsing, or topology compilation;
- final settlement, report text, or save-file schema;
- LimboAI behavior-tree blackboard as authoritative combat state.

## Runtime State

Runtime state exists only during an active battle or recoverable runtime handoff:

| Runtime State | Examples |
|---|---|
| Combat actors | Current HP, mana, cooldown, anchored cell, target, temporary effects. |
| Actor phases | Anchored decision, moving, attack windup/recovery, casting, holding, interrupted, defeated. |
| Battle-group plan execution | Active objective zone, engagement rule, formation intent, plan revision, current battle-group state. |
| Command execution | Current command, accepted runtime order, target area, retreat/protect/follow state, plan supersession state. |
| Local movement steering | Current route hint, route corridor id, steering mode, obstacle-follow side, best distance, and bounded stuck-recovery budget for the active movement intent. |
| Tactical observations | Target object catalog snapshots, global combat zones, temporary local combat situations, tactical fact versions, dirty reasons, and bounded cached slot facts. |
| Battle tactical areas | Group-owned action zones, selected target objects, target regions, temporary regions, engagement state, perception summary, and replan timing. |
| Tactical intent | Enemy active intent plan, selector source, resolved target object, retarget cooldown, leash target, fallback target, and degradation reason. |
| Group route intent | Low-frequency route hints from static route topology, including route profile, next portal or anchor, corridor revision, and invalidation reason. |
| Battle process | Event stream, skill impact, formation density, map-trigger facts received through snapshots. |

Runtime cannot query or mutate Domain state directly. Application snapshots and result contracts isolate the two sides.

## Layered Runtime Ownership

Runtime battle behavior is split into four layers with one-way ownership:

```text
observation facts
-> battle-group commander state
-> actor action state machine
-> runtime validation
```

Observation facts run continuously from authoritative runtime state. They include perception, contact, target object catalog snapshots, global combat-zone candidates, group action-zone snapshots, local slot facts, route-blocking facts, and reachability diagnostics. Observation facts are read-only inputs for decision systems; they do not own command or tactical intent, do not select group objectives by themselves, and do not mutate actor phases.

Battle-group commander state owns plan progression and tactical intent for the hero-led group. It decides whether the group is deploying, advancing, sensing contact, locked to a local fight, moving actors into attack or support slots, attacking, regrouping, returning, retreating, routed, or defeated. It consumes observation facts and commands, then exposes typed actor intents.

Actor action state machines own only per-actor execution phases such as anchored decision, moving, attack recovery, holding, interrupted, and defeated. They do not decide whether the battle group should pursue a target, remain on the objective, or enter local combat.

Runtime validation remains the final authority for topology, footprint, occupancy, reservations, movement commits, attack legality, damage, defeat, and event emission. Validators may reject or degrade a requested action, but they must not silently become a second tactical commander.

Each runtime actor belongs to one battle-group commander state. Multiple visible actors may share that commander state when they are produced from the same hero-led company or command group. Expanded force-count rows, presentation entities, or temporary adapter rows must not create independent commander state unless the accepted battle model says they are separate player-commandable battle groups.


## Battle Tactical Area Runtime State

Runtime stores global combat-zone state separately from group-owned action state.

Combat zones are global battlefield facts computed from all living units, footprints, factions, perception/contact, attacks, damage, and recent defeats. They are not owned by a battle group and must not mutate group intent directly.

Group action zones are commander-group-owned intent facts. They describe where the group is moving, joining, supporting, holding, retreating, or regrouping. Other groups may observe these zones for spacing or tactical response, but only the owning commander state mutates them.

Runtime may maintain global caches of combat-zone and group action-zone snapshots for query, diagnostics, and performance. The caches store immutable or versioned snapshots. They are not global tactical directors and must not update group intent by themselves.

Group engagement state is driven by decoupled facts: perception summaries, damage events, attack events, command changes, and region reachability. Unit-level action logic consumes the group state; it does not own the group state machine.

Non-engaged groups request movement toward their current group action zone, selected target object, or selected target region. Engaged groups request local target, attack-slot, support-slot, queue, flank, regroup, or fallback actions inside a selected combat zone. Runtime remains the final validator for topology, occupancy, reservations, movement, attacks, damage, defeat, events, and outcome.

Group route intent is stored with commander-owned movement state, not as independent actor path authority. A route hint may identify the next static portal, route anchor, gate, chokepoint, or corridor segment for the group. Actors consume that hint through their local movement resolver, and every committed step still validates through Runtime topology, footprint, occupancy, and reservation rules.

Runtime diagnostics must emit complete area snapshots when combat zones are rebuilt and when group action zones are rebuilt. These snapshots include combat-zone bounds, deployment-zone bounds, group action-zone bounds, and living unit positions with footprints and high-level states.

## Battle Space Authority

Live battle uses square-grid anchored realtime combat. The square battle grid remains the runtime spatial authority; this project is not migrating to hexes or freeform physics movement for the accepted battle model.

Runtime owns these combat-space facts:

- actor anchored cell;
- actor reserved next cell, when moving;
- actor state-machine phase;
- actor action timing, impact timing, and action-completion boundary;
- current target actor or target cell;
- attack range, cooldown, and ability execution state;
- emitted movement, attack, damage, cast, interruption, and failure events.

Presentation owns visual interpolation between runtime cells, animation, selection, hover, debug overlays, and feedback. Presentation must not create separate combat truth by moving units into visual attack range independently of runtime facts.

Godot `Area2D` and `CollisionShape2D` may be authored on battle unit scenes for mouse picking, hover, selection, debug, and later visual helpers. They are not authoritative damage resolution in the square-grid realtime model.

## Runtime Tick Rules

Runtime resolution must be deterministic and explainable:

- Decision facts are built from tick-start runtime state, not from positions already mutated by another actor earlier in the same tick.
- A simulation tick advances actor phases, cooldowns, and action timers. It is not, by itself, permission for every actor to choose a new action.
- An actor cannot both move and basic-attack in the same runtime tick.
- Basic attacks resolve only from an anchored runtime position that is in range at decision time.
- Same-tick damage should be resolved as a deterministic batch before same-tick movement mutations that were proposed from the same tick-start facts.
- Actors defeated by same-tick damage cannot then move in that same tick.
- Runtime events must include enough actor, target, command, and spatial context for Presentation, Settlement, Report, and diagnostics to agree on what happened.

## Action Clock

Runtime execution is driven by a fixed simulation cadence plus actor state-machine action boundaries, not by render frames and not by a precomputed full-battle event stream.

- Presentation-backed battles must not simulate the whole battle to completion before playback starts. Runtime should advance in deterministic fixed-time slices that may contain action decisions, movement progress, cell-boundary commits, impact points, attack recoveries, command interrupts, or battle termination.
- Headless tests and reports may advance the same Runtime action clock without Godot playback, but they must use the same phase and action-duration rules as presentation-backed battle.
- `Moving`, `AttackWindup`, `AttackRecovery`, `Casting`, `Holding`, `Interrupted`, and similar non-decision phases persist until their Runtime completion condition is reached. They must not be reset automatically at the start of each simulation tick.
- Fixed simulation ticks advance the central battle clock at a stable cadence. Actor cooldowns and action locks decide whether an actor can choose another action; the outer battle loop does not wait for Presentation animation tasks or jump directly to the next actor-ready timestamp.
- Movement is a continuing actor state. A moving actor may accumulate progress across multiple fixed ticks before crossing to a neighboring cell, and may continue into the next neighbor when runtime intent, topology, occupancy, and reservations still allow it.
- Basic attacks emit at most one damage application per attack action, at the Runtime-defined impact or completion point. Attack speed and recovery determine when the actor can enter a later `AnchoredDecision`; they do not authorize repeated damage on consecutive ticks while the same attack animation is still being presented.
- Presentation may acknowledge that a movement or attack animation has finished playing, but that acknowledgement only advances a Runtime-owned action boundary. Presentation does not create damage, movement, target choice, or pathfinding truth.

## Action And Effect Execution

Runtime distinguishes action execution from effect execution.

Action execution owns actor time and locks:

- movement progress;
- basic attack windup, impact, and recovery;
- skill cast, impact, and recovery;
- interruption, failure, and return to decision boundaries.

Effect execution owns battle-state mutation caused by an action or other source:

- damage, healing, shield, status, control, movement, morale, summon, resource, or future effect primitives;
- target validity at application time;
- emitted effect-result events for Presentation, Report, Settlement, and diagnostics.

Skills, basic attacks, equipment, relics, terrain, city support, and passive triggers should all resolve through the same effect execution layer once their source has produced an effect payload. The effect executor must not decide tactical intent, target acquisition, pathing, or command ownership.

Default active-skill interruption rules are:

- A targeted or non-targeted active skill may interrupt a basic attack before the basic attack's damage impact.
- After a basic attack has applied damage, its recovery cannot be canceled by default.
- An active skill cannot interrupt another active skill by default.
- Canceling basic attack recovery, interrupting a skill, instant release, and fire-and-forget or offhand release require explicit interrupt traits.

Targeted skills check range and lock target identity when Runtime accepts the command. Default skill range uses footprint-aware Manhattan distance between caster and target footprints, producing a diamond-shaped range on the square grid. Execution prechecks must still confirm that the caster and locked target are alive, valid, and targetable. A locked target moving out of range after acceptance does not invalidate the skill; a dead or invalid target makes the skill fail without release.

### Runtime Spatial Skill State

Temporary spatial marks and channeled skill windows are Runtime battle state. They must not be stored in Presentation nodes, Godot collision callbacks, or long-term Domain state.

Runtime owns:

- mark creation, replacement, expiration, owner, source command, and source definition;
- whether a mark resolves from a ground anchor or from an attached live actor;
- legal teleport destination validation around the selected resolved mark anchor;
- actor anchor mutation, occupancy updates, and emitted movement/teleport events;
- channeled damage timing, tick cadence, target selection inside the channel area, and interruption/failure semantics.

Teleport is not ordinary pathfinding. It may move an actor across non-adjacent cells only through an accepted teleport effect primitive, and the final anchor must still pass topology, footprint, occupancy, and same-tick reservation validation. If no legal destination exists near the mark, Runtime must reject or fail the command with a reportable reason instead of letting Presentation place the actor visually.

Thunder Mark Fold commands select one live mark before they select a landing anchor. Runtime must not reinterpret a fold command as "use any live mark." Acceptance locks the requested landing anchor and verifies that the selected mark still belongs to the caster's battle group, still resolves to a live ground or attached-actor anchor, and that the requested landing anchor is inside the content-defined landing radius. Release-time execution repeats these validations before committing displacement, because marks can expire, attached targets can die, and landing cells can become occupied after command acceptance.

Any active or passive displacement that changes an actor anchor outside ordinary neighbor movement must pass through a shared Runtime displacement commit boundary. This boundary owns the final anchor write, clears old movement segment state, reservations, movement intent snapshots, local steering and backtrack memory, and target or local-combat context that was derived from the previous anchor. After displacement, the actor resumes the normal Runtime decision flow from the new anchor unless an active skill lock or queued command explicitly keeps owning the actor. Tactical observations, combat-zone facts, local-combat facts, and movement continuation must be rebuilt from the post-displacement position instead of reusing stale pre-displacement state.

Presentation may snap or animate an emitted displacement event, but it must not decide the post-displacement target, movement continuation, perception result, or combat state.

Spatial transfer of units or skill events is a later extension. It may only redirect Runtime-owned events such as cast impact areas or actor anchors; it must not reinterpret Presentation-only projectile paths as combat truth.

## Actor State Machine

Every living runtime actor has an explicit phase. Movement cannot be represented only by a transient field set and cleared inside one action method.

Decision boundaries include:

- battle start;
- movement completion;
- command change;
- target cell change;
- target defeat;
- reservation rejection;
- path invalidation;
- interruption, retreat, hold, or reacquire transitions.

At an anchored decision boundary, Runtime may ask AI or command logic for intent, then validates movement, attack, cast, hold, retreat, or failure through runtime rules. Cached movement data is non-authoritative and must be invalidated when command, target, actor anchor, target anchor, topology version, route topology version, route profile, route corridor id, dynamic occupancy revision, or movement intent revision changes.

Actors that are not in an anchored decision phase do not start another basic attack. Moving actors may continue their existing movement intent through fixed-tick progress and may refresh the next legal neighbor only at Runtime movement-continuation boundaries. Attack recovery, casting, holding, interrupted, and defeated phases continue until they complete, fail, or are explicitly interrupted by a valid command or runtime rule.

Target acquisition is not the same as movement continuation. Default assault AI follows the active battle-group plan and engagement rule first, then uses local perception to acquire or retain targets inside that scope. Runtime fully scores attack opportunities when acquiring a target, when the retained target is gone or invalid, when an immediate attack opportunity is already in range, or when the engagement rule explicitly allows reacquisition. While marching toward a live retained target or objective, Runtime keeps target ownership sticky and validates only the next movement boundary; it does not rebuild whole-region navigation data for every enemy on every movement step.

Local steering is part of actor movement execution, not a separate commander. A movement intent may carry steering memory so a unit can follow a short static obstacle edge without jittering left and right every boundary. The steering state must reset when command scope, movement intent, objective, retained target, route hint, route corridor, route profile, or topology authority changes. It must also stop when local combat, attack range, skill cast, defeat, or an explicit interrupt takes over the actor phase.

## Battle-Group State Machine

Actor phases remain the low-level action truth. A battle group has its own commander state that expresses plan execution across the hero and corps:

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

The battle-group state machine owns command-scoped intent, not individual cell legality. Actor state machines still validate movement, attack, cast, interruption, recovery, defeated, and action completion.

Battle-group commander state is stored once per battle group, not on each actor as authoritative state. Actors may expose derived or cached state for diagnostics, but reports, commands, tactical regions, and local-combat ownership read the group-owned state.

State transitions are driven by:

- accepted `BattleGroupPlan` values;
- later hero, corps, or combined commands;
- local perception and target validity;
- engagement rule policy;
- objective-zone reachability;
- runtime movement, attack, reservation, and path failure facts.

Engagement rules bias transitions:

| Engagement Rule | Runtime Bias |
|---|---|
| Fire-on-the-move | `AdvancingToObjective` may pause at movement boundaries for local attacks, then resume objective advance. |
| Move-first | Distant or incidental targets are ignored unless they block movement, threaten the hero, or enter immediate range. |
| Attack-first | `SensingContact` promotes to `TargetLocked` earlier and accepts pursuit inside the plan scope. |
| Hold | Pursuit is capped by the held area; target loss returns to hold instead of objective advance. |
| Retreat-first | Survival or morale triggers supersede attack and objective states with retreat. |
| Protect-hero | Target choice and corps movement are constrained by hero distance and threats to the hero. |

Runtime events must include plan state changes when they materially affect movement, target choice, retreat, hold, or battle outcome. Reports should be able to say that a company advanced as planned, was delayed by a chokepoint, switched to attack-first contact, returned to objective, or retreated.

Plan-state logging must stay low-noise. Log meaningful group-state transitions, command supersession, target lock changes, local-combat entry or exit, regroup, retreat, defeat, and important degradation reasons. Do not log every fixed tick or every movement progress update as a state transition.

## Combat Zones And Local Combat Situations

Runtime may build temporary `LocalCombatSituation` facts from authoritative battle state. When those facts drive engaged movement or target choice, they are scoped to a global combat zone selected by the commander group, not to a globally commanding fight authority. These facts help AI reason about nearby active fights without giving behavior trees movement, damage, or settlement authority.

A local combat situation records:

- selected combat-zone id, owner battle-group id for the consuming commander, stable situation id, center cell, and bounded region extent;
- participating actors and nearby actors that satisfy join predicates inside the group-owned local combat region;
- hostile anchors and retained target facts;
- open attack slots and occupied attack slots;
- support slots that help join or pressure the local fight;
- simple local imbalance facts, such as open attack-slot count, occupied attack-slot count, nearby friendly count, nearby hostile count, route-blocking status, and command-scope or leash boundaries;
- version, dirty reason, and last built Runtime time.

Combat zones are refreshed by contact/engagement changes and bounded periodic rebuilds, not by global full recomputation every simulation tick. Local combat situations are refreshed by important Runtime events and lazy rebuilds inside a selected combat zone. Movement, recovery, casting, defeated, and other locked phases do not run behavior-tree decisions merely because the tactical cache changed.

Runtime remains the only owner of actor anchors, reservations, movement progress, attack legality, damage application, defeat, events, and battle outcome. `LocalCombatSituation` facts are advisory inputs for action selection and diagnostics and must remain keyed by owning battle-group id when cached globally.

Runtime local combat response must preserve battle-group identity:

- a battle group has at most one active combat-zone assignment at a time;
- a battle group does not switch combat zones or local situations until target death, command change, leash break, zone merge/split, or local inactivity;
- objective, hold, and protect tasks retain budget unless a direct threat or explicit command overrides them;
- actor decisions use anti-jitter locks for join, return, slot claim, and de-aggro transitions.

Runtime diagnostics must include local-combat decision reasons so AI behavior can be explained without reading behavior-tree internals.

First-slice local-combat movement must not build runtime flow fields on combat hot paths. The battle-group state machine chooses the current intent, target, attack slot, support slot, queue, hold, or return state. The movement resolver then checks only nearby legal anchors and picks a step that reduces distance to the selected target or slot, respects topology, occupancy, and reservations, and avoids obvious immediate backtracking. If no executable local step exists, the actor degrades to queue, support, hold pressure, or a named failure reason instead of constructing a whole-region field.

## Continuous Movement

Continuous movement keeps square-grid cells as combat truth while avoiding one-complete-action-per-cell presentation cadence.

- Runtime stores each moving actor's committed anchor, intended next anchor, movement progress, speed-derived step duration, and movement intent revision.
- A fixed simulation tick advances movement progress by `delta / MoveStepSeconds`.
- When progress reaches the next anchor, Runtime commits the actor to that anchor, emits a movement event, updates occupancy/reservation facts, and either continues toward the next valid neighbor or returns to an anchored decision/hold/attack boundary.
- Runtime may use objective-zone facts, target-specific attack slots, coarse route hints from static route topology, local steering memory, local reservations, and command facts to choose the next neighbor at each continuation boundary. First-slice Runtime movement uses group-scoped route hints plus local neighbor scoring and stateful local steering rather than runtime flow-field construction. Presentation frames never choose movement targets.
- A moving actor cannot basic-attack until Runtime reaches an anchored attack decision. Attack opportunity may stop continuation at the next legal anchor, then the attack action owns the next phase.
- If continuation fails because of occupancy, reservation, topology, target death, command change, route-hint invalidation, steering stuck recovery, or path invalidation, Runtime emits a diagnostic event or failure reason and transitions through the appropriate anchored, queue, hold, support, or interruption phase.
- Presentation treats movement as a sustained state and keeps move animation active until Runtime stops movement, starts another action, marks defeat, or battle presentation finishes.

Static map-scale route planning is not actor-local. When a group needs to cross large static barriers such as rivers, long walls, gates, or chokepoints, Runtime should request or reuse a group route hint from immutable route topology. Individual actors then locally advance toward the current route anchor. Dynamic living-unit blockers do not rebuild the static route graph; they degrade to queue, support, hold pressure, retry, or a named failure reason.

## Attack Rules

- Basic attacks target actors, not damage cells.
- Basic attacks resolve from attacker anchored footprint to target anchored footprint using the ability's grid range.
- A valid basic-attack opportunity is any legal attacker anchor whose footprint is within range of the target footprint and does not overlap it.
- Larger targets naturally expose more valid attack opportunities. Multiple smaller units may attack the same larger unit from different legal footprint anchors when terrain, occupancy, and reservations allow it.
- Runtime emits attack and damage events from the same authority used by settlement and reports.
- Ranged attacks may use longer grid range, but still resolve as actor-target attacks in the first implementation.

## Footprint Runtime Rules

- Each actor may define a rectangular footprint width and height in cells.
- The actor anchor is always the top-left cell of that footprint.
- `1x1`, `1x2`, `2x1`, `2x2`, and `3x3` are valid initial footprint targets.
- The anchor is the stored runtime position, not a shortcut for movement or attack legality. Runtime expands the anchor into covered cells whenever it validates placement, occupancy, reservations, attack range, or area overlap.
- Occupancy, reservations, attack range, and area-effect overlap use covered footprint cells.
- Attack range is measured by shortest square-grid distance between actor footprints.
- Area effects hit an actor when any covered footprint cell overlaps the resolved effect area.
- Snapshot contracts carry footprint width and height into Runtime. Runtime clamps the supported footprint range and does not load Godot resources.

Detailed path legality, topology, and movement planning rules live in `battle-navigation-topology-architecture.md`.

## Inputs

- `BattleStartSnapshot`
- `BattleGroupSnapshot`
- accepted `BattleGroupPlan` values
- `LocationBattleContext`
- compiled `BattleNavigationTopology`
- accepted `RuntimeOrder`
- content definition snapshots needed by runtime abilities and effects

## Outputs

- `BattleEventStream`
- `BattleOutcomeResult`
- runtime diagnostics for command, movement, attack, defeat, and failure causes

## Failure Rules

- No battle may partially write long-term state before Settlement.
- Player retreat, battle interruption, runtime exception, and normal result are separate termination reasons.
- Recoverable interruption restores to a consistent Runtime state or returns to the pre-settlement handoff.
- Unrecoverable exception cannot fabricate victory or defeat.
- Failed or incomplete runtime output must enter explicit safe rollback, failed handoff, or pending manual-resolution state.
- Settlement accepts only complete results with consistent event boundaries and explicit termination reason.

If later user-facing mid-battle save is added, it must persist the battle snapshot, necessary runtime state, and confirmed event stream boundary.

## Acceptance

This architecture is acceptable when:

- Runtime owns live combat truth without writing campaign state;
- Presentation can replay and visualize runtime facts without inventing alternate combat truth;
- actors have explicit state-machine phases and decision boundaries;
- presentation-backed runtime does not precompute the full battle outcome before playback consumes action events;
- presentation-backed runtime uses a stable simulation cadence rather than a presentation-driven wait between actor action boundaries;
- movement, attack, recovery, and cast phases persist across ticks until their Runtime completion boundary;
- basic attacks produce no more than one damage application per attack action;
- static route hints are group-scoped advisory facts and cannot authorize movement that fails Runtime topology, footprint, occupancy, or reservation validation;
- movement, attack, damage, defeat, interruption, and failure are emitted as semantic events;
- settlement and reports can derive their facts from Runtime outputs without recomputing battle truth.
