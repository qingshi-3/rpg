# Hero-Led Light RTS System Architecture

Status: Accepted Architecture

## Gameplay Authority

This document implements `gameplay-design/content-systems-long-term-design.md`.

The accepted direction is hero-led light RTS with strategic city and location management:

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

Chinese design language uses **战斗编组**. Code-facing English should use `BattleGroup` after this proposal is accepted, unless a later accepted proposal changes the naming rule.

## Design Review Lenses

This draft was reviewed against these mature game design and architecture lenses:

- MDA: player experience must drive dynamics and mechanics.
- The Door Problem: simple features must expose state, UI, AI, persistence, and failure boundaries.
- Machinations: resource, loss, recovery, and progression loops must be modelable before code.
- Game Programming Patterns: Command, State, Event Queue, Type Object, Component, and Dirty Flag are references, not mechanical requirements.
- Game AI Pro: automatic unit behavior and tactical decisions need explicit ownership and report attribution.
- Resource-based authoring: Godot `Resource` definitions should hold content; runtime code interprets snapshots and execution state.
- Ability/effect separation: skills, costs, cooldowns, targeting, tags, modifiers, and effects need clear content/runtime boundaries.

## Responsibility

This architecture defines:

- horizontal business system boundaries;
- vertical technical layer boundaries;
- the battle group as the bridge between strategic management and live combat;
- command lifecycle and tactical autonomy rules;
- battle runtime, event, settlement, and report contracts;
- ability/effect resourceization;
- city resource, corps progression, loss, and recovery flow;
- migration principles away from old battle and garrison concepts.

## Does Not Own

This document does not define:

- detailed balance values;
- final UI layouts;
- exact scene tree structures;
- concrete C# class implementation;
- individual skill content;
- full enemy AI implementation;
- complete save-file schema;
- old manual tactical chess or AP/TurnSystem continuation.

## Experience Invariants

These invariants protect the player experience. Future system documents and implementation slices must not silently break them.

| Invariant | Architecture Meaning |
|---|---|
| Hero-led battle group | The selectable combat identity is a battle group: 1 hero + 1 main corps. |
| Separate command authority | The player can issue hero commands, corps commands, and combined commands. |
| Medium-frequency command | The game must not become high-frequency single-soldier micro. |
| Automatic local behavior | Soldiers and corps members act automatically inside player intent. |
| Runtime does not own campaign truth | Battle runtime consumes snapshots and emits events/results; settlement writes long-term state. |
| Reports explain causes | Battle reports explain command, build, resource, skill, equipment, terrain, and city factors. |
| Resource consequences matter | Corps loss, recovery cost, training, equipment upgrades, and city support feed each other. |

Non-breaking rules:

- Do not degrade combat into pure deployment and auto-playback.
- Do not revive grid tactical chess, AP, or turn-action growth as the future battle identity.
- Do not make visible soldiers long-term independent units.
- Do not let UI, Runtime, Report, and Settlement compute separate versions of the same truth.

## Horizontal Business Systems

