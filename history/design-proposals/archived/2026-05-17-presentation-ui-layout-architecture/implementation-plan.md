# Presentation UI Layout Architecture Implementation Plan

Status: MainWorldViewport Isolation Implemented

## Goal

Refactor UI layout in batches so strategic-world, site-management, battle-preparation, and future battle-command UI follow the accepted Presentation/UI architecture without destabilizing gameplay state, battle runtime, or settlement.

## Global Rules For Agents

- Read `AGENTS.md`, `gameplay-alignment/authority-map.md`, and this proposal before editing UI.
- Implement against `expected/system-design/presentation-ui-layout-architecture.md`.
- Keep UI as Presentation only. UI may display facts and submit requests; it must not own rules, settlement, long-term state, or runtime truth.
- Do not create a second unit pool, deployment pool, battle result, reward result, or resource authority in UI.
- Prefer `.tscn` resources and reusable UI scenes over constructing full control trees in gameplay code.
- Keep existing node names during early batches when doing so reduces binding churn.
- Add low-noise logs for mode transitions and missing required hosts.
- Add regression guards for architecture boundaries and layout rules.
- Do not edit archived proposals.

## Batch 0: Proposal Acceptance And Guard Setup

Purpose: freeze the design before code.

Tasks:

- [x] User accepts `expected/system-design/presentation-ui-layout-architecture.md`.
- [x] Change proposal status from `Draft` to `Accepted`.
- [x] Add or update regression tests that read source/scene text and guard:
  - main persistent panels are not right-side anchored;
  - battle preparation has dedicated roster/action containers before old containers are removed;
  - UI code does not introduce settlement/result computation.

Verification:

- [x] `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
- [x] `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj`

## Batch 1: Move Existing Main Panels To Left Primary Workspace

Purpose: solve the immediate map-obstruction problem with minimal binding churn.

Strategic world:

- [x] Update `scenes/world/ui/StrategicWorldHud.tscn` so `SiteDetailPanel` is left anchored.
- [x] Keep current child node names under `SiteDetailPanel`.
- [x] Ensure site hit buttons, labels, and hover summary still render above the world map.
- [x] Do not move action semantics yet.

World site:

- [x] Update `scenes/world/ui/WorldSitePeacetimeHud.tscn` so `SitePeacetimePanel` is left anchored.
- [x] Update `WorldSiteRoot.SiteManagementHud.cs` so panel layout code uses left primary workspace anchors.
- [x] Add a comment near the layout method explaining that the panel is the current left primary workspace during migration.
- [x] Ensure battle preparation starts with the panel on the left and runtime start hides it.

Verification:

- [x] `dotnet build rpg.sln -maxcpucount:2 -v:minimal`
- [x] `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
- [ ] Manual Godot check:
  - big world selected location detail appears left;
  - site management panel appears left;
  - battle-preparation roster appears left;
  - start battle hides the preparation panel.

Rollback boundary:

- Revert only the changed UI scene anchors/layout method if the left placement breaks input.
- Do not change business services to compensate for layout issues.

## Batch 2: Introduce Stable Layout Host Names

Purpose: create a shared vocabulary without rewriting all panels.

Tasks:

- [x] Add left/top/right/minimap/bottom/overlay host names to UI scenes where practical:
  - `TopBarHost`
  - `LeftPrimaryPanelHost`
  - `RightNotificationHost`
  - `MinimapHost`
  - `BottomCommandHost`
  - `OverlayHost`
  - `ModalHost`
- [x] Initially map existing `TopResourceBar` / `SiteTopBar` into `TopBarHost`.
- [x] Initially map existing `SiteDetailPanel` / `SitePeacetimePanel` into `LeftPrimaryPanelHost`.
- [x] Update existing binders to the host-backed node paths so current code still works.
- [x] Add source comments explaining these are layout hosts, not data authorities.

Verification:

- [x] Existing world/site UI still works.
- [x] Regression guards assert host names exist in strategic-world and site HUD scenes.

## Batch 3: Split Battle Preparation Out Of Management Lists

Purpose: fix the semantic coupling where deployment UI is hosted inside garrison/action lists.

Tasks:

- [x] Add dedicated battle-preparation containers under the left primary workspace:
  - `BattlePreparationContent`
  - `BattlePreparationRosterList`
  - `BattlePreparationEnemySummary`
  - `BattlePreparationStatus`
  - `BattlePreparationActionList`
