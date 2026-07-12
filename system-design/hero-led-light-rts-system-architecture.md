# Hero-Led Light RTS System Architecture

Status: Accepted Architecture Index

## Gameplay Authority

This index implements `gameplay-design/content-systems-long-term-design.md` for the hero-led light RTS battle direction.

The accepted battle loop is:

```text
prepare heroes, corps, equipment, professions, cities, and resources
-> enter a full authored battle map
-> command heroes and their troops separately at medium frequency
-> resolve real-time combat through automatic unit behavior and player commands
-> read a battle report that explains why the result happened
-> write consequences back to cities, resources, corps, and campaign state
```

The core architecture term is:

```text
battle group = 1 hero + 1 main corps
```

Runtime-visible force counts may create multiple combat actors for presentation, collision, attack, or damage resolution, but they do not by themselves create separate battle-group commander state. The selectable battle group or accepted battle-group command identity remains the commander boundary. If a compatibility adapter expands old force-count data, it must preserve the owning battle-group identity instead of letting each expanded row become an independent tactical commander.

Chinese design language uses **战斗编组**. Code-facing English should use `BattleGroup` unless a later confirmed discussion updates the naming authority.

## Responsibility

This file is the battle-system routing document. It owns only stable, cross-cutting identity and document routing.

It does not own detailed runtime, navigation, command, AI, settlement, ability, or progression rules. Those rules live in focused system-design documents listed below.

## Experience Invariants

Future system documents and implementation slices must not silently break these invariants:

| Invariant | Architecture Meaning |
|---|---|
| Hero-led battle group | The selectable combat identity is a battle group: 1 hero + 1 main corps. |
| Separate command authority | The player can issue hero commands, corps commands, and combined commands. |
| Medium-frequency command | The game must not become high-frequency single-soldier micro. |
| Deployment before start, beacon commands during battle | Each participating battle group carries deployment and formation facts before start. Runtime destination beacons, issued while battle is running or paused, own the current player movement objective. |
| Automatic local behavior | Soldiers and corps members act automatically inside player intent. |
| Runtime group identity | A runtime actor belongs to exactly one battle-group commander state; actor count, footprint, or presentation entity count must not fragment command ownership. |
| Runtime does not own campaign truth | Battle runtime consumes snapshots and emits events/results; settlement writes long-term state. |
| Reports explain causes | Battle reports explain command, build, resource, skill, equipment, terrain, and city factors. |
| Resource consequences matter | Corps loss, recovery cost, training, equipment upgrades, and city support feed each other. |

Non-breaking rules:

- Do not degrade combat into pure deployment and auto-playback.
- Do not revive grid tactical chess, AP, or turn-action growth as the future battle identity.
- Do not make visible soldiers long-term independent units.
- Do not let UI, Runtime, Report, and Settlement compute separate versions of the same truth.

## Focused Authority Documents

Use progressive disclosure. Read the smallest authority document that matches the task.

| Task Area | Authority Document |
|---|---|
| Battle runtime ownership, actor phases, events, runtime persistence, presentation authority boundary | `battle-runtime-architecture.md` |
| Battle map topology compilation, pathfinding, footprints, occupancy, reservations, path failure diagnostics | `battle-navigation-topology-architecture.md` |
| Hero/corps/combined command lifecycle, validation, runtime order events | `battle-command-architecture.md` |
| Tactical autonomy, player intent precedence, LimboAI behavior-tree boundary | `battle-ai-boundary-architecture.md` |
| Battle target objects, tactical intent plans, target selectors, side-neutral player/enemy intent, and tactical capability boundaries | `battle-tactical-intent-architecture.md` |
| Battle-group-owned target objects/regions, temporary regions, local combat regions, enemy intent consumption, and player-command separation | `battle-group-tactical-region-architecture.md` |
| Snapshot/result contracts, settlement, report attribution, recovery, rollback/failure semantics | `battle-result-settlement-architecture.md` |
| Ability/effect definitions, combat content resourceization, resource and progression loops | `battle-content-progression-architecture.md` |
| Strategic Management to battle session bridge, battle preparation draft ownership, snapshot compilation, scene handoff, and strategic result summary writeback | `strategic-battle-bridge-architecture.md` |

`presentation-ui-layout-architecture.md`, `semantic-map-marker-architecture.md`, and `scene-transition-router-architecture.md` remain separate accepted architecture documents.

## Horizontal Business Systems

