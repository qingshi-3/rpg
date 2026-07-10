# Presentation UI Layout Architecture

## Gameplay Authority

This document implements the accepted hero-led light RTS and strategic-location management direction at the Presentation/UI layer. It does not change gameplay rules.

## Responsibility

Presentation/UI owns:

- Godot UI scene resources and reusable UI controls.
- Layout hosts, panel visibility, input focus, and interaction-mode presentation.
- Displaying definitions, domain state snapshots, application view models, runtime events, command availability hints, reports, and diagnostics.
- Creating UI-local feedback for invalid pointer/input actions.
- Submitting user intent to Application or Runtime-facing command boundaries.
- Submitting scene transition requests to the accepted scene transition router after Application-level validation.

## Does Not Own

Presentation/UI does not own:

- Long-term campaign state.
- Hero, corps, battle-group, garrison, city, resource, equipment, reward, or settlement truth.
- Battle runtime execution, targeting authority, damage, outcome, or event attribution.
- Application-level validation such as ownership, availability, command channel legality, sortie locks, or settlement writeback.
- A second unit pool or deployment authority separate from Application/runtime request state.
- Root scene replacement, loading ownership, preload cache ownership, or transition rollback authority.

## Layer Rules

UI may:

- Read definitions and current state for display through existing query services during migration.
- Build derived display-only view models.
- Disable buttons for basic local availability hints.
- Submit Application requests such as expedition, site entry, and deployment confirmation.
- Submit typed scene transition requests instead of calling root scene-change APIs directly.
- Submit `CommandRequest` when light-RTS command UI is implemented, including destination beacon commands from selected battle groups and right-clicked cells.

UI must not:

- Mutate long-term state directly except through existing migration paths already owned by Application services.
- Compute battle outcome, casualties, rewards, or settlement deltas.
- Duplicate `BattleStartRequest`, `BattleStartSnapshot`, battle group state, or site unit pools as UI-owned models.
- Treat hidden fallback controls as valid behavior when an authoritative path fails.
- Add permanent screen-level panels outside approved layout hosts.

## Layout Host Model

The target layout is a strategy/RTS workspace:

```text
+------------------------------------------------------------------+
| TopBarHost                                                       |
| global resources, time, mode, return/menu                         |
+-------------------------+----------------------------------------+
| LeftPrimaryPanelHost    | MainWorldViewport                       |
| current mode information| map, site, battle scene, units          |
| actions, rosters, lists |                                        |
|                         | RightNotificationHost                   |
|                         | collapsible event/task/warning stack    |
|                         |                                        |
|                         | MinimapHost                             |
|                         | lower-right minimap or navigation aid   |
+-------------------------+----------------------------------------+
| BottomCommandHost                                                |
| battle commands, selected battle-group commands, later light RTS  |
+------------------------------------------------------------------+
| OverlayHost / ModalHost                                          |
| drag preview, selection box, hover tooltip, confirm dialogs       |
+------------------------------------------------------------------+
```

`MainWorldViewport` is a real Godot `SubViewport` hosted by a `SubViewportContainer`, not only a logical rectangle. Strategic map, site map, battle units, battle overlays, and their cameras live inside this viewport tree. HUD panels, modal dialogs, and persistent management UI remain outside it in normal `Control` or `CanvasLayer` UI hosts.

This boundary exists so world rendering cannot become the background of UI panels. If a scene cannot yet be fully wrapped, the migration must isolate one vertical slice at a time and add regression guards for the remaining direct-root world content.

### TopBarHost

Owns:

- global resource summary;
- world time / battle mode label;
- return/menu buttons;
- compact high-priority status.

Does not own:

- long action lists;
- deployment roster;
- settlement report body.
- full-width framed bars when the current scene is meant to keep the game view fullscreen. Strategic-world top UI is split into independently anchored overlay elements.

### LeftPrimaryPanelHost

Owns the main current-mode workspace.

Allowed content:

- strategic-location detail;
- selected army or battle-group detail;
- expedition draft;
- site management;
- battle preparation roster;
- battle preparation confirmation;
- report summary;
- low-frequency battle status.

Does not own:

- runtime command execution;
- settlement truth;
- long-term data mutation.

Strategic-world selection is an exception in the current fullscreen map presentation: selected strategic-location context is shown as a bottom-centered overlay sheet instead of a persistent left-side panel. Entered-city/site management is also map-first: `MainWorldViewport` keeps the site map fullscreen, a narrow left function tab rail provides management entry points, and only the currently opened function appears as an overlay panel. `LeftPrimaryPanelHost` must not reserve permanent horizontal map space for site management. Battle-preparation scenes may still use `LeftPrimaryPanelHost` when they are deliberate workspaces rather than map-overlaid context.

### RightNotificationHost

Owns compact, collapsible notices:

- event notices;
- warnings;
- task prompts;
- state-transition messages;
- diagnostics suitable for player-visible display.

It should not become a second action panel.

### MinimapHost

Owns minimap or navigation aid presentation.

V0 may leave this host empty. It should be reserved in the architecture to prevent the right side from filling with unrelated large panels.

### BottomCommandHost

Owns future high-frequency battle command controls.

V0 may remain hidden. When implemented, command buttons submit `CommandRequest`; they do not directly mutate Runtime or Domain.

### OverlayHost

Owns short-lived viewport overlays:

- drag preview;
- deployment highlight;
- path preview;
- selection box;
- hover tooltips;
- damage numbers;
- action cues.
- fullscreen strategic-world context sheets, such as the selected-city bottom popup, when the UI should preserve the full map view behind it.
- entered-city/site-management function tab rails and task-sized function panels when the site map should remain fullscreen behind them.

Overlay content must remain context-bound and must not become a permanent split-screen management reservation. Strategic-world selected-location overlays appear only while that context is active. Entered-city/site-management function panels appear only for the active function opened from the tab rail and close back to the tab rail.

### ModalHost

Owns blocking dialogs:

- confirmation;
- scene entry, battle entry, and action errors;
- battle gate dialogs;
- unrecoverable failure explanations.

## Hover Presentation Contract

Hover presentation is part of Presentation/UI. It is display-only and must not own gameplay state, strategic rules, battle validation, or command legality. Hover surfaces may consume definitions, view models, rule results, runtime snapshots, and debug data, but they must not mutate those sources or become a second authority for whether an action is legal.

Presentation/UI uses three hover paths:

1. `TooltipText` is allowed only for short, low-risk text that does not need custom layout, multiple fields, rich structure, stateful controls, or special positioning.
2. Complex hover detail uses an authored tooltip scene. The scene should be instantiated through `GameUiSceneFactory` or an equivalent resource-backed factory path, and local widgets should bind display data rather than building ad hoc tooltip control trees in code.
3. Map, battle, construction placement, deployment, debug, and other world-space hover overlays remain owned by their subsystem presenters. They still must share the common Presentation styling and expose clear naming, positioning, and data-source boundaries.

Complex tooltip scenes should use accepted theme variations such as `WorldContextCard` unless a focused proposal accepts a different style. They should keep the triggering control compact and move secondary facts into the hover surface only when those facts help the current context.

World-space hover overlays are not generic UI tooltips. Battle grid hover frames, unit health-bar hover visibility, construction footprint previews, deployment previews, and debug hover panels belong to their owning presentation subsystem. They may share layout helpers or style resources, but their interaction rules stay with the subsystem that owns the active viewport context.

## UI Mode Model

UI mode is presentation state, not gameplay authority.

Recommended modes:

| Mode | Primary Presentation Surface | Data Source | Allowed Writes |
|---|---|---|---|
| `StrategicSelection` | selected location/army/opportunity detail and actions | Strategic state + definitions + Application queries | Application requests only |
| `ExpeditionDraft` | source site, hero/battle-group selection, target selection actions | Strategic state + Application expedition availability | Application expedition request |
| `SiteManagement` | fullscreen site map, left function tab rail, and one active overlay function panel | Site state + definitions + Application view/query services | Application site action requests |
| `BattlePreparation` | player roster, hero/corps deployment, formation, destination-targeting overlays, lower-right start battle action | Application battle request/snapshot draft + deployment draft | Deployment draft confirmation only |
| `BattleRuntime` | selected battle-group status, destination beacons, and commands | Runtime event stream/snapshot + command availability hints | `CommandRequest` only |
| `SettlementReport` | report result and state-change explanation | Settlement/report output | return/acknowledge only |

## Battle Map Operation HUD Suppression

Map-targeting state is a hard HUD suppression state. When the next meaningful player input is a battlefield cell, actor, formation anchor, destination beacon target, skill target, or future map-targeted command point, Presentation must retract or hide all screen-space panels that could cover the battlefield and set those hidden controls to ignore mouse input. Pointer gates are not sufficient as the primary solution because they still leave covered map cells unavailable.

This applies during battle-preparation formation placement, preparation destination-beacon selection, live or tactical-pause runtime destination beacons, hero skill target selection, deployment movement, and any future map-targeted command mode. Tactical pause may display detail panels while the player is reading, but entering a map click operation from pause must suppress those panels until the operation submits, cancels, or returns to the previous UI layer.

Allowed visible feedback during HUD suppression is limited to map-owned or non-blocking context: formation previews, deployment-zone highlights, skill range and target cells, destination markers, selected-unit rings, local invalid-placement feedback, cursor-local prompts, preparation destination-targeting guide arrows, and small prompt docks whose controls ignore mouse input. Preparation destination-targeting may hide the system cursor while the guide arrow owns pointer feedback, but grid hover must remain visible for cell localization. Restoring from suppression must return to the previous battle-preparation or battle-runtime HUD state instead of rebuilding unrelated management UI.

## View Model And Binder Rules

As UI grows, root partials should stop constructing large panels directly from raw state. Use display-only view models and binders.

Recommended classes:

- `StrategicSelectionPanelViewModel`
- `ExpeditionDraftPanelViewModel`
- `SiteManagementPanelViewModel`
- `BattlePreparationPanelViewModel`
- `BattleRuntimeHudViewModel`
- `SettlementReportViewModel`

Rules:

- View models are derived data and are discardable.
- View models must contain IDs, labels, disabled reasons, and display summaries only.
- View models must not contain authoritative mutable state.
- Binders create/update Godot controls from view models.
- User interaction callbacks submit requests/commands to Application or Runtime boundaries.

## Dirty Refresh Rules

Short-term implementation may keep current `RefreshAll()` methods, but new work should move toward explicit refresh reasons:

- selection changed;
- mode changed;
- world tick advanced;
- site state changed;
- battle request changed;
- runtime event received;
- settlement report received.

High-frequency per-frame UI rebuilds are forbidden unless explicitly justified and logged.

## Migration Rules For Current Code

### Strategic World

`StrategicWorldHud.tscn` presents the large map as fullscreen content. `MainWorldViewportHost` fills the root screen; HUD controls overlay it and must not reserve screen space or change the map/camera layout.

Top strategic-world UI is not a single full-width bar. Resource/status, notice, world-clock, and speed/reset controls are independent overlay elements under `TopBarHost`.

`SiteDetailPanel` is a selected-context bottom sheet under `OverlayHost`, centered horizontally near the lower screen edge. It sizes to its current content within viewport-safe width and height limits; when content exceeds that safe height, the body/details area and the action card clip their variable content through internal scroll containers while the sheet frame and action region remain visible. It is hidden when no strategic location or opportunity context is selected. Its content lays out horizontally so it reads as a contextual sheet, not as a left-side menu.

Current bindings in `StrategicWorldRoot.UiBootstrap.cs` and `StrategicWorldRoot.DetailHud.cs` may remain initially if they only display and submit Application requests.

