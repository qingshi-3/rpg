# Strategic Management System Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `gameplay-design/content-systems-long-term-design.md`, `gameplay-design/details/cities-and-locations/README.md`, and `gameplay-design/details/heroes-and-corps/README.md`.

The accepted gameplay direction is city-led strategic management with conquest, bounded city construction regions, city building instances, faction-shared foundation resources, aggregate city reserve soldiers, persistent corps instances, city-supported muster templates, hero-corps assignment, and expeditions. Special routes such as beast taming follow after the foundation operation loop is playable.

## Responsibility

The Strategic Management system owns the game's long-term strategic-management layer:

- strategic-location control, availability, and binding to accepted site map layout ids;
- world-map strategic time, elapsed-time settlement, and paused management-state boundaries;
- faction-shared first-version resources, passive resource-production settlement, and resource rewards for `Money`, `Food`, `Wood`, and `Ore`;
- city identity, authored construction regions, city building definitions and instances;
- city force capacity and aggregate reserve soldiers, including the configured first-version base recovery of `2` reserve soldiers per elapsed world-map pulse;
- corps muster template availability and persistent corps instances;
- hero strategic state, corps assignment, battle-group skill grants/loadouts, and expeditions;
- strategic commands that mutate long-term state;
- strategic presentation view models;
- strategic-side battle handoff intent and battle-result application through a bridge boundary.

Strategic Management is the current strategic-layer authority. Legacy world/site/action/garrison structures are not strategic gameplay authority; any retained boundary adapter must remain subordinate and must not own new strategic facts or rules.

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
- city building types, categories, footprints, costs, upgrade chains, placement requirements, and provided capabilities;
- construction-region definitions and placement metadata;
- corps definitions and muster-template requirements;
- hero-corps aptitude tags;
- hero or battle-group default skill-grant references, as strategic assignment content that points to battle skill definitions;
- strategic map metadata;
- site map layout metadata only as far as the strategic layer needs to bind locations to reusable layout templates;
- battle-entry metadata only as far as the strategic layer needs to know a location can request a battle.

First-version content should be data-driven only where content variation requires it. Do not create a generic scripting language, full condition expression tree, technology-tree framework, or effect-chain editor for the first implementation.

### Strategic State

Strategic state stores durable campaign facts and must be serializable without Godot scene nodes.

State includes:

- world-map strategic time or elapsed-time settlement counters;
- faction-shared resource stores;
- strategic-location ownership and control state;
- location-owned persistent map facts keyed by stable layout or marker ids when those facts matter to campaign state, such as placed buildings, depleted resources, destroyed bridges, or resolved site events;
- city building instances, construction state, placed construction-region/grid positions, optional battle anchor ids, and support state when relevant;
- city force capacity and reserve soldier counts;
- corps instances and their strategic state;
- hero strategic state, corps assignment, and skill grants or loadout slots;
- expedition state;
- strategic pending-battle or resolved-battle markers when needed.
- each active expedition participant's exact valid pre-departure rollback station;
- committed battle-settlement identity keyed by expedition, session, and snapshot, including the original feedback/settlement identity returned by exact replay.

State does not include UI selection, TileMap node state, battle Runtime actors, deployment drag state, or derived availability booleans. Facts such as "this city can muster wolf pack assault" are derived from state plus definitions, not stored as separate authority.

Reusable site map layout scenes are templates, not state. Multiple locations may bind to the same layout id without sharing persistent location facts.

State may store elapsed strategic-time facts needed for save/load, production, timed projects, expeditions, recovery, or diagnostics. It must not store the fact that a UI panel is open as strategic truth; presentation or scene-transition state owns modal UI presence, while the strategic time controller owns whether elapsed world-map time is currently advancing.

### Rules

Rules are deterministic read-only queries over content definitions and strategic state.

Rules answer:

- whether a location can be inspected, occupied, attacked, or used as a source;
- whether resources are sufficient;
- whether a building can be placed in a city construction region;
- which muster templates are available in a city and why;
- whether a corps can be created, restored, trained, or upgraded;
- whether reserve soldiers are available and how much capacity remains;
- whether a hero can lead a corps and with what aptitude;
- whether a hero or battle group has a valid skill grant or loadout slot for strategic assignment;
- whether an expedition can be formed or sent.

Rules do not mutate state, build UI, simulate combat, or provide hidden fallbacks. Every disabled player-facing action should be explainable through a rule result.

### Commands

Commands are the only strategic-state mutation entry point. UI, AI, events, tests, and bridge result application must submit commands instead of editing state directly.

