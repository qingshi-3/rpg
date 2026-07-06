# UI Hover Presentation Contract Implementation Proposal

Status: Implemented - Focused Guard Passing, Full Suite Blocked By Unrelated Taxonomy Guard

## Origin

- Requirement: UI-HOVER-001
- Design Proposal: `design-proposals/archived/2026-07-05-ui-hover-presentation-contract/`
- Authority:
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Blocking Issues: Full-suite acceptance remains blocked by the unrelated existing TileSets resource taxonomy guard.

## Requirement

Implement the first Presentation/UI hover-contract slice so complex building-construction hover detail uses the accepted authored tooltip-scene path, while simple hints and subsystem-owned map/battle overlays remain outside this migration.

## Scope

- Keep build-picker cards compact: card body shows the building icon and building name only.
- Show building footprint, resource cost, and optional disabled reason through a complex hover detail surface.
- Ensure the building hover detail is an authored Godot scene instantiated through `GameUiSceneFactory`.
- Use `TooltipText` only as the Godot custom-tooltip trigger for this card, not as the detailed information surface.
- Add or keep regression guards proving the building hover detail path uses an authored scene, a focused tooltip script, and the factory path.
- Add contract-oriented regression wording so future complex hover panels do not regress into ad hoc local `Control` trees.

## Non-Goals

- Do not migrate every existing `TooltipText` assignment.
- Do not replace Godot native tooltip behavior with a global tooltip manager.
- Do not merge battle grid hover frames, health-bar hover visibility, construction placement previews, deployment previews, or debug hover panels into generic tooltip scenes.
- Do not change Strategic Management construction legality, resource costs, placement validation, or persistent city state.
- Do not introduce new UI art or theme taxonomy beyond existing accepted shared theme/style resources.

## Touched Systems

- Presentation/Common scene factory for authored UI scene creation.
- Presentation/World/Sites build-option card and tooltip binding.
- Authored world UI scenes under `scenes/world/ui/`.
- World-site deployment regression guards under `tests/WorldSiteDeploymentCacheRegression`.

## GodotPrompter Skills

- `godot-ui`
- `csharp-godot`
- `godot-testing`
- `godot-code-review`

## Tests

- Add or keep a regression case in `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs` that checks:
  - `WorldBuildingOptionCard` overrides `_MakeCustomTooltip`;
  - the card delegates complex hover detail creation to `GameUiSceneFactory.CreateWorldBuildingOptionTooltip`;
  - `WorldBuildingOptionTooltip.tscn` exists as an authored `PanelContainer` scene with a focused script;
  - the tooltip script binds title, footprint, cost, and disabled-reason display fields;
  - `GameUiSceneFactory` owns the tooltip scene path and typed instantiate helper;
  - build-picker binding creates cards through `GameUiSceneFactory.CreateWorldBuildingOptionCard`;
  - unavailable building cards remain hoverable, while click authority stays gated by `_selectable`.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` if the focused regression suite passes or if failures are confirmed unrelated to this slice.
- Run `git diff --check`.

## Diagnostics

No new runtime diagnostics are required for the first slice. Missing authored nodes should continue to fail through `GameUiSceneFactory.GetRequiredNode` warnings or static regression failures rather than hidden fallback UI.

## Manual QA

- Open the site-management build tab.
- Hover a build-picker card.
- Confirm the card itself remains compact.
- Confirm the hover detail displays building name, footprint, resource cost, and any disabled reason.
- Confirm the hover detail is presentation-only: it must not place a building, change selected building state, or run construction legality as an authority.

## Acceptance

- The accepted authority document contains the hover presentation contract.
- The originating design proposal is archived.
- Building construction cards use an authored complex tooltip scene for detailed hover information.
- The building hover detail path is created through `GameUiSceneFactory`.
- Regression coverage prevents the building hover detail from reverting to plain detailed `TooltipText` or local ad hoc tooltip control trees.
- Existing simple `TooltipText` hints and subsystem-owned map/battle hover overlays are not forced into this slice.

## Verification Evidence

- RED observed before implementation:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - Failed expected hover-contract guard: `world site build picker uses icon cards and map placement: building option card TooltipText should be a short trigger only; footprint, cost, and disabled reason belong to the authored custom tooltip scene`.
- Implementation:
  - `WorldBuildingOptionCard.ApplyBinding()` now sets `TooltipText` to the building display name only.
  - `_MakeCustomTooltip()` remains the authored complex-detail path through `GameUiSceneFactory.CreateWorldBuildingOptionTooltip`.
- GREEN/focused verification:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Hover guard passed: `PASS world site build picker uses icon cards and map placement`.
  - Suite still exits non-zero because of unrelated existing guard: `resource directory taxonomy keeps assets raw and resources authored: legacy authored asset bucket changed before its migration batch bucket=TileSets expected=0 actual=2`.
- Build:
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with 0 warnings and 0 errors.
- Follow-up hoverability repair:
  - Manual QA reported the building panel still did not show a hover popup.
  - Root cause: `WorldBuildingOptionCard.ApplyBinding()` still set `Disabled = !_selectable`, which blocked the hover path for unavailable cards where cost and failure-reason explanation is most important.
  - RED observed before repair:
    - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
    - Failed expected guard: `world site build picker uses icon cards and map placement: building option cards must remain hoverable when unavailable; click authority stays gated by _selectable instead of BaseButton.Disabled`.
  - Implementation:
    - `WorldBuildingOptionCard.ApplyBinding()` now keeps `Disabled = false` so Godot can request the authored custom tooltip for unavailable cards.
    - `WorldBuildingOptionCard.OnPressed()` remains the selection authority and emits only when `_selectable` is true.
  - GREEN/focused verification:
    - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
    - Hover guard passed: `PASS world site build picker uses icon cards and map placement`.
    - Suite still exits non-zero because of the unrelated existing guard: `resource directory taxonomy keeps assets raw and resources authored: legacy authored asset bucket changed before its migration batch bucket=TileSets expected=0 actual=2`.
  - Build:
    - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Passed with 0 warnings and 0 errors.
  - Whitespace:
    - `git diff --check -- src/Presentation/World/Sites/WorldBuildingOptionCard.cs tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs gameplay-alignment/implementation-proposals/2026-07-05-ui-hover-presentation-contract.md`
    - Passed.
- Whitespace:
  - `git diff --check -- system-design/presentation-ui-layout-architecture.md design-proposals/archived/README.md design-proposals/archived/2026-07-05-ui-hover-presentation-contract gameplay-alignment/implementation-proposals/README.md gameplay-alignment/implementation-proposals/2026-07-05-ui-hover-presentation-contract.md src/Presentation/World/Sites/WorldBuildingOptionCard.cs tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs scenes/world/ui/WorldBuildingOptionTooltip.tscn src/Presentation/World/Sites/WorldBuildingOptionTooltip.cs src/Presentation/Common/GameUiSceneFactory.cs`
  - Passed.
