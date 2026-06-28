# Battle Authority Hardening ROI Proposal

Status: Accepted - Automated Hardening Complete, Manual Desktop Mono QA Waived By User Request On 2026-06-23

## Origin

- Requirement: BATTLE-AUTH-HARDEN-001
- Review Source: 2026-06-21 battle refactor static code review.
- Design Proposal: None required. This proposal implements current accepted architecture instead of changing it.
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/hero-led-light-rts-system-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/battle-navigation-topology-architecture.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/battle-tactical-intent-architecture.md`
  - `system-design/battle-result-settlement-architecture.md`
  - `system-design/battle-content-progression-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Parent Proposal: None.
- Supersedes: None.
- Related Historical Implementation Records:
  - `gameplay-alignment/implementation-proposals/archived/2026-06-21-battle-runtime-actorized-clock-refactor.md`
  - `gameplay-alignment/implementation-proposals/archived/2026-06-18-strategic-battle-result-summary-authority.md`
  - `gameplay-alignment/implementation-proposals/archived/2026-06-19-strategic-battle-launch-snapshot-sync.md`
- Blocking Issues: None known at proposal time.

## Goal

Harden the battle flow after the large runtime refactor by removing the highest-risk fallback and double-authority paths first:

```text
validated strategic/bridge snapshot
-> Runtime owns live combat truth
-> Presentation mirrors Runtime facts
-> Settlement/report/summary share Runtime facts
-> Strategic Management command applies only validated consequences
```

This proposal is ROI-first. It prioritizes defects that can turn broken battle input, runtime exceptions, missing topology, or presentation-only combat logic into normal campaign consequences.

## Optimization Principle

Fix order is determined by:

```text
severity * probability of corrupting campaign state / implementation cost
```

P0 work blocks false settlement, false writeback, and wrong command execution with small or medium changes.

P1 work removes larger double-authority paths that keep legacy battle startup and presentation combat logic alive.

P2 cleanup is recorded only when it directly supports P0/P1. Low-ROI visual resourceization, shader material authoring cleanup, raw FX timer cleanup, and reason-code polish are follow-up work unless required to complete a higher-priority authority fix.

## Current Problems

### P0: False Completion And Writeback

`WorldSiteRoot.BattleRuntime.cs` can catch a battle advance or presentation exception and still continue to `CompleteResolvedBattle`. The adapter can then advance an unfinished runtime to completion. This can transform a failed handoff into normal settlement and strategic writeback.

### P0: Fabricated Strategic Result Facts

`StrategicBattleBridgeService` can produce remaining corps strength from battle outcome and pre-battle strength when actor outcomes or participant counts are missing. Strategic result summaries must not invent losses or survival.

### P0: Missing Command Payload Becomes A Real Skill

`BattleRuntimeHeroSkillCommandResolver.NormalizeSkillId("")` maps an empty skill id to the first-slice hero skill. UI or command construction bugs can therefore release the wrong skill and contaminate cooldown, report, and outcome facts.

### P0: Missing Navigation Topology Becomes A Walkable Map

`BattleNavigationGraph.Create()` can fall back to an actor-derived graph when the compiled topology is missing. Authored terrain, blocked cells, height links, water, walls, and footprint legality can be bypassed.

### P1: Strategic Battle Launch Still Uses Probe Snapshot Authority

Strategic launch paths still call `BattleGroupSessionProbeService.PrepareSnapshot(request)` from a legacy `BattleStartRequest`. Probe fallback identities, strength, and cells can become runtime facts.

### P1: Definitions And Presentation Still Execute Battle Damage

Legacy `Definitions/Battle/Abilities` resources can execute `Apply()` and call Presentation `HealthComponent.ApplyDamage()`. Runtime damage playback also lets Presentation recompute HP and defeated state instead of mirroring Runtime facts.

### P1: Settlement, Report, Summary, And Strategic Commands Can Derive Separate Truths

