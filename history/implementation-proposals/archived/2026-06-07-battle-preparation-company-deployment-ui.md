# Battle Preparation Company Deployment UI Implementation Proposal

Status: Implemented - automated verification complete, manual QA pending
Created: 2026-06-07
Accepted: 2026-06-07
Implemented: 2026-06-07
Verified: 2026-06-07 automated checks passed for related scope; manual QA pending

Originating Design Proposal: `design-proposals/archived/2026-06-07-battle-preparation-drag-deployment-ux/`
Requirement Id: `battle-preparation-drag-deployment-ux`
Related Implementation Proposal:

Authority Documents:
- `gameplay-design/details/combat-command/README.md`
- `system-design/presentation-ui-layout-architecture.md`
- `system-design/world-battle-entry-architecture.md`

## Goal

Implement the accepted battle-preparation flow as a map-first company deployment UI: the player drags a hero company portrait from a narrow roster, previews the full hero-led formation on the battlefield, drops only on legal placement, selects a marker-backed objective on a compact tactical thumbnail, chooses an engagement rule, and starts battle only after every player company plan is complete.

## Godot 4.5 Reference Constraints

- `Control._gui_input()`, `accept_event()`, and `mouse_filter` should own HUD-local clicks and stop them from leaking into map input when the pointer is over active controls.
- Godot Control drag/drop (`_get_drag_data`, `_can_drop_data`, `_drop_data`, `set_drag_preview`) is not the main path for battlefield deployment because the drop target is a map inside `SubViewport`, not another Control-only surface.
- Input events are propagated to `SubViewport` through `SubViewportContainer`; keep using the existing viewport coordinate conversion helpers rather than adding a second map input path.
- Mouse positions are viewport coordinates (`InputEventMouseButton.Position`, `InputEventMouseMotion.Position`, `GetViewport().GetMousePosition()`), so company formation anchors must continue resolving through the current root-screen -> world-viewport -> grid conversion.
- HUD retreat/return should use `CreateTween().BindNode(this)`, `TweenProperty`, parallel tweening, and `Kill()` before starting a replacement tween.
- Screen-space HUD remains under the existing `CanvasLayer`/layout hosts. Battlefield previews, grid highlights, deployment zones, and map-space unit entities remain inside `MainWorldViewport`.

## Boundary

- Presentation/UI owns authored HUD resources, roster rows, current-company controls, objective thumbnail display, drag preview visuals, red/normal feedback, and short player feedback.
- Application/world battle entry owns plan-draft facts and launch validation.
- Runtime owns only the battle after launch; this proposal must not change battle simulation, target acquisition, damage, settlement, or campaign writeback.
- The active data source remains the current `BattleStartRequest`, `PlayerBattleGroupPlans`, `ObjectiveZones`, and `PreferredPlacements`.

## Current Implementation Notes

- `WorldSiteRoot.BattlePreparationHud.cs` already enters preparation, clears player placements, refreshes deployment zones, selects objective zones, selects engagement rules, and validates launch.
- `WorldSiteRoot.BattlePreparationDrag.cs` already supports request-backed drag placement for one force slot and uses footprint-aware validation.
- `WorldSiteRoot.DeploymentFootprint.cs` already expands unit footprints, checks terrain, deployment zones, and occupancy.
- `WorldSiteRoot.BattleObjectivePlanningHud.cs`, `BattleObjectiveMapDialog`, and `BattleObjectiveMapPreview` already provide marker-backed objective zones and simplified map drawing.
- `WorldSitePeacetimeHud.tscn` currently hosts battle preparation inside the left management panel with roster/action lists. This is the main UX problem to replace.

## Current UI Cleanup Requirements

The implementation is not additive. The current battle-preparation UI contains historical product mistakes that must be removed from the active player flow.

Remove from the battle-preparation player flow:

- the `BattlePreparationContent` subtree under `LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content`;
- bindings to `_siteBattlePreparationContent`, `_siteBattlePreparationRosterList`, `_siteBattlePreparationEnemySummary`, `_siteBattlePreparationStatus`, and `_siteBattlePreparationActionList`;
- `SetBattlePreparationContentVisible(true)` as the way to enter battle preparation;
- `UpdateSitePeacetimePanelVisibility("battle_preparation_refresh")` as the way to show battle preparation;
- `BuildBattlePreparationOverview()` and `BuildBattlePreparationSelectionText()` as screen-body presentation;
- `RefreshBattlePreparationForceList()` and `AddBattlePreparationRosterButtons()` as the roster implementation;
- `RefreshBattlePreparationActions()`, `AddBattlePreparationStartButton(Container)`, `AddBattlePreparationObjectiveMapButton(Container)`, and `AddBattlePreparationEngagementRuleButton(Container, ...)` as action-list rendering;
- player-facing target selection that only opens `BattleObjectiveMapDialog` through a large modal;
- abrupt `_sitePeacetimePanel.Visible = false` during roster drag;
- tests that assert `BattlePreparationContent`, `BattlePreparationRosterList`, or `BattlePreparationActionList` are required battle-preparation containers.

