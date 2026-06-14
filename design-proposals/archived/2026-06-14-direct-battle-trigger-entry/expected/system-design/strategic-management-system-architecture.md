# Strategic Management System Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `gameplay-design/content-systems-long-term-design.md`, `gameplay-design/details/cities-and-locations/README.md`, and `gameplay-design/details/heroes-and-corps/README.md`.

The accepted gameplay direction is city-led strategic management with conquest, resource sites, limited city facility slots, persistent corps instances, city-supported muster templates, hero-corps assignment, and first-version beast-taming content.

## Responsibility

The Strategic Management system owns the game's long-term strategic-management layer:

- strategic-location control and availability;
- world-map strategic time, elapsed-time settlement, and paused management-state boundaries;
- faction-shared first-version resources and resource production;
- city identity, limited facility slots, and facility state;
- corps muster template availability and persistent corps instances;
- hero strategic state, corps assignment, and expeditions;
- strategic commands that mutate long-term state;
- strategic presentation view models;
- strategic-side battle handoff intent and battle-result application through a bridge boundary.

Strategic Management is the new strategic-layer authority. Legacy world/site/action/garrison structures are not long-term authority for new strategic-management work.

## Does Not Own

Strategic Management does not own:

- battle Runtime simulation;
- battle AI, movement, damage, skill release, cooldowns, or targeting;
- battle deployment UI internals;
- battle bridge session internals or Runtime-facing DTO implementation details;
- scene-transition router internals;
- Godot TileMap or node state as gameplay authority;
- generic scenario scripting, full diplomacy, logistics, or technology trees in the first version.
- Godot process callbacks, UI panels, or scene nodes as independent gameplay time authorities.

The bridge contract between Strategic Management and battle is defined in `system-design/strategic-battle-bridge-architecture.md`. This document fixes the strategic dependency boundary: Strategic Management may request battles and apply bridge-produced result summaries, but it must never read or mutate battle Runtime live state.

## Architecture Layers

Strategic Management uses five layers.

### Content Definitions

Content definitions describe what the campaign allows. They do not store player progress.

Definitions include:

- strategic locations: cities, resource sites, beast minor sites, enemy targets, and future passes, ruins, or dungeons;
- city identities such as plains human city or beast-border stronghold;
- resource types;
- facility types, facility slots, costs, and provided capabilities;
- corps definitions and muster-template requirements;
- hero-corps aptitude tags;
- strategic map metadata;
- battle-entry metadata only as far as the strategic layer needs to know a location can request a battle.

First-version content should be data-driven only where content variation requires it. Do not create a generic scripting language, full condition expression tree, technology-tree framework, or effect-chain editor for the first implementation.

### Strategic State

Strategic state stores durable campaign facts and must be serializable without Godot scene nodes.

State includes:

- world-map strategic time or elapsed-time settlement counters;
- faction-shared resource stores;
- strategic-location ownership and control state;
- city facility instances;
- corps instances and their strategic state;
- hero strategic state and corps assignment;
- expedition state;
- strategic pending-battle or resolved-battle markers when needed.

State does not include UI selection, TileMap node state, battle Runtime actors, deployment drag state, or derived availability booleans. Facts such as "this city can muster wolf pack assault" are derived from state plus definitions, not stored as separate authority.

State may store elapsed strategic-time facts needed for save/load, production, timed projects, expeditions, recovery, or diagnostics. It must not store the fact that a UI panel is open as strategic truth; presentation or scene-transition state owns modal UI presence, while the strategic time controller owns whether elapsed world-map time is currently advancing.

### Rules

Rules are deterministic read-only queries over content definitions and strategic state.

Rules answer:

- whether a location can be inspected, occupied, attacked, or used as a source;
- whether resources are sufficient;
- whether a facility can be built in a city;
- which muster templates are available in a city and why;
- whether a corps can be created, restored, trained, or upgraded;
- whether a hero can lead a corps and with what aptitude;
- whether an expedition can be formed or sent.

