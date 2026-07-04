# Battle Skill Configuration Authority Cutover

Status: Accepted - Manual QA Confirmed

## Relationship Metadata

- Requirement Id: BSKILL-CFG-001-IMPL-001
- Originating Design Proposal: `design-proposals/archived/2026-06-30-battle-skill-configuration-boundary/proposal.md`
- Implements:
  - `system-design/battle-content-progression-architecture.md`
  - `system-design/strategic-management-system-architecture.md`
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/combat-command/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
- Related Implementation Proposals: None
- GodotPrompter Skills Used:
  - `ability-system`
  - `resource-pattern`
  - `component-system`
  - `csharp-godot`
  - `godot-testing`

## Goal

Replace the current hardcoded and parallel battle skill paths with one authoritative path:

```text
BattleSkillDefinitionResource
-> BattleSkillSnapshotCompiler
-> BattleSkillSnapshot
-> Runtime AbilityController
-> typed EffectExecutor / runtime capability components
-> BattleEventStream and reports
```

The slice migrates the existing first battle-group skill content and thunder skill content into Resource-authored definitions while preserving current playable behavior. At acceptance, Runtime, UI, tests, and reports must no longer depend on `FirstSliceBattleSkillDefinitions`, the old `AbilityDefinition` Resource model, or concrete skill-id behavior branches.

## Architecture Judgment

- System ownership: Combat Runtime owns action lifecycle, effect execution, availability state, and authoritative events. Strategic Management owns persistent battle-group skill grants and loadout assignments. Godot Resources own authored content only.
- Existing implementation to reuse: `BattleAbilityController` action lifecycle, `BattleAbilityTickCoordinator`, `BattleCommitBuffer`, `BattleRuntimeSpatialMark`, `BattleDisplacementCommitBoundary`, `BattleChannelDamageResolver`, `BattleCommanderGroupIdentity`, and existing config loader validation patterns.
- Existing implementation to retire: hardcoded first-slice skill factory, generic `Amount/Duration/Radius` effect payloads, UI branches keyed by thunder skill ids, presentation observers keyed by `SourceDefinitionId`, and `src/Definitions/Battle/Abilities/*`.
- Long-term constraint: no fallback path. Missing resources, invalid grants, unsupported effect resources, missing executors, invalid costs, invalid cooldown rules, and invalid targeting profiles must fail explicitly before Runtime begins or at command submission with a structured reason.

## Scope

This implementation proposal covers one complete authority cutover for battle skills:

- Create typed Godot C# Resources for skill definitions, effect definitions, cost/cooldown rules, targeting/input profiles, timing, interrupt policy, and presentation profile.
- Add a Resource catalog and compiler that converts authored resources plus strategic battle-group grants into Runtime snapshots.
- Replace generic effect payloads with typed snapshot payloads and typed executors.
- Add mature first-version availability state for limited uses, mana cost, cooldown, charge count, and structured disabled reasons.
- Compile only participating battle-group grants into battle snapshots.
- Refactor battle HUD targeting and skill presentation to consume snapshot traits and presentation profile ids.
- Remove hardcoded first-slice skill definitions and old `AbilityDefinition` Resource ability path.
- Update regression tests so they protect the new authority path instead of the old hardcoded path.

## Non-Goals

- Do not implement a generic scripting language or arbitrary expression tree for skills.
- Do not make new effect semantics data-only. A new effect family still requires C# Resource type, typed snapshot payload, and executor code.
- Do not convert all basic attack action lifecycle into skill snapshots in this slice. The old `AbilityDefinition` basic-attack Resource model is removed; existing Runtime basic attacks remain the attack-action system defined by `BattleGroupSnapshot.AttackDamage`, `AttackRange`, cadence, and commit buffer until a separate accepted basic-attack unification proposal changes that authority.
- Do not add a global cooldown. The accepted direction uses per-grant cooldowns, optional shared cooldown groups, charge recovery, and use limits.
- Do not keep `FirstSliceBattleSkillDefinitions` or `AbilityDefinition` as compatibility fallbacks after acceptance.

## Current Implementation Gap

| Area | Current State | Required State |
|---|---|---|
| Skill definitions | `FirstSliceBattleSkillDefinitions.CreateSelectedHeroSkills()` hardcodes five skills in C#. | Authored `.tres` `BattleSkillDefinitionResource` files indexed by stable `SkillDefinitionId`. |
| Snapshot creation | `BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots()` injects all first-slice skills. | `BattleSkillSnapshotCompiler` compiles only grants/loadout slots owned by participating battle groups. |
| Effect payload | `BattleSkillEffectSnapshot` overloads `Amount`, `DurationSeconds`, `TickIntervalSeconds`, and `Radius`. | Typed payloads such as `DamageSkillEffectSnapshot`, `CreateMarkSkillEffectSnapshot`, `TeleportToMarkSkillEffectSnapshot`, and `ChanneledAreaDamageSkillEffectSnapshot`. |
| Runtime effects | `BattleEffectResolver` switches on `BattleSkillEffectKind` and owns thunder-specific branches. | Effect executors dispatch by typed payload class. Thunder mark, teleport, and channel behavior remain reusable executors, not skill-id branches. |
| Availability | Runtime only tracks `UsedHeroSkillKeys` by battle group and skill id. | Availability state keyed by `GrantedSkillId` or `LoadoutSlotId`, with limited uses, cooldown, charge, mana, pending, active, and disabled reason facts. |
| Strategic assignment | `config/battle/first_slice_hero_companies.json` has one `skillId` per battle group and Runtime still receives all hardcoded skills. | Strategic/default battle-group grants list one or more stable `SkillDefinitionId` values, then compiler emits group-owned skill snapshots. |
| UI targeting | `WorldSiteRoot.BattleRuntimeCommandHud.cs` compares thunder skill ids and owns special two-stage or direction flows. | UI reads target/input/preview traits from the selected skill snapshot. |
| Presentation | Thunder tag and spiral observers compare `SourceDefinitionId` to concrete ids. | Runtime events carry presentation profile facts; presentation selects FX by profile/effect presentation tags. |
| Parallel ability path | `AbilityDefinition`, `AbilityEffect`, `DamageAbilityEffect`, `AbilityComponent`, and generated basic attack resources remain separate. | Old ability Resource path is removed; unit resources no longer export `Abilities`; basic attack remains attack-action data, not a second skill authoring model. |
| Tests | Several regression tests assert first-slice C# definitions, `Amount`, and thunder id branches. | Tests assert Resource catalog, compiler, typed payloads, grant filtering, no fallback, no old ability path, and trait-driven UI/presentation. |

