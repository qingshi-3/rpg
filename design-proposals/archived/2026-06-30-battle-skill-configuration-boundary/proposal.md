# Battle Skill Configuration Boundary Proposal

Status: Accepted

## Relationship Metadata

- Requirement Id: BSKILL-CFG-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/combat-command/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
  - `system-design/battle-content-progression-architecture.md`
  - `system-design/strategic-management-system-architecture.md`
- Related Implementation Proposals:
  - Pending

## Current Architecture

The accepted architecture already says battle skills and effects are content definitions, Runtime consumes snapshots, and specific skills should usually be added through resource authoring. The current implementation still keeps first-slice hero skill definitions in C# and binds several UI and presentation flows to concrete skill ids.

This creates related design gaps:

- skill authoring is not yet separated from reusable effect code;
- assigning a skill to a hero or battle group does not have one clear long-term authority;
- UI and presentation still need skill-specific branches instead of reading targeting, input, and presentation traits from the compiled skill snapshot;
- the project currently has multiple ability/skill definition shapes, which risks creating two authoritative implementations for the same runtime responsibility.

## Accepted Direction

Skill configuration does not mean "new skills require no code." The project should distinguish content composition from new runtime semantics.

Configuration owns stable, designer-facing composition:

- skill id, display text, icon, tags, channel, type, range, targeting, area, direction, timing, interrupt policy, cost, cooldown, charges, effect list, and presentation profile;
- hero, battle-group, equipment, or progression references to granted skill ids;
- effect-resource parameters such as base damage, element, duration, radius, mark lifetime, or landing radius.

Code owns reusable behavior interfaces and new runtime semantics:

- effect executors such as damage, healing, displacement, summon, mark creation, teleport-to-mark, and channeled area damage;
- cost, cooldown, targeting, and condition rule implementations when a new rule family is introduced;
- runtime capability/component interfaces such as damage receiver, resource pool, tag container, status holder, shield, spatial anchor, and damage modifier provider.

The preferred model is:

```text
SkillDefinitionResource
-> compiled BattleSkillSnapshot
-> Runtime AbilityController validates cost/cooldown/action state
-> EffectExecutor applies effect payloads
-> target runtime components/capabilities modify or receive the result
-> Runtime event stream records authoritative facts
```

Effects should not inspect concrete target types. For example, a fire damage effect creates a typed damage request; target-side components or capabilities modify that request when appropriate. This keeps skills reusable across units, buildings, terrain objects, summons, and future interactable battlefield entities without the skill knowing their concrete classes.

## C# Type System Direction

The skill system should use C# language features deliberately instead of flattening behavior into string keys, broad dictionaries, or large enum/switch blocks.

Godot-authored skill content should prefer typed Resource inheritance:

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

Concrete resources should be C# `[GlobalClass]` partial classes so the Godot editor can create and assign them. Shared bases should expose only the common contract; concrete subclasses own their typed exported parameters. This keeps authoring inspector-friendly while preserving compile-time types.

Authoring Resources are declarative content, not Runtime behavior owners. Unlike a small standalone Godot ability pattern where an ability Resource may implement `Activate()`, this project keeps execution out of Resources because battle Runtime must consume snapshots and ids, not Godot resources directly. Resource polymorphism ends at the compiler boundary.

Each concrete Resource should compile into a typed, serializable snapshot payload:

```text
DamageSkillEffectResource
-> DamageSkillEffectSnapshot
-> DamageSkillEffectExecutor
```

The snapshot payload is the Runtime contract. It carries authored parameters in named fields, such as `BaseDamage`, `DamageType`, `MarkLifetimeSeconds`, or `LandingRadius`, instead of overloading broad fields such as `Amount`.

Runtime behavior should use polymorphic executors and capability interfaces:

```text
IBattleSkillEffectExecutor<TPayload>
IBattleSkillCostRule
IBattleSkillCooldownRule
IBattleSkillTargetingRule
IDamageReceiver
IDamageModifierProvider
IStatusContainer
IResourcePool
ISpatialAnchor
```

Use abstract base classes where Godot serialization and Inspector authoring need a common Resource type. Use interfaces for runtime services, capability queries, and test seams. Do not export interface-typed fields directly as the main authoring path unless Godot editor support is proven in the project; exported content should stay Resource-based and strongly typed.