Rules do not mutate state, build UI, simulate combat, or provide hidden fallbacks. Every disabled player-facing action should be explainable through a rule result.

### Commands

Commands are the only strategic-state mutation entry point. UI, AI, events, tests, and bridge result application must submit commands instead of editing state directly.

Commands include:

- occupy, lose, discover, or change a strategic location;
- settle elapsed world-map time and time-driven strategic effects;
- settle resource production and resource rewards;
- build, remove, or later upgrade a city facility;
- create, recover, train, or upgrade a corps instance;
- assign or unassign a corps to a hero;
- create or resolve an expedition;
- request a battle handoff intent;
- apply a battle result summary to strategic state.

Commands call rules before mutating state. They return explicit success, failure reasons, changed facts, and strategic events.

Elapsed-time settlement commands are internal strategic commands, not player-facing turn buttons. They may be called by the world-map time controller, diagnostics, tests, or later accepted gameplay flows. They must not be exposed as a generic Civilization-style "end turn" action.

### Presentation And External Boundaries

Presentation is a view-model and input layer over Strategic Management. It may reuse the existing large-map TileMapLayer resource and visual assets, but it does not own strategic truth.

Presentation owns:

- map selection and highlighting;
- location summaries;
- city facility panels;
- corps and hero assignment panels;
- command buttons and disabled reasons.

Presentation does not calculate strategic rules, mutate resources, or create corps directly.

Presentation may request entry into city management or return to the world map, but it does not decide elapsed-time settlement. City-management panels operate while world-map time is paused.

External battle interaction is through the Strategic Battle Bridge. The bridge may translate strategic facts into battle input and battle results back into strategic commands, but the bridge must not become a second strategic-rule owner. Strategic Management remains the only owner of long-term strategic mutation.

## Subsystem Responsibilities

### Strategic Timeflow

Owns Sanguo Qunying-style world-map time for the strategic layer. The large strategic map is the normal running timeline. While it runs, elapsed time can move armies, allow enemy strategic actions, settle passive resource production, refresh opportunities, and later advance construction, training, recovery, or expedition timers.

Entering city management pauses world-map time. Battle preparation, active battle, dialogue, story scenes, reports, and other modal management states also pause world-map time unless a later accepted architecture defines a specific exception.

The system may use settlement ticks, pulses, or elapsed-time batches internally. These names describe implementation granularity, not player-facing turns. Existing or transitional code that uses "step" language must be interpreted as elapsed world-map settlement until it is renamed; it must not justify an end-turn UI.

### Location And Control

Owns strategic-location identity, map presence, ownership, control state, and whether a location provides city management, resources, source permissions, or battle availability.

It does not own facility construction, hero-corps assignment, or combat simulation.

### Resources And Production

Owns faction-shared first-version resources, passive production, costs, rewards, and affordability checks.

It does not own cross-city transport loss, trade, logistics, or battle Runtime resources in the first version.

### Cities And Facilities

Owns city identity, limited facility slots, facility construction state, and facility-provided strategic capabilities.

Facilities enable and support capabilities such as common military training, workshop support, defense, or beast taming. They do not directly instantiate battle units or bypass corps muster rules.

### Corps Muster And Instances

Owns available muster-template evaluation and persistent corps instances.

A muster template is a derived availability result from city identity, facilities, source locations, resources, and later accepted systems. A corps instance is durable strategic state with strength, training, equipment, experience, assignment, and recovery state. Individual soldier stockpiles are not a first-version model.

Severe battle loss should move a corps instance into routed, scattered, recovering, or rebuilding state rather than permanently deleting it by default.

### Heroes And Expeditions

Owns hero strategic availability, hero-corps assignment, aptitude evaluation, and expedition formation.

The first version keeps one hero plus one main corps as the strategic company shape. Multi-corps heroes, secondary corps, and complex army composition are later expansions.

### Strategic Battle Boundary

Owns only strategic-side battle intent and battle-result application. It must not own battle Runtime internals or bridge session implementation details.