| System | Responsibility | Does Not Own | Core Contracts |
|---|---|---|---|
| Hero | Long-term hero identity, base attributes, profession growth, skills, equipment slots, availability. | Corps growth, city resources, battle map behavior, final battle report generation. | `HeroDefinition`, `HeroState`, `HeroBattleSnapshot`, `HeroBattleResult`. |
| Corps | Corps role, fantasy form, tags, level, equipment level, `CorpsStrength`, corps growth. | Individual soldier long-term state, hero attributes, city facility ownership. | `CorpsDefinition`, `CorpsState`, `CorpsBattleSnapshot`, `CorpsBattleResult`. |
| Battle Group | Binds 1 hero and 1 main corps; owns stationing, sortie state, battle selection identity, result ownership. | Hero/corps definitions, battle simulation, direct city resource mutation. | `BattleGroupState`, `BattleGroupSnapshot`, `BattleGroupAssignment`, `BattleGroupResult`. |
| City / Strategic Location | Owns large-map locations, control, garrison relations, resources, facility capacity, defense context, and lightweight non-city states. | Hero growth, internal corps growth, live battle behavior. | `StrategicLocationDefinition`, `StrategicLocationState`, `CityState`, `GarrisonRoster`, `LocationBattleContext`, `ResourceDelta`. |
| Equipment And Armament | Owns hero equipment, token/command items, corps equipment level, equipment origin, build contribution. | Hero base attributes, battle skill execution, random-affix loot as a main loop. | `EquipmentDefinition`, `EquipmentInstance`, `EquipmentAssignment`, `BuildContributionSnapshot`, `CorpsArmamentState`. |
| Combat Runtime | Runs the real-time battle: hero actor, corps group, position, HP, mana, cooldown, command execution, targets, temporary effects, event stream. | Long-term save state, city/hero/corps writeback, final settlement. | `BattleStartSnapshot`, `BattleRuntimeState`, `BattleEventStream`, `BattleOutcomeResult`. |
| Command | Defines hero, corps, and combined command contracts; validates player intent; converts intent into runtime orders. | UI selection state, pathfinding execution, long-term state mutation. | `CommandRequest`, `CommandValidationResult`, `RuntimeOrder`, `CommandEvent`. |
| Report And Settlement | Consumes runtime events/results; creates state deltas and player-readable battle reports. | Simulating combat, redefining content, recomputing runtime facts independently. | `BattleOutcomeResult`, `SettlementPlan`, `StateDeltaSet`, `BattleReportRecord`. |
| Content Definition | Provides static definitions for heroes, corps, equipment, abilities, tags, locations, facilities, maps, rewards, and text. | Player progress, runtime actors, UI layout state. | `DefinitionId`, `ContentBundle`, `DefinitionRegistry`, `ContentValidationReport`. |

Non-city strategic locations share a strategic-location interface but must not inherit the complete city state model. Resource sites, gates, ruins, dungeons, and opportunities keep only the state their gameplay needs.

## Vertical Technical Layers

| Layer | Responsibility | Constraints |
|---|---|---|
| Definitions / Content | Static definitions, Godot resources, resource references, tags, text keys, growth templates, battle map entry data. | No player progress or runtime state. Definitions are referenced by ID and must be validatable. |
| Domain / State | Long-term save authority and core invariants for heroes, corps, battle groups, cities, resources, equipment, and location control. | No Godot scene nodes, animation state, runtime target locks, frame cooldown state, or path data. |
| Application / Services | Use-case orchestration: create battle groups, station, sortie, build snapshots, validate command entry, settle results, generate reports. | No UI authority. No old implementation concepts may leak into target contracts. Long-term writes happen here through explicit services. |
| Runtime / StateMachine | Battle and scene runtime state machines; consumes snapshots and emits semantic events/results. | Does not directly mutate Domain. Runtime state is discardable unless explicitly persisted through a safe runtime save boundary. |
| Presentation / UI | Displays heroes, corps, battle groups, cities, commands, HUD feedback, reports, and settlement results. | UI sends commands and requests. It does not own rules, settlement, or campaign truth. |
| Infrastructure | Save/load, resource loading, logging, IDs, random seed, time, scene switching, diagnostics, test fixtures. | No business rules. It supports systems without deciding gameplay outcomes. |

## Responsibility Matrix