## Target Data Model

### Stable Ids

- `SkillDefinitionId`: authored content id in `BattleSkillDefinitionResource`. It must not be derived from battle id, hero id, battle-group id, corps id, unit id, display name, resource path, or first-slice staging labels.
- `GrantedSkillId`: strategic ownership id. For first-slice default hero grants, use `default_hero:<heroId>:grant:<slotId>` when a stable hero id is available; otherwise use an explicit `default_source:<assignmentId>:grant:<slotId>` adapter id. It must not be derived from battle id, runtime battle-group id, runtime actor id, corps id, unit display name, or resource path.
- `LoadoutSlotId`: strategic slot id such as `primary`, `mobility`, `finisher`, or `assault_01`. Cooldown and charges key off `GrantedSkillId` first, then `LoadoutSlotId` when a grant id is absent.
- `SkillCastId` / `CommandId`: Runtime command correlation id. Runtime-generated values use `skill_cast:<battleId>:<runtimeTick>:<sequence>` and must not be used for definition lookup.
- `SkillActionId`: Runtime action lifecycle id, generated from the accepted command sequence.
- `EffectInstanceId`, `MarkId`, `StatusId`, `ProjectileId`, `SummonId`: Runtime-only instance ids created by durable effect executors.

### Resource Classes

Create the target Resource family under `src/Definitions/Battle/Skills/`:

- `BattleSkillDefinitionResource.cs`
  - `[GlobalClass] public partial class BattleSkillDefinitionResource : Resource`
  - Exported fields: `SkillDefinitionId`, `DisplayName`, `IconText`, `Tags`, `CommandChannel`, `SkillType`, `Timing`, `InterruptPolicy`, `CostRules`, `CooldownRules`, `Targeting`, `Effects`, `Presentation`.
- `BattleSkillDefinitionEnums.cs`
  - Definition-layer enum types for exported Resource fields: `BattleSkillCommandChannelDefinition`, `BattleSkillTypeDefinition`, `BattleSkillInputFlowDefinition`, `BattleSkillTargetKindDefinition`, `BattleSkillRangeMetricDefinition`, `BattleSkillAreaShapeDefinition`, `BattleSkillDirectionModeDefinition`, `BattleSkillCostPayTimingDefinition`, `BattleSkillRefundPolicyDefinition`, `BattleSkillCooldownStartDefinition`, `BattleSkillDamageTypeDefinition`, and `BattleSkillMarkKindDefinition`.
- `BattleSkillTimingResource.cs`
  - Exported fields: `CastSeconds`, `ImpactDelaySeconds`, `RecoverySeconds`.
- `BattleSkillInterruptPolicyResource.cs`
  - Exported fields: `CanInterruptBasicAttackWindup`, `CanCancelBasicAttackRecovery`, `ReleasesWithoutOccupyingCaster`, `CanInterruptActiveChannel`.
- `BattleSkillTargetingProfileResource.cs`
  - Exported fields: `InputFlow`, `TargetKind`, `Range`, `RangeMetric`, `AreaShape`, `AreaRadius`, `DirectionMode`, `RequiresSelectedMark`, `RequiredMarkKind`, `LandingRadius`, `PreviewProfileId`.
- `BattleSkillPresentationProfileResource.cs`
  - Exported fields: `ProfileId`, `CastFxProfileId`, `ImpactFxProfileId`, `MarkFxProfileId`, `AreaFxProfileId`, `SuppressActorCastFx`, `HoldCastAnimationDuringAction`.
- `BattleSkillEffectResource.cs`
  - Abstract base Resource for editor arrays only. It does not execute behavior.
- `DamageSkillEffectResource.cs`
  - Exported fields: `BaseDamage`, `DamageType`, `CanHitActors`, `CanHitWorldObjects`.
- `CreateMarkSkillEffectResource.cs`
  - Exported fields: `MarkKind`, `LifetimeSeconds`, `AttachToActorWhenTargeted`, `ReplaceExistingOwnedMark`.
- `TeleportToMarkSkillEffectResource.cs`
  - Exported fields: `RequiredMarkKind`, `LandingRadius`, `ConsumesMark`.
- `ChanneledAreaDamageSkillEffectResource.cs`
  - Exported fields: `BaseDamage`, `DamageType`, `DurationSeconds`, `TickIntervalSeconds`, `AreaShape`, `Radius`, `FollowsCaster`, `UsesTargetOffset`.
- `BattleSkillCostRuleResource.cs`
  - Abstract base Resource for cost definitions.
- `NoCostSkillCostRuleResource.cs`
  - No exported gameplay fields.
- `ManaCostSkillCostRuleResource.cs`
  - Exported fields: `PoolId`, `Amount`, `PayTiming`, `RefundPolicy`.
- `LimitedUseSkillCostRuleResource.cs`
  - Exported fields: `MaxUses`, `ConsumeTiming`, `RefundPolicy`.
- `BattleSkillCooldownRuleResource.cs`
  - Abstract base Resource for cooldown definitions.
- `NoCooldownSkillCooldownRuleResource.cs`
  - No exported gameplay fields.
- `PerGrantCooldownRuleResource.cs`
  - Exported fields: `DurationSeconds`, `StartsOn`, `SharedCooldownGroupId`.
- `ChargeCooldownRuleResource.cs`
  - Exported fields: `MaxCharges`, `RechargeSeconds`, `StartsFull`.

Rules for these Resources:

- They are shared, declarative, and stateless.
- They do not store current target, current cast, elapsed time, remaining ticks, active cooldown, current charges, generated ids, or damage results.
- They do not call Runtime services, scene tree APIs, or effect executors.
- C# classes extending `Resource` are `partial` and concrete authoring classes use `[GlobalClass]`.

### Snapshot Classes

Modify or replace the current snapshot classes under `src/Application/Battle/Snapshots/`:

- `BattleSkillSnapshot`
  - Fields: `SkillDefinitionId`, `GrantedSkillId`, `LoadoutSlotId`, `OwnerHeroId`, `OwnerBattleGroupId`, `RuntimeCommanderGroupId`, `DisplayName`, `IconText`, `Tags`, `CommandChannel`, `SkillType`, `Targeting`, `Timing`, `InterruptPolicy`, `Costs`, `Cooldown`, `Charges`, `Effects`, `Presentation`.
  - Remove `CasterUnitIds` as the ownership authority. Caster eligibility is granted by strategic loadout ownership and optional targeting/condition traits.
