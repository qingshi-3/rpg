# Battle Skill Configuration Authority Cutover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the accepted battle-skill authority cutover so battle skills are authored as typed Godot Resources, compiled into Runtime snapshots, executed through typed effect executors, and no longer backed by hardcoded first-slice or legacy ability-definition paths.

**Architecture:** Application compiles `BattleSkillDefinitionResource` content plus strategic hero/source grants into `BattleSkillSnapshot` objects. Runtime consumes snapshots, ownership ids, availability state, typed effect payloads, and capability interfaces; it never loads Godot Resources directly. Presentation and HUD consume targeting/input/profile traits from snapshots and Runtime events instead of comparing concrete skill ids.

**Tech Stack:** Godot 4 C#, typed `[GlobalClass]` Resources, JSON config indexes under `config/`, `.tres` authored resources under `assets/`, .NET console regression projects, low-concurrency `dotnet build`.

---

## Authority And Scope

Source implementation proposal: `gameplay-alignment/implementation-proposals/2026-07-01-battle-skill-configuration-authority.md`.

Accepted architecture documents:

- `system-design/battle-content-progression-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/strategic-management-system-architecture.md`
- `gameplay-design/details/combat-command/README.md`

Impact classification: Large.

This plan changes implementation behavior, resource taxonomy, runtime authority, command DTOs, presentation traits, and regression guardrails. It must not edit `AGENTS.md` or accepted authority documents unless implementation exposes an actual design conflict.

## Execution Rules

- [ ] Stay on the current branch. Do not create or switch branches.
- [ ] Do not add fallback skill definitions. Missing definitions, unsupported effect resources, invalid grants, invalid costs, invalid targeting profiles, and missing executors fail with structured reasons.
- [ ] Do not let Runtime load `BattleSkillDefinitionResource`, `.tres`, or config files. Runtime input is compiled snapshots only.
- [ ] Do not keep `SkillId` as a parallel Runtime alias after the command DTO migration. Runtime command payload uses `SkillDefinitionId`.
- [ ] Do not preserve `FirstSliceBattleSkillDefinitions`, `BattleSkillSnapshotFactory.CreateSelectedHeroSkillSnapshots()`, `HeroSkillCommandIds`, `UsedHeroSkillKeys`, or the old `AbilityDefinition` path at acceptance.
- [ ] Keep basic attacks on existing `BattleGroupSnapshot` attack fields in this slice. Do not migrate basic attacks to skill snapshots here.
- [ ] Run the focused regression suite for each touched layer before moving to the next layer when the code compiles.
- [ ] If a required behavior contradicts the accepted architecture, stop implementation and return to the Design Proposal Gate.

## File Structure Map

### Definition And Resource Authoring

- Create under `src/Definitions/Battle/Skills/`:
  - `BattleSkillDefinitionResource.cs`
  - `BattleSkillDefinitionEnums.cs`
  - `BattleSkillTimingResource.cs`
  - `BattleSkillInterruptPolicyResource.cs`
  - `BattleSkillTargetingProfileResource.cs`
  - `BattleSkillPresentationProfileResource.cs`
  - `BattleSkillEffectResource.cs`
  - `DamageSkillEffectResource.cs`
  - `CreateMarkSkillEffectResource.cs`
  - `TeleportToMarkSkillEffectResource.cs`
  - `ChanneledAreaDamageSkillEffectResource.cs`
  - `BattleSkillCostRuleResource.cs`
  - `NoCostSkillCostRuleResource.cs`
  - `ManaCostSkillCostRuleResource.cs`
  - `LimitedUseSkillCostRuleResource.cs`
  - `BattleSkillCooldownRuleResource.cs`
  - `NoCooldownSkillCooldownRuleResource.cs`
  - `PerGrantCooldownRuleResource.cs`
  - `ChargeCooldownRuleResource.cs`

Responsibility: typed, editor-authored, shared, stateless skill content. No Runtime services, no scene tree access, no per-cast state.

### Config And Catalog

- Create `src/Application/Config/BattleSkillDefinitionIndexLoader.cs`.
- Create `src/Application/Battle/Snapshots/BattleSkillDefinitionCatalog.cs`.
- Create `config/battle/battle_skill_definitions.json`.
- Modify `config/battle/first_slice_hero_companies.json`.

Responsibility: load and validate stable skill definition ids, resource paths, aliases, and first-slice battle-group skill assignment content.

### Snapshots And Compiler

- Create under `src/Application/Battle/Snapshots/`:
  - `BattleSkillSnapshotCompiler.cs`
  - `BattleSkillGrantSnapshot.cs`
  - `BattleSkillSnapshotEnums.cs`
  - `BattleSkillTargetingSnapshot.cs`
  - `BattleSkillTimingSnapshot.cs`
  - `BattleSkillInterruptPolicySnapshot.cs`
  - `BattleSkillPresentationSnapshot.cs`
  - `BattleSkillAvailabilityRuleSnapshot.cs`
  - `BattleSkillEffectSnapshotType.cs`
  - `BattleSkillEffectInstancePolicy.cs`
  - `NoCostSkillCostSnapshot.cs`
  - `ManaCostSkillCostSnapshot.cs`
  - `LimitedUseSkillCostSnapshot.cs`
  - `NoCooldownSkillCooldownSnapshot.cs`
  - `PerGrantCooldownSkillCooldownSnapshot.cs`
  - `ChargeCooldownSkillCooldownSnapshot.cs`
  - `DamageSkillEffectSnapshot.cs`
  - `CreateMarkSkillEffectSnapshot.cs`
  - `TeleportToMarkSkillEffectSnapshot.cs`
  - `ChanneledAreaDamageSkillEffectSnapshot.cs`
- Modify:
  - `BattleSkillSnapshot.cs`
  - `BattleSkillEffectSnapshot.cs`
  - `BattleStartSnapshot.cs`
  - `BattleGroupSnapshot.cs`
  - `BattleSnapshotBuilder.cs`
  - `LegacyBattleStartSnapshotAdapter.cs`
  - `BattleGroupSessionProbeService.cs`
  - `StrategicBattleLaunchSnapshotSyncService.cs`

