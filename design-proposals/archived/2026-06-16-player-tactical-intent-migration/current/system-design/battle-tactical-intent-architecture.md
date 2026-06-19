# Battle Tactical Intent Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by separating battlefield business semantics from reusable tactical domain capabilities.

The first implementation slice applies this architecture to enemy-controlled battle groups only. Player-commanded group movement remains under the existing player command and battle-plan path until a later accepted migration slice.

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
- player command validation or the current player-commanded movement implementation;
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

Target objects may originate from authored semantic markers, battle-start snapshots, deployment/objective zones, scenario defaults, enemy group definitions, runtime perception, or combat-zone observation. The target object catalog is a query surface, not a commander. It does not choose intent by itself.

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

### AI Intent Plan

An tactical intent plan is a command-like input for autonomous groups. It describes a verb applied to target selectors plus constraints.

The first slice uses enemy group plans such as:

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
```

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

Business layers may configure these capabilities through intent plans and profiles. They must not duplicate them with scenario-specific movement code.

## Scenario And Business Layer Boundary

Battle scenarios, strategic locations, enemy factions, and encounter definitions are business semantics. They may provide:

- authored or marker-derived target objects;
- default enemy group intent plans;
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

Intent resolution priority is:

```text
encounter explicit enemy group intent
-> enemy group or archetype default intent
-> battle scenario default intent
-> safe fallback intent
```

The fallback must be explicit and diagnosable. It may hold, defend a known region, or advance toward a stable opposing-side objective. It must not silently chase a volatile runtime observation.

## Enemy First-Slice Scope

The first implementation slice connects enemy-controlled battle groups to Tactical Intent.

In scope:

- build a target object catalog from existing deployment/objective markers, battle groups, and runtime observations;
- attach an enemy group intent plan from encounter/group defaults or scenario fallback;
- resolve enemy non-engaged movement through the intent plan and stable target objects;
- keep runtime-observed player clusters as tactical observations, not default long-term movement targets;
- allow temporary or cluster-derived targets only when the active intent explicitly selects them or when fallback policy permits them;
- add low-noise diagnostics for intent selection, target resolution, target retention, retarget rejection, and fallback.

Out of scope:

- migrating player-commanded movement or battle-plan objective handling;
- changing Presentation movement interpolation;
- replacing the local combat slot solver;
- adding new Godot authoring UI for all intent definitions;
- adding campaign-persistent enemy strategic AI.

## Runtime State

Runtime may store battle-scoped tactical intent state:

| State | Owner |
|---|---|
| Target object catalog snapshot | Battle Runtime observation layer |
| Enemy group active intent plan | Battle-group commander state |
| Resolved primary target object id | Battle-group commander state |
| Last accepted target object id and retarget time/tick | Battle-group commander state |
| Leash and fallback resolved target ids | Battle-group commander state |
| Intent reason and degradation reason | Diagnostics and report attribution |

These facts are battle runtime state and are discarded after result emission unless a later accepted runtime-resume boundary says otherwise.

## Contracts

- Presentation consumes runtime movement events only. It must not read or execute tactical intent.
- Runtime validation remains the final authority for movement, attack, occupancy, reservations, damage, and defeat.
- Target objects are references and constraints, not movement commits.
- A volatile tactical observation must not replace a stable movement target unless the active intent and retarget policy allow it.
- Target retargeting must happen at decision boundaries or configured replan boundaries, not every render frame or every fixed simulation tick.
- Enemy intent may override scenario defaults. Scenario defaults may not override explicit enemy group intent.
- Player group movement remains controlled by existing player commands and accepted battle plans in the first slice.

## Failure Rules

- Missing explicit enemy group intent: use enemy archetype default, then scenario default, then safe fallback.
- Selector resolves no valid target: keep the retained valid target if allowed; otherwise run fallback intent.
- Retarget requested before cooldown: reject the retarget, keep the retained target, and emit a low-noise reason.
- Target becomes invalid: clear only that resolved target and use fallback; do not clear the whole battle-group commander state.
- Leash exceeded: stop pursuit, return to the fallback target, or hold according to the active intent.
- Runtime-observed cluster is volatile: it may drive local combat or explicit hunt/harass intents, but cannot become default non-engaged movement target.

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
- player-commanded movement remains unchanged in the first implementation slice;
- enemy movement visual continuity improves without Presentation changes;
- diagnostics can explain enemy intent, target retention, fallback, and retarget suppression.
