# Battle Tactical Intent Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by separating battlefield business semantics from reusable tactical domain capabilities.

The next migration slice applies this architecture to both enemy-controlled and player-commanded battle groups. Player battle preparation, deployment, and command UI remain upstream intent-input producers; Runtime Tactical Intent owns how accepted intent input becomes stable movement and local-combat direction.

## Responsibility

Battle Tactical Intent owns the architecture contract for:

- battle target objects that can be referenced by tactical intent, commands, scenarios, diagnostics, and reports;
- tactical intent plans that describe what a battle group wants to do to one or more target objects;
- target selectors that resolve business-facing intent input into runtime target objects;
- target stability, retarget cooldowns, pursuit bounds, leashes, fallback intent, and local-combat entry gates;
- the boundary between reusable tactical capabilities and scenario-specific business defaults.

## Does Not Own

Battle Tactical Intent does not own:

- movement legality, topology, occupancy, reservations, or pathfinding truth;
- damage, HP, defeat, settlement, or campaign writeback;
- Presentation movement interpolation, animation, selection, hover, or debug drawing;
- player command validation, deployment interaction, or battle-preparation UI;
- map marker authoring, although it may consume marker-derived target objects;
- hardcoded per-scenario enemy behavior.

## Domain Concepts

### Battle Target Object

A battle target object is a runtime or snapshot-scoped tactical noun. It is the thing an intent can reference.

Initial target kinds include:

| Kind | Examples | Typical Use |
|---|---|---|
| Actor | hero, corps actor, summoned unit, siege unit | attack, protect, pursue, suppress |
| Group | player vanguard, enemy reserve, defender garrison group | assault, pressure, delay, avoid |
| Destination Beacon | player-placed runtime command beacon | move, assault, regroup destination |
| Region | deployment zone, objective zone, fallback line, retreat zone | move, hold, regroup, defend |
| Map Feature | gate, breach, bridge, pass, high ground, flank route | assault, block, defend, route |
| Structure | wall segment, tower, altar, camp, control point | destroy, capture, protect |
| Tactical Observation | player cluster, local combat zone, threat source, open flank | local reaction, not default long-term intent |

Each target object must expose:

```text
TargetObjectId
TargetKind
OwnerSide or RelevantSide
Anchor, bounds, or actor/group reference
Tags and role
Allowed uses: movable-to, attackable, defendable, capturable, protectable
Priority
Lifetime: fixed, phase-scoped, or runtime-observed
Stability: stable, sticky, volatile
Invalidation facts
```

Target objects may originate from authored semantic markers, battle-start snapshots, deployment/objective zones, scenario defaults, enemy group definitions, accepted runtime destination beacons, runtime perception, or combat-zone observation. The target object catalog is a query surface, not a commander. It does not choose intent by itself.

### Target Selector

A target selector is a business-facing reference that resolves to one or more battle target objects.

Selectors should use stable semantic names or predicates, not direct runtime actor ids except for commands that explicitly target an actor. Examples:

```text
CityGate
BreachPoint
PlayerVanguard
NearestPlayerBackline
DefensiveLine:inner
EnemyCoreObjective
CurrentLocalCombatZone
```

Selector resolution belongs to tactical-domain services. Scenario and encounter configuration may choose selectors, but must not implement target scoring, target stickiness, movement target generation, or local-combat transitions directly.

### Tactical Intent Plan

A tactical intent plan is a command-like input for a battle group. It describes a verb applied to target selectors plus constraints.

Runtime should use a side-neutral carrier such as:

```text
TacticalIntentPlan
  IntentId
  PrimaryTargetSelector
  SecondaryTargetSelectors
  StyleProfileId
  LeashSelector
  RetargetPolicyId
  EngagementPolicyId
  FallbackIntentId
  IntentSource
```

Enemy configuration, scenario defaults, accepted runtime destination beacon commands, future accepted player battle plans, and later accepted runtime commands may all produce tactical intent plans. Source policy controls priority and overwrite rules; the target-resolution, stability, region movement, and local-combat capabilities are shared.

Initial intent ids should remain small and composable:

| Intent | Meaning |
|---|---|
| AssaultTarget | Move toward and pressure a selected target object. |
| DefendTarget | Hold a target object or bounded region. |
| SallyOut | Leave a defensive context to pressure a selected outside target with a leash. |
| HoldLine | Maintain a line or area and avoid deep pursuit. |
| HarassAndReturn | Engage briefly, then return to a fallback target. |
| ProtectTarget | Stay near and prioritize threats to a protected target. |
| RetreatToTarget | Break contact and move toward a retreat target. |

Intent does not equal scenario. A siege-defense scenario may default to `DefendTarget`, but a specific enemy group may use `SallyOut`, `HarassAndReturn`, or `AssaultTarget`. Scenario defaults are fallback inputs, not behavior authority.

### Tactical Capability

Reusable tactical capabilities are domain services. They know battle movement, target facts, and stability rules, but they do not know story, enemy fantasy, or concrete scenario business.

Initial capabilities include:

- target catalog construction;
- selector resolution;
- target lock and sticky target retention;
- retarget cooldown and invalidation;
- leash and pursuit validation;
- engagement and disengagement gating;
- fallback intent selection;
- region movement goal generation;
- local-combat target and slot selection after engagement;
- diagnostics for why a group chose, retained, rejected, or replaced a target.

Business layers may configure these capabilities through intent plans and profiles. They must not duplicate them with scenario-specific, enemy-specific, or player-specific movement code.

## Scenario And Business Layer Boundary

Battle scenarios, strategic locations, enemy factions, and encounter definitions are business semantics. They may provide:

