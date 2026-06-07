# Runtime Skill Effect Action Flow

Status: Archived - aligned with current definition-backed skill effect implementation; manual QA not retained as active work per user cleanup request on 2026-06-07

## Relationship Metadata

Requirement Id: `REQ-RUNTIME-SKILL-EFFECT-ACTION-ARCHITECTURE`

Originating Design Proposal: `design-proposals/archived/2026-06-06-runtime-skill-effect-architecture/`

Parent Implementation Proposal: None. The earlier one-hero active-skill proposal was deleted during 2026-06-07 cleanup because it no longer matched the current definition-backed target-lock and effect-execution implementation.

Supersedes: deleted active proposal `2026-06-06-one-hero-active-skill-flow.md`

Superseded By: None

Amends: None

Amended By: None

Blocking Issues: None known

Verification Records: 2026-06-06 automated regression and build evidence recorded below.

## Authority

- `gameplay-design/vertical-slices/first-playable-slice.md` VS-11 requires one selected hero active skill that visibly changes battle flow and is report-visible.
- `gameplay-design/details/combat-command/README.md` defines active skill timing, targeted and non-targeted skill commands, target locking, and default interruption behavior.
- `system-design/battle-content-progression-architecture.md` defines skill definitions, action timing, interrupt policy, and source-agnostic effect primitives.
- `system-design/battle-command-architecture.md` defines skill command acceptance, target locking, tactical-pause command semantics, and command-state events.
- `system-design/battle-runtime-architecture.md` defines Runtime action execution, effect execution, actor phases, action locks, and semantic events.
- `system-design/battle-ai-boundary-architecture.md` defines actor behavior as the skill release decision boundary.
- `system-design/battle-result-settlement-architecture.md` defines shared Runtime event source, source action, source definition, effect-result facts, and skill failure attribution.

## Goal

Run one selected hero active skill through the accepted architecture: content definition -> targeted command -> Runtime target lock -> actor release decision -> skill action timing -> generic effect execution -> semantic events -> Presentation feedback and report attribution.

## Scope

- Replace the current hardcoded first-slice hero skill resolver path with a definition-driven Runtime skill order path.
- Support one targeted selected-hero active skill in the battle HUD.
- Add a target-picking UI flow for that skill: press skill, hover valid enemy, click to submit `Hero/CastSkill` with `TargetActorId`.
- Lock targeted skill range at command acceptance.
- Preserve the locked target if it later moves out of range before release.
- Fail without release if the caster or locked target is dead, invalid, or untargetable at release time.
- Add Runtime action state for skill casting and skill recovery.
- Allow the skill to interrupt basic attack windup before damage impact.
- Make the skill wait through basic attack recovery after damage impact by default.
- Reject or keep waiting when another active skill is already casting or recovering, unless a later explicit trait changes this.
- Route skill damage through a reusable Runtime effect executor.
- Emit command, skill action, effect-result, damage, failure, and report facts with source command, source action, and source definition attribution.

## Non-Goals

- No full skill tree, multi-skill loadout, cooldown UI, mana UI, projectile simulation, or all-hero content pass.
- No AI skill casting.
- No full migration of basic attacks into the effect executor in this slice. Basic attack damage may remain in `BattleAttackResolver`, but the new effect executor must be shaped so basic attacks can move into it without changing event consumers.
- No Godot editor resource authoring workflow for all skills. First-slice skill content may be code-defined under the content/definition layer, but Runtime must consume it through definition snapshots instead of hardcoded resolver constants.
- No animated range or target preview while paused. Pause-time previews stay static and input-driven.

## Current Implementation Gap

The existing `BattleRuntimeHeroSkillCommandResolver` proves command submission but violates the accepted target architecture in four ways:

- it hardcodes `FirstSliceHeroSkillDamage`;
- it resolves pending skills immediately on the next Runtime tick instead of using actor action timing;
- it falls back to an arbitrary hostile target when no requested target is supplied;
- it applies damage directly instead of routing through a reusable effect executor.

This proposal turns that path into a migration target rather than a long-term authority.

## Touched Systems And Files

Definitions and snapshots:

- Create `src/Definitions/Battle/Skills/BattleSkillDefinition.cs`
- Create `src/Definitions/Battle/Skills/BattleSkillTargetingMode.cs`
- Create `src/Definitions/Battle/Skills/BattleSkillActionTimingDefinition.cs`
- Create `src/Definitions/Battle/Skills/BattleSkillInterruptPolicyDefinition.cs`
- Create `src/Definitions/Battle/Skills/BattleSkillEffectDefinition.cs`
- Create `src/Definitions/Battle/Skills/BattleSkillEffectKind.cs`
- Create `src/Definitions/Battle/Skills/FirstSliceBattleSkillDefinitions.cs`
- Create `src/Application/Battle/Snapshots/BattleSkillSnapshot.cs`
- Modify `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`
- Modify `src/Application/Battle/Commands/HeroSkillCommandIds.cs`