- `BattleSkillSnapshotEnums`
  - Snapshot-layer enum types consumed by Runtime/UI: `BattleSkillCommandChannel`, `BattleSkillType`, `BattleSkillInputFlow`, `BattleSkillTargetKind`, `BattleSkillRangeMetric`, `BattleSkillAreaShape`, `BattleSkillDirectionMode`, `BattleSkillCostPayTiming`, `BattleSkillRefundPolicy`, `BattleSkillCooldownStart`, `BattleSkillDamageType`, and `BattleSkillMarkKind`.
- `BattleSkillTargetingSnapshot`
  - Fields: `InputFlow`, `TargetKind`, `Range`, `RangeMetric`, `AreaShape`, `AreaRadius`, `DirectionMode`, `RequiresSelectedMark`, `RequiredMarkKind`, `LandingRadius`, `PreviewProfileId`.
- `BattleSkillTimingSnapshot`
  - Fields: `CastSeconds`, `ImpactDelaySeconds`, `RecoverySeconds`.
- `BattleSkillInterruptPolicySnapshot`
  - Fields: `CanInterruptBasicAttackWindup`, `CanCancelBasicAttackRecovery`, `ReleasesWithoutOccupyingCaster`, `CanInterruptActiveChannel`.
- `BattleSkillPresentationSnapshot`
  - Fields: `ProfileId`, `CastFxProfileId`, `ImpactFxProfileId`, `MarkFxProfileId`, `AreaFxProfileId`, `SuppressActorCastFx`, `HoldCastAnimationDuringAction`.
- `BattleSkillCostSnapshot`
  - Abstract base class with sealed concrete subclasses: `NoCostSkillCostSnapshot`, `ManaCostSkillCostSnapshot`, and `LimitedUseSkillCostSnapshot`.
- `BattleSkillCooldownSnapshot`
  - Abstract base class with sealed concrete subclasses: `NoCooldownSkillCooldownSnapshot`, `PerGrantCooldownSkillCooldownSnapshot`, and `ChargeCooldownSkillCooldownSnapshot`.
- `BattleSkillEffectSnapshot`
  - Abstract base class with `EffectSnapshotType`, `EffectInstancePolicy`, `PresentationProfileId`, and sealed concrete subclasses.
- Typed effect snapshots:
  - `DamageSkillEffectSnapshot(BaseDamage, DamageType, CanHitActors, CanHitWorldObjects)`.
  - `CreateMarkSkillEffectSnapshot(MarkKind, LifetimeSeconds, AttachToActorWhenTargeted, ReplaceExistingOwnedMark)`.
  - `TeleportToMarkSkillEffectSnapshot(RequiredMarkKind, LandingRadius, ConsumesMark)`.
  - `ChanneledAreaDamageSkillEffectSnapshot(BaseDamage, DamageType, DurationSeconds, TickIntervalSeconds, AreaShape, Radius, FollowsCaster, UsesTargetOffset)`.

Remove `SkillId` from `BattleSkillSnapshot`. Rename `CommandRequest.SkillId` to `CommandRequest.SkillDefinitionId` and update all command factories, Runtime validation, UI submitters, reports, and tests in the same slice. Runtime and Presentation must not keep a parallel `SkillId` alias.

## Resource And Config Authoring

Create a skill definition index:

- `config/battle/battle_skill_definitions.json`
  - Fields: `skills: [{ "skillDefinitionId": "...", "resourcePath": "res://assets/battle/skills/....tres", "aliases": ["..."] }]`.
  - Required ids:
    - `skill_shield_barrier`
    - `skill_sun_piercer`
    - `skill_thunder_tag_throw`
    - `skill_thunder_mark_fold`
    - `skill_thunder_spiral_break`
  - Required aliases for migration input only:
    - `first_slice_skill_shield_barrier -> skill_shield_barrier`
    - `first_slice_skill_sun_piercer -> skill_sun_piercer`
    - `first_slice_skill_thunder_tag_throw -> skill_thunder_tag_throw`
    - `first_slice_skill_thunder_mark_fold -> skill_thunder_mark_fold`
    - `first_slice_skill_thunder_spiral_break -> skill_thunder_spiral_break`
  - The compiler normalizes aliases to `SkillDefinitionId` before snapshots are created. Runtime command submission, event source attribution, reports, and HUD state use only the normalized `SkillDefinitionId`.

Create authored skill resources:

- `assets/battle/skills/skill_shield_barrier.tres`
  - Target actor, range 8, damage `12`, recovery `0.2`, interrupt windup true, limited uses `1`.
- `assets/battle/skills/skill_sun_piercer.tres`
  - Target actor, range 8, damage `18`, recovery `0.2`, interrupt windup true, limited uses `1`.
- `assets/battle/skills/skill_thunder_tag_throw.tres`
  - Target actor or cell, range 8, damage `12`, create mark `thunder_mark`, mark lifetime `8.0`, releases without occupying caster, can cancel basic attack recovery.
- `assets/battle/skills/skill_thunder_mark_fold.tres`
  - Mark-then-cell input flow, requires selected `thunder_mark`, landing radius `3`, teleport to mark effect, can interrupt active channel.
- `assets/battle/skills/skill_thunder_spiral_break.tres`
  - Directional cell input, range 3, channeled area damage, base damage `14`, duration `1.6`, tick interval `0.2`, radius `1`, follow caster true, use target offset true, suppress actor cast FX, hold cast animation during action.

Update the first-slice battle-group config:

- `config/battle/first_slice_hero_companies.json`
  - Replace single `skillId` with `skillDefinitionIds`.
  - Shield, archer, and assault groups all grant the shared thunder demo kit: `["skill_thunder_tag_throw", "skill_thunder_mark_fold", "skill_thunder_spiral_break"]`.
  - The shield and sun-piercer skill resources remain indexed authored content, but this validation slice does not grant them to the three starting battle groups.

The config loader must validate nonempty arrays, duplicate skill ids, and missing required fields. It must not synthesize extra thunder skills from code when the config only lists one id.

## Compiler Design

Create `BattleSkillSnapshotCompiler` under `src/Application/Battle/Snapshots/`.

Inputs:

- `IReadOnlyDictionary<string, BattleSkillDefinitionResource>` from `BattleSkillDefinitionCatalog`.
- Participating `BattleGroupSnapshot` objects.
- `BattleSkillGrantSnapshot` objects from strategic/default battle-group grants.

Output:

- `IReadOnlyList<BattleSkillSnapshot>` where each item is a compiled, group-owned loadout entry.

Compiler rules:

- Fail with `battle_skill_definition_missing` when a grant references an absent `SkillDefinitionId`.
- Fail with `battle_skill_definition_duplicate` when the catalog has duplicate ids.
- Fail with `battle_skill_grant_duplicate` when two grants share a `GrantedSkillId` in one battle snapshot.
- Fail with `battle_skill_loadout_slot_duplicate` when one owner has duplicate `LoadoutSlotId`.
- Fail with `battle_skill_owner_missing` when a grant references a non-participating battle group.
- Fail with `battle_skill_effect_resource_unsupported` when an effect Resource has no matching compiler mapping.
- Fail with `battle_skill_effect_payload_invalid` for invalid typed fields such as non-positive damage, non-positive channel interval, or teleport radius below 1.
- Fail with `battle_skill_targeting_invalid` when targeting mode and input flow disagree, such as `RequiresSelectedMark` without a mark kind.
- Clamp only display-safe or optional values. Do not clamp invalid gameplay values into valid behavior.

The compiler owns Resource-to-snapshot conversion. Runtime receives only snapshots, ids, primitive values, and typed payloads.

## Strategic Grant Flow

Create a first-slice default grant provider as a bridge into the accepted Strategic Management ownership model:

- `src/Application/Battle/Snapshots/BattleSkillGrantSnapshot.cs`
  - Fields: `GrantedSkillId`, `LoadoutSlotId`, `OwnerHeroId`, `OwnerBattleGroupId`, `RuntimeCommanderGroupId`, `SkillDefinitionId`, `SourceKind`, `SourceId`, `SkillLevel`.
- `src/Application/World/FirstSliceBattleGroupSkillGrantProvider.cs`
  - Reads the validated first-slice battle-group config.
  - Produces one hero-owned grant per configured skill for each participating player battle group whose force/unit maps to a stable hero id.
  - Uses stable grant ids built from `OwnerHeroId` and slot ids when possible. `OwnerBattleGroupId` and `RuntimeCommanderGroupId` stay as battle-context/report attribution fields, not skill ownership authority.

Update battle snapshot build boundaries:

- `BattleSnapshotBuilder.Build()` must remove the unconditional `CreateSelectedHeroSkillSnapshots()` call.
- `LegacyBattleStartSnapshotAdapter.ToSnapshot()` and `BattleGroupSessionProbeService.PrepareSnapshot()` must pass participating groups into the grant provider and compiler.
- `StrategicBattleLaunchSnapshotSyncService.Sync()` must copy compiled skill snapshots with all new fields and must reject missing/invalid active-context skills instead of silently dropping invalid payloads.
- Enemy battle groups receive no hero skill grants unless their strategic/default data explicitly grants them.

## Runtime Design

### Command Submission

Update `CommandRequest` and skill command factories so the submitted content id field is `SkillDefinitionId`. Remove `SkillId` from the command DTO in the same slice.

`BattleRuntimeHeroSkillCommandResolver` must validate:

- the source actor's stable hero identity owns a compiled skill snapshot with matching `SkillDefinitionId`, and grant/loadout ids further disambiguate duplicate slots when present;
- target payload matches `BattleSkillTargetingSnapshot.InputFlow`;
- range checks use `BattleSkillTargetingSnapshot.Range` and `RangeMetric`;
- mark-selection skills use `RequiresSelectedMark`, `RequiredMarkKind`, and `LandingRadius`, not effect kind or concrete skill id;
- directional skills use `DirectionMode`, `AreaShape`, and preview/area facts, not concrete skill id;
- availability state accepts the cast before queuing the order.

Remove:

- `BattleRuntimeHeroSkillCommandResolver.ThunderMark.cs` as a skill-family partial.
- `UsesThunderMarkTeleport()` checks in command resolver and ability controller.
- `UsedHeroSkillKeys` and `BuildSkillKey()` one-use logic as hidden availability authority.

### Availability

Create `BattleSkillAvailabilityState` under `src/Runtime/Battle/`:

- Key: `GrantedSkillId`; fallback key: `LoadoutSlotId` only when grant id is empty in a test fixture.
- Facts: `RemainingUses`, `CurrentCharges`, `NextChargeReadyAtSeconds`, `CooldownReadyAtSeconds`, `PendingCommandIds`, `ActiveActionIds`.
- Rejection reason codes:
  - `skill_grant_missing`
  - `skill_on_cooldown`
  - `skill_no_charges`
  - `skill_use_limit_exhausted`
  - `skill_resource_insufficient`
  - `skill_already_pending`
  - `skill_already_active`
  - `skill_target_required`
  - `skill_target_cell_required`
  - `skill_target_out_of_range`
  - `skill_selected_mark_required`
  - `skill_mark_missing`
  - `skill_mark_destination_invalid`
  - `skill_mark_destination_occupied`

Runtime events must expose availability changes for HUD:

- `CommandRejected` with disabled reason before queueing.
- `CommandAccepted` after cost/use reservation according to cost timing.
- `SkillUsed` when action starts.
- `CommandFailed` with refund reason when release fails after acceptance.
- Optional low-noise `SkillAvailabilityChanged` event if HUD needs active cooldown/charge progress without polling.

First implementation behavior:

- Use limit is explicit through `LimitedUseSkillCostRuleResource`. If existing one-use behavior is retained, all migrated current hero skills get `MaxUses = 1`.
- Cooldown defaults to `NoCooldown` unless a resource specifies `PerGrantCooldown` or `ChargeCooldown`.
- Mana defaults to `NoCost` because no battle mana pool exists yet. The code path must exist and fail with `skill_resource_pool_missing` if a nonzero mana cost references a missing pool.

### Effect Executors

Create typed executor interfaces and implementations under `src/Runtime/Battle/Effects/`:

- `IBattleSkillEffectExecutor`
  - Methods: `bool CanExecute(BattleSkillEffectSnapshot payload)` and `IReadOnlyList<BattleEvent> Execute(BattleEffectExecutionContext context, BattleSkillEffectSnapshot payload)`.
- `DamageSkillEffectExecutor`
  - Uses `DamageSkillEffectSnapshot.BaseDamage`.
  - Sends damage through `BattleEffectReceiver` / `BattleCommitBuffer`.
  - Does not inspect concrete target unit definitions.
- `CreateMarkSkillEffectExecutor`
  - Creates `BattleRuntimeSpatialMark` with `MarkKind`, lifetime, owner group, source actor, command id, action id, and definition id.
  - Replaces existing owned mark only when `ReplaceExistingOwnedMark` is true.
- `TeleportToMarkSkillEffectExecutor`
  - Uses `RequiredMarkKind` and `LandingRadius`.
  - Delegates legality and displacement commit to a generalized displacement boundary, renamed from thunder-specific methods.
