# Battle Content And Progression Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by defining how content definitions, ability/effect primitives, resources, and progression feed battle without becoming runtime hardcoding.

## Responsibility

This architecture owns:

- ability and effect definition boundaries;
- battle skill configuration versus reusable code boundaries;
- battle skill identity, snapshot, effect execution, and availability contracts;
- combat content resourceization;
- hero, corps, equipment, and armament progression inputs;
- resource source, sink, conversion, cap, loss, and recovery loops.

## Does Not Own

This architecture does not own:

- individual skill content balance;
- exact UI layout;
- runtime movement legality or topology;
- final settlement writeback mechanics;
- long-term state schema details.
- persistent hero or battle-group skill loadout mutation rules, except for the battle snapshot contract that consumes those assignments.

## Ability And Effect Definitions

Abilities and effects are content definitions. Runtime instantiates execution state from snapshots; it does not hardcode individual content rules.

Definitions / Content owns:

- `Skill`: display text, tags, channel availability, and content identity.
- `Cost`: mana, limited use, battle resource, condition, or other cost rules.
- `Cooldown`: cooldown timing and reset boundary.
- `Targeting`: target kind, range, valid target rules, area rules.
- `Action`: cast time, impact timing, recovery timing, animation/event tags, and default action-lock behavior.
- `Interrupt Policy`: default action-lock behavior plus explicit traits for exceptions such as canceling basic attack recovery, interrupting another skill, releasing instantly, or releasing without occupying the caster.
- `Effect`: damage, healing, control, movement, summon, shield, morale, resource, or other effect primitives.
- `Tag`: profession, combat class, form, element, faction, equipment, city, origin, or fantasy hook.
- `Modifier`: stat, behavior, cooldown, cost, target, settlement, or report-explanation modifier.

Layer rules:

- Domain saves unlocks, assignments, levels, equipment, and long-term state. It does not duplicate full definitions.
- Application validates whether abilities and effects may enter a snapshot.
- Runtime handles cooldown, cost payment, hit/application, effect state, and emitted events.
- UI displays definitions and availability, but it does not calculate final battle truth.
- Infrastructure loads resources and reports missing or invalid references.
- Skill definitions, UI availability, Runtime command validation, AI release decisions, event emission, and reports consume the same content snapshot. No layer may hardcode a second skill definition as an alternate authority.
- Effects are source-agnostic primitives. Skills, basic attacks, equipment, relics, terrain, city support, and later passive effects should all enter Runtime through effect payloads instead of bespoke damage or healing paths.

Configuration owns designer-facing composition: stable skill id, display text, icon, tags, command channel, type, range, targeting, area, direction, timing, interrupt policy, cost, cooldown, charges, effect list, and presentation profile. It may also reference granted skill ids from hero, battle-group, equipment, or progression state.

Code owns reusable behavior interfaces and new runtime semantics: effect executors, cost/cooldown/targeting rule families, condition rule implementations, and runtime capability interfaces such as damage receiver, resource pool, tag container, status holder, shield, spatial anchor, and damage modifier provider.

The preferred battle skill chain is:

```text
SkillDefinitionResource
-> BattleSkillSnapshotCompiler
-> BattleSkillSnapshot
-> Runtime AbilityController
-> EffectExecutor / Runtime capability components
-> BattleEventStream and reports
```

Effects should not inspect concrete target types. A fire damage effect, for example, creates a typed damage request; target-side components or capabilities modify or receive that request when appropriate. This keeps skills reusable across units, buildings, terrain objects, summons, and future battlefield entities.

Adding a specific skill, equipment effect, or corps trait should usually require Resource authoring plus existing reusable effect primitives. A system change is needed only when a new effect primitive, target type, cost rule, cooldown rule, targeting rule, or cross-system mechanic is introduced.

## Configuration Index Boundary

Repository-level gameplay configuration indexes live under `config/`. These files may reference resource ids and resource paths, but they do not contain Godot-authored resources, imported art, scenes, themes, shaders, or SpriteFrames.

`config/` owns stable content indexes and mappings such as first-slice battle-group bindings, default corps attachments, battle unit resource path indexes, and strategic initial roster data. `assets/` owns the actual Godot resources such as `BattleUnitDefinition`, visuals, audio, animation sets, ability effects, and imported art.

Application code may load `config/` indexes to assemble strategic definitions or locate authored resources. Presentation may consume a config-backed resource path index before falling back to broad asset discovery for legacy library content. Runtime must still consume snapshots and ids, not config files or Godot resources directly.

Config files must fail explicitly when required ids, paths, counts, or mapping keys are missing. Hardcoded first-slice roster lists in C# are not a long-term authority; C# may expose typed query wrappers over config data for existing callers.

## Skill Identity And Assignment Authority

`SkillDefinitionId` is stable content identity. It must not be generated from hero id, battle-group id, corps id, battle id, map id, resource path, display name, first-slice staging labels, or current runtime ownership. Changing a hero, moving a skill to another battle group, renaming a resource path, or retiring a vertical-slice label must not change the skill definition id.

