# Site Management Fullscreen Tab Rail Implementation Proposal

Status: Implemented; verification has unrelated blockers.

## Origin

- Requirement: UI-SITE-001
- Design Proposal: `design-proposals/archived/2026-07-08-site-management-fullscreen-tab-rail/`
- Authority:
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Blocking Issues: None known.

## Requirement

Implement the accepted entered-city management UI model: the site map remains fullscreen, a left vertical tab rail opens one function at a time, other tabs hide while a function panel is open, function panels are task-sized rather than fullscreen by default, and building placement retracts management UI so the map interaction owns the screen.

## Scope

- Change `WorldSitePeacetimeHud.tscn` so site management no longer lives as a permanent split-screen panel under `LeftPrimaryPanelHost`.
- Add an authored `SiteManagementTabRail` using existing tab buttons as vertical function entries.
- Move the `SitePeacetimePanel` function panel under `OverlayHost` as a task-sized overlay, keeping existing section node names stable where practical.
- Keep the strategic resource display as a top-left `TopLeftStatus` bar under `TopBarHost`, matching strategic-world placement instead of living inside any function panel.
- Keep `MilitaryWorkbenchPanel` in `ModalHost` as the larger recruitment workbench, but make opening it hide the tab rail.
- Update `WorldSitePeacetimeHudNodeRefs` to resolve the new authored paths.
- Update `WorldSiteRoot.SiteManagementHud` so default peacetime management shows the tab rail, opens task panels explicitly, hides the rail while a panel/workbench is open, and retracts panels during building placement.
- Update world-site viewport layout logic so peacetime management never reserves a left-side map exclusion strip.
- Add focused regression guards for the new scene structure, viewport behavior, tab hiding, and no hardcoded 520px split-screen layout.

## Non-Goals

- Do not redesign strategic-world selected-location UI.
- Do not change Strategic Management commands, city state, resource rules, recruitment costs, refunds, or building legality.
- Do not redesign battle preparation or battle runtime.
- Do not introduce a final project-wide UI theme-routing taxonomy.
- Do not make every function panel fullscreen.

## Touched Systems

- `system-design/presentation-ui-layout-architecture.md`
- `design-proposals/archived/2026-07-08-site-management-fullscreen-tab-rail/`
- `gameplay-alignment/implementation-proposals/README.md`
- `gameplay-alignment/implementation-proposals/2026-07-08-site-management-fullscreen-tab-rail.md`
- `scenes/world/ui/WorldSitePeacetimeHud.tscn`
- `src/Presentation/World/Sites/WorldSitePeacetimeHudNodeRefs.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteInteraction.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroCorps.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs`

## GodotPrompter Skills

- `godot-ui`
- `responsive-ui`

## Implementation Plan

### Task 1: Red Scene And Source Guards

Add/adjust focused `WorldSiteDeploymentCacheRegression` cases before production changes:

- `WorldSiteManagementHudUsesFullscreenTabRail` should require:
  - `SiteManagementTabRail` exists under `OverlayHost`;
  - `BuildTabButton`, `ConscriptionTabButton`, `RecruitTabButton`, and `OverviewTabButton` are under the vertical rail;
  - `SitePeacetimePanel` is under `OverlayHost`;
  - `SitePeacetimePanel` is not under `LeftPrimaryPanelHost`;
  - the tab rail uses `20250420manaTabD-Sheet.png` through authored scene/theme resource references;
  - function panel sizing uses bounded offsets/minimum sizes instead of full-rect anchors.
- Update the existing old assertions in `WorldSiteRootBattlePreparationUsesDedicatedUiContainers` and `PresentationUiScenePathsPreserveCodeBindings` so they expect overlay paths instead of left-workspace paths.
- Add a source guard requiring:
  - `WorldSitePeacetimeHudNodeRefs` resolves `OverlayHost/SiteManagementTabRail` and `OverlayHost/SitePeacetimePanel`;
  - `WorldSiteRoot.SiteManagementHud.cs` contains explicit tab-rail visibility control;
  - `WorldSiteRoot.cs` no longer calls `ResolveWorldSiteHudViewportRect` or `ShouldReserveSiteHudWorkspace`.
- Run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- Expected red:
  - the new fullscreen tab rail guard fails because the scene still has `SitePeacetimePanel` and tab buttons under `LeftPrimaryPanelHost`;
  - the source guard fails because viewport layout still reserves the split-screen panel.

### Task 2: Author Fullscreen Map Tab-Rail Scene

- Modify `WorldSitePeacetimeHud.tscn`:
  - add `SiteManagementTabRail` under `OverlayHost`, anchored left center or left top with fixed safe margins;
  - move the four function buttons and the return-to-world tab into `OverlayHost/SiteManagementTabRail`;
  - make the rail vertical with stable dimensions for each button, with a readable icon-only short head exposed while collapsed and an internal label with right-side icon while hovered;
  - keep button names stable: `BuildTabButton`, `ConscriptionTabButton`, `RecruitTabButton`, `OverviewTabButton`, `ReturnMapTabButton`;
  - move `SitePeacetimePanel` under `OverlayHost`;
  - keep `SiteResourceLabel`, `SitePanelCloseButton`, `SiteHudTitle`, `ManagementContentScroll`, `SiteBuildSection`, `SiteConscriptionSection`, and `SiteOverviewSection` under the panel so existing binders remain focused, with the close button in the top header and no return-to-world button inside the opened panel;
  - size `SitePeacetimePanel` as a bounded overlay, not a full-height split-screen strip;
  - keep `MilitaryWorkbenchPanel` under `ModalHost`.
