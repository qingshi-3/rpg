# Battle Presentation Decomposition Follow-Up Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review follow-up: remaining optimization points after the TD-004 `WorldSiteRoot` presentation decomposition.
- Tech debt: `TD-004` battle-facing follow-up slices, with related Presentation debts `TD-006`, `TD-007`, and `TD-008` left for later visual-specific passes.
- Authority documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/world-battle-entry-architecture.md`

The user explicitly excluded the strategic-management site-map / facility-slot presentation refactor from this pass. That work remains deferred until the large-map strategic-management redesign.

No design proposal is required for these slices because they are behavior-preserving Presentation refactors. If a slice changes UI host ownership, scene/resource taxonomy, battle-preparation Application validation, Runtime command authority, settlement, or gameplay rules, stop and use the design proposal flow first.

## Scope

Split the remaining battle-facing `WorldSiteRoot` pressure into three small batches:

1. Extract battle objective planning dialog and thumbnail binding into a focused read-only Presentation binder.
2. Extract live Runtime event observation and playback feedback into a focused Presentation observer.
3. Extract battle-preparation roster and compact plan-control binding into a focused Presentation binder.

## Non-Goals

- Do not change strategic-management site-map, facility-slot, city, stronghold, resource-site, or garrison-management presentation.
- Do not redesign the battle-preparation HUD layout or authored scene resources.
- Do not change deployment legality, objective-zone semantics, engagement-rule defaults, reserve exclusion, battle launch readiness, Runtime tick cadence, battle event order, damage timing, teleport semantics, command validation, settlement, or campaign writeback.
- Do not move Application or Runtime authority into Presentation collaborators.
- Do not attempt to close `TD-006`, `TD-007`, or `TD-008` in this pass; those need visual-specific follow-up slices.

## Touched Systems

- `src/Presentation/World/Sites/WorldSiteRoot*.cs`
- New battle-facing Presentation collaborators under `src/Presentation/World/Sites/`
- `tests/WorldSiteDeploymentCacheRegression` source-architecture guards
- This implementation proposal index

## Tests

- Add RED source-architecture guards before each extraction.
- Run `tests/WorldSiteDeploymentCacheRegression` after each batch.
- Run `tests/TargetBattleArchitectureRegression` and project build after all batches.

## Diagnostics

- Preserve existing low-noise `GameLog` messages.
- New collaborators must stay Presentation-only: they may bind UI, observe emitted runtime events, and trigger visual feedback, but must not advance Runtime, mutate strategic state, apply settlement, or create hidden fallback behavior.

## Manual QA

No Godot editor launch is required for these behavior-preserving source decompositions unless automated checks expose a runtime-only issue. Optional manual QA after all batches: enter battle preparation, select a company, open objective planning, drag/deploy a company, launch live battle, observe movement/skill/damage/teleport feedback, and use runtime command pause.

## Acceptance Evidence

- 2026-06-13: RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because `BattleObjectivePlanningHudBinder`, `BattleRuntimeLivePresentationObserver`, and `BattlePreparationHudBinder` did not exist, and the updated teleport-observation guard could not find `ObserveAsync`.
- 2026-06-13: GREEN: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after extracting objective planning HUD binding, live runtime event observation, and battle-preparation roster/plan-control binding. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-13: `git diff --check -- <battle-presentation-followup-files>` passed with only a CRLF/LF normalization warning for `WorldSiteRoot.BattleObjectivePlanningHud.cs`.