Responsibility: convert Resource and strategic grant data into immutable Runtime input. Remove `CasterUnitIds`, generic `Amount`, and old targeting/effect enums from the Runtime contract.

### Strategic Grant Bridge

- Create `src/Application/World/FirstSliceBattleGroupSkillGrantProvider.cs`.
- Modify the current first-slice technical id helper only where existing callers still require it; do not expand that helper into a new gameplay authority.

Responsibility: bridge current first-slice config into accepted strategic hero/source skill grants without treating battle groups as the skill owner. Battle-group ids may remain battle-context and compatibility fields during this slice, but command validation should resolve skill ownership from the source actor and stable hero/grant identity.

### Runtime Command And Availability

- Modify:
  - `src/Application/Battle/Commands/CommandRequest.cs`
  - `src/Presentation/World/Sites/BattleRuntimeHeroSkillCommandRequestFactory.cs`
  - `src/Runtime/Battle/BattleRuntimeSession.cs`
  - `src/Runtime/Battle/BattleRuntimeState.cs`
  - `src/Runtime/Battle/BattleRuntimeActor.cs`
  - `src/Runtime/Battle/BattleRuntimePendingHeroSkillCommand.cs`
  - `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver.cs`
  - `src/Runtime/Battle/BattleAbilityController.cs`
  - `src/Runtime/Battle/BattleAbilityEffectReleaseBoundary.cs`
- Create `src/Runtime/Battle/BattleSkillAvailabilityState.cs`.

Responsibility: validate ownership, target payloads, availability, cost/use/cooldown facts, pending/active state, command acceptance, and command rejection reason codes.

### Runtime Effect Execution

- Create under `src/Runtime/Battle/Effects/`:
  - `IBattleSkillEffectExecutor.cs`
  - `BattleSkillEffectExecutorRegistry.cs`
  - `DamageSkillEffectExecutor.cs`
  - `CreateMarkSkillEffectExecutor.cs`
  - `TeleportToMarkSkillEffectExecutor.cs`
  - `ChanneledAreaDamageSkillEffectExecutor.cs`
  - `BattleEffectKindLabels.cs`
- Modify:
  - `BattleEffectResolver.cs`
  - `BattleEffectPayload.cs`
  - `BattleEffectExecutionContext.cs`
  - `BattleEffectReceiver.cs`
  - `BattleChannelDamageResolver.cs`
  - `BattleCommitBuffer.cs`
  - `BattleDisplacementCommitBoundary.cs`

Responsibility: execute typed payloads through polymorphic executors and narrow Runtime capability interfaces. No executor compares concrete `SkillDefinitionId` values.

### HUD And Presentation

- Modify:
  - `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`
  - `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - `src/Presentation/World/Sites/BattleRuntimeSkillUsageResolver.cs`
  - `src/Presentation/World/Sites/BattleRuntimeSkillFilter.cs`
  - `src/Presentation/World/Sites/BattleRuntimeHeroSkillTargetPresentation.cs`
  - `src/Presentation/World/Sites/BattleRuntimeLivePresentationObserver.cs`
  - `src/Presentation/World/Sites/BattleRuntimeHeroFramePresenter.cs`
  - `src/Presentation/World/Sites/BattleRuntimeSkillSlot.cs`
  - `src/Presentation/Battle/Entities/BattleUnitRoot.SkillPresentation.cs`
  - `src/Runtime/Battle/Events/BattleEvent.cs`
- Create:
  - `src/Presentation/World/Sites/BattleRuntimeMarkTargetingPresentation.cs`
  - `src/Presentation/World/Sites/BattleRuntimeSkillProfilePresentationObserver.cs`

Responsibility: drive skill input, previews, disabled states, cast FX, mark FX, teleport FX, and channel FX from snapshot traits and Runtime event presentation profile fields.

### Old Path Removal

- Delete old skill definition path:
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
  - `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver.ThunderMark.cs`
- Delete old ability Resource path:
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
- Delete old basic attack ability resources:
  - `assets/battle/abilities/militia_basic_attack.tres`
  - `assets/battle/abilities/player_knight_basic_attack.tres`
  - `assets/battle/abilities/skeleton_archer_basic_attack.tres`
  - `assets/battle/abilities/skeleton_warrior_basic_attack.tres`

Responsibility: ensure one final skill and effect authority.

## Phase 0: RED Guardrail Tests

Purpose: make the current hardcoded path fail before new implementation work begins.

Files:

- Create `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleSkillConfigurationAuthority.cs`.
- Create `tests/TargetBattleArchitectureRegression/TargetBattleSkillConfigurationAuthorityRegressionCases.cs`.
- Create `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.SkillConfigurationAuthority.cs`.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`.
- Modify `tests/TargetBattleArchitectureRegression/Program.cs`.
- Modify `tests/BattleHitFeedbackRegression/Program.cs`.

Steps:

- [ ] Add source guard tests in `WorldSiteDeploymentCacheRegressionCases.BattleSkillConfigurationAuthority.cs`:
  - `BattleSkillAuthorityDeletesHardcodedFirstSliceSkillPath`
  - `BattleSkillAuthorityDeletesLegacyAbilityDefinitionPath`
  - `BattleSkillAuthorityDeletesOldBasicAttackAbilityResources`
  - `BattleSkillAuthorityRejectsHudFallbackFactory`
