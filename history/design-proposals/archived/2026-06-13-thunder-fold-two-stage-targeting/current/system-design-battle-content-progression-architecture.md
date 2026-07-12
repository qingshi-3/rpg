# Battle Content And Progression Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by defining how content definitions, ability/effect primitives, resources, and progression feed battle without becoming runtime hardcoding.

## Responsibility

This architecture owns:

- ability and effect definition boundaries;
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

Adding a specific skill, equipment effect, or corps trait should usually require only Resource authoring. A system change is needed only when a new effect primitive, target type, or cross-system rule is introduced.

## Configuration Index Boundary

Repository-level gameplay configuration indexes live under `config/`. These files may reference resource ids and resource paths, but they do not contain Godot-authored resources, imported art, scenes, themes, shaders, or SpriteFrames.

`config/` owns stable content indexes and mappings such as first-slice hero-company bindings, default corps attachments, battle unit resource path indexes, and strategic initial roster data. `assets/` owns the actual Godot resources such as `BattleUnitDefinition`, visuals, audio, animation sets, ability effects, and imported art.

Application code may load `config/` indexes to assemble strategic definitions or locate authored resources. Presentation may consume a config-backed resource path index before falling back to broad asset discovery for legacy library content. Runtime must still consume snapshots and ids, not config files or Godot resources directly.

Config files must fail explicitly when required ids, paths, counts, or mapping keys are missing. Hardcoded first-slice roster lists in C# are not a long-term authority; C# may expose typed query wrappers over config data for existing callers.

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
| Teleport to mark | Move the caster to a legal anchor near a live mark after Runtime validates topology, footprint, occupancy, and reservations. |
| Channeled area damage | Apply repeated damage from the caster's current Runtime anchor for a finite duration. |
| Event transfer | Future primitive that redirects a unit, projectile, cast impact, or area event through a mark. This requires explicit event contracts before implementation. |

Presentation may show thrown tags, mark glyphs, trails, afterimages, and impact effects, but it must not decide mark attachment, teleport legality, final actor anchor, damage ticks, or event transfer results.

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
- progression, resource cost, loss, and recovery have clear ownership;
- Runtime consumes snapshots and emits facts instead of owning long-term progression.
