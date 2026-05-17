# Current Presentation UI Layout Implementation

## Gameplay Authority

The current gameplay direction is hero-led light RTS with strategic-location and content-system management. UI is not gameplay authority.

## Current Entry Points

### Strategic World

`StrategicWorldRoot` is a `Control` scene root.

Current UI construction:

```text
StrategicWorldRoot
-> BuildUi()
-> Instantiate scenes/world/ui/StrategicWorldHud.tscn
-> AddChild(hud)
-> BindStrategicHud(hud)
-> BuildMapArea()
-> BuildSiteHoverSummaryPanel()
```

Important files:

- `src/Presentation/World/StrategicWorldRoot.UiBootstrap.cs`
- `src/Presentation/World/StrategicWorldRoot.DetailHud.cs`
- `src/Presentation/World/StrategicWorldRoot.ExpeditionHud.cs`
- `scenes/world/ui/StrategicWorldHud.tscn`

Current layout:

- `TopResourceBar` is a top full-width bar.
- `SiteDetailPanel` is a right-side full-height detail/action panel.
- Site hit buttons and labels are dynamically added directly to `StrategicWorldRoot`.
- Hover summary is a floating panel directly added to `StrategicWorldRoot`.

Current data binding:

- UI reads `StrategicWorldRuntime.State` and definitions directly in root partials.
- UI builds labels and buttons directly.
- User actions call presentation methods that then call application/world services.

### World Site

`WorldSiteRoot` is a `Node2D` scene root with a `CanvasLayer`.

Current UI construction:

```text
WorldSiteRoot
-> BuildSiteHud()
-> Instantiate scenes/world/ui/WorldSitePeacetimeHud.tscn
-> CanvasLayer.AddChild(_siteHudRoot)
-> bind top bar and right panel controls
```

Important files:

- `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteExplorationPresentation.cs`
- `scenes/world/ui/WorldSitePeacetimeHud.tscn`
- `scenes/world/sites/SiteExplorationHud.tscn`

Current layout:

- `SiteTopBar` is a top full-width bar.
- `SitePeacetimePanel` is a right-side full-height panel.
- `ApplySitePeacetimePanelLayout()` hardcodes right-side anchoring and offsets.
- Exploration has an additional HUD scene.
- Battle preparation reuses `SitePeacetimePanel`.

Current battle-preparation binding:

- `RefreshBattlePreparationForceList()` writes player deployment roster entries into `_siteGarrisonList`.
- `RefreshBattlePreparationActions()` writes the start-battle button into `_siteActionList`.
- The deployment panel is therefore semantically a site-management panel repurposed for battle preparation.

## Current Problems

### No Unified Host Contract

Top bars, detail panels, action lists, hover panels, debug panels, and battle-preparation panels are created independently by scene roots. There is no shared host contract such as `TopBarHost`, `LeftPrimaryPanelHost`, `NotificationHost`, `MinimapHost`, `OverlayHost`, or `ModalHost`.

### Right-Side Main Panels Cover Game Content

The main strategic-world and site panels occupy the right side of the viewport. This conflicts with common strategy/RTS layout expectations where the left side often hosts main information/actions while the right side is reserved for minimap and compact notifications.

### Mode Content Is Mixed

`SitePeacetimePanel` currently contains site management, deployment, defense, threats, actions, and recent feedback. This mixes mode-specific presentation content and makes mode transitions fragile.

### UI Binding Is Too Close To Scene Roots

Root partials directly build labels, button rows, action rows, and strings from world state. This is usable for the prototype but should be contained by view-model/binder boundaries as UI grows.

### UI Can Accidentally Become A Second Authority

The current battle-preparation panel modifies `BattleStartRequest.PreferredPlacements` directly as a migration path. This must not expand into UI-owned unit pools, UI-owned battle groups, UI-owned settlement, or duplicated long-term state.

## Current Safe Reuse

These pieces can be reused during migration:

- Existing Godot UI scenes as resource-backed UI shells.
- Existing button/row reusable scenes from `GameUiSceneFactory`.
- Existing label and list binding functions where they only display state.
- Existing Application services for actions, expedition, site entry, battle request building, and result application.

## Current Unsafe Growth Direction

Future work should avoid:

- Adding more permanent floating panels directly to scene roots.
- Reusing garrison or action lists for unrelated UI modes.
- Adding business-state mutation to UI controls.
- Letting UI construct separate unit pools, battle outcomes, rewards, or settlement results.
- Hiding broken lifecycle transitions with additional fallback visibility toggles.