| Horizontal System | Definitions / Content | Domain / State | Application / Services | Runtime / StateMachine | Presentation / UI | Infrastructure |
|---|---|---|---|---|---|---|
| Hero | Hero templates, skill pools, growth refs. | Owned heroes, growth, equipment slots, availability. | Queries, sortie checks, snapshot assembly. | Hero actor input. | Hero panel, skill buttons. | Definition load, save. |
| Corps | Combat class, form, tags, growth templates. | Level, equipment level, strength. | Training, recovery, snapshot assembly. | Corps group behavior input. | Corps state display. | Definition load, save. |
| Battle Group | Binding rules, aptitude refs. | Hero+corps binding, station/sortie state. | Create, station, sortie, lock, release. | Runtime battle-group root. | Battle-group selection and status. | ID, save. |
| City / Strategic Location | Location, facility, battle-map refs. | Control, garrison roster, resources, facility capacity. | Stationing, resource settlement, battle context. | Defense condition input. | City/location management UI. | Scene switch, save. |
| Equipment And Armament | Equipment templates, origin, tags, armament rules. | Inventory, assignment, corps armament level. | Equipment changes, build contribution. | Equipment effect snapshot input. | Equipment UI. | Resource load, save. |
| Command | Command definitions, availability refs. | No runtime command state. | Validation and permission checks. | Runtime order execution and events. | Command panel and targeting UI. | Input mapping. |
| Combat Runtime | Battle map and enemy definition refs. | No persistent runtime actors. | Snapshot build and result receive. | Core execution layer. | Battle HUD and feedback. | Scene, logs, random seed. |
| Report And Settlement | Text, result categories, reward refs. | Report history and applied state changes. | Settlement plan, state writeback, report generation. | Event/result provider. | Report UI. | Save, logs. |
| Content Definition | Static content entry. | No player state. | Queries and validation. | Read-only snapshot input. | Text/icon display. | Resource scanning and validation. |

## Persistent State

Persistent state is Domain authority and must be saveable:

| State | Examples |
|---|---|
| Hero | Ownership, level/rank, base attributes, profession mastery, skill setup, equipment slots. |
| Corps | Corps type, level, equipment level, `CorpsStrength`, training progress. |
| Battle Group | Hero-corps binding, current location, stationing/sortie/unavailable state. |
| City / Strategic Location | Control, resources, garrison roster, facilities, defense conditions, lightweight non-city states. |
| Equipment And Resources | Inventory, assignment, materials, special resources, armament upgrade state. |
| Report History | Completed battle report records and references to applied settlement results. |

`CorpsStrength` is the long-term corps strength value and uses the accepted 0-100 range. Visible soldiers are Runtime/Presentation mapping only. Long-term state does not track individual soldier identity, individual soldier experience, individual soldier equipment, or individual soldier casualty records.

## Runtime State

Runtime state exists only during the active battle, scene, or service operation:

| Runtime State | Examples |
|---|---|
| Combat actors | Current HP, mana, cooldown, position, path, target, temporary effects. |
| Command execution | Current command, command queue, target area, retreat state, protect/follow state. |
| Tactical AI | Target choice, local avoidance, group movement, pursuit and fallback decisions. |
| Battle process | Event stream, skill impact, visible soldier presentation, formation density, map triggers. |
| Temporary environment | Battle-only objects, one-shot battle conditions, active runtime modifiers. |

Runtime cannot query or mutate Domain state directly. Application snapshots and result contracts isolate the two sides.

## Battle Space Authority

Live battle uses square-grid anchored realtime combat. The square battle grid remains the runtime spatial authority; this proposal does not migrate the project to hexes or freeform physics movement.

Runtime owns these combat-space facts:

- actor anchored cell;
- actor reserved next cell, when moving;
- movement state such as anchored, moving, attacking, casting, defeated, or interrupted;
- current target actor or target cell;
- attack range, cooldown, and ability execution state;
- emitted movement, attack, damage, cast, interruption, and failure events.

Presentation owns visual interpolation between runtime cells, animation, selection, hover, debug overlays, and feedback. Presentation must not create separate combat truth by moving units into visual attack range independently of runtime facts.

Godot `Area2D` and `CollisionShape2D` may be authored on battle unit scenes for mouse picking, hover, selection, debug, and later visual helpers. They are not authoritative damage resolution in the square-grid realtime model.

Movement rules:

- Deployment places actors on valid square-grid cells.
- During live battle, an actor may reserve and move to a valid neighbor cell.
- First implementation uses 8-neighbor movement unless map data forbids diagonal transitions.
- Occupancy and reservation prevent multiple living actors from owning the same cell.
- Actors cannot perform basic attacks while moving between cells.

