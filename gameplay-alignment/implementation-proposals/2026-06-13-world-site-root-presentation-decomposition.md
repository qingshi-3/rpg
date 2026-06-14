# World Site Root Presentation Decomposition Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review batch: architecture review cleanup batch 3.
- Tech debt: `TD-004` in `gameplay-alignment/tech-debt-register.md`.
- Authority documents:
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/world-site-management-architecture.md`
  - `gameplay-design/content-systems-long-term-design.md`

The accepted architecture already says Presentation/UI owns layout hosts, panel visibility, display binding, transient drag feedback, and intent submission, while Application/Runtime own durable state, battle truth, command validation, and scene transition authority.

No design proposal is required for these slices because they are behavior-preserving Presentation refactors. If a slice needs to change UI host ownership, scene/resource taxonomy, Application validation, Runtime command authority, or gameplay rules, stop and use the design proposal flow first.

## Scope

Split the current `WorldSiteRoot` god-node pressure into three small batches:

1. Extract site-management HUD list binding into a focused Presentation binder.
2. Extract battle-preparation map-drag request-backed placement and footprint rules into a focused Presentation collaborator.
3. Extract battle-runtime HUD presentation binding into a focused Presentation collaborator.

Each batch must keep `WorldSiteRoot` responsible for lifecycle routing, authoritative data resolution, Application/Runtime boundary calls, and scene-host ownership.

## Non-Goals

- Do not redesign the world-site HUD layout.
- Do not change authored `.tscn` scene paths or reusable row scene contracts.
- Do not change gameplay rules, site actions, facility rules, garrison authority, deployment legality, battle runtime command semantics, or settlement behavior.
- Do not move durable state mutation from Application/Runtime into Presentation.
- Do not attempt to close all of `TD-004` in this pass; the target is three low-risk decomposition slices with guards against regrowth.

## Touched Systems

- `src/Presentation/World/Sites/WorldSiteRoot*.cs`
- New focused Presentation collaborators under `src/Presentation/World/Sites/`
- `tests/WorldSiteDeploymentCacheRegression` source-architecture guards
- Implementation proposal index

## Tests

- Add source-architecture guards that assert the new collaborators exist and are used by `WorldSiteRoot`.
- Preserve existing battle preparation, battle runtime HUD, presentation authoring, and deployment cache regression coverage.
- Run `tests/WorldSiteDeploymentCacheRegression` after each batch.
- Run `tests/TargetBattleArchitectureRegression` and project build after all three batches.

## Diagnostics

- Preserve existing low-noise `GameLog` messages when moving code into collaborators.
- New collaborators may receive diagnostics context, but must not create hidden fallback behavior when required UI nodes or authoritative data are missing.

## Manual QA

No Godot editor launch is required for these behavior-preserving source decompositions unless automated checks expose a runtime-only interaction issue. Optional manual QA after all three batches: open a site, inspect facility/garrison/action lists, drag a company in battle preparation, start a live battle, use command buttons, and return to the strategic map.

## Review Corrections

- 2026-06-13: Read-only review found the battle-preparation drag controller still owned the normal peacetime footprint fallback through a null-context branch. The controller now only builds footprint cells for resolved battle-preparation drag contexts; `WorldSiteRoot` keeps the peacetime `BuildSitePlacementFootprintCells` fallback.
- 2026-06-13: Read-only review questioned `BattleRuntimeSkillUsageResolver` being outside `WorldSiteRoot`. That helper predated this slice. The accepted boundary for this batch is that `BattleRuntimeHeroFramePresenter` does not own usage resolution; `WorldSiteRoot` remains the call site and passes a usage-state delegate into the presenter.

## Acceptance Evidence

- 2026-06-13: Batch 1 RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because `WorldSiteManagementPanelBinder` did not exist.
- 2026-06-13: Batch 1 GREEN: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after extracting `WorldSiteManagementPanelBinder`.
- 2026-06-13: Batch 2 RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because `BattlePreparationDeploymentDragController` did not exist.
- 2026-06-13: Batch 2 GREEN: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after extracting the request-backed battle-preparation map drag controller.
- 2026-06-13: Batch 3 RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because `BattleRuntimeHeroFramePresenter` did not exist.
- 2026-06-13: Batch 3 GREEN: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after extracting `BattleRuntimeHeroFramePresenter` and fixing the batch 2 peacetime footprint boundary.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-13: `git diff --check -- <td-004-batch-files>` passed with only a line-ending normalization warning for `WorldSiteRoot.BattleRuntimeCommandHud.cs`.
