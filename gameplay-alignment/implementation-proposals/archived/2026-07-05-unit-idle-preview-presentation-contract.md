# Battle Unit Animated Preview Presentation Contract Implementation Proposal

Status: Archived By User Request - Implemented; Manual QA Not Retained As Active Work

## Origin

- Requirement: UI-UNIT-PREVIEW-001
- Design Proposal: None; this slice implements existing Presentation/UI and Strategic Management authority.
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Blocking Issues: Full `WorldSiteDeploymentCacheRegression` acceptance is expected to remain blocked by the unrelated existing TileSets resource taxonomy guard until that migration batch is resolved.

## Requirement

Create a reusable Presentation/UI battle-unit preview path that can play a battle unit animation in authored UI and world-overlay scenes. Idle remains the default animation, and static first-frame texture extraction remains only as a compatibility adapter for surfaces that have not yet migrated to the reusable animated preview scene. Military UI must stop treating whole battle spritesheet PNG paths as reusable icons.

## Scope

- Add a shared Presentation resolver that maps a battle unit definition id to a `BattleUnitAnimatedPreviewModel` containing `BattleUnitDefinition.Visual.SpriteFrames`, animation name, and visual layout metadata.
- Keep a static texture adapter that maps the same model to `GetFrameTexture(animation, 0)` only for legacy surfaces that still require `Texture2D`.
- Author the reusable animated preview scene under `scenes/ui/common/` so it is not owned by world-site, beacon, recruitment, expedition, or battle-gate scenes.
- Author a second reusable `BattleUnitPlinthPreview` scene under `scenes/ui/common/` that composes the recruitment plinth with the animated hero display. Recruitment UI and battle beacons should reuse this component instead of each parent scene hand-aligning a standalone plinth and animation node. Parent scenes may place the component, scale it as one unit, and swap plinth texture style; they must not override hero offset, plinth size, hero max size, or preview layout mode.
- Resolve the idle animation name from the unit visual animation set, falling back to `idle` only when the visual animation set does not override it.
- Cache resolved animated preview models by battle unit id and animation name, and cache static preview textures separately for legacy consumers.
- Keep building and resource icons on their existing `IconPath` / atlas-resource path.
- Carry battle unit ids through Strategic Management military view models used by the current city military UI.
- Replace military workbench hero cards, selected hero portrait, and muster cards so they bind the reusable animated preview scene from battle unit ids rather than displaying idle frame textures.
- Keep the battle destination beacon pointer and target-cell frame separate from the reusable plinth preview. The beacon scene owns destination semantics; the common plinth preview owns only the station-plus-hero display.
- Existing corps rows, expedition rows, battle-gate cards, and battle-preparation rows may continue using the static adapter until their own UI surfaces are migrated.
- Extend the same preview contract to strategic-world expedition draft rows, the battle trigger brief/detail modal, and battle-preparation roster rows.
- Add regression guards so future military UI cannot reintroduce raw spritesheet `IconPath` loading for hero or corps previews.

## Non-Goals

- Do not change combat rules, battle unit definitions, strategic command validation, expedition legality, or battle launch logic.
- Do not generate new portrait image files in this slice.
- Do not crop or hardcode source PNG rectangles in UI code.
- Do not make Strategic Management depend on battle Runtime or Presentation internals.
- Do not redesign battle-runtime command HUD or battle-report surfaces in this slice; those surfaces may adopt the shared resolver after the expedition, battle-gate, and deployment entry points are stable.

## Touched Systems

- Strategic Management definitions and dashboard view models for military display ids.
- Presentation/Common shared unit preview resolver and animated preview scene script.
- Presentation/World/Sites military workbench cards, muster cards, corps rows, and binders.
- Strategic-world expedition draft row scene and binder.
- Strategic battle-gate modal force preview cards.
- Battle-preparation roster row binding.
- Regression tests under `tests/StrategicManagementRegression` and `tests/WorldSiteDeploymentCacheRegression`.

## GodotPrompter Skills

- `godot-ui`
- `responsive-ui`
- `scene-organization`
- `csharp-godot`
- `godot-testing`

## Tests

- Update Strategic Management regression coverage so military view models expose battle unit ids for hero, muster-template, and corps-row previews.
- Update Presentation anti-rot coverage so hero/corps/muster controls bind `Texture2D` or battle unit ids and do not call `GD.Load<Texture2D>(_iconPath)` for military unit previews.
- Add coverage that the shared resolver loads `BattleUnitDefinition`, reads `Visual.SpriteFrames`, uses `AnimationSet.IdleAnimation`, exposes animated preview models, and does not crop raw PNGs.
- Add coverage that the military workbench uses `BattleUnitAnimatedPreview.tscn` from `scenes/ui/common/` instead of `TextureRect`-only idle-frame slots.
- Add coverage that expedition, battle-gate, and battle-preparation surfaces route through the shared resolver and do not load raw spritesheet PNGs directly.
- Run `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`; accept only the known unrelated TileSets taxonomy failure.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.
- Run `git diff --check`.

## Diagnostics

The preview resolver should log missing unit definitions, missing visual resources, missing SpriteFrames, missing idle animations, and empty idle animation frame lists once per unit/animation key. Missing previews remain visible as empty UI slots rather than falling back to raw spritesheet PNGs. Animated preview controls hide their `AnimatedSprite2D` when no model is available.

## Manual QA