Strategic Management owns persistent hero or battle-group skill grants and loadout slots. A grant or slot points to one `SkillDefinitionId` and may carry level, modifiers, source, and slot facts. It does not duplicate full skill definitions.

Separate ids carry separate responsibilities:

| Id | Responsibility |
|---|---|
| `SkillDefinitionId` | Stable content identity for definitions, unlocks, reports, balance references, and lookup. |
| `GrantedSkillId` or `LoadoutSlotId` | Persistent strategic ownership entry for a hero, battle group, equipment, or progression source. |
| `SkillCastId` or `CommandId` | Runtime-only correlation id for one submitted cast command. |
| `SkillActionId` | Runtime-only id for one accepted action lifecycle. |
| `EffectInstanceId`, `MarkId`, `StatusId`, `SummonId`, or `ProjectileId` | Runtime-only ids for objects produced by a cast. |

Definition lookup uses `SkillDefinitionId`. Ownership, cooldown, charge, and per-slot modifier state should key off `GrantedSkillId` or `LoadoutSlotId` when the same definition can appear in multiple slots or with different modifiers. UI behavior comes from targeting, input, cost, cooldown, and presentation traits in the compiled snapshot, not from comparing concrete skill ids.

## Typed Authoring Resource And Snapshot Boundary

Godot-authored skill content should use typed C# Resource inheritance where it improves editor authoring and compile-time shape:

```text
BattleSkillDefinitionResource : Resource
BattleSkillEffectResource : Resource
  DamageSkillEffectResource
  CreateMarkSkillEffectResource
  TeleportToMarkSkillEffectResource
  ChanneledAreaDamageSkillEffectResource
BattleSkillCostRuleResource : Resource
BattleSkillCooldownRuleResource : Resource
BattleSkillTargetingRuleResource : Resource
BattleSkillPresentationProfileResource : Resource
```

Concrete resources should be C# `[GlobalClass]` partial classes so the Godot editor can create and assign them. Authoring Resources are declarative content, not Runtime behavior owners. Runtime consumes snapshots and ids, not Godot Resource instances directly, so Resource polymorphism ends at the compiler boundary.

Each concrete Resource compiles into a typed, serializable snapshot payload such as:

```text
DamageSkillEffectResource
-> DamageSkillEffectSnapshot
-> DamageSkillEffectExecutor
```

Snapshot payloads are the Runtime contract. They carry named fields such as `BaseDamage`, `DamageType`, `MarkLifetimeSeconds`, or `LandingRadius` instead of overloading broad fields such as `Amount`.

Runtime behavior should use polymorphic executors and capability interfaces. Use abstract base classes where Godot serialization and Inspector authoring need a common Resource type. Use interfaces for runtime services, capability queries, and test seams. Enums are acceptable for small closed sets such as command channel or area shape, but they must not become the main extension mechanism for effects, costs, targeting, or presentation behavior.

## Skill Action And Effect Definition Boundary

A skill definition describes what may be released. It does not decide whether the actor is currently allowed to release it.

Skill definitions provide:

- identity, display, tags, and report labels;
- command channel and caster eligibility;
- targeted or non-targeted targeting mode;
- range and target-lock requirements;
- cost, cooldown, charge, limited-use, or battle-resource rules;
- action timing such as cast, impact, and recovery;
- default interrupt policy and explicit interrupt traits;
- one or more effect payload references.

Runtime actor behavior and validation decide when an accepted skill order can start. Runtime action execution owns cast, impact, recovery, interruption, and failure timing. Runtime effect execution owns applying the resulting effect payloads.

Basic attacks should be represented as the same shape over time: an action with attack windup, one impact point, recovery, and a basic-attack effect payload. Basic attacks may keep a narrower implementation during migration, but they must not become a second long-term effect authority.

## Cost, Cooldown, And Availability Ownership

Skill configuration may define:

- resource costs such as mana, stamina, battle resource, limited per-battle use, or charges;
- cooldown duration, cooldown category, shared cooldown group, charge recovery time, and whether the skill starts on cooldown;
- activation gates such as required tag, forbidden tag, required live mark, required equipment class, or required battle state;
- cooldown start timing such as command accepted, cast start, effect release, or successful completion;
- refund policy for failed, interrupted, canceled, or invalidated casts.

Runtime owns authoritative availability state: current resource pools, cooldown timers, charge counts, pending/casting/recovery/interrupted/completed action state, cost payment, refunds, rejection reasons, and emitted availability events.

The default mature model supports per-granted-skill cooldown, optional shared cooldown groups, charge-based skills, per-battle use limits, and mana cost. A global cooldown is allowed only if a later command-feel proposal accepts it. The hero-led light RTS direction should not inherit an MMO-style global cooldown by default.

UI may display cooldown, resource shortage, charge recovery, and disabled reasons from Runtime events and snapshot traits. It must not decide final skill legality.

## Effect Definition, Executor, And Runtime Instance Boundary

Skill effects are separated into definition, execution, and runtime-instance layers.

