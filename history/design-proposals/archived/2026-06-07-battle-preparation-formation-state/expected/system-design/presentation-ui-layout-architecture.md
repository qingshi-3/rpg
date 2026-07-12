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
- Submit `CommandRequest` when light-RTS command UI is implemented.

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

Overlay content must be transient and must not become a permanent management panel.

### ModalHost

Owns blocking dialogs:

- confirmation;
- scene entry, battle entry, and action errors;
- battle gate dialogs;
- unrecoverable failure explanations.

## UI Mode Model

UI mode is presentation state, not gameplay authority.

Recommended modes:

| Mode | LeftPrimaryPanelHost Content | Data Source | Allowed Writes |
|---|---|---|---|
| `StrategicSelection` | selected location/army/opportunity detail and actions | Strategic state + definitions + Application queries | Application requests only |
| `ExpeditionDraft` | source site, hero/battle-group selection, target selection actions | Strategic state + Application expedition availability | Application expedition request |
| `SiteManagement` | city/strategic-location facilities, garrison, actions, feedback | Site state + definitions + Application view/query services | Application site action requests |
| `BattlePreparation` | player roster, hero/corps deployment, objective-zone selection, engagement-rule selection, enemy preview summary, start battle | Application battle request/snapshot draft + battle-group plan draft | Deployment and plan draft confirmation only |
| `BattleRuntime` | selected battle-group status and future commands | Runtime event stream/snapshot + command availability hints | `CommandRequest` only |
| `SettlementReport` | report result and state-change explanation | Settlement/report output | return/acknowledge only |

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

`StrategicWorldHud.tscn` may be migrated first by moving `SiteDetailPanel` into the left primary workspace while keeping node names stable.

Current bindings in `StrategicWorldRoot.UiBootstrap.cs` and `StrategicWorldRoot.DetailHud.cs` may remain initially if they only display and submit Application requests.

`StrategicWorldRoot.tscn` must host `WorldMapRoot`, `WorldCamera`, and map-space overlay controls under `MainWorldViewportHost/MainWorldViewport`. Site hit buttons and labels may remain `Control` nodes during migration, but they must attach to a viewport-local overlay instead of the root UI canvas.

### World Site

`WorldSitePeacetimeHud.tscn` may be migrated first by moving `SitePeacetimePanel` into the left primary workspace while keeping node names stable.

`ApplySitePeacetimePanelLayout()` must stop hardcoding right-side anchors. Rename or wrap it when practical so the method name no longer implies peacetime-only ownership.

World-site map, units, battle overlays, debug world nodes, and battle camera belong in `MainWorldViewport`. Site management HUD, battle-preparation panels, selection vignette, modal dialogs, and future command UI belong outside the viewport. The current `WorldSiteRoot` scene may migrate after strategic world isolation if changing the root type would create a high-risk runtime rewrite.

### Battle Preparation

Battle preparation must stop presenting roster units through `_siteGarrisonList` as a long-term design.

The target battle-preparation UI is a map-first company planning workflow:

```text
compact hero-company roster / drag source
-> optionally switch the current battle formation
-> drag hero portrait into battlefield
-> show full hero-led company formation preview
-> validate placement through full formation footprint
-> commit legal formation placement
-> tactical thumbnail objective selection for current company
-> compact current-company engagement-rule selection
-> plan confirmation
```

The battle-preparation roster is a narrow switcher and drag source, not a text-heavy panel. It displays company portrait, company name, and a compact status marker such as complete, partial, or missing. It must not become the place where objective text, engagement-rule explanations, enemy summaries, or long action instructions accumulate.

Formation selection belongs to the current-company plan controls, not the roster row and not a large formation editor. The selected formation is the formation used by drag preview. If the player has not changed it in battle preparation, it is initialized from the hero company's strategic default formation. The player may change it before placement or after placement; after-placement changes must request a transactional recompute and keep the previous valid placement when the new formation does not fit.

Roster rows and battle-preparation HUD docks are authored Godot scene resources. `WorldSitePeacetimeHud.tscn` owns the dock layout through normal `Control` anchors and containers; reusable rows such as `BattlePreparationRosterRow.tscn` own their child structure. C# may bind data, connect signals, toggle visibility, and animate relative retreat offsets, but it must not rebuild this layout through ad hoc `new` Control trees or runtime anchor helpers.