- Add or reuse resource-backed tab styling from `20250420manaTabD-Sheet.png` without creating runtime controls in C#.
- Update `WorldSitePeacetimeHudNodeRefs` paths to the new scene structure.
- Run the same regression command.
- Expected green for scene/path guards; source/layout guards may remain red until Task 3.

### Task 3: Implement Tab Rail State And Fullscreen Viewport Layout

- Modify `WorldSiteRoot.SiteManagementHud.cs`:
  - store a `Control _siteManagementTabRail`;
  - default peacetime management state shows the tab rail and hides `SitePeacetimePanel`;
  - selecting build, conscription, or overview opens `SitePeacetimePanel`, applies section visibility, and hides the rail;
  - opening recruitment hides the rail and uses the existing military workbench modal;
  - closing the task panel returns to the tab rail;
  - starting strategic building placement hides both the panel and tab rail until placement completes or clears.
  - top-left resource bar stays visible during normal city management and slides upward out of the way during building placement and battle-preparation unit placement.
- Modify `WorldSiteRoot.cs`:
  - make `ResolveMainWorldViewportRect()` use the authored fullscreen viewport rect for site management instead of reserving a panel strip;
  - remove or retire `ShouldReserveSiteHudWorkspace()` and `ResolveWorldSiteHudViewportRect()` if no longer needed.
- Modify `WorldSiteRoot.SiteInteraction.cs` if needed so placement selection/cancel calls restore tab-rail visibility through the same site-management state function.
- Run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - `git diff --check`

## Tests

- Focused Presentation regression:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- Build:
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Diff hygiene:
  - `git diff --check`

Known existing issue: the full `WorldSiteDeploymentCacheRegression` suite may still exit nonzero on unrelated battle-runtime UI skin and resource taxonomy guards. Task-specific guards must pass; any unrelated blocker must be named in verification evidence.

## Diagnostics

- Keep logs low-noise.
- Update existing layout logs so they report fullscreen site-management viewport state rather than `reserveUi=true` split-screen state.
- Missing authored nodes should continue to fail through `GameUiSceneFactory.GetRequiredNode` and focused regression guards.
- Do not add per-frame tab hover or placement logs.

## Manual QA

- Enter a player-held city.
- Confirm the map fills the screen with no left reserved map strip.
- Confirm the left side shows only the vertical function tab rail by default.
- Hover each function tab and confirm the tab visibly responds.
- Click build, conscription, and overview; confirm only the selected function panel opens and the other tabs hide.
- Close the function panel and confirm the tab rail returns.
- Click recruitment and confirm the larger hero-first workbench opens while the tab rail is hidden.
- Start building placement and confirm management panels/tabs retract while the map footprint preview follows the mouse.
- Confirm the resource bar is pinned to the top-left outside the building panel during normal city management.
- Confirm the resource bar smoothly retreats upward during building placement and battle-preparation unit placement, then returns afterward.
- Cancel or complete placement and confirm the expected management entry state returns.

## Acceptance

- The design proposal is archived and authority documents are current.
- `WorldSitePeacetimeHud.tscn` authors `SiteManagementTabRail` and task-sized function panels outside the permanent left workspace.
- Peacetime site-management viewport layout remains fullscreen.
- Other tabs hide while one function panel or the recruitment workbench is open.
- The resource bar is a top-left overlay bar, not a child of `SitePeacetimePanel`.
- Resource bar text continues to bind from Strategic Management dashboard resources.
- Building placement and battle-preparation unit placement retreat the resource bar upward with the rest of the map-owned interaction HUD.
- Opening a function tab retracts the tab first, then bounces the bounded panel in from the left with a small overshoot before settling.
- Closing a function panel bumps it slightly right, retracts it left, then smoothly pops the left-edge tab rail back out.
- Build placement retracts management UI while map placement owns interaction.
- Focused Presentation regression guards pass, with only documented unrelated blockers accepted.

## Verification Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` was rerun after the left-edge, readable-text, drawer-tab, return-tab, and bounce-panel follow-up. The task-specific guards passed, including fullscreen site viewport layout, `SiteManagementTabRail` as a manual left-edge drawer host, 44px readable icon-only collapsed tab heads, hover slide-out to the left screen edge with internal Chinese labels, right-side tab icons, no external tab tooltip popup, return-to-world as an outside rail tab, top-header close button, left-side panel bounce-in with overshoot, close-time right bump plus left retraction, tab rail pop-back animation, flush-left task-sized `SitePeacetimePanel`, tab-rail visibility state, building placement preview lifecycle, recruitment workbench ownership, ManaSoul GUI skin validation, and updated node refs. The command still exited nonzero on unrelated blockers: the battle-runtime pause detail skin guard and the `TileSets` resource taxonomy guard.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` exited `0` with `0` warnings and `0` errors.
- `git diff --check` exited `0`; Git reported only CRLF-to-LF normalization warnings for existing text files.