- [ ] Each source guard test reads the project root and asserts:
  - `src/Definitions/Battle/Skills/FirstSliceBattleSkillDefinitions.cs` does not exist.
  - `src/Application/Battle/Snapshots/BattleSkillSnapshotFactory.cs` does not exist.
  - `src/Application/Battle/Commands/HeroSkillCommandIds.cs` does not exist.
  - `src/Definitions/Battle/Abilities/AbilityDefinition.cs` does not exist.
  - `src/Presentation/Battle/Entities/AbilityComponent.cs` does not exist.
  - `WorldSiteRoot.BattleRuntimeCommandHud.cs` does not contain `CreateSelectedHeroSkillSnapshots`.
  - `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver*.cs` does not contain `TeleportToThunderMark`, `HeroSkillCommandIds`, or `ThunderMarkFoldSkillId`.
  - `BattleSkillEffectSnapshot.cs` does not expose shared semantic fields named `Amount`, `DurationSeconds`, `TickIntervalSeconds`, and `Radius`.
- [ ] Add behavior-contract tests in `TargetBattleSkillConfigurationAuthorityRegressionCases.cs`:
  - `SkillCommandUsesSkillDefinitionIdNotSkillId`
  - `RuntimeRequiresOwnedGrantOrLoadout`
  - `RuntimeRejectsDuplicateGrantAvailabilityKeys`
  - `RuntimeRejectsUseLimitExhaustedThroughAvailabilityState`
  - `TypedDamagePayloadHasBaseDamageNotAmount`
  - `TypedExecutorRegistryRejectsUnsupportedPayload`
- [ ] Add presentation-contract tests in `BattleHitFeedbackRegressionCases.SkillConfigurationAuthority.cs`:
  - `SkillPresentationUsesProfileIdsNotThunderSkillIds`
  - `SkillUsedEventsExposePresentationProfileFields`
  - `MarkAndChannelPresentationObserversAvoidConcreteSkillIds`
- [ ] Register the new test methods in each `Program.cs`.
- [ ] Run the focused suites and record the RED output in the implementation proposal acceptance evidence section when implementation starts:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`

Expected result: the suites fail against the current code because hardcoded skill files, generic payload fields, `SkillId`, thunder-id branches, and old ability resources still exist.

Exit criteria:

- [ ] RED failures point at the intended old paths.
- [ ] No production code has been changed in this phase.

## Phase 1: Resource Schema, Config Index, And Authored Skill Assets

Purpose: create the authoring surface and content files without making Runtime consume Resources.

Files:

- Create all Resource classes listed in the Definition And Resource Authoring section.
- Create `src/Application/Config/BattleSkillDefinitionIndexLoader.cs`.
- Create `config/battle/battle_skill_definitions.json`.
- Create skill resources under `assets/battle/skills/`.
- Modify `config/battle/first_slice_hero_companies.json`.

Steps:

- [ ] Implement C# Resource classes with `[GlobalClass] public partial class ... : Resource` for concrete authoring types.
- [ ] Keep abstract Resource bases free of execution methods. They exist only for Inspector arrays and compiler dispatch.
- [ ] Export these `BattleSkillDefinitionResource` fields:
  - `SkillDefinitionId`
  - `DisplayName`
  - `IconText`
  - `Tags`
  - `CommandChannel`
  - `SkillType`
  - `Timing`
  - `InterruptPolicy`
  - `CostRules`
  - `CooldownRules`
  - `Targeting`
  - `Effects`
  - `Presentation`
- [ ] Implement definition enums for command channel, skill type, input flow, target kind, range metric, area shape, direction mode, cost pay timing, refund policy, cooldown start, damage type, and mark kind.
- [ ] Implement cost Resource families:
  - `NoCostSkillCostRuleResource`
  - `ManaCostSkillCostRuleResource`
  - `LimitedUseSkillCostRuleResource`
- [ ] Implement cooldown Resource families:
  - `NoCooldownSkillCooldownRuleResource`
  - `PerGrantCooldownRuleResource`
  - `ChargeCooldownRuleResource`
- [ ] Implement effect Resource families:
  - `DamageSkillEffectResource`
  - `CreateMarkSkillEffectResource`
  - `TeleportToMarkSkillEffectResource`
  - `ChanneledAreaDamageSkillEffectResource`
- [ ] Implement `BattleSkillDefinitionIndexLoader` so it validates:
  - nonempty `skillDefinitionId`
  - nonempty `resourcePath`
  - duplicate ids
  - duplicate aliases
  - alias collision with another canonical id
  - missing required ids for the five migrated first-slice skills
- [ ] Create `config/battle/battle_skill_definitions.json` with canonical ids:
  - `skill_shield_barrier`
  - `skill_sun_piercer`
  - `skill_thunder_tag_throw`
  - `skill_thunder_mark_fold`
  - `skill_thunder_spiral_break`
- [ ] Add migration aliases in the index:
  - `first_slice_skill_shield_barrier`
  - `first_slice_skill_sun_piercer`
  - `first_slice_skill_thunder_tag_throw`
  - `first_slice_skill_thunder_mark_fold`
  - `first_slice_skill_thunder_spiral_break`
- [ ] Author `assets/battle/skills/skill_shield_barrier.tres`:
  - target actor
  - range `8`
  - damage `12`
  - recovery `0.2`
  - interrupt basic attack windup `true`
  - limited uses `1`
  - presentation profile `skill_default_damage`
- [ ] Author `assets/battle/skills/skill_sun_piercer.tres`:
  - target actor
  - range `8`
  - damage `18`
  - recovery `0.2`
  - interrupt basic attack windup `true`
  - limited uses `1`
  - presentation profile `skill_default_damage`
- [ ] Author `assets/battle/skills/skill_thunder_tag_throw.tres`:
  - select actor or cell input flow
  - range `8`
  - damage `12`
  - create mark kind `thunder_mark`
  - mark lifetime `8.0`
  - releases without occupying caster `true`
  - can cancel basic attack recovery `true`
  - presentation profile `skill_mark_projectile`
- [ ] Author `assets/battle/skills/skill_thunder_mark_fold.tres`:
  - mark-then-landing-cell input flow
  - requires mark kind `thunder_mark`
  - landing radius `3`
  - teleport-to-mark effect
  - can interrupt active channel `true`
  - presentation profile `skill_mark_teleport`
- [ ] Author `assets/battle/skills/skill_thunder_spiral_break.tres`:
  - direction area input flow
  - range `3`
  - area shape grid radius
  - damage `14`
  - duration `1.6`
  - tick interval `0.2`
  - radius `1`
  - follows caster `true`
  - uses target offset `true`
  - suppress actor cast FX `true`
  - hold cast animation during action `true`
  - presentation profile `skill_channeled_area`
- [ ] Modify `config/battle/first_slice_hero_companies.json` from single `skillId` to `skillDefinitionIds` arrays:
  - shield battle group: `["skill_shield_barrier"]`
  - archer battle group: `["skill_sun_piercer"]`
  - assault battle group: `["skill_thunder_tag_throw", "skill_thunder_mark_fold", "skill_thunder_spiral_break"]`
- [ ] Extend the existing first-slice config loader validation so `skillDefinitionIds` arrays are nonempty and contain no duplicate ids per battle group.

Validation commands:

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`