Commands include:

- occupy, lose, discover, or change a strategic location;
- settle elapsed world-map time and time-driven strategic effects;
- settle resource rewards and accepted time-driven strategic effects;
- build or remove a city building, with building upgrades reserved for a later accepted capability;
- create or replenish a corps instance;
- assign or unassign a corps to a hero;
- replace a hero's main corps through a city muster template, including old-corps full-refund settlement and new-corps assignment;
- assign, unassign, unlock, or modify a hero or battle-group skill grant;
- create or resolve an expedition;
- request a battle handoff intent;
- apply a battle result summary to strategic state.

Commands call rules before mutating state. They return explicit success, failure reasons, changed facts, and strategic events.

Battle-result application is orchestrated as a candidate-state transaction around the command boundary: apply to an isolated candidate, durably save the complete candidate, publish it as current state, then consume the identity-matched Bridge Active Context. A save failure leaves the prior live state and context retryable. Exact committed replay returns the recorded successful settlement identity; a conflicting replay fails without duplicate rewards, losses, capture, or feedback.

Elapsed-time settlement commands are internal strategic commands, not player-facing turn buttons. They may be called by the world-map time controller, diagnostics, tests, or later accepted gameplay flows. They must not be exposed as a generic Civilization-style "end turn" action.

### Presentation And External Boundaries

Presentation is a view-model and input layer over Strategic Management. It may reuse the existing large-map TileMapLayer resource and visual assets, but it does not own strategic truth.

Presentation owns:

- map selection and highlighting;
- location summaries;
- city construction panels, placement preview, construction-region highlighting, and disabled reasons;
- corps and hero assignment panels;
- command buttons and disabled reasons.

Presentation does not calculate strategic rules, mutate resources, or create corps directly.

Presentation may request entry into city management or return to the world map, but it does not decide elapsed-time settlement. City-management panels operate while world-map time is paused.

External battle interaction is through the Strategic Battle Bridge. The bridge may translate strategic facts into battle input and battle results back into strategic commands, but the bridge must not become a second strategic-rule owner. Strategic Management remains the only owner of long-term strategic mutation.

## Subsystem Responsibilities

### Strategic Timeflow

Owns Sanguo Qunying-style world-map time for the strategic layer. The large strategic map is the normal running timeline. The current foundation advances strategic army movement and settles passive resource production and city reserve recovery. Enemy strategic actions, opportunity refresh, construction duration, corps training, and expedition-preparation timers require later accepted capability slices.

Entering city management pauses world-map time. Battle preparation, active battle, dialogue, story scenes, reports, and other modal management states also pause world-map time unless a later accepted architecture defines a specific exception.

The system may use settlement ticks, pulses, or elapsed-time batches internally. These names describe implementation granularity, not player-facing turns. Existing or transitional code that uses "step" language must be interpreted as elapsed world-map settlement until it is renamed; it must not justify an end-turn UI.

### Location And Control

Owns strategic-location identity, map presence, ownership, control state, and whether a location provides city management, resources, source permissions, or battle availability.

It does not own city building construction, hero-corps assignment, or combat simulation.

### Resources And Production

Owns faction-shared first-version resources, passive production, costs, rewards, and affordability checks.

It does not own cross-city transport loss, trade, logistics, or battle Runtime resources in the first version.

### Cities And Facilities

Owns city identity, authored construction regions, placement legality, city building instances, construction state, and building-provided strategic capabilities.

The current foundation supports bounded building placement, resource costs, durable city building instances, and passive economy production. City reserve recovery is a base city rule and does not require a building. Common military training gates, hero recruitment access, workshop support, defense effects, reserve-capacity modifiers, and special routes are later building-capability slices unless a focused accepted rule explicitly introduces them. The old direct building scalar fields for per-pulse production, reserve recovery, and force-capacity bonuses are retired implementation scaffolding; future building effects must use a focused economy/capability architecture instead of re-adding ad hoc fields. Buildings do not directly instantiate battle Runtime units or bypass corps muster rules.

The foundation construction model is bounded RTS-style placement: building-panel selection, mouse-attached preview, grid snapping, footprint validation, overlap checks, region bounds checks, resource checks, and explicit building eligibility. Construction regions define buildable space and must not reject a placement only because the building category differs from the region label. It does not include workers, road connectivity, gathering paths, production-efficiency simulation, or full RTS production queues. Later economy/capability work may use terrain, tile, resource context, or local map facts as efficiency modifiers without making them hidden category bans.

### Corps Muster And Instances