Enums remain useful for small closed sets, such as command channel or area shape. They should not become the main extension mechanism for effects, costs, targeting, or presentation behavior. Adding a new effect family should usually add a concrete Resource type, a typed snapshot payload, and a matching executor, not add another meaning to a shared `Amount` field and another branch in a central switch.

## Effect Definition, Executor, And Runtime Instance Boundary

Skill effects should be separated into definition, execution, and runtime-instance layers.

Effect definitions are reusable, shared, and stateless authoring assets. A `DamageSkillEffectResource`, `BurnSkillEffectResource`, or similar concrete Resource may expose typed parameters for the Godot Inspector and compile into a snapshot payload, but it must not store per-cast or per-target facts such as current target, elapsed time, remaining ticks, hit count, generated ids, or mutable damage results. Godot Resources are shared by default, so mutable runtime state belongs outside the Resource.

Effect executors are reusable runtime code. A `DamageSkillEffectExecutor` or similar executor receives a cast/action context, typed payload, target or capability resolver, and event writer. It is not created as a Godot Node per unit or per cast. Immediate effects such as damage, healing, resource changes, instant displacement, or instant mark consumption should create execution requests and authoritative events, then finish without spawning an effect node.

Runtime effect instances are created only when the effect has lasting gameplay state, a lifecycle, or a world representation. Statuses, buffs, debuffs, auras, marks, persistent zones, channeled effects, projectiles, summons, and similar effects may create `EffectInstanceId`, `StatusId`, `MarkId`, `ProjectileId`, or `SummonId` records that point back to the source definition, command, and action ids. These records may be plain C# Runtime state objects. A Godot Node or scene may mirror them for presentation, collision, physics, or visible actors, but it must not become the gameplay source of truth.

The guiding rule is: effect definitions are reused, effect executors are reused, each cast creates only execution context and events by default, and runtime instances exist only for durable gameplay state or required world entities.

## Stable Skill Identity Direction

Skill identity must be stable content identity, not a derived runtime key.

`SkillDefinitionId` should be authored as a durable content id. It must not be generated from hero id, battle-group id, corps id, battle id, map id, resource path, display name, first-slice staging labels, or current runtime ownership. Changing a hero, moving a skill to another battle group, renaming a scene/resource path, or retiring a vertical-slice label must not change the skill definition id.

Separate ids should carry separate responsibilities:

| Id | Responsibility | Stability |
|---|---|---|
| `SkillDefinitionId` | Stable content identity for definitions, unlocks, reports, balance references, and lookup. | Long-lived across saves and content migrations. |
| `GrantedSkillId` or `LoadoutSlotId` | A hero, battle group, equipment, or progression-owned granted skill entry. It can point to one `SkillDefinitionId` plus level, modifiers, source, and slot facts. | Stable within persistent ownership state. |
| `SkillCastId` or `CommandId` | One submitted cast command during one battle. | Runtime-only correlation id. |
| `SkillActionId` | One action lifecycle created by an accepted cast. | Runtime-only correlation id. |
| `EffectInstanceId`, `MarkId`, `StatusId`, `SummonId`, or `ProjectileId` | Runtime objects produced by a cast. | Runtime-only instance id; each instance points back to source command/action/definition ids. |

Runtime lookups should use the narrowest id that matches the question:

- definition lookup uses `SkillDefinitionId`;
- ownership and cooldown lookup should use `GrantedSkillId` or `LoadoutSlotId` when the same definition can appear in multiple slots or with different modifiers;
- event attribution records source definition, command, action, and effect-instance ids separately;
- UI behavior comes from targeting, input, cost, cooldown, and presentation traits in the compiled snapshot, not from comparing concrete skill ids.

Human-readable id strings are allowed for content authoring, but they must be treated as stable keys. If a shipped id must change, the project needs an explicit migration alias, not broad string replacement. Runtime-generated ids should use monotonic sequence values or scoped unique ids instead of composing from mutable display/ownership facts or coordinates.

## Cost, Cooldown, And Availability Direction

Skill availability should follow mature action-game and RPG ability patterns without overbuilding a generic rules engine.

Configuration may define:

- resource costs such as mana, stamina, battle resource, limited per-battle use, or charges;
- cooldown duration, cooldown category, shared cooldown group, charge recovery time, and whether the skill starts on cooldown;
- activation gates such as required tag, forbidden tag, required live mark, required weapon/equipment class, or required battle state;
- cooldown start timing, such as on command accepted, on cast start, on effect release, or on successful completion;
- refund policy for failed, interrupted, canceled, or invalidated casts.

Runtime owns the authoritative availability state:

- current resource pools;
- cooldown timers and charge counts;
- pending, casting, recovery, interrupted, and completed action state;
- cost payment, refunds, and rejection reasons;
- emitted events for HUD, reports, diagnostics, and settlement attribution.

The default first mature model should support per-granted-skill cooldown, optional shared cooldown groups, charge-based skills, per-battle use limits, and mana cost. A global cooldown is allowed only if a later command-feel proposal accepts it; the hero-led light RTS direction should not inherit an MMO-style global cooldown by default.

Cooldown state should key off `GrantedSkillId` or `LoadoutSlotId`, not only `SkillDefinitionId`, because the same definition may later appear in multiple slots with different levels, equipment modifiers, cooldown groups, or source ownership. `SkillDefinitionId` remains the source content identity for reports and definition lookup.

The Runtime event stream must expose enough availability facts for UI to bind directly to state changes instead of polling or recomputing rules in Presentation. UI may display cooldown, resource shortage, charge recovery, and disabled reasons, but it must not decide final skill legality.

## Single Implementation Authority Direction

The long-term system must have one authoritative skill definition and execution path.

The accepted target is:

```text
SkillDefinitionResource
-> BattleSkillSnapshotCompiler
-> BattleSkillSnapshot
-> Runtime AbilityController
-> EffectExecutor / Runtime capability components
-> BattleEventStream and reports
```

Other ability-definition paths may exist only as explicit migration inputs before the accepted implementation slice removes or converts them. They must not stay as fallbacks, parallel feature paths, or alternate authorities. In particular:

- first-slice hardcoded C# skill definitions should be replaced by resource-authored definitions and removed after migration;
- any existing `AbilityDefinition` Resource model must either become the target `SkillDefinitionResource` family or be deleted after its useful data is migrated;
- Presentation-level ability helpers must not own gameplay truth after the Runtime snapshot path becomes authoritative;
- missing resource definitions, invalid effect payloads, or unsupported rule types should fail explicitly with diagnostics instead of falling back to a legacy skill model.

The migration goal is not compatibility-by-layering. It is convergence: one content authoring model, one snapshot compiler, one runtime ability controller, one effect execution boundary, and one event/report source of truth.

## Resolved Design Decisions

- Persistent skill ownership lives in Strategic Management as hero or battle-group skill grants/loadout slots. These entries point to stable skill definition ids and may carry slot, level, source, progression, equipment, or modifier facts.
- Battle UI behavior comes from compiled skill snapshot traits, including targeting, input flow, cost, cooldown, charge, disabled reason, and presentation profile. Concrete skill ids identify content but must not become the UI's main behavior switch.

## Non-Goals

- No generic scripting language for all possible skills.
- No requirement that every future skill can be created without code.
- No long-term fallback from the target skill system to legacy ability definitions or first-slice hardcoded definitions.
- No code, scene, resource, or config implementation directly from this design proposal. Implementation starts only through a follow-up implementation proposal.

## Acceptance Criteria

- The accepted authority documents define the boundary between configurable skill composition and reusable code behavior.
- The accepted architecture identifies the long-term owner of hero or battle-group skill assignment.
- The accepted architecture defines when `SkillId` is valid as identity and when behavior must come from snapshot traits, resources, or capability interfaces.
- The accepted architecture defines when a skill effect is a reusable definition, a reusable executor, an immediate execution request, or a durable runtime instance.
- The accepted architecture defines skill availability ownership for cost, cooldown, charges, refunds, and disabled reasons.
- The accepted architecture requires one authoritative skill definition and execution path, with legacy paths removed or converted rather than retained as fallback.
- A follow-up implementation proposal can name the first migration slice, validation tests, runtime diagnostics, and manual QA evidence.