Exit criteria:

- [ ] Resource classes compile.
- [ ] Config index loader rejects malformed indexes with deterministic reason codes.
- [ ] Authored skill resources exist at the paths referenced by `battle_skill_definitions.json`.
- [ ] Runtime code still does not load Resources.

## Phase 2: Snapshot Contract And Compiler

Purpose: replace generic snapshot fields with typed Runtime contracts and make Application own Resource-to-snapshot conversion.

Files:

- Create all snapshot classes listed in the Snapshots And Compiler section.
- Modify `BattleSkillSnapshot.cs`.
- Replace `BattleSkillEffectSnapshot.cs` with an abstract base and sealed concrete payloads.
- Modify `BattleStartSnapshot.cs`.
- Modify `BattleRuntimeSession.cs` clone and validation logic.

Steps:

- [ ] Replace `BattleSkillSnapshot.SkillId` with `SkillDefinitionId`.
- [ ] Add `GrantedSkillId`, `LoadoutSlotId`, `OwnerHeroId`, `OwnerBattleGroupId`, and `RuntimeCommanderGroupId` to `BattleSkillSnapshot`.
- [ ] Replace top-level targeting fields with `BattleSkillTargetingSnapshot`.
- [ ] Replace timing fields with `BattleSkillTimingSnapshot`.
- [ ] Replace interrupt booleans with `BattleSkillInterruptPolicySnapshot`.
- [ ] Replace generic effect fields with typed effect snapshots:
  - `DamageSkillEffectSnapshot`
  - `CreateMarkSkillEffectSnapshot`
  - `TeleportToMarkSkillEffectSnapshot`
  - `ChanneledAreaDamageSkillEffectSnapshot`
- [ ] Add cost snapshots:
  - `NoCostSkillCostSnapshot`
  - `ManaCostSkillCostSnapshot`
  - `LimitedUseSkillCostSnapshot`
- [ ] Add cooldown snapshots:
  - `NoCooldownSkillCooldownSnapshot`
  - `PerGrantCooldownSkillCooldownSnapshot`
  - `ChargeCooldownSkillCooldownSnapshot`
- [ ] Implement `BattleSkillDefinitionCatalog` to hold canonical definitions and alias normalization.
- [ ] Implement `BattleSkillSnapshotCompiler` mappings:
  - definition enums to snapshot enums
  - targeting Resource to targeting snapshot
  - timing Resource to timing snapshot
  - interrupt Resource to interrupt snapshot
  - cost Resource to cost snapshot
  - cooldown Resource to cooldown snapshot
  - effect Resource to typed effect snapshot
  - presentation Resource to presentation snapshot
- [ ] Make compiler failures explicit:
  - `battle_skill_definition_missing`
  - `battle_skill_definition_duplicate`
  - `battle_skill_grant_duplicate`
  - `battle_skill_loadout_slot_duplicate`
  - `battle_skill_owner_missing`
  - `battle_skill_effect_resource_unsupported`
  - `battle_skill_effect_payload_invalid`
  - `battle_skill_targeting_invalid`
- [ ] Update `BattleRuntimeSession.ValidateSkillSnapshots` to validate the new snapshot contract:
  - `SkillDefinitionId` present
  - `GrantedSkillId` or `LoadoutSlotId` present
  - owner group present
  - target/input consistency
  - typed payload values valid
  - cost/cooldown snapshot values valid
  - no duplicate `GrantedSkillId`
  - no duplicate owner+loadout slot pair
- [ ] Update `BattleRuntimeSession.CloneSkillDefinitions` to preserve typed nested snapshots without clamping gameplay values.
- [ ] Update tests in `TargetBattleHeroSkillRegressionCases.cs` and `TargetBattleThunderMarkSkillRegressionCases*.cs` to use helper builders for typed skill snapshots instead of constructing `Amount` payloads inline.

Validation commands:

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`

Exit criteria:

- [ ] Tests no longer reference `BattleSkillEffectSnapshot.Amount`, `DurationSeconds`, `TickIntervalSeconds`, or `Radius`.
- [ ] Runtime snapshot validation rejects malformed typed payloads before battle begins.

## Phase 3: Strategic Grants And Battle Snapshot Integration

Purpose: compile only the skills granted to participating heroes/sources into the battle snapshot.

Files:

- Create `BattleSkillGrantSnapshot.cs`.
- Create `FirstSliceBattleGroupSkillGrantProvider.cs`.
- Modify:
  - `BattleSnapshotBuilder.cs`
  - `LegacyBattleStartSnapshotAdapter.cs`
  - `BattleGroupSessionProbeService.cs`
  - `StrategicBattleLaunchSnapshotSyncService.cs`
  - `BattleGroupSnapshot.cs`

Steps:

- [ ] Implement `BattleSkillGrantSnapshot` fields:
  - `GrantedSkillId`
  - `LoadoutSlotId`
  - `OwnerHeroId`
  - `OwnerBattleGroupId`
  - `RuntimeCommanderGroupId`
  - `SkillDefinitionId`
  - `SourceKind`
  - `SourceId`
  - `SkillLevel`
- [ ] Implement `FirstSliceBattleGroupSkillGrantProvider` so it:
  - reads validated first-slice battle-group config
  - emits one grant per configured `SkillDefinitionId`
  - uses stable grant ids based on assignment content and slot ids
  - emits grants only for participating player battle groups
  - never synthesizes extra thunder skills in code
- [ ] Replace `BattleSnapshotBuilder.Build()` unconditional `CreateSelectedHeroSkillSnapshots()` call with catalog + grant provider + compiler.
- [ ] Ensure `BuildBattleRuntimeSkillSnapshots()` in Presentation returns an empty list if the Runtime snapshot has no compiled skills.
- [ ] Update bridge and probe paths so `LegacyBattleStartSnapshotAdapter.ToSnapshot()` and `BattleGroupSessionProbeService.PrepareSnapshot()` compile skills from participating groups.
- [ ] Update `StrategicBattleLaunchSnapshotSyncService` copy logic to copy every nested skill snapshot field and typed effect payload.
- [ ] Delete `BattleSkillSnapshotFactory.cs` and `FirstSliceBattleSkillDefinitions.cs` once no caller remains.

Validation commands:

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`

