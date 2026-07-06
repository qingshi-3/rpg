# Unit Idle Preview Presentation Contract Implementation Proposal

Status: Implemented - Pending Manual QA

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

Create a reusable Presentation/UI unit preview path that displays the first frame of a battle unit's idle animation for hero, corps, expedition, battle-gate, and deployment surfaces. Military UI must stop treating whole battle spritesheet PNG paths as reusable icons.

## Scope

- Add a shared Presentation resolver that maps a battle unit definition id to `BattleUnitDefinition.Visual.SpriteFrames.GetFrameTexture(idleAnimation, 0)`.
- Resolve the idle animation name from the unit visual animation set, falling back to `idle` only when the visual animation set does not override it.
- Cache resolved preview textures by battle unit id and animation name.
- Keep building and resource icons on their existing `IconPath` / atlas-resource path.
- Carry battle unit ids through Strategic Management military view models used by the current city military UI.
- Replace military workbench hero cards, selected hero portrait, muster cards, and existing corps rows so they bind preview textures from battle unit ids rather than loading raw PNG icon paths.
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
- Presentation/Common shared unit preview texture resolver.
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
- Add coverage that the shared resolver loads `BattleUnitDefinition`, reads `Visual.SpriteFrames`, uses `AnimationSet.IdleAnimation`, calls `GetFrameTexture(..., 0)`, and does not crop raw PNGs.
- Add coverage that expedition, battle-gate, and battle-preparation surfaces route through the shared resolver and do not load raw spritesheet PNGs directly.
- Run `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`; accept only the known unrelated TileSets taxonomy failure.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.
- Run `git diff --check`.

## Diagnostics

The preview resolver should log missing unit definitions, missing visual resources, missing SpriteFrames, missing idle animations, and empty idle animation frame lists once per unit/animation key. Missing previews remain visible as empty UI slots rather than falling back to raw spritesheet PNGs.

## Manual QA

- Open city management military UI.
- Confirm hero cards, selected hero portrait, muster cards, and existing corps rows show a single unit frame rather than a full spritesheet.
- Confirm unavailable muster cards remain hoverable and still show cost/reason tooltip detail.
- Confirm building selection cards still use their building icons and are not routed through the battle unit preview resolver.
- Form an expedition from the strategic world and confirm each selectable company row shows hero and corps idle-frame previews.
- Trigger a battle and confirm both the brief and detail battle-gate modal states show force preview cards.
- Enter battle preparation and confirm roster rows show hero idle-frame previews instead of whole spritesheets or color blocks.

## Acceptance

- A shared Presentation resolver owns battle-unit idle-frame preview extraction.
- Military Strategic Management view models expose stable battle unit ids for previews.
- Current military workbench surfaces use idle-frame preview textures instead of raw PNG `IconPath` loads.
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