| System | Responsibility | Does Not Own |
|---|---|---|
| Hero | Long-term hero identity, base attributes, profession growth, skills, equipment slots, availability. | Corps growth, city resources, battle map behavior, final battle report generation. |
| Corps | Corps role, fantasy form, tags, level, equipment level, `CorpsStrength`, corps growth. | Individual soldier long-term state, hero attributes, city facility ownership. |
| Battle Group | Binds 1 hero and 1 main corps; owns stationing, sortie state, battle selection identity, result ownership. | Hero/corps definitions, battle simulation, direct city resource mutation. |
| City / Strategic Location | Owns large-map locations, control, garrison relations, resources, facility capacity, defense context, and lightweight non-city states. | Hero growth, internal corps growth, live battle behavior. |
| Equipment And Armament | Owns hero equipment, token/command items, corps equipment level, equipment origin, build contribution. | Hero base attributes, battle skill execution, random-affix loot as a main loop. |
| Combat Runtime | Runs the real-time battle from snapshots and emits semantic events/results. | Long-term campaign state, city/hero/corps writeback, final settlement. |
| Command | Defines hero, corps, and combined command contracts; validates player intent; converts intent into runtime orders. | UI selection state, pathfinding execution, long-term state mutation. |
| Report And Settlement | Consumes runtime events/results; creates state deltas and player-readable battle reports. | Simulating combat, redefining content, recomputing runtime facts independently. |
| Content Definition | Provides static definitions for heroes, corps, equipment, abilities, tags, locations, maps, rewards, and text. | Player progress, runtime actors, UI layout state. |

Non-city strategic locations share a strategic-location interface but must not inherit the complete city state model. Resource sites, gates, ruins, dungeons, and opportunities keep only the state their gameplay needs.

## Vertical Technical Layers

| Layer | Responsibility | Constraint |
|---|---|---|
| Definitions / Content | Static definitions, Godot resources, resource references, tags, text keys, growth templates, battle map entry data. | No player progress or runtime state. |
| Domain / State | Long-term campaign authority and core invariants for heroes, corps, battle groups, cities, resources, equipment, and location control. | No Godot scene nodes, animation state, runtime target locks, frame cooldown state, or path data. |
| Application / Services | Use-case orchestration: create battle groups, station, sortie, build snapshots, validate command entry, settle results, generate reports. | No UI authority. Long-term writes happen through explicit services. |
| Runtime / StateMachine | Battle and scene runtime state machines; consumes snapshots and emits semantic events/results. | Does not directly mutate Domain. Runtime state is discardable unless a future accepted runtime-resume boundary says otherwise. |
| Presentation / UI | Displays heroes, corps, battle groups, cities, commands, HUD feedback, reports, and settlement results. | UI sends commands and requests. It does not own rules, settlement, pathfinding, or campaign truth. |
| Infrastructure | Resource loading, logging, IDs, random seed, time, scene switching, diagnostics, test fixtures. | No business rules. It supports systems without deciding gameplay outcomes. |

## Key Flows

### Create, Station, And Sortie Battle Group

```text
Content definitions
-> Application validates hero, corps, location, resource, and binding rules
-> Domain creates or updates BattleGroupState
-> City / Strategic Location records garrison or sortie relation
-> Application locks relevant long-term state when battle handoff begins
```

### Enter Battle

```text
Strategic Management command creates battle intent
-> Strategic Battle Bridge opens a battle-preparation session
-> Battle preparation records deployment, reserve, and current-battle formation facts for participating battle groups
-> Bridge validates launch readiness and builds BattleStartSnapshot
-> SceneTransitionRouter carries bridge session handoff and enters the battle scene
-> Runtime creates battle actors and tactical state
-> UI displays battlefield, selected battle groups, destination beacons, and available commands
```

### Player Command

```text
UI creates CommandRequest, such as a shared destination beacon for selected battle groups
-> Application validates ownership and channel
-> Runtime accepts or rejects order in battle context
-> Runtime executes, interrupts, completes, or fails order
-> EventStream records meaningful command facts
```

### Resolve Battle

```text
Runtime emits EventStream + BattleOutcomeResult
-> Settlement builds SettlementPlan
-> Report builds BattleReportRecord from the same facts
-> Strategic Battle Bridge builds StrategicBattleResultSummary
-> Strategic Management applies consequences through commands
-> UI shows settled result and explanation
```

## Current Authority And Legacy Isolation

| Principle | Rule |
|---|---|
| Current architecture | New work follows hero-led light RTS, battle groups, Strategic Management, and reportable settlement as the running-game authority. |
| Retired concepts stay outside core contracts | AP, TurnSystem, old auto-battle, and old unit-count force models cannot become Domain or Runtime foundations. |
| Boundary adapters are explicit | Retained old data may be converted into current snapshots or states only at a named compatibility boundary. |
| Adapters must be removable | Compatibility code stays outside the core architecture, owns no new gameplay facts, and remains removable. |
| No double authority | One runtime responsibility has one target implementation. Old and new paths must not compete as equal authorities. |
| Contracts govern presentation | Definition, Domain, Snapshot, Result, Event, and Settlement contracts remain authoritative over UI and scene presentation. |
| Explicit failure | Missing mappings or invalid old data fail clearly with diagnostics instead of hidden fallback. |

## Acceptance

This index is acceptable when:

- future work can identify the smallest focused authority document for the task;
- detailed battle rules are no longer buried in this top-level file;
- Runtime, navigation, command, AI, settlement, and content/progression ownership are routed to separate documents;
- any retained old implementation is constrained as compatibility input or an adapter, not an alternative gameplay authority.