Effect definitions are reusable, shared, and stateless authoring assets. A concrete Resource may expose typed parameters for the Godot Inspector and compile into a snapshot payload, but it must not store per-cast or per-target facts such as current target, elapsed time, remaining ticks, hit count, generated ids, or mutable damage results. Godot Resources are shared by default, so mutable runtime state belongs outside the Resource.

Effect executors are reusable runtime code. An executor receives a cast/action context, typed payload, target or capability resolver, and event writer. It is not created as a Godot Node per unit or per cast. Immediate effects such as damage, healing, resource changes, instant displacement, or instant mark consumption create execution requests and authoritative events, then finish without spawning an effect node.

Runtime effect instances are created only when the effect has lasting gameplay state, a lifecycle, or a world representation. Statuses, buffs, debuffs, auras, marks, persistent zones, channeled effects, projectiles, summons, and similar effects may create runtime records keyed by `EffectInstanceId`, `StatusId`, `MarkId`, `ProjectileId`, or `SummonId`. A Godot Node or scene may mirror them for presentation, collision, physics, or visible actors, but it must not become the gameplay source of truth.

## Ability Spatial Contracts

Ability spatial contracts should support these extension points:

| Contract | Purpose |
|---|---|
| Target mode | Unit target, cell target, direction target, or self-centered execution. |
| Direction mode | Free angle, 8-way snap, 4-way snap, or forward arc. |
| Area shape | Single actor, single cell, line, cone, circle radius, or grid radius. |
| Range metric | Square-grid range rule used by the selected target and area mode. |
| Resolution source | Actor facts and grid facts owned by Runtime, not UI or presentation-only collision callbacks. |

The first square-grid realtime implementation only needs actor-target basic attacks and the contract fields required to avoid hardcoding future skills into the wrong model.

## Spatial Mark And Teleport Effect Boundary

High-tier spatial hero skills may create temporary Runtime marks and later consume or reference them. A mark is a battle-only effect state with owner battle group, source actor, source skill, source command, lifetime, and either a ground anchor or an attached target actor. Marks are content-driven effects, but Runtime owns whether the mark exists, where it currently resolves, and whether a later skill may use it.

Content may define these first spatial effect primitives:

| Primitive | Runtime Meaning |
|---|---|
| Mark creation | Create or replace a battle-only coordinate mark at a legal ground anchor or on a valid actor. |
| Teleport to mark | Move the caster to a legal anchor near the selected live mark after Runtime validates mark ownership, content-defined landing radius, topology, footprint, occupancy, and reservations. |
| Channeled area damage | Apply repeated damage from the caster's current Runtime anchor for a finite duration. |
| Event transfer | Future primitive that redirects a unit, projectile, cast impact, or area event through a mark. This requires explicit event contracts before implementation. |

Presentation may show thrown tags, mark glyphs, trails, afterimages, and impact effects, but it must not decide mark attachment, teleport legality, final actor anchor, damage ticks, or event transfer results.

Spatial skill definitions may expose mark-selection and landing-radius parameters through snapshots. For the first Thunder Mark Fold implementation, the landing radius is 3 square-grid cells from the selected mark anchor. Presentation may use that snapshot value to render legal landing candidates, but Runtime remains the final authority for whether the selected mark and destination are still valid when the command is accepted and released.

## Single Skill Implementation Authority

The long-term system has one authoritative skill definition and execution path:

```text
SkillDefinitionResource
-> BattleSkillSnapshotCompiler
-> BattleSkillSnapshot
-> Runtime AbilityController
-> EffectExecutor / Runtime capability components
-> BattleEventStream and reports
```

Other ability-definition paths may exist only as explicit migration inputs before the accepted implementation slice removes or converts them. They must not remain as fallbacks, parallel feature paths, or alternate authorities. Missing resource definitions, invalid effect payloads, or unsupported rule types should fail explicitly with diagnostics instead of falling back to a legacy skill model.

## Resource And Progression Flow

Resource and progression flow must be modelable before code:

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

Negative feedback entry points belong in capacity, maintenance, recovery cost, training efficiency, workshop efficiency, post-battle losses, and city defense pressure. This document does not define balance values.

## First-Phase Content Scope

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

Explicit non-goals:

- AP, TurnSystem, or old tactical chess loop;
- pure post-deployment auto-battle;
- individual soldier long-term growth;
- one hero with multiple main corps;
- large-scale RTS box selection and high-frequency micro;
- full diplomacy or government simulation;
- public order, intelligence, or city damage as first-phase core city attributes;
- non-city locations inheriting the full city model.

## Acceptance

This architecture is acceptable when:

- specific skills and equipment effects can usually be added as resources;
- new runtime primitives require explicit architecture proposals;
- skill identity, grants/loadouts, cost, cooldown, effect payloads, effect executors, and runtime instances have separate ownership;
- UI and reports consume skill snapshot traits and Runtime events instead of hardcoding concrete skill behavior;
- legacy or first-slice ability-definition paths are removed or converted instead of retained as fallbacks;
- progression, resource cost, loss, and recovery have clear ownership;
- Runtime consumes snapshots and emits facts instead of owning long-term progression.