Strategic Management battle-result handling can recompute rewards and feedback from target definitions and victory state instead of applying facts carried from settlement/report summaries.

### P2: Runtime Defaults Hide Invalid Content

Runtime still supplies legacy attack damage, fallback HP, default targeting, default interrupt policy, and null effect payload behavior. These should be rejected by snapshot validation in production paths, but they are lower ROI than blocking false settlement and launch fallback first.

## Scope

### 1. Completion And Writeback Guardrails

- Split normal runtime completion from failed or interrupted runtime/presentation handoff.
- Stop battle result completion when live-clock advancement throws before accepted settlement facts exist.
- Ensure forced headless `AdvanceToCompletion()` is not used to repair a failed presentation-backed battle path.
- Keep runtime exception, battle interruption, player retreat, normal victory, and normal defeat as distinct termination reasons.

### 2. Strategic Result Summary Validation

- Reject strategic summary creation when runtime output, event stream, settlement plan, report record, snapshot id, or bridge session id is missing or mismatched.
- Reject summary creation when actor outcomes cannot map back to stable hero and corps instance ids.
- Remove full-survival or full-wipe strength inference from missing actor outcomes.
- Carry rewards, losses, recovery requirements, location consequences, and report reference from settlement/report facts instead of recomputing them in Strategic Management command handling.

### 3. Command And Snapshot Required Facts

- Require non-empty active skill ids at Runtime command acceptance.
- Fail empty or unknown skill ids with a reportable command failure.
- Require compiled navigation topology for production Runtime launch.
- Require actor start footprints to be legal inside the compiled topology before battle start.
- Keep any actor-derived navigation graph only as an explicitly named test or legacy diagnostic helper.

### 4. Strategic Launch Snapshot Cutover

- Stop Strategic Management-backed launches from using `BattleGroupSessionProbeService.PrepareSnapshot()` as authoritative Runtime input.
- Make Bridge Active Context and its validated `BattleStartSnapshot` the strategic battle launch source.
- Keep legacy `BattleStartRequest` only as a removable UI adapter while preparation UI is migrated.
- Clear or bypass static `BattleSessionHandoff` when a Bridge Active Context battle is active, so stale legacy requests cannot leak back into strategic completion.

### 5. Runtime-Only Damage And Effect Execution

- Retire or isolate legacy executable ability resources under `Definitions/Battle/Abilities`.
- Convert retained ability resources into data-only payloads or remove them from battle runtime paths.
- Make Presentation health state a mirror of Runtime event facts.
- Runtime damage/effect events should carry enough after-state for Presentation, Settlement, Report, and diagnostics to agree without Presentation recalculation.
- Remove or route presentation-side direct damage helpers such as unused battle modifier damage through Runtime effect sources.

### 6. Production Snapshot Validation

- Add production-path validation for combat stats and skill/effect snapshots.
- Reject missing HP, attack damage, attack speed, range, skill target mode, interrupt policy, timing, or null effect payloads before Runtime begins.
- Preserve explicit test fixture paths for focused Runtime tests, but do not let test defaults enter production battle launch.

## Non-Goals

- No new battle gameplay rules, new skills, new target types, or new effect primitives.
- No battle balance tuning.
- No rewrite of battle preparation UX beyond routing existing draft facts through the accepted bridge boundary.
- No final shader/material/tile overlay resourceization cleanup unless required by a P0/P1 authority fix.
- No broad reason-code enum migration unless touching a reason for an accepted failure path.
- No full local building support or in-battle support activation.
- No mid-battle save/resume.
- No archived proposal body edits.

## Touched Systems