Attack rules:

- Basic attacks target actors, not damage cells.
- Basic attacks resolve from attacker anchored cell to target anchored cell using the ability's grid range.
- Runtime emits attack and damage events from the same authority used by settlement and reports.
- Ranged attacks may use longer grid range, but still resolve as actor-target attacks in the first implementation.

Footprint rules:

- Each actor may define a rectangular footprint width and height in cells.
- The actor anchor is always the top-left cell of that footprint.
- `1x1`, `1x2`, `2x1`, `2x2`, and `3x3` are valid initial footprint targets.
- Runtime may still choose movement by evaluating neighboring anchor cells.
- Candidate movement is valid only if all cells covered by the candidate footprint are walkable, unoccupied, and unreserved.
- Occupancy and reservation are stored per covered cell.
- Pre-battle deployment drag previews resize the existing hover selection frame over the same covered footprint cells that placement validation uses from the top-left anchor. Dragged sprites are positioned at the footprint center, and the top-left anchor snaps from pointer center thresholds instead of following raw mouse coordinates inside a cell.
- Attack range is measured by shortest square-grid distance between actor footprints.
- Area effects hit an actor when any covered footprint cell overlaps the resolved effect area.
- Unit sprites are authored from varied source pixel sizes. Presentation derives a uniform footprint visual scale from the largest footprint side and a tuning coefficient, so runtime occupancy can grow without stretching art or blindly mapping every occupied cell to a full sprite-scale step.
- Snapshot contracts carry footprint width and height into Runtime. Runtime clamps the supported footprint range and does not load Godot resources.

## Snapshot And Result Contracts

| Contract | Owner | Purpose |
|---|---|---|
| `BattleStartSnapshot` | Application | Frozen battle input from long-term state and definitions. |
| `BattleGroupSnapshot` | Application | Initial facts for one battle group in one battle. |
| `LocationBattleContext` | Application | City/location inputs such as defense, facilities, entrances, and terrain context. |
| `CommandRequest` | Presentation/Application | Player intent before full runtime execution. |
| `RuntimeOrder` | Runtime | Accepted command converted into executable runtime order. |
| `BattleEventStream` | Runtime | Semantic event source for report, settlement, UI feedback, and diagnostics. |
| `BattleOutcomeResult` | Runtime | Battle termination, outcome, losses, rewards, and result summary. |
| `SettlementPlan` | Application | Proposed long-term changes derived from result and events. |
| `StateDeltaSet` | Application | Applied long-term mutations. |
| `BattleReportRecord` | Application/Presentation | Player-readable explanation of already-settled facts. |

## Command Lifecycle

The command lifecycle describes how player intent becomes runtime behavior and how failures become feedback and report facts.

```text
Create Intent
-> Client-Side Basic Check
-> Submit Command
-> Application Validation
-> Runtime Acceptance / Rejection
-> Execution
-> Interrupt / Complete / Fail
-> UI Feedback
-> EventStream Attribution
```

Layer rules:

- UI creates command intent and performs only basic availability hints such as selected battle group and disabled buttons.
- Application validates battle existence, player ownership, battle-group availability, and whether the requested command channel matches hero/corps/combined authority.
- Runtime validates battle-context facts such as whether the target still exists, is reachable, or can be affected.
- Accepted runtime commands must become traceable states: `accepted`, `rejected`, `interrupted`, `completed`, `failed`.
- Commands do not directly mutate long-term state.

Event rules:

- UI-local invalid input does not enter `BattleEventStream`; it only produces UI feedback.
- Application rejection defaults to diagnostics, not battle events.
- Runtime rejection enters `BattleEventStream` because it explains battle state.
- Command accepted, interrupted, superseded, completed, or failed events must enter `BattleEventStream`.

## Autonomy And Tactical AI