- `ChanneledAreaDamageSkillEffectExecutor`
  - Creates caster-owned `BattleRuntimeActiveChannel` with typed damage, duration, tick interval, area shape, radius, and target offset.
  - Channel ticking remains owned by `BattleAbilityController`.

Replace `BattleEffectResolver` switch with `BattleSkillEffectExecutorRegistry`:

- Registry is pure C# Runtime code.
- Unsupported payload type throws or returns `battle_skill_effect_executor_missing` during validation.
- No executor compares concrete `SkillDefinitionId` values.

### Capability Boundary

Introduce narrow runtime capability interfaces before adding target-type-specific logic:

- `IDamageReceiver`
- `IDamageModifierProvider`
- `IResourcePool`
- `IStatusContainer`
- `ISpatialAnchor`

This slice only wires damage receiver and resource pool checks needed by current behavior. It must not introduce a broad component framework beyond the interfaces used by the migrated effects.

## Presentation And HUD Design

### Snapshot Traits

Add UI-facing traits to `BattleSkillSnapshot` through nested snapshots instead of UI id checks:

- `InputFlow` values:
  - `ImmediateSelf`
  - `SelectActor`
  - `SelectCell`
  - `SelectActorOrCell`
  - `SelectMarkThenLandingCell`
  - `SelectDirectionArea`
- `PreviewProfileId` values:
  - `diamond_range`
  - `mark_candidates`
  - `landing_radius`
  - `directional_square_area`
- `Presentation.ProfileId` values:
  - `skill_default_damage`
  - `skill_mark_projectile`
  - `skill_mark_teleport`
  - `skill_channeled_area`

### HUD Refactor

Modify `WorldSiteRoot.BattleRuntimeCommandHud.cs`:

- Replace `ThunderFoldTargetingStage` with generic `SkillTargetingStage` values: `None`, `PrimarySelection`, `SecondarySelection`.
- Replace `_battleRuntimeThunderFoldSelectedMarkId` with `_battleRuntimeSelectedRuntimeAnchorId`.
- Replace `_battleRuntimeThunderFoldSelectedMarkSurface` with `_battleRuntimeSelectedRuntimeAnchorSurface`.
- Replace concrete-id branches for fold and spiral with `InputFlow` dispatch.
- `BuildBattleRuntimeSkillSnapshots()` must return an empty list when Runtime has no skill snapshots. It must not call `BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots()`.
- `ResolveBattleRuntimeHeroSkillRange()` reads `BattleSkillTargetingSnapshot.Range`.

Modify `BattleRuntimeSkillUsageResolver`:

- Remove `HeroSkillCommandIds.ThunderMarkFoldSkillId`.
- Use `BattleSkillTargetingSnapshot.RequiresSelectedMark` and `RequiredMarkKind` to decide whether a skill is unavailable due to no live owned mark.
- Use Runtime availability state/events for used, pending, cooldown, charge, and resource shortage display.

Modify `BattleRuntimeHeroSkillTargetPresentation`:

- Keep generic range and footprint helpers.
- Replace `TryResolveThunderSpiralTargetCenter` with `TryResolveDirectionalAreaCenter(BattleSkillTargetingSnapshot targeting, ...)`.
- Replace `BuildThunderSpiralAreaCells` with `BuildAreaPreviewCells(BattleSkillTargetingSnapshot targeting, ...)`.
- Rename `BattleRuntimeThunderFoldTargetingPresentation` to `BattleRuntimeMarkTargetingPresentation` and parameterize it by `MarkKind` and `LandingRadius`.

### Skill Presentation

Modify presentation observers:

- Replace `BattleRuntimeThunderTagPresentationObserver.IsOffhandSkillReleaseEvent()` id comparison with `runtimeEvent.PresentationProfileId == "skill_mark_projectile"` or equivalent event presentation profile.
- Replace `BattleRuntimeThunderSpiralPresentationObserver.IsThunderSpiralSkillUsedEvent()` id comparison with `runtimeEvent.PresentationProfileId == "skill_channeled_area"`.
- `BattleUnitRoot.SkillPresentation.cs` keeps existing FX scenes as profile implementations, but method names and selection logic become profile-oriented:
  - `PlayMarkProjectilePresentation`
  - `PlayRuntimeMarkPresentation`
  - `PlayChanneledAreaPresentation`
- `BattleEvent` adds presentation fields copied from the snapshot at release time:
  - `PresentationProfileId`
  - `CastFxProfileId`
  - `ImpactFxProfileId`
  - `AreaFxProfileId`
  - `SuppressActorCastFx`
  - `HoldCastAnimationDuringAction`

## Old Ability Path Removal

Delete or replace the following old ability model files in this slice:

- `src/Definitions/Battle/Abilities/AbilityDefinition.cs`
- `src/Definitions/Battle/Abilities/AbilityEffect.cs`
- `src/Definitions/Battle/Abilities/DamageAbilityEffect.cs`
- `src/Definitions/Battle/Abilities/AbilityTargetRule.cs`
- `src/Definitions/Battle/Abilities/SingleHostileUnitTargetRule.cs`
- `src/Definitions/Battle/Abilities/AbilityTargetMode.cs`
- `src/Definitions/Battle/Abilities/AbilityDirectionMode.cs`
- `src/Definitions/Battle/Abilities/AbilityAreaShape.cs`
- `src/Presentation/Battle/Entities/AbilityComponent.cs`
- `src/Presentation/Battle/Abilities/BattleAbilityQueries.cs`

Modify old consumers:

- `src/Definitions/Battle/BattleUnitDefinition.cs`
  - Remove `Abilities`.
  - Keep `AttackDamage`, `AttackRange`, attack timing, and attack presentation fields.
- `src/Presentation/Battle/Entities/BattleUnitFactory.cs`
  - Remove `AbilityComponent` copying and the `legacy-attack-fallback` warning.
- `src/Presentation/Battle/Actions/BattleActionResult.cs`
  - Remove `AbilityDefinition Ability` from action result. Local presentation results carry `ActionDisplayName`; Runtime playback reads source attribution from Runtime events.
- `src/Presentation/Battle/Intents/BattleIntent.cs`
  - Remove `AbilityDefinition PreferredAbility`.
- `src/Presentation/Battle/Intents/BattleIntentTemplate.cs`
  - Remove `preferredAbility` argument from `Create`.

Delete old basic attack definition resources:

- `assets/battle/abilities/militia_basic_attack.tres`
- `assets/battle/abilities/player_knight_basic_attack.tres`
- `assets/battle/abilities/skeleton_archer_basic_attack.tres`
- `assets/battle/abilities/skeleton_warrior_basic_attack.tres`