- authored or marker-derived target objects;
- default enemy group intent plans and player destination-beacon command intent input;
- target selectors;
- style profiles;
- scenario fallback rules;
- content-facing names and tags.

They must not directly implement:

- per-tick target-region rebuilds;
- movement next-step selection;
- pathfinding or route hint construction;
- local-combat slot scoring;
- target stickiness, cooldowns, leashes, or fallback state machines.

Enemy intent resolution priority is:

```text
encounter explicit enemy group intent
-> enemy group or archetype default intent
-> battle scenario default intent
-> safe fallback intent
```

Player intent resolution priority is:

```text
accepted runtime player command
-> accepted destination beacon command or future battle-start player battle plan
-> player-scoped autonomous fallback when no player command is active
-> safe fallback intent
```

The fallback must be explicit and diagnosable. It may hold, defend a known region, or advance toward a stable opposing-side objective. It must not silently chase a volatile runtime observation.

## Player Migration Scope

The enemy first slice connected enemy-controlled battle groups to Tactical Intent. The player migration slice connects player-commanded battle groups to the same Runtime Tactical Intent path.

In scope:

- generalize AI-named intent DTOs, state fields, sources, and policies into side-neutral tactical intent names;
- convert accepted destination beacon commands into player-sourced tactical intent plans during battle;
- keep battle preparation and deployment as upstream input producers, not Tactical Intent responsibilities;
- resolve both player and enemy non-engaged movement through active tactical intent and stable target objects, destination beacons, or regions;
- remove the old player-only objective-anchor movement path as a runtime authority after equivalent beacon tactical intent coverage exists;
- keep runtime-observed player clusters as tactical observations, not default long-term movement targets;
- allow temporary or cluster-derived targets only when the active intent explicitly selects them or when fallback policy permits them;
- add low-noise diagnostics for intent selection, target resolution, target retention, retarget rejection, and fallback.

Out of scope:

- changing deployment UI operations or battle-preparation selection UX;
- changing Presentation movement interpolation;
- replacing the local combat slot solver;
- adding new Godot authoring UI for all intent definitions;
- adding campaign-persistent enemy strategic AI.

## Runtime State

Runtime may store battle-scoped tactical intent state:

| State | Owner |
|---|---|
| Target object catalog snapshot | Battle Runtime observation layer |
| Battle group active tactical intent plan | Battle-group commander state |
| Resolved primary target object id | Battle-group commander state |
| Last accepted target object id and retarget time/tick | Battle-group commander state |
| Leash and fallback resolved target ids | Battle-group commander state |
| Intent source and command-source state | Battle-group commander state |
| Intent reason and degradation reason | Diagnostics and report attribution |

These facts are battle runtime state and are discarded after result emission unless a later accepted runtime-resume boundary says otherwise.

## Contracts

- Presentation consumes runtime movement events only. It must not read or execute tactical intent.
- Runtime validation remains the final authority for movement, attack, occupancy, reservations, damage, and defeat.
- Target objects are references and constraints, not movement commits.
- A volatile tactical observation must not replace a stable movement target unless the active intent and retarget policy allow it.
- Target retargeting must happen at decision boundaries or configured replan boundaries, not every render frame or every fixed simulation tick.
- Enemy intent may override scenario defaults. Scenario defaults may not override explicit enemy group intent.
- Player group movement is controlled by accepted player commands, especially destination beacon commands, expressed as player-sourced tactical intent. Tactical AI may execute inside that scope but must not rewrite player intent.
- Deployment code may create placement and formation facts, but must not own target stickiness, movement target generation, retarget cooldowns, local-combat transitions, or pathing decisions.

## Failure Rules

- Missing explicit enemy group intent: use enemy archetype default, then scenario default, then safe fallback.
- Selector resolves no valid target: keep the retained valid target if allowed; otherwise run fallback intent.
- Retarget requested before cooldown: reject the retarget, keep the retained target, and emit a low-noise reason.
- Target becomes invalid: clear only that resolved target and use fallback; do not clear the whole battle-group commander state.
- Leash exceeded: stop pursuit, return to the fallback target, or hold according to the active intent.
- Runtime-observed cluster is volatile: it may drive local combat or explicit hunt/harass intents, but cannot become default non-engaged movement target.
- Player group has no active destination beacon or player command: hold or use a player-scoped autonomous fallback only when fallback is explicitly allowed.

## Diagnostics

Diagnostics must answer:

- which intent plan was selected and from which source;
- which target selector resolved to which target object;
- whether the target was retained, replaced, rejected, or invalidated;
- whether movement is pursuing a stable target, local combat target, or fallback target;
- whether a volatile observation was ignored because intent did not authorize it.

Diagnostics should be low-noise and tied to meaningful changes, not emitted every fixed tick.

## Acceptance

This architecture is acceptable when:

- battle scenarios provide target objects and default intent, not hardcoded behavior;
- enemy group intent can be configured independently from scenario type;
- a siege-defense enemy can either defend, sally out, harass, retreat, or assault based on intent input;
- non-engaged enemy movement uses stable intent-owned targets instead of chasing moving actors or frequently rebuilt volatile clusters by default;
- player-commanded movement and enemy movement consume the same tactical intent, selected-beacon or selected-region, group-action-zone, and movement path;
- player deployment remains an input workflow and does not become Tactical Intent ownership;
- the old player-only objective movement authority is removed or reduced to a compatibility adapter that does not own new behavior;
- enemy movement visual continuity improves without Presentation changes;
- diagnostics can explain player and enemy intent source, target retention, fallback, and retarget suppression.