`StrategicWorldRoot.tscn` must host `WorldMapRoot`, `WorldCamera`, and map-space overlay controls under `MainWorldViewportHost/MainWorldViewport`. Site hit buttons and labels may remain `Control` nodes during migration, but they must attach to a viewport-local overlay instead of the root UI canvas.

### World Site

`WorldSitePeacetimeHud.tscn` should migrate site management from a permanent split-screen `SitePeacetimePanel` to a fullscreen-map management model. The site map stays in `MainWorldViewport` at full root size during peacetime management. A narrow vertical `SiteManagementTabRail` is anchored to the left edge outside the viewport, and opened function panels live under the approved overlay/modal hosts without changing the map camera or viewport layout.

`ApplySitePeacetimePanelLayout()` must stop hardcoding a permanent left panel width or right-side viewport reservation. Rename or wrap it when practical so the method name no longer implies peacetime-only ownership.

World-site map, units, battle overlays, debug world nodes, and battle camera belong in `MainWorldViewport`. Site management HUD, battle-preparation panels, selection vignette, modal dialogs, and future command UI belong outside the viewport. The current `WorldSiteRoot` scene may migrate after strategic world isolation if changing the root type would create a high-risk runtime rewrite.

The site-management default state shows only compact map overlays plus the left tab rail. The rail contains the major city functions, such as build, conscription, recruitment, and overview. It should use authored tab/button resources derived from `assets/textures/ui/tinyrpg_manasoulgui_v_1_0/20250420manaTabD-Sheet.png`. The atlas frame may be cut into normal, hover, pressed, and disabled regions or equivalent texture-backed theme resources. Do not stretch the full sheet as one raw panel image.

Tab hover animation is local Presentation feedback. It may shift the tab, swap frame state, tint, or play a short tween, but it must not change selected function state, command legality, or Strategic Management data.

Clicking a tab opens only that function's panel and hides the other tab entries until the panel closes. Closing the panel returns to the tab rail. Opening a different function should be an explicit close-then-open or direct switch inside the active panel surface; the player should not see the whole function menu competing with an open management panel.

Function panels are task-sized overlays, not fullscreen by default. Overview and conscription should prefer compact panels sized to their content. Build selection should use a left or near-left picker panel, then hide management panels while map placement owns the interaction. Recruitment may remain a large bounded workbench because hero selection and troop option cards need space, but it still should not define every site-management panel as fullscreen.

The first-version recruitment surface is also the hero main-corps reassignment workbench. It should present a hero selector and troop option cards as the primary flow. A separate corps tab should not remain in the site-management tab bar unless it owns a distinct accepted workflow beyond what the recruitment workbench already provides.

Troop option cards must show reserve-soldier and resource requirements directly on the card as compact attributes, preferably icon plus amount. When a selected hero already has a corps, the card should still present the selected corps requirements rather than printing old-corps refund and final net reserve/resource change. Strategic Management commands remain the authority for replacement validation and refund settlement; Presentation may consume settlement projections for diagnostics or future confirmation surfaces, but normal recruitment cards must not become accounting breakdowns.

The site-management build function separates building choice from map placement. The first step is an RTS-style building picker made from reusable authored card scenes. Each card displays the building icon and the bottom building name only. Hover detail may show only the footprint and resource cost. Construction region id, default coordinates, category, disabled reason, and other placement validation facts belong to the later map-placement interaction and must not be printed into the picker card.

After the player chooses a building card, Presentation enters a temporary strategic-building placement mode and retracts site-management panels and tab entries that could block the map. The selected building follows the mouse as a footprint preview in the world viewport. The preview resolves the current snapped grid anchor and marker-backed `ConstructionRegion` every mouse move, then asks Strategic Management rules for the current failure reason. Valid previews use a buildable treatment; invalid previews use an error treatment and keep the pending selection active. Presentation must not reject a placement because a building category differs from a region label.