- [x] Update `RefreshBattlePreparationForceList()` to bind player roster into `BattlePreparationRosterList`.
- [x] Update start-battle button binding to use `BattlePreparationActionList`.
- [x] Keep consuming the existing battle request/snapshot source; do not duplicate unit data.
- [x] Keep `SiteGarrisonList` for actual garrison/site management display only.
- [x] Update visibility so management content and preparation content are mutually exclusive by UI mode.

Verification:

- [ ] Deployment roster appears in dedicated deployment content.
- [ ] Garrison list remains a garrison list in site management.
- [ ] Drag from roster still writes to the authoritative deployment request/draft.
- [ ] Start battle hides preparation content and enters runtime.

## Batch 4: Add UI Mode Binder Boundary

Purpose: reduce direct root partial UI construction over time.

Tasks:

- [x] Add a presentation-only `WorldUiMode` or equivalent enum if needed.
- [x] Add mode-specific bind/refresh methods with explicit names:
  - `BindStrategicSelectionPanel`
  - `BindExpeditionDraftPanel`
  - `BindSiteManagementPanel`
  - `BindBattlePreparationPanel`
  - `BindBattleRuntimeHud`
  - `BindSettlementReportPanel`
- [x] Keep them in Presentation; do not move business rules into them.
- [x] Defer display-only view model classes until a panel exceeds simple binding.
- [x] Preserve explicit refresh reasons on site panel visibility updates.

Verification:

- [x] Source guards confirm view models live under Presentation and do not mutate Domain state.
- [x] Existing tests pass.

## Batch 4.5: Real MainWorldViewport Isolation

Purpose: separate game rendering from the UI canvas so map/site/battle content cannot remain a full-screen background behind management panels.

Strategic world:

- [x] Update `scenes/world/StrategicWorldRoot.tscn` so `MainWorldViewportHost` is a `SubViewportContainer` in the main world area.
- [x] Move `WorldCamera`, `WorldMapRoot`, map tile layers, anchors, and strategic navigation nodes under `MainWorldViewportHost/MainWorldViewport`.
- [x] Add a viewport-local `WorldMapOverlay` for site hit buttons and labels.
- [x] Update exported NodePaths and binding code so strategic map, camera, anchors, visual layer, and overlay resolve through `MainWorldViewport`.
- [x] Keep HUD panels outside `MainWorldViewport`.
- [x] Update map projection and pointer handling so root-screen input is converted to viewport-local coordinates before map navigation or selection.

World site:

- [x] Evaluate whether `WorldSiteRoot` can be wrapped without changing runtime authority.
- [x] If safe in this batch, move site map, unit roots, battle overlays, debug world nodes, and battle camera under a real `MainWorldViewport`.
- [x] Keep `CanvasLayer`, site management HUD, battle-preparation UI, modal UI, and selection vignette outside the viewport.
- [x] World-site isolation completed in this batch, so no remaining site-scene isolation gap was recorded.

Verification:

- [x] Scene/source regression guard confirms strategic world uses `SubViewportContainer` and `SubViewport`.
- [x] Scene/source regression guard confirms map content is parented under `MainWorldViewport`.
- [x] Scene/source regression guard confirms site hit controls attach to `WorldMapOverlay`, not the root.
- [x] `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
- [x] `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Batch 5: Right Notification And Minimap Reservation

Purpose: stop the right side from becoming another main action panel.

Tasks:

- [ ] Add compact right notification stack.
- [ ] Route existing notice text to top bar or right notification stack based on severity.
- [ ] Add empty minimap host placeholder, hidden if no minimap implementation exists.
- [ ] Add comments/rules that right side must not host persistent action lists.

Verification:

- [ ] No main action list is right anchored.
- [ ] Notifications are compact and collapsible or low-obstruction.

## Batch 6: Future Battle Command Host

Purpose: prepare light-RTS command UI without violating command architecture.

Tasks:

- [ ] Add `BottomCommandHost` controls only when battle runtime command work begins.
- [ ] Buttons create `CommandRequest`.
- [ ] Application validates command ownership/channel.
- [ ] Runtime accepts/rejects battle-context facts.
- [ ] UI displays event-stream feedback and does not compute runtime truth.

Verification:

- [ ] Command validation distinguishes UI-local feedback, Application rejection, and Runtime rejection.
- [ ] Runtime events remain the source for command execution feedback.

## Completion Criteria

- [x] Main persistent UI panels use left primary workspace.
- [x] Battle preparation has dedicated UI containers.
- [x] UI host names exist and future work can target them.
- [x] No UI-owned duplicate business state is introduced.
- [x] Regression tests protect the layout and authority boundaries.
- [x] Expected document is ready to merge into `system-design/`.