Command and Runtime skill flow:

- Replace or shrink `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver.cs`
- Replace `src/Runtime/Battle/BattleRuntimePendingHeroSkillCommand.cs` with a definition-backed runtime order shape, or keep the filename only as a narrow adapter if it no longer owns skill rules.
- Modify `src/Runtime/Battle/BattleRuntimeState.cs`
- Modify `src/Runtime/Battle/BattleRuntimeSessionController.cs`
- Modify `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- Modify `src/Runtime/Battle/BattleRuntimeActor.cs`
- Modify `src/Runtime/Battle/BattleRuntimeActorPhase.cs`
- Modify `src/Runtime/Battle/BattleRuntimeActorStateMachine.cs`

Effect execution:

- Create `src/Runtime/Battle/Effects/BattleEffectExecutionContext.cs`
- Create `src/Runtime/Battle/Effects/BattleEffectPayload.cs`
- Create `src/Runtime/Battle/Effects/BattleEffectResult.cs`
- Create `src/Runtime/Battle/Effects/BattleEffectResolver.cs`
- Modify `src/Runtime/Battle/BattleRuntimeEventFactory.cs`
- Modify `src/Runtime/Battle/Events/BattleEvent.cs`
- Modify `src/Runtime/Battle/Events/BattleEventKind.cs`

Presentation and reports:

- Modify `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`
- Modify `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- Modify `src/Presentation/Battle/BattleGridHighlightOverlay.cs` only if the target-picking highlight cannot reuse existing hover/selection layers.
- Modify `src/Application/Battle/Reports/BattleReportBuilder.cs`
- Modify `src/Application/Battle/Reports/BattleReportRecord.cs`

Regression tests:

- Modify `tests/TargetBattleArchitectureRegression/TargetBattleHeroSkillRegressionCases.cs`
- Modify `tests/TargetBattleArchitectureRegression/Program.cs`
- Modify `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroCorps.cs`
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- Modify `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` if report attribution assertions need a focused home.

## Implementation Steps

1. Add skill definition and snapshot types.
   - Definitions must express targeting mode, range, action timing, interrupt policy, and damage-effect payload for `first_slice_hero_breakthrough`.
   - `BattleStartSnapshot` must carry the selected hero skill snapshot into Runtime.
   - Runtime code must not read `FirstSliceBattleSkillDefinitions` directly after session creation.

2. Replace command acceptance with definition-backed validation.
   - `Hero/CastSkill` must require a known skill id and matching selected hero battle group.
   - Presentation may provide the selected visible caster actor id; Runtime must use that caster as the range/action authority instead of re-resolving a hidden group proxy.
   - Targeted skills must require `TargetActorId`.
   - Runtime acceptance must validate target exists, hostile targetability, and range at acceptance time.
   - Accepted command must store a locked target id and target-lock acceptance facts.

3. Add Runtime skill order and actor action state.
   - Add actor fields for current skill action id, skill id, source command id, locked target id, cast start, impact time, and recovery end.
   - Add actor phases for casting and skill recovery.
   - Skill action timing must advance only through Runtime time, not Presentation animation.

4. Implement release decision rules.
   - If actor is in `AnchoredDecision`, start the accepted skill action when prechecks pass.
   - If actor is in basic attack windup before damage impact, interrupt that attack and start the skill.
   - If actor is in attack recovery after damage impact, keep the skill order waiting until recovery completes.
   - If actor is casting or recovering from another skill, do not start the new skill by default.
   - If caster or locked target is dead or invalid at release, emit failure and consume or clear the order according to command-failure semantics.

5. Implement reusable effect execution for the skill.
   - The first effect primitive is direct damage.
   - The resolver must accept source command id, source action id, source definition id, caster, locked target, and payload.
   - The resolver must mutate Runtime HP, mark defeat, and emit effect-result/damage events.
   - The resolver must be source-agnostic so basic attacks can later call it with a basic-attack payload.