The next valid map click submits `BuildCityBuilding` through Strategic Management commands. Presentation may show placement feedback, invalid reasons, region highlights, and footprint previews during this map-placement step, but it still does not own building legality or strategic state mutation. `ConstructionRegion` markers are the presentation bridge between the authored map and Strategic Management region ids; legacy `BuildingSlot`/`FacilitySlot` presentation must not become the new Strategic Management construction authority.

### Battle Preparation

Battle preparation must stop presenting roster units through `_siteGarrisonList` as a long-term design.

The target battle-preparation UI is a map-first battle-group deployment workflow:

```text
compact battle-group roster / placement source
-> optionally switch the current battle formation
-> click battle group to enter formation-follow placement
-> show full battle-group formation preview
-> validate placement through full formation footprint
-> click legal placement to commit formation placement
-> enter focused destination targeting with a curved guide arrow
-> left-click reachable destination cell to set initial beacon
-> lower-right start-battle confirmation when launch-ready
```

The battle-preparation roster is a narrow switcher and placement source, not a text-heavy panel. It displays battle-group portrait, battle-group name, and a compact deployment status marker such as deployed, needs destination, reserve, or invalid. It must not become the place where objective text, engagement-rule explanations, enemy summaries, or long action instructions accumulate.

Formation selection belongs to the current-battle deployment controls, not the roster row and not a large formation editor. The selected formation is the formation used by drag preview. If the player has not changed it in battle preparation, it is initialized from the battle group's strategic default formation. The player may change it before placement or after placement; after-placement changes must request a transactional recompute and keep the previous valid placement when the new formation does not fit.

Roster rows and battle-preparation HUD docks are authored Godot scene resources. `WorldSitePeacetimeHud.tscn` owns the dock layout through normal `Control` anchors and containers; reusable rows such as `BattlePreparationRosterRow.tscn` own their child structure. C# may bind data, connect signals, toggle visibility, and animate relative retreat offsets, but it must not rebuild this layout through ad hoc `new` Control trees or runtime anchor helpers.

Reusable row controls must tolerate binding before `_Ready()`. The row script stores the pending view-model fields, resolves child nodes in `_Ready()`, and reapplies the pending binding so freshly instantiated rows do not appear empty. Child controls inside a row, such as avatar, name, and status labels, should ignore mouse input so the authored row root receives the click/drag event.

Roster row input must make click-to-place the primary preparation interaction. Clicking a row selects that battle group and enters formation-follow placement mode when the group still needs placement or the player is replacing its placement. Dragging may remain as a compatibility shortcut, but it must enter the same placement-follow state and must not be required for normal deployment.

Selecting a battle group for placement or moving an already placed battle-group formation creates a viewport overlay preview for the whole battle-group formation. The preview follows the mouse and must render the hero and corps arrangement, not only a single icon. Valid placement renders normally. Invalid placement renders the whole preview in an error treatment and may show a short local reason such as outside deployment zone, blocked terrain, or overlap. Formation adaptation may adjust spacing or fallback shape, but the preview must never show overlapping members as legal. Placement commit validation is still Application/runtime-ready data validation; Presentation only visualizes the current result.

While in placement-follow mode, persistent HUD and management controls must leave the battlefield view and ignore mouse input. The top status bar, compact roster, deployment controls, lower-right start-battle button, and nonessential hints slide or hide offscreen and return after placement commit or cancellation. Deployment-zone highlights, formation preview, and legality feedback stay visible because they are the active placement context.

Objective selection and engagement-rule selection are not mandatory battle-preparation steps in the destination-beacon flow. Optional objective/tactical markers may still be displayed as map information, route hints, or future planning affordances, but Presentation must not block launch because no objective marker was selected.

After a battle group is placed, Presentation enters preparation destination-targeting immediately instead of relying on a text prompt. All larger battle-preparation HUD docks stay suppressed. A map-owned overlay draws a curved card-style guide from the placed battle group to the pointer, tapering from thin to wide to thin and ending in an arrowhead. The system cursor is hidden while this guide owns pointer feedback. Battle-grid hover remains visible so the player can still identify the destination cell. Left-clicking a reachable cell accepts the initial beacon; invalid cells show local feedback and keep targeting active. Cancel returns to the previous battle-preparation HUD state without committing a new destination.

