# Agent Implementation Guide: Presentation UI Layout Architecture

This guide is the working instruction for agents implementing the UI refactor. It must be read together with:

- `AGENTS.md`
- `gameplay-alignment/authority-map.md`
- `design-proposals/active/2026-05-17-presentation-ui-layout-architecture/expected/system-design/presentation-ui-layout-architecture.md`
- `design-proposals/active/2026-05-17-presentation-ui-layout-architecture/implementation-plan.md`

## Required Working Posture

This is a Presentation/UI refactor, not a gameplay rewrite.

Do:

- keep UI changes resource-backed where practical;
- preserve existing gameplay behavior during early batches;
- add comments near non-trivial UI lifecycle boundaries;
- use focused regression guards;
- keep each batch small enough to test.

Do not:

- compute battle outcome, losses, rewards, settlement, or resource truth in UI;
- create a UI-owned unit pool or deployment pool;
- add fallback windows when an authoritative host is missing;
- move Application, Runtime, or Domain responsibilities into UI;
- rewrite unrelated battle/runtime architecture while moving panels.

## Current Files To Know

Strategic world:

- `scenes/world/ui/StrategicWorldHud.tscn`
- `src/Presentation/World/StrategicWorldRoot.cs`
- `src/Presentation/World/StrategicWorldRoot.UiBootstrap.cs`
- `src/Presentation/World/StrategicWorldRoot.DetailHud.cs`
- `src/Presentation/World/StrategicWorldRoot.ExpeditionHud.cs`

World site:

- `scenes/world/ui/WorldSitePeacetimeHud.tscn`
- `scenes/world/sites/WorldSiteRoot.tscn`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteExplorationPresentation.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteInteraction.cs`

Reusable UI:

- `src/Presentation/Common/GameUiSceneFactory.cs`
- `src/Presentation/Common/GameUiSkin.cs`
- `scenes/world/ui/WorldPrimaryActionButton.tscn`
- `scenes/world/ui/WorldSecondaryActionButton.tscn`
- `scenes/world/ui/WorldExpeditionCountRow.tscn`

Regression tests:

- `tests/WorldSiteDeploymentCacheRegression/`
- `tests/TargetBattleArchitectureRegression/`
- `tests/BattleHitFeedbackRegression/`

## Batch 1 Detailed Steps: Move Main Panels Left

### 1. Strategic World HUD

Edit `scenes/world/ui/StrategicWorldHud.tscn`.

Target behavior:

- `TopResourceBar` remains top.
- `SiteDetailPanel` becomes the left primary panel.
- Keep child node paths under `SiteDetailPanel` stable so `BindStrategicHud()` still resolves controls.
- Do not add a new data model.

Expected panel anchor shape:

```text
anchor_left = 0.0
anchor_right = 0.0
anchor_top = 0.0
anchor_bottom = 1.0
offset_left = 24-ish
offset_top = top bar height + margin
offset_right = left panel width
offset_bottom = negative bottom margin
```

Avoid:

- anchoring the main panel to `anchor_left = 1.0`;
- moving site map buttons into the panel;
- changing action callbacks.

### 2. World Site HUD

Edit `scenes/world/ui/WorldSitePeacetimeHud.tscn`.

Target behavior:

- `SiteTopBar` remains top.
- `SitePeacetimePanel` becomes the left primary panel during migration.
- Keep child node paths stable for existing code.

Edit `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`.

Target behavior:

- `ApplySitePeacetimePanelLayout()` must no longer force right-side layout.
- Add a concise comment explaining this method controls the migration left primary workspace.
- `UpdateSitePeacetimePanelVisibility()` can continue to control visibility for management/preparation in Batch 1.

### 3. Tests For Batch 1

Add source/scene regression checks to `tests/WorldSiteDeploymentCacheRegression/` or a focused existing UI/layout test area.

Minimum guards:

- `StrategicWorldHud.tscn` main detail panel is not right anchored.
- `WorldSitePeacetimeHud.tscn` main site panel is not right anchored.
- `WorldSiteRoot.SiteManagementHud.cs` does not set `_sitePeacetimePanel.AnchorLeft = 1.0f`.
- Existing deployment lifecycle still contains runtime panel hiding through `SetBattleRuntimeEnabled(true)`.

### 4. Batch 1 Verification

Run:

```bash
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Manual Godot checks:

- select a world location: detail panel is on the left;
- start expedition flow: left panel still shows actions;
- enter hostile site battle preparation: roster panel is on the left;
- click start battle: preparation panel disappears;
- runtime battle units remain on map.

## Batch 2 Detailed Steps: Introduce Host Names

This batch may be done by wrapping existing controls rather than rewriting them.

Add or map host names:

- `TopBarHost`
- `LeftPrimaryPanelHost`
- `RightNotificationHost`
- `MinimapHost`
- `BottomCommandHost`
- `OverlayHost`
- `ModalHost`

Rules:

- Host controls are layout containers only.
- Existing detail panels may remain inside `LeftPrimaryPanelHost`.
- Empty hosts may be hidden.
- Do not add business data to host scripts.

Tests:

- scene text contains host names;
- no main action list appears under right notification/minimap hosts.

## Batch 3 Detailed Steps: Dedicated Battle Preparation Content

Current issue:

`RefreshBattlePreparationForceList()` writes roster buttons into `_siteGarrisonList`. This is semantically wrong.

Target:

Add dedicated controls:

- `BattlePreparationContent`
- `BattlePreparationRosterList`
- `BattlePreparationEnemySummary`
- `BattlePreparationStatus`
- `BattlePreparationActionList`

Implementation rules:

- Continue using the active battle request/snapshot as data source.
- Dragging a roster unit must update the authoritative deployment request/draft, not a UI clone.
- `SiteGarrisonList` returns to site-management garrison display only.
- `SiteActionList` returns to site-management actions only.
- Preparation and management content visibility is mutually exclusive.

Tests:

- source contains dedicated battle-preparation container bindings;
- source no longer adds battle preparation roster buttons to `_siteGarrisonList`;
- deployment drag still references the existing authoritative request placement flow.

## Batch 4 Detailed Steps: View Model / Binder Boundary

Do this after the layout is stable.

Introduce view models only when they simplify real complexity. They must be display-only.

Good view model fields:

- `Id`
- `Title`
- `SummaryLines`
- `Rows`
- `Actions`
- `DisabledReason`
- `Notice`

Bad view model fields:

- mutable `WorldSiteState`;
- mutable `BattleGroupState`;
- mutable runtime actors;
- computed settlement losses;
- duplicated unit lists that can diverge from authoritative request/snapshot data.

Each binder should:

- clear/reuse UI rows;
- set text and disabled state;
- attach callbacks that submit requests;
- not mutate Domain or Runtime directly.

## Batch 5 Detailed Steps: Right Notifications And Minimap Reservation

Target:

- Right side contains compact notice stack and minimap/navigation aid only.
- It does not contain primary action panels.

V0 may implement:

- a hidden `MinimapHost`;
- a simple compact `RightNotificationHost`;
- routing existing notice text to either top bar or notification host.

Avoid:

- moving deployment roster to right side;
- moving site management actions to right side;
- adding a second detail panel.

## Batch 6 Detailed Steps: Future Bottom Command Host

Do not implement command UI until battle command work begins.

When implemented:

- selected battle group and available command definitions display in `BottomCommandHost`;
- button click creates `CommandRequest`;
- Application validates ownership/channel;
- Runtime validates battle-context facts;
- event stream drives feedback.

## Review Checklist For Every Batch

- Does this batch keep UI Presentation-only?
- Does it use current authoritative data instead of cloning state?
- Are mode transitions explicit?
- Are permanent panels in approved hosts?
- Are transient overlays kept transient?
- Are player-visible texts Chinese?
- Did tests cover the behavior that was easy to regress?
- Did source files stay below oversized thresholds?

## Expected Batch Commit Shape

Each batch should be separately reviewable:

- UI scene/resource changes;
- minimal binding code changes;
- regression tests;
- no unrelated architecture or gameplay changes.

If a batch reveals that the expected architecture is wrong, stop and update the proposal for user acceptance before continuing.
