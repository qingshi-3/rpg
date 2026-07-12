# Strategic Battle Bridge Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `gameplay-design/content-systems-long-term-design.md`, `system-design/strategic-management-system-architecture.md`, `system-design/hero-led-light-rts-system-architecture.md`, and `system-design/battle-result-settlement-architecture.md`.

The accepted loop is:

```text
strategic expedition targets a hostile battle-capable location
-> world-map arrival opens a battle trigger confirmation
-> prepare battle groups on an authored battle map
-> run hero-led light RTS Runtime
-> explain the result through settlement and report facts
-> apply consequences back to Strategic Management state through commands
```

## Responsibility

The Strategic Battle Bridge owns the cross-system contract between Strategic Management and battle.

It owns:

- accepting a strategic battle intent from Strategic Management commands;
- creating a transient strategic battle session;
- exposing battle-preparation data for participating battle groups;
- exposing eligible local building support for pre-battle selection or confirmation when a battle occurs at a city or stronghold;
- validating launch readiness at the bridge boundary;
- compiling an immutable `BattleStartSnapshot` for battle Runtime;
- compiling current-battle local support snapshots without exposing full city building state to Runtime;
- carrying scene-transition handoff context for the active battle session;
- receiving complete Runtime outcome, Runtime event stream, settlement plan, and battle report facts;
- producing a `StrategicBattleResultSummary`;
- submitting battle-result application back through Strategic Management commands.

## Does Not Own

The Strategic Battle Bridge does not own:

- strategic content definitions, resources, facilities, corps muster, hero assignment, expedition rules, or long-term state mutation;
- battle Runtime simulation, movement, damage, targeting, AI, skill execution, cooldowns, or action timing;
- battle report truth independent from Runtime events and settlement facts;
- UI layout, drag presentation, HUD rendering, or authored Godot scene structure;
- root scene replacement or loading policy;
- persistence of live battle Runtime state;
- generic scenario scripting or arbitrary battle effect rules;
- legacy world army, garrison, site-management, or auto-battle authority.

## Persistent State

Strategic Management owns durable battle-related strategic facts, such as pending battle markers, participant locks, expedition state, corps assignment, corps strength, location ownership, rewards, recovery state, and battle history.

The bridge session is runtime state by default. It may reference durable strategic IDs, but it must not become another persistent owner for heroes, corps, resources, locations, or expeditions.

Future save/resume for an active battle requires a separate proposal that defines which bridge session fields, battle-preparation draft facts, Runtime facts, and scene context are persistable.

## Runtime State

Bridge runtime state may include:

- `StrategicBattleSessionId`;
- originating strategic command or pending-battle ID;
- battle kind;
- attacker and defender faction IDs;
- source and target strategic location IDs;
- participant references for each battle group;
- map definition ID and battle scene path;
- available entrances, deployment zones, and optional objective/tactical zones from authored map metadata;
- battle-preparation draft state;
- local building support selection or confirmation draft state when available;
- launch validation result and failure reason;
- return route and rollback context;
- matching snapshot ID after launch;
- result-consumption state after Runtime completion.

This state is a session envelope around battle preparation and Runtime launch. It is not the same object as `BattleStartSnapshot`.

### Bridge Active Context

Strategic Management-backed battles use a Bridge Active Context as the long-term active handoff.

The active context is a typed runtime envelope owned by the Strategic Battle Bridge. It may contain:

- `StrategicBattleSession`;
- compiled `BattleStartSnapshot`;
- battle-preparation draft and launch readiness;
- scene path, return route, and rollback context supplied by world flow;
- Runtime outcome, event stream, settlement plan, and battle report after completion;
- result-consumption state.

The active context is the authoritative in-memory carrier for Strategic Management battle preparation, launch, and return. It is not persistent Strategic Management state, and it must not mutate heroes, corps, locations, resources, or expeditions directly.

Temporary compatibility fields may be projected from the active context into legacy preparation UI models while those UI slices are being migrated. Those projections are adapters, not authorities.

## Inputs

Inputs are:

- `StrategicBattleIntent` or equivalent pending-battle facts created by Strategic Management commands;
- read-only Strategic Management definitions and state views needed to compile battle participants and location context;
- the world-map battle trigger confirmation after the expedition arrives at the target;
- battle map entry metadata, semantic deployment markers, optional semantic objective/tactical markers, entrances, and compiled navigation topology;
- battle-preparation player choices: participating battle groups, deployment placements, and formation;
- pre-battle local building support choices when the target location offers eligible support;
- complete `BattleOutcomeResult`, `BattleEventStream`, `SettlementPlan`, and `BattleReportRecord` values after Runtime completion.

## Outputs

Outputs are:

- bridge session creation result;
- battle-preparation view data and launch readiness reasons;
- local building support view data, selection state, and disabled reasons;
- immutable `BattleStartSnapshot`;
- typed scene-transition handoff payload for the active bridge session;
- `StrategicBattleResultSummary`;
- Strategic Management command requests to apply battle consequences;
- low-noise diagnostics for session creation, launch, rejection, result mismatch, and result application.

## Contracts

### Two-Phase Start

Strategic battles start in two phases:

```text
Strategic Management command creates battle intent and locks strategic participants
-> world-map arrival focuses the target and gets explicit player confirmation to trigger battle
-> Strategic Battle Bridge opens a preparation session
-> bridge creates a Bridge Active Context with session, route, and battle-facing draft state
-> player or AI completes deployment choices
-> bridge validates launch readiness
-> bridge compiles BattleStartSnapshot
-> scene transition enters battle runtime presentation
```

Strategic Management decides whether a battle may be requested. The bridge decides whether the requested battle can be compiled and launched. Runtime decides how the battle resolves.

### Snapshot Boundary

`BattleStartSnapshot` is the immutable Runtime input. It should contain battle-facing facts such as battle groups, hero and corps definitions, combat stats, location context, optional objective/tactical zones, skill snapshots, and topology references.

The bridge session envelope carries scene path, return route, rollback context, pending strategic IDs, and preparation workflow facts. These should not be added to `BattleStartSnapshot` unless Runtime directly needs them.

### Participant Identity

Strategic participants are battle groups:

```text
battle group = 1 hero + 1 main corps instance
```

The bridge participant reference must preserve:

- strategic participant ID;
- hero instance ID and definition ID;
- corps instance ID and definition ID;
- faction ID;
- source strategic location ID;
- role in the battle;
- pre-battle corps strength;
- deployment eligibility and reserve state.

Battle `SourceForceId` and actor `SourceStateId` must map back to this participant identity. The long-term source is the corps instance, not a stockpile of individual soldiers or an old garrison row.

### Battle Preparation Draft

Battle preparation draft state is Application/Bridge-owned. Presentation may edit it only through bridge-facing requests.

The draft records current-battle choices:

- participating or reserve status;
- deployment placement;
- selected formation;
- selected or confirmed local building support entries when available;
- launch readiness and disabled reasons.

The draft does not require objective-zone or engagement-rule choices for launch. Runtime starts deployed player groups in the default attack posture; live destination beacon commands provide the normal player movement objective during battle.

The draft is not long-term strategic state unless a separate Strategic Management command explicitly saves a preference, such as a default formation.

The foundation implementation slice has no mandatory local building support completion requirement. A follow-up slice may add pre-battle support selection or confirmation using the accepted local support snapshot boundary. Scouting, siege-method selection, or other strategic entry modes require a separate accepted design before becoming battle-entry requirements.

### Local Building Support Snapshot Boundary

When a battle occurs at a city or stronghold, Strategic Management owns the city building facts, reserve soldiers, support charges, costs, cooldown-like state, and building eligibility. The bridge may read those facts through accepted Strategic Management views and translate eligible support into current-battle support snapshots.

Each support snapshot should contain only battle-facing facts for the current battle, such as:

```text
support type
source strategic location id
source building instance id
display name
trigger mode
available charges
optional BattleAnchorId
cost or consumption preview
effect parameters
```

Combat consumes support snapshots as current-battle capabilities. It must not query Strategic Management for building definitions, construction regions, reserve soldier recovery, resource stores, or city economy facts.