This subsystem can say "this expedition requests battle against this target" and later "apply this battle result summary". The bridge contract, preparation session, snapshot compilation, scene handoff, and result-summary shape are owned by `strategic-battle-bridge-architecture.md`.

The first version does not own a mandatory strategic battle-preparation choice before battle entry. A player expedition may request a battle directly after reaching a hostile battle-capable target; deployment and launch readiness then belong to the Strategic Battle Bridge.

## Data-Driven Boundary

First-version Strategic Management should be selectively data-driven.

Data-driven:

- location definitions;
- city identity definitions;
- resource definitions;
- facility definitions and basic costs;
- corps definitions and basic muster/recovery costs;
- hero aptitude tags;
- strategic map metadata;
- high-level battle-entry metadata.

Code-owned rule families:

- strategic command execution;
- state transitions;
- resource payment and rewards;
- facility construction;
- corps creation, recovery, training, and equipment upgrading;
- hero-corps assignment;
- expedition formation;
- battle result application.

Do not build a generic rule scripting language, full technology framework, arbitrary effect chains, automated strategic AI, complex logistics, or editor tooling as part of the first Strategic Management rebuild.

## Clean Rebuild Policy

Strategic Management is a clean rebuild of the strategic layer. The goal is clean final architecture, not preserving legacy strategic code during intermediate implementation steps.

Rules:

- Do not keep dual strategic authorities.
- Do not write legacy fallback paths.
- Do not keep new and old strategic state in sync through double writes.
- Do not preserve intermediate compilation or runtime behavior by keeping stale strategic authority.
- Reuse only the large-map TileMapLayer resource and other pure presentation assets when useful.
- Keep existing battle Runtime behavior out of the strategic refactor except through the Strategic Battle Bridge boundary.

During implementation, the project may be temporarily non-compiling while the strategic layer is being replaced. The final accepted implementation must compile, run, and verify only after the new Strategic Management authority has replaced the old strategic layer.

## Legacy Authority Retirement

The following legacy responsibilities must not receive new strategic-management behavior:

- world action resolver as a universal action authority;
- world site management as the new city-management authority;
- garrison count state as player core corps authority;
- old world army state as the new expedition authority;
- old battle-result applier as the new strategic result settlement authority;
- first-slice strategic definition factory as the new content authority;
- action-list UI as the long-term strategic-management UI model.

These may be deleted, replaced, or temporarily broken during the clean rebuild. If a path remains after the rebuild, it must be renamed or constrained so it cannot be mistaken for Strategic Management authority.

## Contracts

- Strategic state can be mutated only through Strategic Management commands.
- Elapsed strategic time can advance only through the strategic world-map time controller or explicit diagnostics/tests.
- City management, battle preparation, battle execution, dialogue, and reports pause world-map elapsed time by default.
- City-management commands must not advance world-map time merely because the player opened or used a city screen.
- Rules must be deterministic read-only queries over definitions and state.
- Presentation must consume view models and submit commands.
- Derived availability is not persistent authority.
- Battle Runtime must never be referenced as a live dependency by Strategic Management.
- Battle integration must follow `strategic-battle-bridge-architecture.md` before implementing battle-side changes.

## Failure Rules

- Missing definitions fail explicitly.
- Invalid commands return structured failure reasons.
- Commands must not partially apply state on failed validation.
- Missing bridge-session or result-summary mappings block battle handoff implementation instead of creating direct Runtime dependencies.
- A retained legacy path must fail or be removed rather than silently owning new strategic behavior.

## Acceptance

This architecture is acceptable when:

- a future implementation can replace the old strategic layer without dual strategic authorities;
- a strategic state can be understood without reading Godot scene nodes or battle Runtime state;
- all player, AI, event, and battle-result strategic mutations go through commands;
- location, resource, facility, corps, hero, and expedition responsibilities are separable;
- first-version content can add locations, facilities, resources, corps, and hero aptitude through focused definitions without a generic scripting system;
- battle integration follows the explicit Strategic Battle Bridge contract instead of leaking Runtime state into Strategic Management.