- Open city management military UI.
- Confirm hero cards, selected hero portrait, and muster cards play the battle unit idle animation rather than showing a static first frame or a full spritesheet.
- Confirm hero cards, selected hero portrait, muster cards, and destination beacons use the same plinth-backed hero display alignment, with the left hero-list card hero no longer floating above the plinth center.
- Confirm existing corps rows still show the static first-frame adapter until their own migration slice.
- Confirm unavailable muster cards remain hoverable and still show cost/reason tooltip detail.
- Confirm building selection cards still use their building icons and are not routed through the battle unit preview resolver.
- Form an expedition from the strategic world and confirm each selectable company row shows hero and corps idle-frame previews.
- Trigger a battle and confirm both the brief and detail battle-gate modal states show force preview cards.
- Enter battle preparation and confirm roster rows show hero idle-frame previews instead of whole spritesheets or color blocks.

## Acceptance

- A shared Presentation resolver owns battle-unit animated preview model resolution and legacy first-frame texture extraction.
- The reusable animated preview scene is named `BattleUnitAnimatedPreview.tscn` and lives under `scenes/ui/common/`.
- The reusable plinth-backed hero display scene is named `BattleUnitPlinthPreview.tscn` and lives under `scenes/ui/common/`.
- `BattleUnitPlinthPreview` owns the fixed hero-over-plinth alignment. Recruitment cards, selected hero details, and destination beacons may only position/scale the whole component and set the plinth texture.
- Battle destination beacons compose `BattleUnitPlinthPreview.tscn` with their arrow and target-cell frame instead of owning a bespoke plinth and hero animation subtree.
- Military Strategic Management view models expose stable battle unit ids for previews.
- Current military workbench surfaces use the reusable animated preview scene instead of raw PNG `IconPath` loads or `TextureRect`-only idle-frame slots.
- Strategic-world expedition rows, battle-gate modals, and battle-preparation roster rows use idle-frame preview textures instead of raw PNG loads.
- Regression tests prevent reintroducing direct raw spritesheet icon loading for hero and corps UI.
- Existing command, recruitment, replenishment, expedition, and battle-entry behavior remains unchanged.

## Verification Evidence

- RED `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: failed at `strategic management dashboard carries military preview unit ids`, confirming the old military `IconPath` contract was still active.
- RED `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: failed at `world site recruitment uses hero first military workbench`, confirming the shared preview resolver was missing. The existing unrelated TileSets resource taxonomy guard also failed.
- GREEN `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed.
- GREEN `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: the military workbench preview guard passed; command still exits non-zero only because the known unrelated TileSets resource taxonomy guard remains failing.
- GREEN `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- GREEN `git diff --check`: passed; Git emitted existing resource line-ending warnings only.
- GREEN `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`: passed after expanding expedition, battle-gate, and deployment preview surfaces; command emitted the existing source-generator warning about missing `GodotProjectDir` in the test project.
- GREEN-with-known-blocker `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: `unit idle previews reach expedition battle gate and deployment surfaces` passed; command still exits non-zero only because the unrelated TileSets resource taxonomy guard remains failing.
- GREEN `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors after the expanded preview surface wiring.
- GREEN `git diff --check`: passed; Git emitted existing resource line-ending warnings only.
- RED `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: failed at the new animated-preview guards because `BattleUnitAnimatedPreview.tscn`, `BattleUnitPreviewResolver.cs`, and military-workbench animated preview slots did not exist yet. The unrelated TileSets resource taxonomy guard also failed.
- GREEN-with-known-blocker `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: `battle destination beacon uses reusable scene with hero texture variable`, `world site recruitment uses hero first military workbench`, and `unit idle previews reach expedition battle gate and deployment surfaces` passed after renaming the reusable scene to `scenes/ui/common/BattleUnitAnimatedPreview.tscn`, renaming the resolver to `BattleUnitPreviewResolver`, and migrating the military workbench hero/muster/selected-hero slots to animated previews. The command still exits non-zero only because the unrelated TileSets resource taxonomy guard remains failing.
- RED `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: failed at the military workbench animated-preview slot guard after detecting that card previews used world-overlay visual-bounds layout instead of the previous TextureRect slot size and center.
- GREEN-with-known-blocker `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: `world site recruitment uses hero first military workbench` passed after adding `BattleUnitAnimatedPreviewLayoutMode.FrameRect` and restoring the old recruitment card slot centers and sizes while keeping idle animation playback. The command still exits non-zero only because the unrelated TileSets resource taxonomy guard remains failing.
- RED `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: failed at the new plinth-preview boundaries because `BattleUnitPlinthPreview.tscn` did not exist, beacon selection outline paths still targeted the old direct plinth/hero subtree, and recruitment cards still owned separate plinth plus animated-preview nodes. The unrelated TileSets resource taxonomy guard also failed.
- GREEN-with-known-blocker `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: destination beacon marker and military workbench plinth-preview guards passed after adding `BattleUnitPlinthPreview`, migrating hero card, selected hero, muster card, and beacon scenes to compose it, and leaving the beacon arrow outside the outline shader path. The command still exits non-zero only because the unrelated TileSets resource taxonomy guard remains failing.
- RED `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: failed after tightening the plinth-preview contract because parent scenes still overrode `PlinthSize`, `HeroOffset`, `HeroMaxSize`, and `HeroPreviewLayoutMode`, which allowed the same component to render with inconsistent internal alignment. The unrelated TileSets resource taxonomy guard also failed.
- GREEN-with-known-blocker `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`: destination beacon marker and military workbench plinth-preview guards passed after moving plinth size, hero offset, hero max size, and preview layout mode into fixed `BattleUnitPlinthPreview` constants and replacing per-instance alignment overrides with whole-component scale/position. The command still exits non-zero only because the unrelated TileSets resource taxonomy guard remains failing.