Owns available muster-template evaluation and persistent corps instances.

A muster template is a derived availability result from city identity, buildings, source locations, resources, and later accepted systems. A corps instance is durable strategic state with strength, training, equipment, experience, assignment, and recovery state.

The current player-facing foundation supports corps creation from available muster templates, reserve/resource-funded replenishment, hero assignment, and full-refund hero main-corps replacement. Training, level progression, equipment upgrades, and durable hero equipment loadouts are not current player-facing loops; existing progression fields and reward samples are extension anchors for later accepted capabilities.

City reserve soldiers are an aggregate city manpower pool, not individual soldier records. Corps creation and replenishment consume reserve soldiers and resources. Active forces should be derived from corps, battle group, and garrison instances when possible, with the invariant:

```text
ActiveForces + ReserveForces <= CityForceCapacity
```

For each controlled city, first-version elapsed-time settlement recovers `min(2 * elapsedPulses, remaining force capacity)` reserve soldiers. The rate belongs to the Strategic Management economy definitions and Runtime must not duplicate it as a hidden constant. Recovery is free and has no first-version building requirement. Strategic state does not persist a manual-conscription queue or automatic-conscription policy, and Strategic Management exposes no player command for either concept.

When recovery changes a city, elapsed-time settlement emits one aggregated low-noise recovery event for that city. It does not emit one event per pulse.

Severe battle loss should move a corps instance into routed, scattered, recovering, or rebuilding state rather than permanently deleting it by default.

### Heroes And Expeditions

Owns hero strategic availability, hero-corps assignment, aptitude evaluation, battle-group skill grants/loadouts, and expedition formation.

The first version keeps one hero plus one main corps as the strategic battle-group shape. Multi-corps heroes, secondary corps, and complex army composition are later expansions.

Hero main-corps replacement is a Strategic Management command, not a UI-side mutation. In the first version, the city recruitment workbench may request replacement from an available muster template. If the hero already has a main corps, the command settles the old corps back into the city with full refund and no extra replacement loss, based on the old corps' current remaining strength, then creates and assigns the new corps. The command must report consume, refund, and net resource/reserve changes for Presentation. The replacement flow must not silently create hidden city corps inventory by parking the old corps as another managed corps instance.

Skill definitions themselves are battle content definitions. Strategic Management owns persistent assignment facts such as which hero or battle group has which granted skill slot, where the grant came from, and which strategic modifiers apply before battle snapshot compilation. It must not duplicate skill effect definitions, cooldown rules, targeting rules, or Runtime availability state.

### Strategic Battle Boundary

Owns only strategic-side battle intent and battle-result application. It must not own battle Runtime internals or bridge session implementation details.

The current foundation can request battle for an expedition against a hostile target and apply the resulting victory, defeat, corps-loss, ownership, reward, and hero-feedback facts back to strategic state. The bridge contract, preparation session, snapshot compilation, scene handoff, and result-summary shape are owned by `strategic-battle-bridge-architecture.md`.

This establishes the foundation strategic battle loop; it does not mean terrain, facilities, corps automatic skills, equipment, bonds, or city influence already provide complete battle-outcome explanation. Those remain later battle-rule and reporting capabilities.

The first version does not own a mandatory strategic battle-preparation choice before battle entry. A player expedition may request a battle directly after reaching a hostile battle-capable target; deployment and launch readiness then belong to the Strategic Battle Bridge.

## Data-Driven Boundary

First-version Strategic Management should be selectively data-driven.

Data-driven:

- location definitions;
- city identity definitions;
- resource definitions;
- city building definitions, footprints, basic costs, categories, and placement requirements;
- construction-region definitions;
- corps definitions and basic muster/recovery costs;
- hero aptitude tags;
- hero or battle-group default skill-grant references;
- strategic map metadata;
- site map layout ids and high-level layout bindings for strategic locations;
- high-level battle-entry metadata.

Code-owned rule families:

- strategic command execution;
- state transitions;
- resource payment and rewards;
- city building placement, construction, and upgrade state transitions;
- reserve soldier recovery and capacity validation;
- corps creation, recovery, training, and equipment upgrading;
- hero-corps assignment;
- hero main-corps replacement, including full-refund settlement and net-cost reporting;
- skill-grant assignment, unlock, and modifier validation;
- expedition formation;
- battle result application.

Do not add a generic rule scripting language, full technology framework, arbitrary effect chains, automated strategic AI, complex logistics, or broad editor tooling to the current foundation without a focused accepted capability.

## Single-Authority Policy