- Strategic Battle Bridge result and launch services under `src/Application/StrategicBattleBridge/`.
- Strategic Management battle-result command handling under `src/Application/StrategicManagement/`.
- World battle launch and runtime adapters under `src/Application/World/`.
- Battle scene handoff and site runtime presentation under `src/Presentation/World/Sites/`.
- Runtime command, navigation, session, and effect boundaries under `src/Runtime/Battle/`.
- Battle content and legacy ability definitions under `src/Definitions/Battle/` and `assets/battle/abilities/`.
- Battle presentation health and runtime event observer under `src/Presentation/Battle/` and `src/Presentation/World/Sites/`.
- Architecture regression tests under `tests/TargetBattleArchitectureRegression/`.
- Battle hit feedback and presentation tests under `tests/BattleHitFeedbackRegression/`.
- Strategic Management regression tests under `tests/StrategicManagementRegression/`.
- World-site anti-rot tests under `tests/WorldSiteDeploymentCacheRegression/` when scene/resource boundaries change.

## GodotPrompter Skills

Use these implementation skills before code/resource work:

- `csharp-godot`
- `csharp-signals` if signal contracts change.
- `resource-pattern` for data-only ability/effect resources.
- `scene-organization` when removing legacy handoff or presentation-side combat entry points.
- `godot-ui` only if preparation UI adapter changes are required.
- `godot-testing`
- `godot-debugging`
- `godot-code-review` before final acceptance review.

## Implementation Plan

### Phase 0: Lock RED Tests For P0 Failures

- Add regression tests that prove a runtime/presentation exception cannot call strategic writeback.
- Add tests that `StrategicBattleResultSummary` rejects missing actor outcomes and missing participant mappings.
- Add tests that empty active skill ids are rejected instead of normalized to the first-slice skill.
- Add tests that production Runtime launch rejects missing navigation topology.

Expected result before implementation: these tests fail against current code.

### Phase 1: Block False Completion And False Facts

- Change presentation-backed battle completion so exceptions before accepted settlement produce failed/interrupted handoff state.
- Remove adapter behavior that advances an interrupted live-clock battle to normal completion.
- Change summary creation to require complete Runtime outcome, event stream, settlement plan, report record, snapshot id, and participant mapping.
- Remove outcome-based survival/wipe inference when actor outcomes are missing.
- Change empty skill id handling to explicit command failure.

Stop gate: no strategic writeback is possible from incomplete runtime output or missing skill command payload.

### Phase 2: Require Topology And Bridge Snapshot Authority

- Require compiled `BattleNavigationTopology` for production `BattleRuntimeSession.Begin`.
- Validate actor start footprints against topology before actors enter live Runtime.
- Move strategic launch to Bridge Active Context plus validated `BattleStartSnapshot`.
- Restrict `BattleGroupSessionProbeService` to diagnostics or explicitly named legacy compatibility paths.
- Clear or bypass static `BattleSessionHandoff` for Bridge Active Context battles.

Stop gate: a Strategic Management battle cannot launch from probe fallback identities, fallback cells, or missing topology.

### Phase 3: Collapse Damage And Effect Authority Into Runtime

- Remove executable damage/target logic from retained `Definitions/Battle/Abilities` resources or isolate those resources outside active battle flow.
- Replace Presentation damage replay with Runtime HP/defeat mirror updates.
- Ensure Runtime damage/effect events include target actor id, source actor or source effect, damage/effect amount, HP before/after when relevant, and defeated/routed fact when relevant.
- Remove unused Presentation direct damage helper paths, or route them into Runtime effect sources if still required.

Stop gate: no active battle path outside Runtime can apply damage, decide defeat, or feed settlement/report damage facts.

### Phase 4: Validate Production Content Snapshots

- Add production snapshot validation for actor stats, active skills, basic attacks, effect payloads, action timing, and interrupt policy.
- Replace runtime default combat stats with validation failures on production paths.
- Keep focused test builders explicit about default fixture values.
- Convert null-as-empty effect and commit paths that represent internal invariants into explicit failure events or exceptions.

Stop gate: missing production battle content fails before runtime or emits an explicit runtime failure, instead of silently producing zero-effect or default-damage combat.

### Phase 5: Strategic Command Consequence Alignment