Reusable row controls must tolerate binding before `_Ready()`. The row script stores the pending view-model fields, resolves child nodes in `_Ready()`, and reapplies the pending binding so freshly instantiated rows do not appear empty. Child controls inside a row, such as avatar, name, and status labels, should ignore mouse input so the authored row root receives the click/drag event.

Roster row input must keep click selection and drag deployment separate. Mouse press only records the possible interaction. Selection fires on release if the drag threshold was not crossed. Drag starts from mouse motion after the threshold and must not trigger a selection refresh first, because roster refresh can destroy the drag source before the deployment preview starts.

Dragging a company portrait or already placed company formation creates a viewport overlay preview for the whole hero-led company formation. The preview must render the hero and corps arrangement, not only a single icon. Valid placement renders normally. Invalid placement renders the whole preview in an error treatment and may show a short local reason such as outside deployment zone, blocked terrain, or overlap. Formation adaptation may adjust spacing or fallback shape, but the preview must never show overlapping members as legal. Drop validation is still Application/runtime-ready data validation; Presentation only visualizes the current result.

While dragging, persistent HUD and management controls should move out of the battlefield view. The top status bar, compact roster, current-company plan controls, start-battle button, and nonessential hints may slide offscreen and return after pointer release. Deployment-zone highlights, formation preview, and legality feedback stay visible because they are the active drag context.

The tactical objective-selection step belongs to Presentation, but only as a view and input surface. It displays objective-zone markers, route previews, and company geography from Application/runtime-ready data. It submits objective and rule choices back to the battle-entry Application boundary. It does not create pathfinding truth, runtime targets, or a separate battle snapshot.

The target selector is a compact tactical thumbnail for the currently selected company, not a row of abstract buttons and not a large management panel. The player opens or focuses the thumbnail after placing the company, then clicks a marker-backed target region. The thumbnail is derived from the TileMapLayer-built grid data and may simplify art detail down to land/water color blocks, but objective regions must be actual semantic markers from the active map. V0 maps that have not authored dedicated `ObjectiveZone` markers may expose enemy-side deployment-zone markers as visible assault target regions; this is marker-backed and must not fabricate hidden target cells.

The horizontal battlefield identity must stay visible through the planning flow. A zoomed-out objective view may compress the map into a tactical overview, but it should still read as a side-scrolling battlefield with lanes, height changes, gates, bridges, and route bands rather than an unrelated top-down board.

Migration path:

1. Keep existing behavior stable while the panel is moved left.
2. Replace text-heavy battle-preparation panels with a compact roster, battlefield overlays, current-company controls, tactical thumbnail, and fixed start-battle action.
3. Bind player roster, deployment status, objective selection, engagement rules, and start-battle actions to battle-preparation-specific containers.
4. Continue consuming the same battle request/snapshot and battle-group plan draft source; do not create a separate UI unit pool.
5. Reserve `OverlayHost` for deployment highlights, drag formation previews, invalid-placement feedback, route previews, objective-zone outlines, and the tactical thumbnail/overview transition.

### Battle Runtime

Battle runtime may hide the left primary panel in V0. Future command UI belongs in `BottomCommandHost` and submits `CommandRequest`.

## Failure Rules

- If a required UI host is missing, fail visibly with a low-noise diagnostic log. Do not silently create an untracked fallback panel.
- If a mode tries to bind to an unrelated container, fail the regression guard.
- If Application rejects a request, show UI feedback and do not emit battle runtime events.
- If Runtime rejects a command, display the runtime event/reason from the event stream.
- If a scene transition request is rejected or fails, display the router-provided failure reason and do not create a second local fallback transition.

## Acceptance

The UI architecture is acceptable when:

- main persistent operation panels use a left primary workspace;
- game/world rendering is isolated in a real `MainWorldViewport` instead of sharing the root UI canvas;
- right side is reserved for compact notifications and minimap/navigation aid;
- deployment UI is no longer semantically hosted in garrison/action management lists;
- UI modes are presentation-only and do not become gameplay authority;
- root scene code can identify which host owns each panel;
- root scene code routes player-facing scene transitions through the accepted scene transition router;
- new UI work has a clear data source and write boundary;
- regression tests prevent reintroducing right-side main-panel hardcoding;
- future command UI can submit `CommandRequest` without direct Runtime or Domain mutation.