Battle preparation does not need a persistent bottom plan bar. The only persistent launch control should be a lower-right start-battle button outside map-operation suppression states. It remains disabled with a player-readable reason until launch readiness is satisfied, including at least one deployed participating battle group and required initial destination beacon facts.

Migration path:

1. Keep existing behavior stable while the panel is moved left.
2. Replace text-heavy battle-preparation panels with a compact roster, battlefield overlays, current-battle-group deployment controls, a preparation destination-targeting guide, and a lower-right start-battle action.
3. Bind player roster, deployment status, formation, and start-battle actions to battle-preparation-specific containers.
4. Continue consuming the same battle request/snapshot and deployment draft source; do not create a separate UI unit pool.
5. Reserve `OverlayHost` and battle map overlays for deployment highlights, formation-follow previews, invalid-placement feedback, preparation destination-targeting guide arrows, destination beacons, optional route previews, and optional objective/tactical marker outlines.

### Battle Runtime

Battle runtime may hide the left primary panel in V0. Runtime selection and command UI belongs in `BottomCommandHost` and viewport overlays, then submits `CommandRequest`.

The first battle-runtime visual baseline is map-first: live battle defaults to the fullscreen map with only compact, low-profile status when useful; selected or paused detail UI may appear for reading and command selection, but it is not allowed to remain over the map while the player is choosing a cell or target. World-space unit HP/status presentation may be more visible than screen-space panels because it moves with the map object and does not reserve a screen corner.

Presentation owns the input gesture and feedback for destination beacons:

- select one or more player battle groups during preparation, live battle, or tactical pause;
- left-click a valid destination cell during preparation destination-targeting, or use the accepted live-battle command input gesture for runtime/tactical-pause beacon commands;
- show hover or click feedback for invalid cells without emitting battle events;
- display accepted or preparation-seeded destination beacons as world-space overlays tied to command facts;
- show rejection reasons emitted by Application or Runtime;
- never sample beacon flow fields, move actors visually along uncommitted routes, or create movement truth outside Runtime events.

Spacebar tactical pause may be used to change selection and submit destination beacon commands. Pause-time commands update command intent only; they do not advance movement, attack, cooldown, perception, or flow-field consumption until Runtime resumes.

## Failure Rules

- If a required UI host is missing, fail visibly with a low-noise diagnostic log. Do not silently create an untracked fallback panel.
- If a mode tries to bind to an unrelated container, fail the regression guard.
- If Application rejects a request, show UI feedback and do not emit battle runtime events.
- If Runtime rejects a command, display the runtime event/reason from the event stream.
- If a scene transition request is rejected or fails, display the router-provided failure reason and do not create a second local fallback transition.

## Acceptance

The UI architecture is acceptable when:

- workspace-oriented persistent operation panels use an approved workspace host; entered-city/site-management uses a fullscreen site map with a left function tab rail and task-sized overlay panels instead of a permanent split-screen reservation;
- fullscreen strategic-world map presentation uses overlay context sheets instead of left-panel map reservations;
- game/world rendering is isolated in a real `MainWorldViewport` instead of sharing the root UI canvas;
- right side is reserved for compact notifications and minimap/navigation aid;
- deployment UI is no longer semantically hosted in garrison/action management lists;
- UI modes are presentation-only and do not become gameplay authority;
- root scene code can identify which host owns each panel;
- root scene code routes player-facing scene transitions through the accepted scene transition router;
- new UI work has a clear data source and write boundary;
- hover presentation follows the simple `TooltipText`, authored complex tooltip scene, or subsystem-owned world-overlay path and remains display-only;
- regression tests prevent reintroducing right-side main-panel hardcoding;
- future command UI can submit `CommandRequest`, including destination beacon commands, without direct Runtime or Domain mutation.