- Move battle rewards, losses, recovery requirements, location consequences, and report references into the bridge summary from settlement/report facts.
- Keep `StrategicManagementCommandService.ApplyBattleResultSummary` as validation and application only.
- Remove target-definition victory reward recomputation from the command application path.
- Preserve low-noise user-facing feedback by displaying accepted summary/report facts.

Stop gate: Strategic Management no longer derives separate battle-result truth from target definitions after Runtime settlement facts exist.

## Tests

Add or update regression coverage for:

- presentation-backed runtime exception blocks completion and writeback;
- interrupted live-clock battles are not force-advanced into normal victory or defeat;
- summary creation fails when runtime outcome, event stream, settlement plan, report record, snapshot id, or bridge session id is missing or mismatched;
- summary creation fails when actor outcomes do not map to hero/corps instance ids;
- missing actor outcomes do not infer remaining corps strength;
- empty skill id produces command failure and does not start first-slice skill cooldown or event output;
- production launch rejects missing compiled navigation topology;
- production launch rejects illegal actor start footprint;
- strategic launch does not call probe snapshot as the authoritative Runtime source;
- legacy ability resources cannot apply Presentation damage in active battle flow;
- Runtime damage event playback updates Presentation mirror state without recalculating defeat;
- Strategic Management battle-result command applies rewards and consequences from summary/report facts and does not recompute them from target definition plus outcome;
- missing production skill/effect/stat snapshot fields fail validation explicitly.

Recommended verification commands:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Recommended static guard scans:

```powershell
rg -n "PrepareSnapshot\(request\)" src\Application\World src\Presentation\World
rg -n "NormalizeSkillId\(string skillId\)|FirstSliceHeroSkillId" src\Runtime\Battle
rg -n "CreateFromActors" src\Runtime\Battle
rg -n "ApplyDamage\(" src\Definitions src\Presentation\World\Sites src\Presentation\Battle
rg -n "LegacyCorpsAttackDamage|new BattleRuntimeActor\(\)|Array.Empty<BattleEffect" src\Runtime\Battle
```

The scans are not pass/fail by raw zero matches. Acceptance requires every remaining match to be either a test fixture, an explicitly named legacy compatibility boundary, or a presentation mirror that does not own combat truth.

## Diagnostics

Add low-noise diagnostics for:

- battle launch rejected because compiled topology is missing;
- actor start footprint rejected before Runtime begin;
- bridge summary rejected because runtime/settlement/report facts are incomplete or mismatched;
- command rejected because skill id is missing or unknown;
- battle completion blocked because live-clock advancement failed;
- legacy/probe path attempted to feed a Strategic Management battle;
- production snapshot rejected because required combat stat or effect payload is missing.

Do not add per-frame, per-animation, per-node, or per-movement-candidate logs.

## Manual QA

Desktop Mono QA should confirm:

1. A normal strategic battle can still launch, run, settle, show report feedback, and return to the strategic layer.
2. A failed or interrupted battle handoff does not apply rewards, corps losses, location capture, or recovery state.
3. Missing/invalid battle preparation facts keep the launch button disabled or show a clear failure reason.
4. Hero skill buttons reject missing/invalid skill data instead of casting a default skill.
5. Battle damage visuals, health bars, and defeated presentation match Runtime facts.
6. Victory rewards and target occupation/development shown after battle match the accepted report/summary.

## Acceptance

This proposal is accepted when:

- incomplete runtime output cannot settle as normal victory or defeat;
- Strategic Management writeback requires a validated bridge summary derived from Runtime, settlement, and report facts;
- empty skill id no longer casts a default skill;
- production Runtime launch requires compiled navigation topology and legal actor start footprints;
- Strategic Management-backed battle launch no longer uses probe snapshot facts as authoritative Runtime input;
- active battle damage and defeat are Runtime facts mirrored by Presentation, not Presentation calculations;
- legacy executable ability resources no longer provide an active damage/targeting path;
- production snapshot validation fails explicitly for missing combat stats or malformed skill/effect payloads;
- automated verification commands pass or any unrelated pre-existing failures are documented with exact failing guard names;
- manual QA evidence is recorded here after execution.