Exit criteria:

- [ ] Each participating hero/source receives only its configured skill grants.
- [ ] Shared `SkillDefinitionId` values may appear in multiple hero/source grants without becoming ambiguous.
- [ ] Reserve or non-participating heroes/sources do not contribute skill snapshots.
- [ ] `BattleSkillSnapshotFactory.cs` and `FirstSliceBattleSkillDefinitions.cs` are deleted.

## Phase 4: Command DTO Rename And Ownership Validation

Purpose: remove fragile `SkillId` command semantics and validate skill use through compiled grant/loadout ownership.

Files:

- Modify:
  - `CommandRequest.cs`
  - `BattleRuntimePendingHeroSkillCommand.cs`
  - `BattleRuntimeHeroSkillCommandResolver.cs`
  - `BattleAbilityController.cs`
  - `BattleRuntimeActor.cs`
  - `BattleEvent.cs`
  - `BattleRuntimeHeroSkillCommandRequestFactory.cs`
  - all command submitters and tests that construct `CommandRequest`

Steps:

- [ ] Rename `CommandRequest.SkillId` to `CommandRequest.SkillDefinitionId`.
- [ ] Add `GrantedSkillId` and `LoadoutSlotId` to command request or resolved pending command state when needed for ownership and availability.
- [ ] Rename `BattleRuntimePendingHeroSkillCommand.SkillId` to `SkillDefinitionId`.
- [ ] Store `GrantedSkillId` and `LoadoutSlotId` on pending commands.
- [ ] Update accepted/rejected/failed Runtime events so `SourceDefinitionId` is the normalized `SkillDefinitionId`.
- [ ] Validate the source actor's stable hero identity owns a compiled snapshot where:
  - `SkillDefinitionId` matches the request
  - `OwnerHeroId` matches the source actor's stable hero id
  - grant or loadout slot matches when the request carries those ids
- [ ] Keep `BattleGroupId` as battle-context and command/report attribution only; it must not be the hero skill ownership authority.
- [ ] Replace target validation branches with `BattleSkillTargetingSnapshot.InputFlow`, `TargetKind`, `Range`, `RangeMetric`, `RequiresSelectedMark`, `RequiredMarkKind`, `LandingRadius`, `DirectionMode`, and `AreaShape`.
- [ ] Remove `NormalizeSkillId` alias usage from Runtime command matching after Application has normalized aliases.
- [ ] Delete `HeroSkillCommandIds.cs` when all callers use compiled snapshot ids or test constants local to test files.
- [ ] Delete `BattleRuntimeHeroSkillCommandResolver.ThunderMark.cs` after mark validation is trait-driven.

Validation commands:

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- `rg -n "CommandRequest\\s*\\{[^}]*SkillId|\\.SkillId\\b|HeroSkillCommandIds|ThunderMarkFoldSkillId|TeleportToThunderMark" src tests -g "*.cs"`

Expected `rg` result: no production references. Test references are allowed only where the test asserts deletion or scans old text absence.

Exit criteria:

- [ ] Runtime command submission no longer has a `SkillId` property.
- [ ] Skill definition lookup is stable under battle id, hero id, actor id, and file path changes.
- [ ] Concrete thunder skill ids do not drive Runtime validation.

## Phase 5: Availability State

Purpose: replace `UsedHeroSkillKeys` with explicit per-grant availability facts.

Files:

- Create `src/Runtime/Battle/BattleSkillAvailabilityState.cs`.
- Modify:
  - `BattleRuntimeState.cs`
  - `BattleRuntimeHeroSkillCommandResolver.cs`
  - `BattleAbilityController.cs`
  - `BattleAbilityEffectReleaseBoundary.cs`
  - `BattleRuntimeSkillUsageResolver.cs`
  - `BattleEvent.cs`

Steps:

- [ ] Implement `BattleSkillAvailabilityState` keyed by `GrantedSkillId`, with fallback to `LoadoutSlotId` only for fixtures that omit grants.
- [ ] Track:
  - `RemainingUses`
  - `CurrentCharges`
  - `NextChargeReadyAtSeconds`
  - `CooldownReadyAtSeconds`
  - `PendingCommandIds`
  - `ActiveActionIds`
- [ ] Initialize availability from snapshot cost and cooldown facts at battle start.
- [ ] On command submission, reject before queueing with:
  - `skill_grant_missing`
  - `skill_on_cooldown`
  - `skill_no_charges`
  - `skill_use_limit_exhausted`
  - `skill_resource_insufficient`
  - `skill_already_pending`
  - `skill_already_active`
- [ ] On accepted command, reserve use, charge, cooldown, or pending state according to cost/cooldown timing.
- [ ] On skill action start, mark active action id.
- [ ] On effect release failure, apply configured refund policy and emit a failure reason.
- [ ] On action completion or failure, clear active/pending state and emit availability facts for HUD.
- [ ] Keep first migrated skills at `LimitedUse.MaxUses = 1`.
- [ ] Keep cooldown default at `NoCooldown`.
- [ ] Keep mana default at `NoCost`; when a mana cost references a missing pool, reject with `skill_resource_pool_missing`.
- [ ] Remove `UsedHeroSkillKeys` and all `BuildSkillKey` one-use logic.

