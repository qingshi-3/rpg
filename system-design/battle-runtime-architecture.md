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
- final settlement, report text, or long-term state schema;
- LimboAI behavior-tree blackboard as authoritative combat state.

## Runtime State

Runtime state exists only during an active battle or recoverable runtime handoff:

| Runtime State | Examples |
|---|---|
| Combat actors | Current HP, mana, cooldown, anchored cell, target, temporary effects. |
| Actor phases | Anchored decision, moving, attack windup/recovery, casting, holding, interrupted, defeated. |
| Command execution | Current command, accepted runtime order, target area, retreat/protect/follow state. |
| Battle process | Event stream, skill impact, formation density, map-trigger facts received through snapshots. |

Runtime cannot query or mutate Domain state directly. Application snapshots and result contracts isolate the two sides.

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

Runtime execution is driven by actor state-machine action boundaries, not by render frames and not by a precomputed full-battle event stream.

- Presentation-backed battles must not simulate the whole battle to completion before playback starts. Runtime should advance in deterministic slices bounded by action decisions, impact points, movement completions, attack recoveries, command interrupts, or battle termination.
- Headless tests and reports may advance the same Runtime action clock without Godot playback, but they must use the same phase and action-duration rules as presentation-backed battle.
- `Moving`, `AttackWindup`, `AttackRecovery`, `Casting`, `Holding`, `Interrupted`, and similar non-decision phases persist until their Runtime completion condition is reached. They must not be reset automatically at the start of each simulation tick.
- Basic attacks emit at most one damage application per attack action, at the Runtime-defined impact or completion point. Attack speed and recovery determine when the actor can enter a later `AnchoredDecision`; they do not authorize repeated damage on consecutive ticks while the same attack animation is still being presented.
- Presentation may acknowledge that a movement or attack animation has finished playing, but that acknowledgement only advances a Runtime-owned action boundary. Presentation does not create damage, movement, target choice, or pathfinding truth.

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

At an anchored decision boundary, Runtime may ask AI or command logic for intent, then validates movement, attack, cast, hold, retreat, or failure through runtime rules. Cached movement data is non-authoritative and must be invalidated when command, target, actor anchor, target anchor, topology version, dynamic occupancy revision, or movement intent revision changes.

Actors that are not in an anchored decision phase do not replan movement, reacquire targets, or start another basic attack. Their existing phase is advanced until it completes, fails, or is explicitly interrupted by a valid command or runtime rule.

## Attack Rules

- Basic attacks target actors, not damage cells.
- Basic attacks resolve from attacker anchored footprint to target anchored footprint using the ability's grid range.
- Runtime emits attack and damage events from the same authority used by settlement and reports.
- Ranged attacks may use longer grid range, but still resolve as actor-target attacks in the first implementation.

## Footprint Runtime Rules

- Each actor may define a rectangular footprint width and height in cells.
- The actor anchor is always the top-left cell of that footprint.
- `1x1`, `1x2`, `2x1`, `2x2`, and `3x3` are valid initial footprint targets.
- Occupancy, reservations, attack range, and area-effect overlap use covered footprint cells.
- Attack range is measured by shortest square-grid distance between actor footprints.
- Area effects hit an actor when any covered footprint cell overlaps the resolved effect area.
- Snapshot contracts carry footprint width and height into Runtime. Runtime clamps the supported footprint range and does not load Godot resources.

Detailed path legality, topology, and movement planning rules live in `battle-navigation-topology-architecture.md`.

## Inputs

- `BattleStartSnapshot`
- `BattleGroupSnapshot`
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

If later user-facing battle resume is added, it must preserve the battle snapshot, necessary runtime state, and confirmed event stream boundary through a separate accepted architecture.

## Acceptance

This architecture is acceptable when:

- Runtime owns live combat truth without writing campaign state;
- Presentation can replay and visualize runtime facts without inventing alternate combat truth;
- actors have explicit state-machine phases and decision boundaries;
- presentation-backed runtime does not precompute the full battle outcome before playback consumes action events;
- movement, attack, recovery, and cast phases persist across ticks until their Runtime completion boundary;
- basic attacks produce no more than one damage application per attack action;
- movement, attack, damage, defeat, interruption, and failure are emitted as semantic events;
- settlement and reports can derive their facts from Runtime outputs without recomputing battle truth.