## Verification Evidence

2026-06-21 implementation evidence:

- Implemented P0 guardrails for false completion/writeback, fabricated strategic facts, missing hero skill id, and missing navigation topology.
- Implemented Phase 2 high-ROI runtime start validation for illegal actor start footprints.
- Implemented Phase 2 Strategic Management-backed launch cutover: active-context launches now synchronize final preparation draft facts through a bridge-owned launch snapshot sync service instead of `BattleGroupSessionProbeService.PrepareSnapshot(request)`.
- Added launch guardrails that reject unmapped compatibility player forces with `strategic_battle_launch_participant_mapping_missing` and reject missing topology at the adapter boundary instead of reporting a false runtime start.
- Implemented first Phase 4 production snapshot validation guardrail for Strategic Management-backed launches: missing combat HP, attack damage, range, speed, movement timing, attack timing, or impact timing now fails before Runtime defaults can apply.
- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: all behavioral checks passed; still fails the pre-existing oversized-file guard for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: battle-runtime guardrails, topology-related fixtures, and strategic launch probe-authority guard passed; still fails unrelated pre-existing cleanup guards `semantic marker authoring uses business subclasses` and `legacy manual battle authority docs stay deleted`.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`: passed.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed.

2026-06-22 continuation evidence:

- Implemented the first Runtime-only damage/defeat presentation cleanup: Runtime `DamageApplied` events now carry target HP before/after facts, and live battle Presentation mirrors those Runtime facts through `HealthComponent.MirrorRuntimeDamage()` instead of recalculating HP via Presentation `ApplyDamage()`.
- Kept hit reaction and defeated presentation timing impact-aligned while moving the health-state authority to Runtime event facts.
- Added regression coverage that Runtime damage events carry target HP mirror facts and active live runtime playback no longer calls Presentation `ApplyDamage(damage, actor)`.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`: passed.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: all behavioral checks passed; still fails the pre-existing oversized-file guard for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: battle runtime/presentation guardrails passed; still fails unrelated pre-existing cleanup guards `semantic marker authoring uses business subclasses` and `legacy manual battle authority docs stay deleted`.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed.

2026-06-22 production skill snapshot validation evidence:

- Implemented the next Phase 4 Runtime launch guardrail: malformed skill snapshots now reject `BattleRuntimeSession.Begin()` before Runtime state construction instead of being corrected by default targeting, default effect kinds, clamped timings, or empty effect lists.
- Added explicit Runtime regression coverage for invalid skill targeting mode, targeted skill range, empty effect lists, invalid effect enum values, and malformed channeled area damage payload timing.
- `CloneSkillDefinitions()` now preserves already-validated snapshot facts instead of replacing malformed skill/effect fields with defaults.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: all behavioral checks passed; still fails the pre-existing oversized-file guard for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed.

2026-06-22 production group snapshot and strategic consequence evidence:

- Extended production `BattleRuntimeSession.Begin()` validation so malformed group snapshots fail before Runtime state construction instead of relying on fallback faction ids, HP, attack stats, action timings, or footprints.
- Extended Runtime skill validation for missing caster bindings and invalid ordinary damage amounts, so malformed production skills cannot become zero-effect or default-authority Runtime actions.
- Added explicit fixture combat stats/topology helpers for Runtime regression tests, keeping test defaults out of production launch semantics.
- Added a headless diagnostic-only minimal battle stat/topology path in `BattleGroupBattleFlowService.RunMinimalBattle`; normal production launches must still provide authored combat stats.
- Implemented the first Phase 5 Strategic Management consequence-alignment slice: `StrategicBattleResultSummary` can carry explicit consequence facts, and `StrategicManagementCommandService.ApplyBattleResultSummary` applies summary-carried feedback/rewards/resources/equipment instead of recomputing those facts from target definitions when `HasConsequenceFacts` is true.
- Added regression coverage that explicit strategic battle summary consequences override target-definition victory rewards during command application.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: all behavioral checks passed; still fails the pre-existing oversized-file guard for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed.