The strategic-layer replacement is complete: Strategic Management is the current authority. The following rules protect that single-authority boundary.

Rules:

- Do not keep dual strategic authorities.
- Do not write legacy fallback paths.
- Do not keep new and old strategic state in sync through double writes.
- Do not preserve stale strategic authority for compatibility or build convenience.
- Reuse only the large-map TileMapLayer resource and other pure presentation assets when useful.
- Strategic changes interact with battle only through the Strategic Battle Bridge boundary.

## Legacy Authority Retirement

The following legacy responsibilities must not receive new strategic-management behavior:

- world action resolver as a universal action authority;
- world site management as the new city-management authority;
- garrison count state as player core corps authority;
- old world army state as the new expedition authority;
- old battle-result applier as the new strategic result settlement authority;
- first-slice strategic definition factory as the new content authority;
- action-list UI as the long-term strategic-management UI model.

Any retained legacy path is an explicit adapter or presentation-only carrier. It must be renamed or constrained so it cannot be mistaken for Strategic Management authority, and it must not own new strategic facts, rules, or state mutation.

## Contracts

- Strategic state can be mutated only through Strategic Management commands.
- Expedition creation captures every participant's valid current station before clearing the corps station field. Cancellation and battle-entry rollback validate the complete restoration plan before changing any hero, corps, expedition, or carrier association, then restore those exact stations without partial mutation.
- Strategic save documents are versioned and migrate incrementally. The current save is promoted only from a flushed same-directory staging document, preserving a recoverable previous complete document where supported; load accepts only a complete current or recoverable previous document and never treats a null state as a new campaign.
- Elapsed strategic time can advance only through the strategic world-map time controller or explicit diagnostics/tests.
- City management, battle preparation, battle execution, dialogue, and reports pause world-map elapsed time by default.
- City-management commands must not advance world-map time merely because the player opened or used a city screen.
- City construction uses authored bounded construction regions and must not be implemented as unrestricted full-map RTS construction.
- City manpower follows `ActiveForces + ReserveForces <= CityForceCapacity`; reserve soldiers recover at the configured first-version base rate only through Strategic Management time settlement and are consumed only through Strategic Management commands.
- Rules must be deterministic read-only queries over definitions and state.
- Presentation must consume view models and submit commands.
- Hero main-corps replacement must go through Strategic Management; Presentation may consume replacement projections for validation, diagnostics, or future confirmation surfaces but must not calculate or apply replacement settlement as authority. Normal recruitment cards present the selected corps requirements, not the full replacement accounting breakdown.
- Derived availability is not persistent authority.
- Strategic skill grants/loadouts reference stable battle skill definition ids and do not duplicate skill definitions or battle Runtime cooldown state.
- Battle Runtime must never be referenced as a live dependency by Strategic Management.
- Battle integration must follow `strategic-battle-bridge-architecture.md` before implementing battle-side changes.
- Strategic-location definitions may bind to site map layout ids, but persistent location facts must stay in Strategic Management state and be keyed by location id plus stable layout or marker ids.
- Reusing one layout for multiple strategic locations must not share built facilities, resources, bridge state, control, garrison, event progress, or battle results.

## Failure Rules

- Missing definitions fail explicitly.
- Invalid commands return structured failure reasons.
- Commands must not partially apply state on failed validation.
- Malformed, null-state, unsupported-future, or unrecoverable save documents fail explicitly. Migration may derive a missing expedition rollback station from `SourceLocationId` only when current state and definitions prove it was that participant's valid departure city; otherwise migration fails instead of guessing.
- Battle settlement persistence failure, identity mismatch, or conflicting replay leaves published state unchanged and retryable where applicable.
- Missing bridge-session or result-summary mappings block battle handoff implementation instead of creating direct Runtime dependencies.
- A retained legacy path must fail or be removed rather than silently owning new strategic behavior.

## Acceptance

This architecture is acceptable when:

- the running game uses Strategic Management as the sole strategic gameplay authority without dual strategic state or mutation paths;
- a strategic state can be understood without reading Godot scene nodes or battle Runtime state;
- all player, AI, event, and battle-result strategic mutations go through commands;
- location, resource, facility, corps, hero, and expedition responsibilities are separable;
- first-version content can add locations, facilities, resources, corps, and hero aptitude through focused definitions without a generic scripting system;
- strategic locations can reference reusable site map layouts without making Godot scene nodes or layout templates persistent authority;
- battle integration follows the explicit Strategic Battle Bridge contract instead of leaking Runtime state into Strategic Management.