Modify the one currently observed unit resource reference:

- `assets/battle/units/neutral/首领_噬法者/unit.tres`
  - Remove the unused `AbilityDefinition` ext_resource line.

Do not delete `assets/battle/abilities/fx/`; those are presentation assets, not old ability-definition authority.

## Files To Touch

### Create

- `src/Definitions/Battle/Skills/BattleSkillDefinitionResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillDefinitionEnums.cs`
- `src/Definitions/Battle/Skills/BattleSkillTimingResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillInterruptPolicyResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillTargetingProfileResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillPresentationProfileResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillEffectResource.cs`
- `src/Definitions/Battle/Skills/DamageSkillEffectResource.cs`
- `src/Definitions/Battle/Skills/CreateMarkSkillEffectResource.cs`
- `src/Definitions/Battle/Skills/TeleportToMarkSkillEffectResource.cs`
- `src/Definitions/Battle/Skills/ChanneledAreaDamageSkillEffectResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillCostRuleResource.cs`
- `src/Definitions/Battle/Skills/NoCostSkillCostRuleResource.cs`
- `src/Definitions/Battle/Skills/ManaCostSkillCostRuleResource.cs`
- `src/Definitions/Battle/Skills/LimitedUseSkillCostRuleResource.cs`
- `src/Definitions/Battle/Skills/BattleSkillCooldownRuleResource.cs`
- `src/Definitions/Battle/Skills/NoCooldownSkillCooldownRuleResource.cs`
- `src/Definitions/Battle/Skills/PerGrantCooldownRuleResource.cs`
- `src/Definitions/Battle/Skills/ChargeCooldownRuleResource.cs`
- `src/Application/Config/BattleSkillDefinitionIndexLoader.cs`
- `src/Application/Battle/Snapshots/BattleSkillDefinitionCatalog.cs`
- `src/Application/Battle/Snapshots/BattleSkillSnapshotCompiler.cs`
- `src/Application/Battle/Snapshots/BattleSkillGrantSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillSnapshotEnums.cs`
- `src/Application/Battle/Snapshots/BattleSkillTargetingSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillTimingSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillInterruptPolicySnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillPresentationSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillAvailabilityRuleSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillEffectSnapshotType.cs`
- `src/Application/Battle/Snapshots/BattleSkillEffectInstancePolicy.cs`
- `src/Application/Battle/Snapshots/NoCostSkillCostSnapshot.cs`
- `src/Application/Battle/Snapshots/ManaCostSkillCostSnapshot.cs`
- `src/Application/Battle/Snapshots/LimitedUseSkillCostSnapshot.cs`
- `src/Application/Battle/Snapshots/NoCooldownSkillCooldownSnapshot.cs`
- `src/Application/Battle/Snapshots/PerGrantCooldownSkillCooldownSnapshot.cs`
- `src/Application/Battle/Snapshots/ChargeCooldownSkillCooldownSnapshot.cs`
- `src/Application/Battle/Snapshots/DamageSkillEffectSnapshot.cs`
- `src/Application/Battle/Snapshots/CreateMarkSkillEffectSnapshot.cs`
- `src/Application/Battle/Snapshots/TeleportToMarkSkillEffectSnapshot.cs`
- `src/Application/Battle/Snapshots/ChanneledAreaDamageSkillEffectSnapshot.cs`
- `src/Application/World/FirstSliceBattleGroupSkillGrantProvider.cs`
- `src/Runtime/Battle/BattleSkillAvailabilityState.cs`
- `src/Runtime/Battle/Effects/IBattleSkillEffectExecutor.cs`
- `src/Runtime/Battle/Effects/BattleSkillEffectExecutorRegistry.cs`
- `src/Runtime/Battle/Effects/DamageSkillEffectExecutor.cs`
- `src/Runtime/Battle/Effects/CreateMarkSkillEffectExecutor.cs`
- `src/Runtime/Battle/Effects/TeleportToMarkSkillEffectExecutor.cs`
- `src/Runtime/Battle/Effects/ChanneledAreaDamageSkillEffectExecutor.cs`
- `src/Runtime/Battle/Effects/BattleEffectKindLabels.cs`
- `src/Presentation/World/Sites/BattleRuntimeMarkTargetingPresentation.cs`
- `src/Presentation/World/Sites/BattleRuntimeSkillProfilePresentationObserver.cs`
- `config/battle/battle_skill_definitions.json`
- `assets/battle/skills/skill_shield_barrier.tres`
- `assets/battle/skills/skill_sun_piercer.tres`
- `assets/battle/skills/skill_thunder_tag_throw.tres`
- `assets/battle/skills/skill_thunder_mark_fold.tres`
- `assets/battle/skills/skill_thunder_spiral_break.tres`

### Modify

- `config/battle/first_slice_hero_companies.json`
- `src/Application/World/FirstSliceHeroCompanyIds.cs`
- `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleGroupSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSkillEffectSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleSnapshotBuilder.cs`
- `src/Application/Battle/Adapters/LegacyBattleStartSnapshotAdapter.cs`
- `src/Application/Battle/BattleGroupSessionProbeService.cs`
- `src/Application/StrategicBattleBridge/StrategicBattleLaunchSnapshotSyncService.cs`
- `src/Application/Battle/Commands/CommandRequest.cs`
- `src/Presentation/World/Sites/BattleRuntimeHeroSkillCommandRequestFactory.cs`
- `src/Presentation/World/Sites/BattleRuntimeSkillSlot.cs`
- `src/Runtime/Battle/BattleRuntimeSession.cs`
- `src/Runtime/Battle/BattleRuntimeState.cs`
- `src/Runtime/Battle/BattleRuntimeActor.cs`
- `src/Runtime/Battle/BattleAbilityController.cs`
- `src/Runtime/Battle/BattleAbilityEffectReleaseBoundary.cs`
- `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver.cs`
- `src/Runtime/Battle/BattleDisplacementCommitBoundary.cs`
- `src/Runtime/Battle/BattleCommitBuffer.cs`
- `src/Runtime/Battle/BattleEffectReceiver.cs`
- `src/Runtime/Battle/Effects/BattleEffectResolver.cs`
- `src/Runtime/Battle/Effects/BattleChannelDamageResolver.cs`
- `src/Runtime/Battle/Events/BattleEvent.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Presentation/World/Sites/BattleRuntimeSkillUsageResolver.cs`
- `src/Presentation/World/Sites/BattleRuntimeSkillFilter.cs`
- `src/Presentation/World/Sites/BattleRuntimeHeroSkillTargetPresentation.cs`
- `src/Presentation/World/Sites/BattleRuntimeLivePresentationObserver.cs`
- `src/Presentation/World/Sites/BattleRuntimeHeroFramePresenter.cs`
- `src/Presentation/Battle/Entities/BattleUnitRoot.SkillPresentation.cs`
- `src/Definitions/Battle/BattleUnitDefinition.cs`
- `src/Presentation/Battle/Entities/BattleUnitFactory.cs`
- `src/Presentation/Battle/Actions/BattleActionResult.cs`
- `src/Presentation/Battle/Intents/BattleIntent.cs`
- `src/Presentation/Battle/Intents/BattleIntentTemplate.cs`
- `assets/battle/units/neutral/首领_噬法者/unit.tres`
- Relevant regression tests under `tests/WorldSiteDeploymentCacheRegression/`, `tests/TargetBattleArchitectureRegression/`, and `tests/BattleHitFeedbackRegression/`.