6. Wire Presentation target picking.
   - The skill button enters a target-picking mode instead of immediately firing.
   - Hovering enemies may show static target/range affordance while paused.
   - Target preview and Runtime command acceptance must use the same selected visible caster actor and range rule, so the player is not shown a valid-looking range from one unit while Runtime validates from another.
   - Clicking a valid enemy submits `CommandRequest` with `Channel = Hero`, `Kind = CastSkill`, `SkillId`, selected caster actor id, and `TargetActorId`.
   - Local UI can reject missing target selection, but Runtime remains the final authority.

7. Update report attribution.
   - Reports must record whether the skill was used, failed, or affected a target.
   - Damage and failure facts must read Runtime events, not recompute skill legality.
   - Report records must preserve enough source definition/action text to explain the skill use.

8. Remove or constrain old hardcoded paths.
   - Remove `FirstSliceHeroSkillDamage` from Runtime resolver logic.
   - Remove target fallback that picks the nearest hostile when a targeted skill command lacks `TargetActorId`.
   - Keep compatibility names only when they delegate to the new definition-backed path.

## Tests

Target battle architecture regression must cover:

- targeted skill command without `TargetActorId` is rejected;
- targeted skill command out of range at acceptance is rejected;
- targeted skill command in range is accepted and locks target identity;
- targeted skill command with an explicit selected caster uses that caster for range validation and release;
- locked target moving out of range before release still receives the skill effect;
- locked target defeated before release makes the skill fail without damage;
- skill ordered during attack recovery waits until recovery completes;
- skill ordered during pre-impact attack windup interrupts the basic attack before damage;
- skill ordered while another skill is casting or recovering does not interrupt by default;
- effect resolver emits damage/effect events with source command, source action, and source definition.

World site regression must cover:

- battle HUD skill button enters target-picking mode for the selected hero;
- target-picking range preview uses the selected visible caster, not every member of the hero company;
- target click builds `Hero/CastSkill` with the selected enemy actor id;
- pause-time target picking submits command intent without advancing Runtime.

Report regression must cover:

- successful skill use appears in `BattleReportRecord`;
- skill failure reason appears when the locked target dies before release;
- report facts read Runtime events with source definition/action attribution.

Run after implementation:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Diagnostics

- Command acceptance, rejection, failure, release, interruption, and completion must enter `BattleEventStream` with low-noise reason codes.
- Effect execution must emit event data with source command id, source action id, source definition id, actor id, target id, effect kind, and applied delta.
- Presentation may log one low-noise line when entering target-picking mode, submitting the skill, or receiving a Runtime rejection.
- Do not log per-frame hover updates or every paused preview refresh.

## Manual QA

Bonefield runtime:

1. Start battle with the current selected hero company.
2. Pause with Space.
3. Select the hero company.
4. Press the hero skill button and verify target-picking mode starts.
5. Hover an enemy and verify static target affordance updates while Runtime remains paused.
6. Click the enemy and verify the command is acknowledged without advancing battle state during pause.
7. Resume and verify the hero releases the skill at the next valid release or interrupt boundary.
8. Repeat with the hero in attack recovery and verify the skill waits until recovery ends.
9. Repeat with the locked target defeated before release and verify the skill fails without damage.
10. Finish or exit battle and verify the report records skill use or failure.

## Acceptance Evidence

2026-06-06 automated verification:

- Red check: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed before implementation on `skill damage feedback preserves runtime source attribution`, proving the missing Runtime source attribution in Presentation feedback.
- Green check: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after `BattleDamageEvent`, `BattleDamageNumberSpec`, and Runtime damage playback preserved source command/action/definition/effect fields.
- Green check: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed, including the added `runtime skill command waits behind active skill by default` coverage.
- Green check: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed, including target-picking and pause-time command-intent coverage.
- Green check: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed.
- Green check: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after targeted skill commands carried the selected visible caster actor id, so range acceptance and release no longer fall back to the hidden hero proxy.
- Green check: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after target preview range was tied to the Runtime skill snapshot instead of a duplicate Presentation constant.
- Green check: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after targeted skill range acceptance switched to footprint-aware Manhattan diamond distance while basic attack range tests remained unchanged.
- Green check: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after pause-time skill preview excluded square-corner cells outside the diamond.
- Green check: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after skill range highlights switched to fill-only solid diamond tiles without internal cell border lines.

Known remaining verification caveat:

- The automated commands still print the existing Godot `ScriptPathAttributeGenerator` warning when `GodotProjectDir` is unavailable, plus older nullable warnings in unrelated regression files. No new nullable warnings remain in `TargetBattleHeroSkillRegressionCases.cs`.
- Bonefield manual QA has not been run in this session, so this implementation proposal is not marked accepted or archived.