2026-06-22 runtime fallback cleanup evidence:

- Removed Presentation runtime HUD fallback from missing skill id to `HeroSkillCommandIds.FirstSliceHeroSkillId`; runtime skill buttons and target picking now use explicit skill ids from selected skill snapshots, and missing skill config reports `hero_skill_missing`.
- Added a guard that `BattleRuntimeSkillUsageResolver` and the hero skill command HUD treat blank skill ids as unavailable instead of querying first-slice skill state.
- Removed `LegacyCorpsAttackDamage` and `ResolveAttackDamage`; Runtime actors now use validated snapshot attack damage directly after production group validation.
- Changed production `BattleNavigationGraph.Create()` to require compiled topology and throw on missing/empty topology; actor-derived navigation graph creation is retained only as explicitly named `CreateDiagnosticFromActors`.
- Updated legacy probe/HUD regression fixtures to provide explicit combat stats and topology so tests do not depend on production defaults.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`: passed.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: all behavioral checks passed; still fails the pre-existing oversized-file guard for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: battle launch/probe/runtime guardrails passed; still fails unrelated cleanup guards `semantic marker authoring uses business subclasses` and `legacy manual battle authority docs stay deleted`.
- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `git diff --check`: passed.

2026-06-22 completion hardening evidence:

- Split oversized regression guard files back under their accepted line budgets instead of widening the oversized-code allowlist.
- Deleted the retired `BuildingSlotMapMarker.tscn` authoring scene and removed the recreated `docs/` tree so old manual/AP battle authority cannot re-enter the active document route.
- Added authored interrupt-policy presence to `BattleSkillSnapshot`; `BattleSkillSnapshotFactory`, strategic launch snapshot sync, and Runtime cloning now preserve the presence fact, and production Runtime launch rejects missing interrupt policy with `battle_skill_interrupt_policy_missing` while accepting explicitly authored all-false policies.
- Removed the no-active-context Runtime startup path from `WorldSiteBattleGroupRuntimeAdapter`; `WorldSiteRoot` now requires `StrategicBattleActiveContext` before battle Runtime activation instead of falling back to probe snapshot authority.
- Removed Presentation `HealthComponent.ApplyDamage()` so active battle damage can only enter Presentation through `MirrorRuntimeDamage()` using Runtime HP before/after facts.
- Removed blank `new BattleRuntimeActor()` fallbacks from Runtime effect, effect-release, and displacement boundaries; missing effect context now fails explicitly or no-ops without fabricating actor identity.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: passed.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: passed.
- Static scan of `src/` for `PrepareSnapshot(request)`, Presentation `ApplyDamage(`, `new BattleRuntimeActor()`, `LegacyCorpsAttackDamage`, and `ResolveAttackDamage` now leaves only the diagnostic `BattleGroupSessionProbeService.PrepareSnapshot(request)` owner and Runtime-owned effect resolver method names.
- Final automated verification:
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: passed.
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: passed.
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed.
  - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`: passed when run alone. A prior parallel run crashed in GodotSharp `ProjectSettings`/`GameLog` initialization, so final evidence uses the sequential run required by the project low-load rule.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
  - `git diff --check`: exited 0; Git reported line-ending normalization warnings for touched test files but no whitespace errors.

Manual QA disposition before archive:

- Manual desktop Mono QA was not executed. Automated tests and static architecture guards are complete, and the pending manual QA scope was waived by user archive request on 2026-06-23.

## Reopen Gate

Stop and create an amendment design proposal before continuing if implementation requires:

- new battle gameplay rules;
- new effect primitives or target types;
- changing the accepted bridge, settlement, or Runtime ownership model;
- mid-battle persistence;
- new local building support behavior;
- player-facing combat identity changes.