### Delete

- `src/Definitions/Battle/Skills/FirstSliceBattleSkillDefinitions.cs`
- `src/Definitions/Battle/Skills/BattleSkillDefinition.cs`
- `src/Definitions/Battle/Skills/BattleSkillEffectDefinition.cs`
- `src/Definitions/Battle/Skills/BattleSkillActionTimingDefinition.cs`
- `src/Definitions/Battle/Skills/BattleSkillInterruptPolicyDefinition.cs`
- `src/Definitions/Battle/Skills/BattleSkillTargetingMode.cs`
- `src/Definitions/Battle/Skills/BattleSkillEffectKind.cs`
- `src/Application/Battle/Snapshots/BattleSkillTargetingMode.cs`
- `src/Application/Battle/Snapshots/BattleSkillEffectKind.cs`
- `src/Application/Battle/Snapshots/BattleSkillSnapshotFactory.cs`
- `src/Application/Battle/Commands/HeroSkillCommandIds.cs`
- `src/Presentation/World/Sites/BattleRuntimeThunderFoldTargetingPresentation.cs`
- `src/Presentation/World/Sites/BattleRuntimeThunderTagPresentationObserver.cs`
- `src/Presentation/World/Sites/BattleRuntimeThunderSpiralPresentationObserver.cs`
- Old ability model files listed in "Old Ability Path Removal".
- Old basic attack definition resources listed in "Old Ability Path Removal".
- `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver.ThunderMark.cs`

## Implementation Steps

- [ ] Add RED tests that fail on the current hardcoded path:
  - `FirstSliceBattleSkillDefinitions.cs` must not exist.
  - `BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots()` must not exist.
  - `WorldSiteRoot.BattleRuntimeCommandHud.cs` must not call the first-slice snapshot factory.
  - `BattleRuntimeHeroSkillCommandResolver*.cs` must not contain `HeroSkillCommandIds.ThunderMarkFoldSkillId` or `TeleportToThunderMark` validation branches.
  - `BattleSkillEffectSnapshot` must not expose `Amount`, `DurationSeconds`, `TickIntervalSeconds`, and `Radius` as shared semantic fields.
  - `src/Definitions/Battle/Abilities/AbilityDefinition.cs` and `AbilityComponent.cs` must not exist.
- [ ] Add Resource classes and the skill definition index loader.
- [ ] Author the five migrated skill `.tres` files and `config/battle/battle_skill_definitions.json`.
- [ ] Convert first-slice battle-group skill config from one `skillId` to `skillDefinitionIds`.
- [ ] Add grant snapshots and the first-slice default grant provider.
- [ ] Implement `BattleSkillSnapshotCompiler` and fail-fast diagnostics.
- [ ] Replace `BattleSnapshotBuilder` skill injection with grant-driven compilation.
- [ ] Update strategic bridge snapshot copy to preserve all new skill snapshot fields.
- [ ] Replace generic effect snapshots with typed payloads.
- [ ] Add typed effect executor registry and executors.
- [ ] Replace thunder-specific command validation with targeting trait validation.
- [ ] Add `BattleSkillAvailabilityState` and remove `UsedHeroSkillKeys`.
- [ ] Refactor HUD targeting flows to use `InputFlow` and preview traits.
- [ ] Refactor presentation observers to use presentation profile fields.
- [ ] Delete old `AbilityDefinition` Resource path and clean unit/resource references.
- [ ] Update regression fixtures from hardcoded `SkillId/Amount` construction to typed helper builders.
- [ ] Run focused tests and then full low-concurrency build.

## Tests

Update and run these tests:

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - Assert battle snapshot skill definitions come from Resource catalog and grants.
  - Assert only selected participating battle-group grants enter the snapshot.
  - Assert unselected or reserve battle-group skills are absent.
  - Assert no UI fallback to hardcoded first-slice skills exists.
  - Assert HUD and command submission prefer `OwnerHeroId` for skill ownership; `OwnerBattleGroupId` or `RuntimeCommanderGroupId` may be used only for current battle context, report attribution, or legacy fixture compatibility.
  - Assert old `AbilityDefinition` files and old basic attack resources are gone.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - Assert command submission requires owned grant/loadout.
  - Assert missing skill definition fails before Runtime starts.
  - Assert duplicate skill definition ids fail before Runtime starts.
  - Assert duplicate grants fail before Runtime starts.
  - Assert typed damage executor applies damage and preserves source attribution.
  - Assert create-mark executor creates attached and ground marks from typed payload.
  - Assert teleport executor validates selected mark, mark kind, landing radius, occupancy, and topology.
  - Assert channeled area executor creates caster-owned channels and preserves tick timing.
  - Assert cooldown/use-limit duplicate submissions reject through `BattleSkillAvailabilityState`.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - Assert skill cast, mark, teleport, and channeled-area presentation use profile ids, not concrete skill ids.
  - Assert actor-attached cast FX suppression comes from presentation profile.
  - Assert held cast animation duration comes from Runtime event action duration plus profile trait.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Run only after focused regression suites pass.
- `git diff --check`

## Diagnostics

Add low-noise logs for these boundaries:

- `BattleSkillDefinitionCatalogLoaded count=<n> path=<path>`.
- `BattleSkillDefinitionInvalid id=<id> path=<path> reason=<reason>`.
- `BattleSkillGrantCompilationFailed owner=<group> grant=<grant> skill=<skillDefinitionId> reason=<reason>`.
- `BattleSkillSnapshotCompiled owner=<group> grant=<grant> skill=<skillDefinitionId> effects=<n>`.
- `BattleSkillCommandRejected group=<group> grant=<grant> skill=<skillDefinitionId> reason=<reason>`.
- `BattleSkillAvailabilityChanged group=<group> grant=<grant> skill=<skillDefinitionId> uses=<remaining> charges=<current> cooldownReadyAt=<seconds>`.
- `BattleSkillEffectExecutorMissing skill=<skillDefinitionId> payload=<payloadType>`.