Validation commands:

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- `rg -n "UsedHeroSkillKeys|hero_skill_already_used|BuildSkillKey" src tests -g "*.cs"`

Expected `rg` result: no production references. Test references are allowed only where asserting absence.

Exit criteria:

- [ ] One-use skills become unavailable because `LimitedUseSkillCostSnapshot` is exhausted.
- [ ] Duplicate submissions reject through availability state instead of a hidden battle-group+skill key.

## Phase 6: Typed Effect Executors And Runtime Capability Boundaries

Purpose: remove central effect-kind payload switching and execute typed payloads through reusable code.

Files:

- Create executor files listed in Runtime Effect Execution.
- Modify:
  - `BattleEffectResolver.cs`
  - `BattleEffectPayload.cs`
  - `BattleAbilityEffectReleaseBoundary.cs`
  - `BattleEffectExecutionContext.cs`
  - `BattleEffectReceiver.cs`
  - `BattleChannelDamageResolver.cs`
  - `BattleDisplacementCommitBoundary.cs`
  - `BattleRuntimeSpatialMark.cs`
  - `BattleRuntimeActiveChannel.cs`

Steps:

- [ ] Define `IBattleSkillEffectExecutor` with:
  - `bool CanExecute(BattleSkillEffectSnapshot payload)`
  - `IReadOnlyList<BattleEvent> Execute(BattleEffectExecutionContext context, BattleSkillEffectSnapshot payload)`
- [ ] Implement `BattleSkillEffectExecutorRegistry` with an ordered executor list and a fail-fast unsupported payload path.
- [ ] Convert `BattleAbilityEffectReleaseBoundary` to pass the typed payload directly to the registry.
- [ ] Remove `BattleEffectPayload` after no caller needs the generic wrapper.
- [ ] Implement `DamageSkillEffectExecutor` using `DamageSkillEffectSnapshot.BaseDamage` and the existing `BattleEffectReceiver` / `BattleCommitBuffer` path.
- [ ] Implement `CreateMarkSkillEffectExecutor` using mark kind, lifetime, attachment, owner, source command, action, and definition facts.
- [ ] Generalize `BattleDisplacementCommitBoundary` from thunder-specific method names to mark-teleport validation and commit methods.
- [ ] Implement `TeleportToMarkSkillEffectExecutor` using required mark kind, landing radius, selected mark id, topology, footprint, occupancy, and displacement commit boundary.
- [ ] Implement `ChanneledAreaDamageSkillEffectExecutor` using base damage, damage type, duration, tick interval, area shape, radius, follows caster, and target offset.
- [ ] Update `BattleChannelDamageResolver` and active channel state to read typed channel facts instead of generic amount/radius fields.
- [ ] Add narrow capability interfaces used in this slice:
  - `IDamageReceiver`
  - `IResourcePool`
- [ ] Keep additional capability interfaces out of production code until a migrated effect uses them.

Validation commands:

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- `rg -n "BattleEffectPayload|switch\\s*\\([^\\)]*EffectKind|TeleportToThunderMark|CreateThunderMark|StartChanneledAreaDamage|\\.Amount\\b|DurationSeconds|TickIntervalSeconds|\\.Radius\\b" src/Runtime src/Application -g "*.cs"`

Expected `rg` result: no generic Runtime effect payload authority. Remaining typed class property names such as `ChanneledAreaDamageSkillEffectSnapshot.DurationSeconds` are valid.

Exit criteria:

- [ ] Damage, mark creation, teleport, and channel effects dispatch by payload class.
- [ ] Executors do not compare concrete skill ids.
- [ ] Teleport and channel behavior remain reusable effect primitives.

## Phase 7: HUD Input Flow And Preview Traits

Purpose: make HUD skill buttons, disabled states, target picking, and previews driven by snapshot traits.

Files:

- Modify:
  - `WorldSiteRoot.BattleRuntimeCommandHud.cs`
  - `BattleRuntimeSkillUsageResolver.cs`
  - `BattleRuntimeSkillFilter.cs`
  - `BattleRuntimeHeroSkillTargetPresentation.cs`
  - `BattleRuntimeHeroSkillCommandRequestFactory.cs`
  - `BattleRuntimeSkillSlot.cs`
- Create `BattleRuntimeMarkTargetingPresentation.cs`.
- Delete `BattleRuntimeThunderFoldTargetingPresentation.cs`.

Steps:

- [ ] Replace `ThunderFoldTargetingStage` with `SkillTargetingStage.None`, `PrimarySelection`, and `SecondarySelection`.
- [ ] Replace `_battleRuntimeThunderFoldSelectedMarkId` with `_battleRuntimeSelectedRuntimeAnchorId`.
- [ ] Replace `_battleRuntimeThunderFoldSelectedMarkSurface` with `_battleRuntimeSelectedRuntimeAnchorSurface`.
- [ ] Dispatch skill press by `BattleSkillTargetingSnapshot.InputFlow`:
  - `ImmediateSelf`
  - `SelectActor`
  - `SelectCell`
  - `SelectActorOrCell`
  - `SelectMarkThenLandingCell`
  - `SelectDirectionArea`
- [ ] Make range rendering read `BattleSkillTargetingSnapshot.Range`.
- [ ] Make preview rendering read `PreviewProfileId`, `AreaShape`, `AreaRadius`, `DirectionMode`, and `LandingRadius`.
- [ ] Make `BattleRuntimeSkillUsageResolver` consume Runtime availability facts for pending, active, use-limit, cooldown, charges, and resource shortage.
- [ ] Make mark-dependent disabled state read `RequiresSelectedMark` and `RequiredMarkKind`.
- [ ] Rename spiral helper methods:
  - `TryResolveThunderSpiralTargetCenter` to `TryResolveDirectionalAreaCenter`
  - `BuildThunderSpiralAreaCells` to `BuildAreaPreviewCells`