Tactical AI exists to keep player input at medium frequency. The player gives intent; Runtime AI turns it into continuous movement, targeting, and local decisions.

Player command owns:

- main battle-group target;
- hero, corps, and combined command channel;
- tactical intent such as attack, defend, protect, retreat, regroup, or hold;
- hero skill cast intent and target;
- whether a new command overrides current autonomous behavior.

Tactical AI owns:

- path following and local avoidance;
- ordinary attack target choice;
- visible soldier movement and attack presentation;
- group spacing and formation maintenance;
- protect, pursuit, retreat path choice;
- degradation when a command cannot be fully executed.

Precedence:

```text
explicit player command
-> active command constraints
-> battle group tactical posture
-> autonomous local behavior
-> safe fallback
```

Failure degradation:

- Target lost: choose a valid target inside command scope; otherwise hold or regroup.
- Path unreachable: try local reroute; otherwise stop advance and emit a failure candidate.
- Protect target out of range: prioritize return/protect; if impossible, hold nearest safe position.
- Pursuit too deep: pursuit must obey retreat, protect, and area-hold constraints.
- Retreat blocked or late: emit explicit failure candidates for report attribution.

AI behavior must be attributable to a command, default posture, or safe fallback. Reports must not describe AI-driven outcomes as source-less randomness.

## Ability And Effect Definitions

Abilities and effects are content definitions. Runtime instantiates execution state from snapshots; it does not hardcode individual content rules.

Definitions / Content owns:

- `Skill`: display text, tags, channel availability, and content identity.
- `Cost`: mana, limited use, battle resource, condition, or other cost rules.
- `Cooldown`: cooldown timing and reset boundary.
- `Targeting`: target kind, range, valid target rules, area rules.
- `Effect`: damage, healing, control, movement, summon, shield, morale, resource, or other effect primitives.
- `Tag`: profession, combat class, form, element, faction, equipment, city, origin, or fantasy hook.
- `Modifier`: stat, behavior, cooldown, cost, target, settlement, or report-explanation modifier.

Layer rules:

- Domain saves unlocks, assignments, levels, equipment, and long-term state. It does not duplicate full definitions.
- Application validates whether abilities and effects may enter a snapshot.
- Runtime handles cooldown, cost payment, hit/application, effect state, and emitted events.
- UI displays definitions and availability, but it does not calculate final battle truth.
- Infrastructure loads resources and reports missing or invalid references.

Adding a specific skill, equipment effect, or corps trait should usually require only Resource authoring. A system change is needed only when a new effect primitive, target type, or cross-system rule is introduced.

Ability spatial contracts should support these extension points:

| Contract | Purpose |
|---|---|
| Target mode | Unit target, cell target, direction target, or self-centered execution. |
| Direction mode | Free angle, 8-way snap, 4-way snap, or forward arc. |
| Area shape | Single actor, single cell, line, cone, circle radius, or grid radius. |
| Range metric | Square-grid range rule used by the selected target and area mode. |
| Resolution source | Actor facts and grid facts owned by Runtime, not UI or presentation-only collision callbacks. |

The first square-grid realtime implementation only needs actor-target basic attacks and the contract fields required to avoid hardcoding future skills into the wrong model.

## Resource And Progression Flow

Resource and progression flow must be modelable before code. The architecture uses this loop:

```text
sources -> converters -> caps -> sinks -> battle loss -> recovery -> settlement writeback
```

Sources:

- city production: Food, Money, BuildingMaterials, SpecialResources;
- resource sites, ruins, dungeons, and opportunities;
- battle rewards, occupation rewards, defense rewards;
- facility, control, and strategic-location outputs.

Sinks:

- corps level training;
- corps equipment level upgrades;
- hero equipment forging, maintenance, or upgrades;
- post-battle recovery, replenishment, repair, and healing;
- defense, garrison, facilities, and special unlocks.

Converters:

- `TrainingCapacity`: resources and time into corps level growth or supported cap.
- `WorkshopCapacity`: resources and facility capacity into corps equipment level.
- battle: risk into experience, reward, losses, and campaign state changes.
- facilities: city identity into efficiency, caps, unlocks, or cost modifiers.

Caps and timing:

- Resource caps come from storage, facilities, control state, and strategic-location links.
- Garrison, training, workshop, and defense each use city capability limits.
- Application freezes battle input before battle.
- Runtime does not write long-term resources.
- Settlement writes rewards, losses, experience, recovery entry points, and city/location changes.

Loss-recovery loop:

`CorpsStrength` loss is applied after battle through Settlement. Recovery depends on battle outcome, retreat state, city support, available resources, and facility capacity. If resources are insufficient, the system leaves an explicit unrecovered state instead of silently restoring strength.

Negative feedback entry points belong in capacity, maintenance, recovery cost, training efficiency, workshop efficiency, post-battle losses, and city defense pressure. This document does not define balance values.

## Battle Report Attribution And Failure Rules

Report and Settlement share the same event source. Report explains; Settlement writes. They must not derive separate facts.

Events with settlement or explanation value should express, when applicable:

```text
actor
source command
target
effect type
resource delta
failure reason candidate
```

`resource delta` is optional for non-resource events. Do not fill fake values just to satisfy a shape.

If the source is environment, city facility, equipment, passive effect, AI fallback, or system interruption, the event must state that source explicitly.

Shared source contract:

- Runtime emits `BattleEventStream` and `BattleOutcomeResult`.
- Settlement derives state changes from the same event/result source.
- Report derives explanation from the same event/result source.
- UI may display report facts but must not compute new report truth.
- Any loss, reward, resource change, or recovery requirement used by Settlement must be traceable to result or event facts.

Failure candidates are created where the event happens and ranked during report generation. Typical candidates include:

- frontline collapsed;
- hero overextended during assault;
- ranged corps lacked protection;
- cavalry was countered by spear, terrain, or chokepoint pressure;
- key skill failed because of mana, cooldown, or target state;
- corps equipment level was too low;
- city defense or recovery support was insufficient;
- retreat was ordered too late, causing high corps loss.

## Runtime Persistence And Failure Rules

First phase does not require user-facing mid-battle save. It must still support safe interruption boundaries:

- no battle may partially write long-term state before Settlement;
- player retreat, battle interruption, runtime exception, and normal result are separate termination reasons;
- recoverable interruption restores to a consistent Runtime state or returns to the pre-settlement handoff;
- unrecoverable exception cannot fabricate victory or defeat;
- failed or incomplete runtime output must enter explicit safe rollback, failed handoff, or pending manual-resolution state;
- Settlement accepts only complete results with consistent event boundaries and explicit termination reason.

If later user-facing mid-battle save is added, it must persist the battle snapshot, necessary runtime state, and confirmed event stream boundary.

## Pattern Mapping

The following patterns are architecture references:

| Pattern | Project Use | Constraint |
|---|---|---|
| Command | Player command intent and runtime orders. | Commands affect Runtime first; long-term changes wait for Settlement. |
| State | Battle sessions, battle groups, command execution, and termination states. | State transitions must produce clear failure semantics. |
| Event Queue / Observer | Runtime event stream feeding UI, Report, Settlement, and diagnostics. | Report and Settlement share facts; they do not each recompute. |
| Type Object | Content definitions for heroes, corps, abilities, equipment, tags, and modifiers. | Definitions describe content; instances hold state. |
| Component | Runtime actor capabilities such as movement, targeting, effect handling, and tactical AI. | Componentization is an implementation option, not a reason to fragment Domain state. |
| Dirty Flag | UI refresh and derived view models. | Detailed UI refresh strategy belongs in a later UI architecture document. |

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
Domain long-term state + Definitions
-> Application builds BattleStartSnapshot
-> Runtime creates battle actors and tactical state
-> UI displays battlefield, selected battle group, and available commands
```

### Player Command

```text
UI creates CommandRequest
-> Application validates ownership and channel
-> Runtime accepts or rejects order in battle context
-> Runtime executes, interrupts, completes, or fails order
-> EventStream records meaningful command facts
```

### Resolve Battle

```text
Runtime emits EventStream + BattleOutcomeResult
-> Settlement builds SettlementPlan
-> Application applies StateDeltaSet
-> Report builds BattleReportRecord from the same facts
-> UI shows settled result and explanation
```

## First-Phase Architecture Scope

Recommended content scope:

```text
1 core city
1-2 resource sites
1 ruin or dungeon
3 heroes
3 corps classes
1 city light-RTS battle
1 equipment sample set
1 corps level and equipment-level progression sample
basic battle report
```

Required architecture skeletons:

| Scope | Goal |
|---|---|
| Content definition entry | Unified IDs and definition loading for heroes, corps, battle groups, equipment, locations, maps, abilities, tags, and text. |
| Long-term state model | Minimal saveable state for Hero, Corps, BattleGroup, StrategicLocation/City, Equipment, Resource. |
| Battle group lifecycle | Create, station, sortie, lock, enter battle, settle, release. |
| Battle snapshot contract | Runtime cannot read save state directly. |
| Three command channels | Hero, corps, and combined command contracts and validation skeletons. |
| Light RTS runtime skeleton | Separate hero actor and corps group with movement, attack, hold, retreat, and skill support. |
| Event/result contract | Event stream, battle outcome, settlement plan, and state delta boundaries. |
| Report skeleton | Explain outcome, contribution, corps loss, skills, equipment/city influence, rewards, and failure reasons. |

Placeholders:

| Scope | Placeholder Rule |
|---|---|
| Complex facilities | Keep facility capability entry; do not expand full tree yet. |
| Advanced equipment collection | Keep grade, origin, tag, and build-contribution hooks. |
| Profession/tag bonds | Keep trigger and report hooks; do not expand all combinations yet. |
| Non-city locations | Share strategic-location interface with only lightweight per-type state. |
| Enemy AI and map events | Keep runtime input and event output boundaries; do not lock exact behavior. |
| Balance values | Define ownership and flow only. |

Explicit non-goals:

- AP, TurnSystem, or old tactical chess loop;
- pure post-deployment auto-battle;
- individual soldier long-term growth;
- one hero with multiple main corps;
- large-scale RTS box selection and high-frequency micro;
- full diplomacy or government simulation;
- public order, intelligence, or city damage as first-phase core city attributes;
- non-city locations inheriting the full city model.

## Migration Principles

| Principle | Rule |
|---|---|
| Target architecture first | New systems follow hero-led light RTS, battle groups, strategic management, and reportable settlement. |
| Old concepts stay outside core contracts | AP, TurnSystem, old auto-battle, and old unit-count force models cannot become new Domain or Runtime foundations. |
| Temporary adapters are allowed | Old data may be converted into new snapshots or states during migration. |
| Adapters must be removable | Temporary migration code stays outside the core architecture and can be deleted after migration. |
| No double authority | One runtime responsibility has one target implementation. Old and new paths must not compete as equal authorities. |
| Contracts before presentation | Stabilize Definition, Domain, Snapshot, Result, Event, and Settlement contracts before replacing UI and scene presentation. |
| Explicit failure | Missing mappings or invalid old data fail clearly with diagnostics instead of hidden fallback. |

## Acceptance

This architecture is acceptable when:

- future work can identify which horizontal system owns each gameplay responsibility;
- future work can identify which vertical layer owns each data shape or rule;
- battle groups are the bridge between city/strategic preparation and live combat;
- Runtime cannot directly mutate long-term campaign state;
- UI cannot become state or settlement authority;
- command lifecycle, tactical AI, ability/effect definitions, resource flow, and report attribution all have explicit architecture positions;
- first-phase implementation can be planned without reviving old chess/AP/auto-battle authority;
- old implementation can be treated as migration input rather than target architecture.