Keep or migrate:

- `EnterBattlePreparation()`, request setup, deployment-zone refresh, request-backed placement sync, objective-zone plan writing, engagement-rule plan writing, and launch validation.
- `BattleObjectiveMapPreview` drawing logic and marker-backed map data builders.
- Existing map-entity dragging for already placed formations only if it remains subordinate to company-level plan state and does not reintroduce single-unit roster deployment as the primary flow.
- `BattleObjectiveMapDialog` may remain temporarily as a debug/fallback control, but the implementation is not accepted until the compact thumbnail is the normal player-facing target selector.

Regression tests must move with the cleanup: any test that currently preserves the old panel/list path should become a negative guard or be replaced by a compact-HUD guard.

## Scope

- Replace the text-heavy battle-preparation panel with authored compact battle-preparation HUD controls.
- Remove historical battle-preparation panel/list bindings from the active battle-preparation flow.
- Convert the roster from force-slot buttons into a narrow hero-company switcher and drag source.
- Carry strategic hero-company default formation into the current battle plan as `InitialFormationId`.
- Use the current battle formation when building company drag previews.
- Prevent formation adaptation from accepting overlapping member footprints.
- Drag one company row to preview and commit the whole hero-led formation, not a single unit.
- Add smooth HUD retreat on company drag start and restore on release/cancel.
- Embed a compact marker-backed tactical objective thumbnail for the selected company.
- Move engagement rule selection into compact current-company controls.
- Require each player company to have valid formation placement, valid objective zone, and explicitly selected engagement rule before launch.
- Keep existing map/entity/deployment-zone visuals stable during preparation.

## Non-Goals

- No live battle command UI changes after battle starts.
- No Runtime combat, AI, navigation, settlement, or report-rule changes.
- No large-scale RTS box selection.
- No new persistent campaign aggregate beyond the default formation field on the existing army/company state path.
- No hardcoded one-off UI tree construction in C# for the new HUD.
- No hidden fallback panel if an authored UI node is missing.
- No full strategic management formation editor in this slice; expose the persistent field and battle-preparation consumption path first.

## Proposed File Responsibilities

- `scenes/world/ui/WorldSitePeacetimeHud.tscn`
  - Delete the old `BattlePreparationContent` panel subtree from `SitePeacetimePanel`.
  - Add battle-preparation-specific HUD docks under existing approved hosts.
  - Keep site management panel for management mode, but hide it during battle preparation.
  - Provide stable node names for roster dock, current-company plan bar, objective thumbnail dock, start-battle dock, and drag-retreat roots.
- `scenes/world/ui/BattlePreparationRosterRow.tscn`
  - Reusable row for avatar, company name, and status marker only.
- `scenes/world/ui/BattlePreparationObjectiveThumbnail.tscn`
  - Compact panel wrapping `BattleObjectiveMapPreview` for the selected company.
- `src/Presentation/World/Sites/BattlePreparationRosterRow.cs`
  - Binds company identity, optional avatar texture, selected state, status marker, click, and drag-start signal.
- `src/Presentation/World/Sites/BattlePreparationObjectiveThumbnail.cs`
  - Binds cells, marker-backed regions, selected target, and emits objective selection.
- `src/Application/World/BattlePreparationCompanyFormationPlanner.cs`
  - Builds a deterministic company formation draft from a selected group, anchor cell, direction, deployment cache, grid map, and current request placements.
  - Validates all member footprints as one transaction and returns either a complete placement draft or a failure reason.
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationHud.cs`
  - Builds company row view models, binds compact HUD, computes complete/partial/missing state, and launch validation feedback.
  - Removes the old overview/body/action-list battle-preparation binder from the active path.
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationDrag.cs`
  - Replaces single-force roster drag state with company drag state, multi-entity preview, red/normal feedback, and transactional commit/restore.
- `src/Presentation/World/Sites/WorldSiteRoot.BattleObjectivePlanningHud.cs`
  - Reuse existing marker-backed map data for compact thumbnail binding.