- [ ] Parameterize mark targeting presentation by `MarkKind` and `LandingRadius`.
- [ ] Ensure no HUD code calls or recreates first-slice fallback skill snapshots.

Validation commands:

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `rg -n "ThunderFold|ThunderSpiral|ThunderTag|HeroSkillCommandIds|CreateSelectedHeroSkillSnapshots|CasterUnitIds" src/Presentation/World/Sites -g "*.cs"`

Expected `rg` result: old names absent except deleted-file references in git history are not relevant.

Exit criteria:

- [ ] A new skill using existing input and preview traits requires Resource authoring plus existing runtime effect primitives, not a new HUD branch.

## Phase 8: Presentation Profile Refactor

Purpose: make live and playback presentation consume profile facts from Runtime events.

Files:

- Modify:
  - `BattleEvent.cs`
  - `BattleRuntimeLivePresentationObserver.cs`
  - `BattleUnitRoot.SkillPresentation.cs`
  - `BattleRuntimeHeroFramePresenter.cs`
- Create:
  - `BattleRuntimeSkillProfilePresentationObserver.cs`
- Delete:
  - `BattleRuntimeThunderTagPresentationObserver.cs`
  - `BattleRuntimeThunderSpiralPresentationObserver.cs`

Steps:

- [ ] Add presentation fields to `BattleEvent`:
  - `PresentationProfileId`
  - `CastFxProfileId`
  - `ImpactFxProfileId`
  - `MarkFxProfileId`
  - `AreaFxProfileId`
  - `SuppressActorCastFx`
  - `HoldCastAnimationDuringAction`
- [ ] Copy presentation snapshot fields into `CommandAccepted`, `SkillUsed`, mark-created, teleport, channel-start, and effect events where Presentation consumes them.
- [ ] Replace thunder tag offhand checks with `PresentationProfileId == "skill_mark_projectile"` plus `SuppressActorCastFx` or equivalent event traits.
- [ ] Replace thunder spiral checks with `PresentationProfileId == "skill_channeled_area"` plus `HoldCastAnimationDuringAction`.
- [ ] Keep current FX scenes as profile implementations in `BattleUnitRoot.SkillPresentation.cs`.
- [ ] Rename profile-oriented methods:
  - `PlayMarkProjectilePresentation`
  - `PlayRuntimeMarkPresentation`
  - `PlayChanneledAreaPresentation`
- [ ] Ensure Presentation does not calculate gameplay truth from profile ids. Profiles select visuals only.

Validation commands:

- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- `rg -n "ThunderTagPresentationObserver|ThunderSpiralPresentationObserver|ThunderTagThrowSkillId|ThunderSpiralBreakSkillId|SourceDefinitionId ==|SourceDefinitionId\\s*!=|HeroSkillCommandIds" src/Presentation src/Runtime -g "*.cs"`

Expected `rg` result: production presentation code has no concrete skill-id behavior switch.

Exit criteria:

- [ ] Mark projectile, teleport, and channeled area presentation still render through existing assets.
- [ ] Presentation selection is profile-oriented.

## Phase 9: Old Ability Path Removal

Purpose: remove the parallel legacy ability authoring model and basic-attack ability resources.

Files:

- Delete all old ability model files listed in Old Path Removal.
- Delete old basic attack resources listed in Old Path Removal.
- Modify:
  - `src/Definitions/Battle/BattleUnitDefinition.cs`
  - `src/Presentation/Battle/Entities/BattleUnitFactory.cs`
  - `src/Presentation/Battle/Actions/BattleActionResult.cs`
  - `src/Presentation/Battle/Intents/BattleIntent.cs`
  - `src/Presentation/Battle/Intents/BattleIntentTemplate.cs`
  - `assets/battle/units/neutral/首领_噬法者/unit.tres`

Steps:

- [ ] Remove `BattleUnitDefinition.Abilities`.
- [ ] Keep `AttackDamage`, `AttackRange`, attack timing, and attack presentation fields.
- [ ] Remove `AbilityComponent` copying from `BattleUnitFactory`.
- [ ] Remove the `legacy-attack-fallback` warning.
- [ ] Remove `AbilityDefinition Ability` from `BattleActionResult`.
- [ ] Remove `AbilityDefinition PreferredAbility` from `BattleIntent`.
- [ ] Remove the `preferredAbility` argument from `BattleIntentTemplate.Create`.
- [ ] Remove the `AbilityDefinition` ext_resource and property references from `assets/battle/units/neutral/首领_噬法者/unit.tres`.
- [ ] Delete the four old basic attack `.tres` resources.
- [ ] Keep `assets/battle/abilities/fx/` untouched because those are presentation assets.

Validation commands:

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- `rg -n "AbilityDefinition|AbilityEffect|DamageAbilityEffect|AbilityComponent|BattleAbilityQueries|PreferredAbility|legacy-attack-fallback|basic_attack\\.tres" src assets tests -g "*.cs" -g "*.tres"`

Expected `rg` result: no production or resource references to the removed model. Tests may contain absence assertions.

Exit criteria:

- [ ] Basic attacks still compile through existing attack-action fields.
- [ ] There is no second ability Resource model.

## Phase 10: Regression Fixture Migration And Anti-Rot Sweep

Purpose: update tests and source guardrails so they protect the new authority instead of the old hardcoded shape.

Files:

- Modify relevant files under:
  - `tests/WorldSiteDeploymentCacheRegression/`
  - `tests/TargetBattleArchitectureRegression/`
  - `tests/BattleHitFeedbackRegression/`

Steps:

- [ ] Replace tests named around first-slice hardcoded skill definitions with tests around Resource catalog, grants, and compiler output.
- [ ] Replace fixture helpers that construct `BattleSkillSnapshot.SkillId` with helpers that set `SkillDefinitionId`, `GrantedSkillId`, `LoadoutSlotId`, `OwnerHeroId`, `OwnerBattleGroupId`, `Targeting`, `Timing`, `InterruptPolicy`, costs, cooldowns, and typed effects.
- [ ] Replace fixture helpers that construct `BattleSkillEffectSnapshot.Amount` with typed effect helper methods:
  - `DamageSkillEffectSnapshot(BaseDamage: ...)`
  - `CreateMarkSkillEffectSnapshot(MarkKind: ...)`
  - `TeleportToMarkSkillEffectSnapshot(LandingRadius: ...)`
  - `ChanneledAreaDamageSkillEffectSnapshot(BaseDamage: ..., DurationSeconds: ..., TickIntervalSeconds: ..., Radius: ...)`
- [ ] Update command tests to submit `SkillDefinitionId`.
- [ ] Add source guard tests for:
  - no `FirstSliceBattleSkillDefinitions`
  - no `BattleSkillSnapshotFactory`
  - no `HeroSkillCommandIds`
  - no old ability Resource path
  - no Runtime Resource loading
  - no concrete skill-id HUD/presentation branches
- [ ] Keep test constants local to test fixtures where they express content ids, not system authority.

Validation commands:

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`

Exit criteria:

- [ ] All focused suites pass.
- [ ] Tests fail if a hardcoded fallback or old ability authority returns.

## Phase 11: Final Verification And Manual QA

Purpose: prove the implementation meets the accepted proposal before marking it accepted.

Automated commands:

- [ ] `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- [ ] `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- [ ] `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- [ ] `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- [ ] `git diff --check`
- [ ] `dotnet build-server shutdown`

Final source scans:

- [ ] `rg -n "FirstSliceBattleSkillDefinitions|CreateSelectedHeroSkillSnapshots|HeroSkillCommandIds|UsedHeroSkillKeys|BattleEffectPayload|AbilityDefinition|AbilityComponent|BattleAbilityQueries|legacy-attack-fallback" src assets tests -g "*.cs" -g "*.tres" -g "*.json"`
- [ ] `rg -n "SourceDefinitionId ==|SourceDefinitionId\\s*!=|ThunderMarkFoldSkillId|ThunderSpiralBreakSkillId|ThunderTagThrowSkillId|TeleportToThunderMark|CreateThunderMark|StartChanneledAreaDamage" src/Runtime src/Presentation -g "*.cs"`
- [ ] `rg -n "GD\\.Load<BattleSkillDefinitionResource|ResourceLoader\\.Load<BattleSkillDefinitionResource|\\.tres|battle_skill_definitions\\.json" src/Runtime -g "*.cs"`

Expected scan result:

- Runtime contains no Resource loading or config loading.
- Production code contains no old hardcoded factory or legacy ability model.
- Presentation and Runtime contain no concrete skill-id behavior branches.
- Remaining content ids appear only in config, resources, catalog aliases, local tests, or reports.

Manual QA:

- [ ] Start one Bonefield assault through the current playable path.
- [ ] Confirm shield battle group shows only `skill_shield_barrier`.
- [ ] Confirm archer battle group shows only `skill_sun_piercer`.
- [ ] Confirm assault battle group shows `skill_thunder_tag_throw`, `skill_thunder_mark_fold`, and `skill_thunder_spiral_break`.
- [ ] Confirm a battle with no Runtime skill snapshots shows no skill buttons and logs a compilation or ownership reason instead of creating fallback buttons.
- [ ] Confirm thunder tag can target an enemy or empty cell and creates the mark presentation.
- [ ] Confirm thunder mark fold is disabled before a live owned mark exists.
- [ ] Confirm thunder mark fold uses mark selection first, then legal landing-cell selection.
- [ ] Confirm thunder spiral shows directional area preview and plays channeled-area presentation.
- [ ] Confirm a used one-use skill becomes unavailable through `skill_use_limit_exhausted` or equivalent availability reason.

Acceptance evidence to record in the implementation proposal:

- [ ] RED regression failure command output from Phase 0.
- [ ] Focused regression command outputs.
- [ ] Final build output.
- [ ] `git diff --check` output.
- [ ] Manual QA battle id, selected battle groups, HUD skill ids, and observed disabled reason codes.

## Checkpoints And Stop Gates

- [ ] Stop after Phase 0 if RED tests fail for unrelated existing broken tests instead of the intended skill-system guards.
- [ ] Stop after Phase 1 if Godot C# Resource authoring requires a different serialization shape than the accepted Resource model.
- [ ] Stop after Phase 2 if typed snapshots cannot represent any accepted current skill without reintroducing generic semantic fields.
- [ ] Stop after Phase 3 if strategic hero/source ownership cannot produce stable `GrantedSkillId` or `LoadoutSlotId` values without deriving from Runtime battle ids, runtime battle-group ids, or actor ids.
- [ ] Stop after Phase 4 if any UI or Runtime caller still needs `SkillId` as a parallel alias after command submission is migrated.
- [ ] Stop after Phase 5 if availability cannot distinguish pending, active, cooldown, charge, use-limit, and resource shortage states from Runtime facts.
- [ ] Stop after Phase 6 if an effect executor needs to inspect a concrete target type instead of a Runtime capability or typed payload.
- [ ] Stop after Phase 7 if a new targeting flow requires concrete skill-id checks instead of snapshot traits.
- [ ] Stop after Phase 8 if presentation profile ids start deciding gameplay legality.
- [ ] Stop after Phase 9 if deleting old ability files removes basic attack behavior; basic attacks must continue through attack-action snapshot fields in this slice.

## Implementation Order Summary

1. RED guardrails.
2. Resource schema and authored content.
3. Snapshot compiler and typed payload contract.
4. Grant provider and snapshot build integration.
5. Command DTO rename and ownership validation.
6. Availability state.
7. Typed effect executors.
8. HUD trait-driven targeting.
9. Presentation profile dispatch.
10. Legacy ability deletion.
11. Fixture migration, full verification, and manual QA.