The first support interaction starts as pre-battle support selection or confirmation. The contract must preserve a later upgrade path where support entries can appear in the battle HUD and be triggered manually during combat.

### Result Summary

The bridge consumes complete Runtime and settlement facts and creates `StrategicBattleResultSummary`.

The summary should contain:

- bridge session ID and snapshot ID;
- battle outcome and termination reason;
- objective results;
- per-participant result mapped to hero and corps instance IDs;
- hero battle state, such as survived, defeated, retreated, or unavailable-for-report;
- corps result, including remaining strength, routed/scattered/recovering indicators, and recovery entry point;
- rewards, losses, experience, recovery requirements, and strategic-location consequences proposed by settlement;
- local support entries used, unused, failed, and the consumption facts that Strategic Management should apply;
- battle report reference and major failure-attribution candidates.

Strategic Management applies this summary only through a command. The bridge must not directly mutate strategic state.

For Strategic Management-backed battles, the summary is built from Bridge Active Context plus Runtime outcome, event stream, settlement plan, and battle report facts. It must not be reconstructed from legacy `BattleStartRequest` and `BattleResult` as source authority.

### Scene Transition Handoff

Scene transition carries the active bridge session context. It does not validate battle rules or own battle truth.

The long-term handoff payload is the Bridge Active Context or a typed reference to it, not the legacy `BattleSessionHandoff` request/result store. The scene router may write or cancel this payload during root-scene replacement, but it must not fabricate battle results or strategic consequences.

Scene roots may read the active context through bridge-facing APIs. They must not create parallel active Strategic Management battle state, and they must not require `BattleSessionHandoff` to boot a Strategic Management-backed battle.

### Legacy Retirement

The following are legacy bridge artifacts and must not receive new Strategic Management behavior:

- `BattleStartRequest` as the strategic-to-battle authority;
- `BattleResult` as the strategic writeback authority;
- `WorldBattleRequestBuilder`;
- `WorldBattleResultApplier`;
- old world army, garrison, and site-state source kinds;
- static `BattleSessionHandoff` as the long-term bridge session.

Temporary migration adapters may exist only when explicitly scoped by an implementation proposal. They must convert legacy facts into the accepted bridge/snapshot/result contracts and must remain removable.

After the Bridge Active Context cutover, Strategic Management-backed battle entry must not depend on:

- `WorldBattleRequestBuilder` to produce the authoritative battle entry payload;
- static `BattleSessionHandoff` to store active request/result state;
- legacy `BattleResult` to carry Runtime completion back into Strategic Management.

Legacy non-Strategic battle paths may remain only as explicitly scoped compatibility paths until retired.

## Failure Rules

- Missing strategic intent, participant, hero, corps, location, map, deployment zone, or navigation context fails the bridge session explicitly. Missing objective-zone markers do not fail launch for battle kinds that use runtime destination beacons instead of pre-battle objective selection.
- Invalid battle-preparation draft state blocks launch and reports player-readable disabled reasons.
- A launched battle must have a snapshot ID that can be matched to the bridge session.
- Incomplete Runtime output cannot be settled as normal victory or defeat.
- Result/session mismatch blocks Strategic Management writeback.
- Settlement and report cannot derive separate truths from Runtime facts.
- The bridge must not fabricate rewards, losses, corps recovery, location capture, or campaign state changes.
- Scene transition failure cancels or rolls back only the launch/session boundary; it does not produce battle results.

## Acceptance

This architecture is acceptable when:

- new Strategic Management work can request and resolve battles without depending on legacy world army, garrison, or site-management state;
- `BattleStartSnapshot` remains the Runtime input and is not inflated into a scene-transition or strategic-state carrier;
- Runtime results and event streams are the source for settlement, report, and strategic result summaries;
- Strategic Management state changes from battle results happen only through Strategic Management commands;
- scene transition carries Bridge Active Context or a typed reference to it without owning gameplay validation or outcome truth;
- local building support crosses the boundary as support snapshots and usage summaries, not as full city building state or direct Combat-to-Strategic queries;
- legacy request/result builders and appliers are clearly retired as long-term authorities.