- `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
  - Remove old battle-preparation panel/list node bindings.
  - Bind new authored controls and hide/show battle-preparation HUD separately from management panel.
- `src/Presentation/Common/GameUiSceneFactory.cs`
  - Add scene paths and factory helpers for the roster row and objective thumbnail.
- `tests/WorldSiteDeploymentCacheRegression/`
  - Replace old positive assertions for `BattlePreparationActionList` and `BattlePreparationContent` with negative guards.
  - Add regression guards for resource-backed compact HUD, company-level drag, transactional formation commit, HUD retreat hooks, compact objective thumbnail, explicit rule completion, and no return to text-heavy action lists.

## Expected Implementation

### Company Model

Battle preparation should treat `BuildBattlePreparationPlayerGroups()` as the player-facing company list. Each row represents one `BattleRuntimeCommandGroupView`, normally one hero plus attached corps. The row label uses the hero display name; the row status is:

- `check`: every member placement is committed, objective is selected from current marker-backed zones, and the group key is in `_explicitBattlePreparationRuleGroups`.
- `dash`: at least one required part is present but the plan is incomplete.
- `cross`: no meaningful plan has been started.

Enemy forces remain visible on the battlefield through existing prepared enemy placements; they should not appear as draggable roster rows in the player company switcher.

### Formation Draft

Add a single formation planner instead of spreading formation math through UI event handlers.

Inputs:

- `BattleStartRequest`.
- selected company/group key.
- candidate formation anchor from the mouse/grid conversion.
- deployment side, faction id, and attack direction.
- `BattleGridMap`.
- `WorldSiteRuntimeDeploymentCache`.
- existing committed placements from the same request.

Output:

- ordered member drafts: force id, force index, placement id, unit id, faction id, anchor surface, footprint size, and covered cells.
- aggregate covered cells.
- `IsValid` and a stable failure reason such as `placement_cell_invalid`, `placement_cell_water`, `placement_cell_not_deployable`, or `placement_cell_occupied`.

The draft must be transactional: no `PreferredPlacements` are changed during hover. On valid drop, apply every member placement together. On invalid release or cancel, restore the previous committed company placements and preview entities.

Formation packing should be deterministic and small:

- hero force first, then corps forces by force id and index;
- direction-aware front/back axis from `WorldSiteAttackDirection`;
- lateral lane fill before adding a deeper row;
- each unit footprint reserves its full covered cells;
- the returned formation anchor represents the top-left of the formation bounding box, while preview entities are centered on their own footprints.

### Drag Preview

During company drag:

- create temporary `BattleEntity` previews for every member in the company;
- raise preview entities above map rendering using the existing high-Z drag behavior;
- update member positions from the current formation draft on mouse motion;
- draw aggregate legal footprint through hover/normal feedback;
- draw aggregate invalid footprint through `BattleGridHighlightKind.Invalid`;
- modulate every preview entity into the same red invalid treatment when the draft is invalid;
- keep deployment-zone overlay visible.

Do not rebuild all existing battle-preparation entities during mouse motion.

### HUD Layout

Battle preparation should hide the left management panel and use only compact mode-specific HUD:

- narrow roster dock at lower-left or left edge, showing avatar/name/status;
- current-company plan bar near the lower center, with objective and engagement controls;
- objective thumbnail in `MinimapHost` or compact overlay host, backed by `BattleObjectiveMapPreview`;
- start-battle action as a small fixed command, disabled or showing failure feedback until all companies are complete;
- top bar reduced to battle/site status only.

`SitePeacetimePanel` remains the management/settlement panel. It should not be visible solely because `_isBattlePreparationActive` is true, and battle preparation should not write tactical plan text into `SiteHudBody`, `SiteSelectionLabel`, facility lists, garrison lists, or site action lists.

All persistent HUD controls in the retreat set should slide offscreen during drag and return after release. Use a single retreat controller/tween path so roster, plan bar, objective thumbnail, top status, and start button cannot drift into different animation states.

### Input Handling

- Roster row click selects the current company and refreshes the objective/strategy controls.
- Roster row drag starts company formation preview and calls `GetViewport().SetInputAsHandled()`.
- While dragging, nonessential HUD controls should set `MouseFilter = Ignore` after retreat starts so map movement is not blocked.
- On drop, map/grid resolution keeps using current viewport conversion helpers.
- Existing map entity dragging may remain as a refinement path for already placed units, but the primary deployment flow is company-row drag. If map dragging remains for individual members, it must keep request-backed validation and refresh the company status.

### Objective Thumbnail

Reuse `BuildBattleObjectiveMapCells()` and `BuildBattleObjectiveMapRegions()`.

- The thumbnail must draw terrain from active grid data.
- Objective choices must come from `ObjectiveZone` markers, with enemy deployment markers as the current V0 fallback.
- Player deployment regions may be shown but not selectable.
- Clicking a selectable region writes the selected zone into the current company plan through the existing `ApplyBattlePreparationObjectiveZoneToPlan` path.
- The large modal dialog may remain temporarily for debugging or fallback, but the player-facing target UX for this proposal is the compact thumbnail.

### Launch Validation

`CanLaunchPreparedBattle()` must check per player company:

- every member force slot has a committed placement;
- every selected objective exists in the current request `ObjectiveZones`;
- `_explicitBattlePreparationRuleGroups` contains the group key.

Default engagement rules may still exist internally for compatibility, but they do not count as player-selected strategy.

## Task Plan

1. Add failing cleanup tests for the current wrong UI paths:
   - `WorldSitePeacetimeHud.tscn` no longer contains `BattlePreparationContent`, `BattlePreparationRosterList`, `BattlePreparationEnemySummary`, `BattlePreparationStatus`, or `BattlePreparationActionList` under `SitePeacetimePanel`;
   - `WorldSiteRoot.SiteManagementHud.cs` no longer binds `_siteBattlePreparationContent`, `_siteBattlePreparationRosterList`, `_siteBattlePreparationEnemySummary`, `_siteBattlePreparationStatus`, or `_siteBattlePreparationActionList`;
   - `WorldSiteRoot.BattlePreparationHud.cs` no longer calls `SetBattlePreparationContentVisible(true)`, `RefreshBattlePreparationForceList()`, or `RefreshBattlePreparationActions()`;
   - battle preparation no longer writes plan body text through `BuildBattlePreparationOverview()` or `BuildBattlePreparationSelectionText()`.
2. Remove the old panel/list scene nodes, stale fields, and stale binding methods from the active battle-preparation path.
3. Add failing tests for compact battle-preparation HUD authoring:
   - new authored dock nodes exist outside `SitePeacetimePanel`;
   - new roster row and objective thumbnail scenes are loaded through `GameUiSceneFactory`;
   - battle preparation routes start/objective/rule controls through compact dedicated controls, not `BattlePreparationActionList`.
4. Add failing tests for company-level plan completion:
   - roster status distinguishes complete/partial/missing;
   - launch rejects a group that only has a default, non-explicit engagement rule.
5. Add the formation planner tests:
   - one hero plus three corps produces four placement drafts;
   - all member footprints are included in aggregate validation;
   - occupied terrain rejects the whole draft without mutating prior placements.
6. Implement `BattlePreparationCompanyFormationPlanner` with deterministic packing and request-backed validation.
7. Add `BattlePreparationRosterRow.tscn` and `BattlePreparationObjectiveThumbnail.tscn`, then add factory paths.
8. Update `WorldSitePeacetimeHud.tscn` with battle-preparation docks under existing layout hosts.
9. Bind new HUD nodes in `WorldSiteRoot.SiteManagementHud.cs`.
10. Refactor `WorldSiteRoot.BattlePreparationHud.cs` to build company rows and compact current-company controls.
11. Refactor `WorldSiteRoot.BattlePreparationDrag.cs` to use company drag state, multi-entity preview, invalid red treatment, and transactional commit/restore.
12. Bind compact objective thumbnail using the existing marker-backed map data and selected company plan.
13. Add HUD retreat/return tween hooks and call them on company drag start/end/cancel.
14. Run regression tests and a low-concurrency build.

## Tests

Primary automated verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Add or update regression cases to assert:

- battle-preparation HUD is resource-backed and not rebuilt through direct `new Button`, `new Label`, or ad hoc container trees;
- old battle-preparation panel/list nodes and fields are not used by the active flow;
- existing tests no longer preserve `BattlePreparationActionList` as the accepted action surface;
- company roster rows are compact and contain avatar/name/status markers only;
- primary battle-preparation action wiring no longer depends on the old long action list;
- company drag uses all forces in the selected `BattleRuntimeCommandGroupView`;
- hover validation does not mutate `PreferredPlacements`;
- valid drop commits all member placements together;
- invalid drop restores previous placements and renders invalid red feedback;
- two `2x1` members placed side by side cover four distinct cells and never collapse into a three-cell overlap;
- narrow deployment zones can use a non-overlapping fallback shape instead of blocking deployment when a valid shape exists;
- pointer intent outside the deployment zone projects to the nearest legal whole-company formation instead of freezing the preview;
- invalid formation previews still return every company member draft so the visible formation moves as one unit;
- battle-preparation plans initialize missing `InitialFormationId` from the source army default formation, falling back to the standard formation;
- HUD retreat is called on drag start and restored on drag end/cancel;
- compact objective thumbnail uses marker-backed map regions and active grid cells;
- launch requires explicit engagement-rule selection per company.

## Diagnostics

Add low-noise logs:

- `BattlePreparationCompanySelected group=...`
- `BattlePreparationCompanyDragStarted group=... members=...`
- `BattlePreparationCompanyDragPreview group=... valid=... reason=...` only when validity state changes, not every mouse motion
- `BattlePreparationCompanyDragCommitted group=... placements=...`
- `BattlePreparationCompanyDragCancelled group=... reason=...`
- `BattlePreparationHudRetreatChanged active=... reason=...`
- `BattlePreparationPlanCompletionChanged group=... status=...`

## Manual QA

Use the local playable site battle entry and verify:

1. Enter pre-battle deployment from the world flow.
2. The battlefield remains the main screen; no large left essay panel appears.
3. The lower-left roster is narrow and shows only avatar/name/status.
4. Dragging the hero company row shows the full company formation on the map.
5. During drag, persistent HUD slides offscreen and deployment-zone/formation feedback remains visible.
6. Legal hover renders normal; illegal hover renders the whole formation red.
7. Releasing on an illegal position does not corrupt previous committed placements.
8. Releasing on a legal position commits all company members.
9. The tactical thumbnail shows authored target regions and lets the selected company choose one.
10. Engagement strategy can be selected from compact controls.
11. Start battle is blocked until every player company has placement, objective, and explicit strategy.
12. Starting battle enters the existing runtime with the selected plan.

Also verify the absence of old behavior:

- no left management panel appears just because battle preparation is active;
- no enemy force list is shown in the player roster;
- no start/objective/strategy controls are rendered as a vertical action-list stack;
- dragging a roster row never deploys only one force slot when the selected company has multiple member forces.

## Risks

- Current battle-preparation code is concentrated in large `WorldSiteRoot` partials. Keep the new formation planner and thumbnail/row scripts focused to avoid making the root partials worse.
- The old modal objective dialog can coexist during migration, but player-facing controls must not keep routing through a large modal as the only target-selection path.
- Existing terminal output shows some older Chinese text as mojibake. Do not "fix" unrelated encoding or copy stale garbled strings into new player-facing UI.
- If group formation validation cannot be made Application-owned without destabilizing current code, stop and create a smaller architecture repair proposal instead of hiding validation in UI-only code.

## Acceptance Evidence

- 2026-06-07 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: battle-preparation company formation planner passed outside-anchor projection to the nearest legal whole draft, whole invalid preview generation, non-overlap placement, current-plan formation drag usage, and UI source guards that prevent company drag preview from freezing on invalid grid-cell anchors. The run still fails on unrelated pre-existing `legacy manual battle authority docs stay deleted`.
- 2026-06-07 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: battle-preparation plan default formation propagation, current-plan formation drag usage, two-`2x1` member no-overlap placement, narrow-zone fallback adaptation, transactional company placement, and `WorldSiteRoot` line-budget guards passed. The run still fails on unrelated pre-existing `legacy manual battle authority docs stay deleted`.
- 2026-06-07 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: battle-preparation UI, scene-authored dock layout, company deployment, compact objective thumbnail, explicit plan launch validation, resource-backed authoring, and `WorldSiteRoot` line-budget guards passed. The run still fails on unrelated pre-existing `legacy manual battle authority docs stay deleted`.
- 2026-06-07 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: roster row binding and drag regression passed, including bind-before-ready preservation, release-only selection, drag-threshold start, and child input pass-through. The run still fails on unrelated pre-existing `legacy manual battle authority docs stay deleted`.
- 2026-06-07 `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- 2026-06-07 `git diff --check`: passed with no whitespace errors; output only reported existing CRLF/LF normalization warnings.
- 2026-06-07 `dotnet build-server shutdown`: completed after build.
- Manual QA through the playable Godot flow is still pending.

## Stop Conditions

Stop and return to design discussion if implementation requires:

- Presentation to own a second battle request, unit pool, or battle snapshot;
- hidden objective cells that are not marker-backed;
- Runtime combat or AI behavior changes;
- broad refactoring of world/battle scene transition;
- reviving large text panels as the primary battle-preparation surface.
