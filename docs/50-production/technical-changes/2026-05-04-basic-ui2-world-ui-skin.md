# 2026-05-04 Basic UI 2 World UI Skin

## Background

The project now uses the in-project `assets/textures/ui/basic-ui/2/` asset set for strategic-world and site-operation UI polish. These assets are treated as a project UI skin, not as loose one-off image references in gameplay code.

## Changes

- Added `GameUiSkin` as the centralized world UI theme helper.
- `GameUiSkin` builds one Godot `Theme` and assigns controls through `ThemeTypeVariation`; gameplay UI should not set ad hoc stylebox overrides for this skin.
- Applied `basic-ui/2` top-bar art to the strategic world HUD and site peacetime HUD.
- Applied framed panel art to the strategic site detail panel and the site peacetime operation panel.
- Applied reusable button skin styles to top-bar commands, site actions, threat rows, expedition controls, facility rows, and site map operation markers.
- Text buttons use the whole empty button texture scaled as one image; do not nine-slice the button art.
- Applied direct texture buttons for world-clock controls: pause, continue, and quick speed use the matching `basic-ui/2/btn` button art.
- Applied the same skin layer to battle alert and pre-battle dialogs.

## Asset Contract

- Gameplay UI code should call `GameUiSkin` instead of loading `basic-ui/2` textures directly.
- Window-like frames must be `PanelContainer` controls using `StyleBoxTexture` through the shared Theme variation. The texture asset is assigned to the stylebox, and `TextureMargin*` defines the non-stretched outer frame.
- `basic-ui/2` is currently used for panels, buttons, and dialogs.
- Empty text buttons should use `btn/button_empty_4.png` or another complete empty-button asset directly, not sliced frame composition.
- World-clock icon controls should use direct `TextureButton` assets from `basic-ui/2/btn`, not text labels on generic buttons.
- Strategic map site icon indicators remain separate; do not use panel assets as map markers.
- New UI asset choices should be added to `GameUiSkin` first, then consumed by screens.

## Verification

- Build with `dotnet build rpg.sln`.
- Open `StrategicWorldRoot` and confirm the top HUD, right detail panel, action buttons, threat buttons, and expedition controls use the new skin.
- Enter a player-held `WorldSite` and confirm the peacetime top HUD, operation panel, facility/action rows, and map operation markers use the new skin.
- Trigger a battle announcement and confirm both dialogs still show the expected text and buttons.