Do not add per-frame logs for cooldown countdowns, channel ticks, hover preview refreshes, or HUD polling.

## Manual QA

After automated verification passes, run one Bonefield assault from the current playable path and confirm:

- Shield, archer, and assault battle groups each show thunder tag, thunder mark fold, and thunder spiral break from shared skill definitions.
- Starting battle with no Runtime skill snapshot shows no skill buttons and logs the missing compilation reason; it does not create hardcoded fallback buttons.
- Thunder tag can target an enemy or empty cell and creates the mark presentation.
- Thunder mark fold is disabled before a live owned mark exists.
- Thunder mark fold uses a two-stage mark-then-landing flow driven by traits.
- Thunder spiral shows directional area preview and plays channeled-area presentation.
- A used one-use skill becomes unavailable because its configured use limit is exhausted, not because of hidden `UsedHeroSkillKeys`.

## Acceptance Criteria

This proposal is accepted after implementation only when:

- `FirstSliceBattleSkillDefinitions.cs` is deleted and no code references it.
- `BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots()` is deleted and no UI/runtime path calls a hardcoded fallback factory.
- Battle skill `.tres` resources and `config/battle/battle_skill_definitions.json` are the only skill definition source for migrated skills.
- Battle snapshots include only skills granted to participating battle groups.
- Runtime consumes compiled snapshots and never loads Godot Resources directly.
- Effect execution dispatches through typed payload executors, not a central semantic `Amount` switch.
- Cost, use limit, cooldown, and charge availability are explicit snapshot/runtime facts.
- UI targeting and presentation use snapshot traits/profile ids instead of concrete skill ids.
- Old `AbilityDefinition` Resource model, old basic attack ability resources, and `AbilityComponent` are removed.
- Focused regression suites and `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` pass.

## Acceptance Evidence Required

Before marking this proposal accepted, record the exact command outputs for:

- RED regression failure before implementation.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- `git diff --check`

Record manual QA outcome with the battle id, selected battle groups, skill ids shown in HUD, and any failed reason codes observed.

## Verification Evidence - 2026-07-02

Current acceptance state: accepted. The skill authority cutover passes the focused skill, HUD, presentation, build, source-scan checks, and user-confirmed Bonefield manual QA below. The user confirmed the unrelated oversized-file guard outside this skill-system slice is not an acceptance blocker for this proposal.

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - Result: pass.
  - Evidence: command exited `0`; skill authority tests passed, including resource index/grant arrays, no hardcoded first-slice fallback, no old `AbilityDefinition` path, no old basic attack ability resources, and no HUD fallback factory.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - Result: skill-related checks pass; command exits nonzero because of an unrelated architecture guard.
  - Evidence: skill authority tests passed, including `skill command uses skill definition id not skill id`, owned grant/loadout validation, duplicate availability key rejection, use-limit exhaustion, typed damage payload, and unsupported typed executor rejection.
  - Non-blocking external failure: `oversized code files are tracked and no new ones are introduced` reports `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs:1177>1032`. This file has no skill-system diff in this cutover. Do not claim the full Target suite passed; handle the UI file-size guard in a separate decomposition slice.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - Result: pass.
  - Evidence: command exited `0`; presentation profile tests passed, including profile-id dispatch, skill event presentation fields, mark/channel observers avoiding concrete skill ids, and teleport movement barrier diagnostics.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Result: pass.
  - Evidence: command exited `0`; build output reported `0` warnings and `0` errors.
- `git diff --check`
  - Result: pass with line-ending warnings only.
  - Evidence: command exited `0`; warnings were CRLF/LF normalization notices, not whitespace errors.
- Production/resource old-path scan:
  - Command: `rg -n "FirstSliceBattleSkillDefinitions|CreateSelectedHeroSkillSnapshots|HeroSkillCommandIds|UsedHeroSkillKeys|BattleEffectPayload|AbilityDefinition|AbilityComponent|BattleAbilityQueries|legacy-attack-fallback" src assets -g "*.cs" -g "*.tres" -g "*.json"`
  - Result: no matches.
- Runtime/presentation concrete-id branch scan:
  - Command: `rg -n "SourceDefinitionId ==|SourceDefinitionId\s*!=|ThunderMarkFoldSkillId|ThunderSpiralBreakSkillId|ThunderTagThrowSkillId|TeleportToThunderMark|CreateThunderMark|StartChanneledAreaDamage" src/Runtime src/Presentation -g "*.cs"`
  - Result: no matches.
- Runtime Resource/config loading scan:
  - Command: `rg -n "GD\.Load<BattleSkillDefinitionResource|ResourceLoader\.Load<BattleSkillDefinitionResource|\.tres|battle_skill_definitions\.json" src/Runtime -g "*.cs"`
  - Result: no matches.

Manual QA evidence - 2026-07-05:

- Result: pass; user confirmed acceptance with "验收通过".
- Latest runtime log evidence: `C:\Users\qs\AppData\Roaming\Godot\app_userdata\rpg\logs\rpg-20260705.log`.
- Battle id: `strategic_battle:expedition_0001:fa57f11b2b654cd1bce24b4d82335e15`.
- Logged selected battle groups in that run: `strategic_participant:expedition_0001:hero_archer_captain:corps_0002` and `strategic_participant:expedition_0001:hero_cavalry_captain:corps_0003`.
- Runtime evidence: `StrategicBattleSkillSnapshotCompiled ... groups=2 skills=6`; the same first-slice config grants `skill_thunder_tag_throw`, `skill_thunder_mark_fold`, and `skill_thunder_spiral_break` to shield, archer, and assault starting groups.
- User QA confirms the HUD skill ids, thunder tag mark creation, mark-fold disabled-before-mark state, mark-then-landing flow, thunder spiral directional preview/presentation, and one-use availability behavior.
- Failed reason codes observed: none blocking acceptance; one-use unavailability is expected to surface through the configured availability reason, not through hidden `UsedHeroSkillKeys`.

Remaining acceptance work:

- None for this proposal.

Follow-up outside this proposal:

- Decompose or rebaseline the unrelated `WorldSiteRoot.SiteManagementHud.cs` oversized-file guard in the appropriate UI scope.
