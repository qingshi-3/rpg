# Battle Map Operation HUD Suppression Implementation Proposal

Status: Implementation In Progress; Bottom Detail Runtime HUD Correction

## Origin

- Requirement: UI-BATTLE-HUD-001
- Design Proposal: `design-proposals/archived/2026-07-08-battle-map-operation-hud-suppression/`
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends:
  - `gameplay-alignment/implementation-proposals/2026-07-05-battle-runtime-readability-hud.md`
- Amended By: None
- Blocking Issues: Known unrelated `WorldSiteDeploymentCacheRegression` resource taxonomy guard may still fail on `TileSets expected=0 actual=2`.

## Requirement

Implement the accepted battle HUD interaction rule that screen-space HUD must not block map operations. During formation placement, destination-beacon selection, skill target selection, and future map-targeted command states, blocking HUD panels suppress/retract and ignore mouse input; when the operation submits or cancels, the previous battle HUD layer returns.

## Scope

- Add a unified battle map-operation HUD suppression state in Presentation.
- Suppress runtime summary, runtime command bar, and bottom command host while runtime map targeting is active.
- Suppress battle-preparation roster, plan bar, minimap/thumbnail docks, and blocking prompts while placement or destination targeting is active.
- Keep map-space overlays, destination markers, formation previews, target rings, deployment zones, and mouse-ignored prompt feedback visible.
- Add the reference-driven battle-runtime visual reset needed by this flow:
  - live/default battle keeps the fullscreen map primary and does not show a large bottom summary or command panel;
  - tactical pause uses one bottom detail panel for reading hero, troop, skill, and command-queue facts;
  - skill controls live in the bottom tactical-pause detail area, not in a separate round command wheel;
  - any map-targeting operation hides the blocking bottom detail panel until submit, cancel, or return.
- Replace the previous ManaSoul management-style runtime panel baseline with a combat HUD skin closer to the approved reference: dark translucent panel surfaces, narrow gold separators, compact portrait/unit preview, readable HP/MP bars, icon-first skill buttons, and no persistent large frame during live map play.
- Reuse existing project battle FX as skill-slot icon resources where possible. First-slice hero skills carry `IconPath` through the definition snapshot into the HUD; skill slots fall back to text glyphs only when a skill has no icon resource.
- Reuse the existing `BattleUnitPlinthPreview` unit-preview component in the runtime hero detail frame so selected heroes show their battle unit visual when a `HeroBattleUnitId` resolves; keep the text portrait glyph only as fallback.
- Repair the screenshot QA mismatch: live runtime summary is no longer a default bottom strip, tactical-pause detail is a single authored bottom surface, hero switching uses frameless plinth/unit idle previews with secondary name labels, the redundant selected-hero preview is removed, skill slots use the original frame until hover, and the round command wheel/regroup button are removed.
- Preserve current command submission, validation, Runtime authority, and battle-preparation plan authority.

## Non-Goals

- Do not implement the final command taxonomy or reintroduce a full radial-command system in this slice.
- Do not finalize the global UI theme-routing taxonomy.
- Do not redesign strategic-world, site-management, recruitment, or settlement UI.
- Do not change battle AI, damage, movement, cooldown, result settlement, or persistence.
- Do not remove valid world-space overlays just because screen-space HUD is suppressed.

## Touched Systems

- `system-design/presentation-ui-layout-architecture.md`
- `design-proposals/archived/2026-07-08-battle-map-operation-hud-suppression/`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
  - `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`
  - `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeDestinationBeacon.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparationDrag.cs`
  - `resource/ui/icons/battle/skills/`
  - `resource/battle/skills/`
  - `scenes/world/ui/WorldSitePeacetimeHud.tscn`
  - `scenes/world/ui/BattleRuntimeSkillSlot.tscn`
  - `scenes/world/ui/BattleRuntimeHeroSwitchButton.tscn`
  - `scenes/world/ui/BattleRuntimeHeroTroopSummaryRow.tscn`
  - `tests/WorldSiteDeploymentCacheRegression/`

## GodotPrompter Skills

- `using-godot-prompter`
- `godot-ui`
- `hud-system`
- `responsive-ui`
- `assets-pipeline`
- `input-handling`
- `csharp-godot`
- `godot-testing`

## Tests

  - Add Presentation anti-rot coverage that:
  - a named battle map-operation HUD suppression state/helper exists;
  - runtime skill target picking enters suppression and cancel/submit restores it;
  - preparation formation placement enters suppression and clear/commit restores it;
  - runtime destination-beacon map targeting is an explicit operation rather than only a pointer gate;
  - suppression hides or mouse-ignores runtime and preparation screen-space HUD controls.
  - battle-runtime skill slots use real `TextureRect` icons from resource-backed `IconPath` before falling back to text glyphs.
  - battle-runtime hero detail reuses the existing unit-preview component before falling back to a text portrait glyph.
  - live/default battle runtime does not show a large bottom summary or command panel.
  - tactical pause hides the live summary surface before showing the bottom combat-detail panel.
  - the round command wheel and overlay radial menu are absent from the battle-runtime HUD.
  - battle-runtime skill controls are bound through the bottom tactical-pause detail panel.
  - battle-runtime hero switch controls reuse frameless plinth/unit idle previews without an extra selected-preview or outline layer.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`; accept only documented unrelated taxonomy guard failures if still present.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.
- Run `git diff --check`.

## Diagnostics

- Add low-noise logs when the battle map-operation HUD suppression state enters, exits, or restores a prior layer.
- Do not add per-frame logs for mouse movement, hover preview, or continuous target highlighting.

## Manual QA

- In battle preparation, click a battle group to place formation and confirm roster/plan/minimap HUD leaves the map while the preview follows the mouse.
- Commit placement and confirm the previous preparation HUD returns with a non-blocking destination prompt.
- Right-click a destination and confirm no screen-space HUD blocks the clicked cell, then confirm HUD returns after the destination is accepted.
- In runtime, select a group, open tactical pause, press a hero skill, and confirm command/detail HUD disappears while choosing the skill target.
- Cancel skill targeting and confirm tactical-pause HUD returns.
- Submit a skill target and confirm tactical-pause state remains as before, but blocking target-picking HUD does not stay over the map.
- During live or paused destination-beacon selection, confirm the map is unobstructed and command UI returns after submit/cancel.
- During live runtime with no active selection change, confirm no large bottom summary or command panel is visible.
- Open tactical pause and confirm skills live in the bottom detail panel rather than a round command wheel, and no regroup button is shown.
- Switch heroes and confirm each switch button shows only a frameless plinth/unit idle preview with the hero name below.
- Open tactical pause and confirm the deep combat-detail panel reads as a combat surface, not a site-management or recruitment panel.

## Acceptance

- Accepted authority documents describe the HUD suppression contract.
- The implementation proposal links the archived design proposal and authority documents.
- Map-targeting interactions no longer rely on visible HUD pointer gates as the main way to avoid map input conflicts.
- Screen-space HUD does not cover map cells while the player is choosing a map target.
- Default live battle does not keep a persistent large bottom panel over the map.
- Tactical-pause command UI keeps hero facts, skills, and command queue in one bottom detail area, with no regroup button.
- Runtime hero switching uses frameless plinth/unit idle previews with secondary name labels and no redundant selected-hero preview.
- Returning/canceling restores the previous battle HUD layer without reopening unrelated management UI.
- Focused regression guards and build verification are recorded below.

## Verification Evidence

- 2026-07-08 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Result: Failed only on known unrelated resource taxonomy guard: `TileSets expected=0 actual=2`.
  - Relevant battle HUD suppression guards passed, including:
    - `battle runtime target picking suppresses command hud`
    - `battle map operations suppress blocking screen hud`
    - `battle runtime right click submits destination beacon command`
    - `battle preparation hud retreats during company drag`
    - `battle preparation right click stores initial destination beacon`
    - `world site root partial set stays below anti-rot line budget`
  - Added RED/GREEN evidence for `ui_cancel` restoration: the first run failed on missing `TryHandleBattleMapOperationHudSuppressionCancelInput`, and the later run passed the same guard after adding the cancel coordinator.
- 2026-07-08 `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Result: Passed, 0 warnings, 0 errors.
- 2026-07-08 `git diff --check`
  - Result: Passed; Git reported a line-ending warning for an existing touched test file but no whitespace errors.
- 2026-07-08 `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Result: Passed, 0 warnings, 0 errors after adding battle-runtime skill `IconPath` propagation and resource-backed skill icons.
- 2026-07-08 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Result: Failed only on known unrelated resource taxonomy guard: `TileSets expected=0 actual=2`.
  - Relevant visual/data-flow guards passed, including `battle runtime hud uses manasoul radial command presentation` and `battle skill definitions live in content layer and map to snapshots`.
- 2026-07-08 RED/GREEN for runtime hero visual reuse:
  - RED: `battle runtime hud uses manasoul radial command presentation` failed until the authored HUD scene and presenter exposed `BattleRuntimeHeroPlinthPreview`.
  - GREEN: same guard passed after the runtime hero frame reused `BattleUnitPlinthPreview` via `BattleUnitPreviewResolver.ResolveAnimatedPreview(selected.HeroBattleUnitId)`, with `BattleRuntimeHeroAvatarLabel` kept as fallback.
- 2026-07-08 `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Result: Passed, 0 warnings, 0 errors after wiring runtime hero detail to the reusable unit-preview component.
- 2026-07-08 `git diff --check`
  - Result: Passed; Git reported line-ending warnings for touched files but no whitespace errors.
- 2026-07-08 Godot CLI lookup (`where.exe godot`, `where.exe godot4`, `where.exe Godot_v4.5-stable_mono_win64.exe`, plus `D:\godot` scan)
  - Result: Not found, so scene-load and in-game visual/manual QA remain pending.
- 2026-07-08 RED/GREEN for screenshot-driven runtime HUD layout repair:
  - RED: `battle runtime hud uses manasoul radial command presentation` failed while the pause HUD kept independently anchored hero/detail/radial slabs.
  - RED: `battle runtime hud hides hero controls when pause ends` failed while tactical pause still showed the live runtime summary strip behind the command layer.
  - GREEN: both guards passed after tactical pause hid `BattleRuntimeSummaryBar` and `WorldSitePeacetimeHud.tscn` authored `BattleRuntimeCommandBar/CommandMargin` as one bottom-centered `HBoxContainer` command dock.
  - Visual repair also compacted runtime skill slots, hero switch buttons, and live summary rows; summary rows now ignore mouse because they are status-only.
- 2026-07-08 `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Result: Failed only on known unrelated resource taxonomy guard: `TileSets expected=0 actual=2`.
  - Relevant battle HUD guards passed, including:
    - `battle runtime hud uses manasoul radial command presentation`
    - `battle runtime hud hides hero controls when pause ends`
    - `battle runtime target picking suppresses command hud`
    - `battle map operations suppress blocking screen hud`
    - `battle runtime viewport stays fullscreen during hud and pause`
- 2026-07-08 `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Result: Passed, 0 warnings, 0 errors.
- 2026-07-08 `git diff --check`
  - Result: Passed; Git reported line-ending warnings for existing touched files but no whitespace errors.
- 2026-07-08 Godot CLI lookup (`where.exe godot`, `where.exe godot4`, `where.exe Godot_v4.5-stable_mono_win64.exe`)
  - Result: Not found, so updated in-game screenshot/manual visual QA remains pending.
- 2026-07-08 refactor verification after reference-driven HUD repair:
  - Extracted battle-runtime skill targeting rules and selected-group resolution into focused helpers so `WorldSiteRoot*.cs` stays under the anti-rot budget.
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
    - Result: Failed only on known unrelated resource taxonomy guard: `TileSets expected=0 actual=2`.
    - Relevant guards passed, including `battle runtime hud uses reference driven map first command flow`, `battle map operations suppress blocking screen hud`, `battle runtime hud hides hero controls when pause ends`, `battle runtime viewport stays fullscreen during hud and pause`, and `world site root partial set stays below anti-rot line budget`.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
- 2026-07-08 reference-driven tactical detail and unit nameplate repair:
  - Tactical pause detail now uses the selected parchment-scroll battle UI kit through `battle_runtime_pause_scroll_panel.tres`, `battle_runtime_pause_scroll_inner_panel.tres`, and `travel_book_lite_theme.tres`; the previous dark pause panel resources are no longer referenced by `WorldSitePeacetimeHud.tscn`.
  - Unit head HP UI now uses a reusable authored nameplate kit in `BattleUnitBase.tscn`: unit name strip, shared red HP fill, HP value text, and faction-tinted frame/title accents bound by `BattleUnitHealthBarComponent`.
  - RED evidence:
    - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
      - Result: Failed on `battle unit base authors health bar and SpriteFrames animation backend` before the nameplate scene/component repair; existing unrelated failures also remained for the missing preview workbench and selected-hero spotlight.
    - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
      - Result: Failed on `battle runtime hud uses reference driven map first command flow` before the parchment-scroll pause detail repair; the known unrelated `TileSets expected=0 actual=2` taxonomy guard also remained.
  - GREEN evidence:
    - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
      - Result: The new unit nameplate guard passed. The run still failed only on pre-existing unrelated guards: missing `BattleUnitPreviewWorkbench.tscn` and selected-hero spotlight filtering.
    - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
      - Result: The parchment-scroll tactical detail guard passed. The run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
    - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
      - Result: Passed, 0 warnings, 0 errors.
    - `git diff --check`
      - Result: Passed with CRLF normalization warnings only; no whitespace errors.
- 2026-07-08 tactical detail frame visual correction after screenshot feedback:
  - Root cause: the previous parchment pass still read as a flat rectangular fill because it used a broad travel-book page texture as the panel background instead of a framed popup slice.
  - Corrected the battle-runtime tactical detail frame to use a project-owned copy of `Kb_base.png` from the external asset library at `assets/textures/ui/battle-runtime/battle_runtime_keyboard_panel_sheet.png`.
  - `battle_runtime_pause_scroll_panel.tres` now uses `StyleBoxTexture.region_rect` to crop the decorated brown fantasy frame from the sheet and nine-patch margins to preserve corners and border detail. `battle_runtime_pause_scroll_inner_panel.tres` uses a cleaner same-sheet crop for inner information boxes.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to require a `Kb_base`-derived project texture, `region_rect`, and no travel-book page fill.
    - GREEN: the same guard passed after copying the asset into the project and rewiring both pause detail style resources to the keyboard-panel sheet. The run still failed on unrelated guards: recruitment workbench q-bounce animation and the known `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
- 2026-07-08 outer-frame-only correction after screenshot feedback:
  - Root cause: the complex `Kb_base.png` frame was applied to both the outer popup and inner information blocks, and the outer `StyleBoxTexture` used tile-fit stretching, causing repeated ornate frame fragments.
  - Corrected `battle_runtime_pause_scroll_panel.tres` to use the `Kb_base` crop only as the outer popup frame with stretch mode instead of tiling.
  - Reverted `battle_runtime_pause_scroll_inner_panel.tres` to a simple translucent `StyleBoxFlat` parchment content surface, so hero facts and command queue blocks do not reuse the complex frame art.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to require outer-only `Kb_base` usage, non-tiled outer stretching, and simple non-ornate inner panels.
    - GREEN: the same guard passed after the outer/inner style resources were corrected. The run still failed only on unrelated guards: recruitment workbench q-bounce animation and the known `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
  - Godot CLI lookup (`where.exe godot`, `where.exe godot4`, `where.exe Godot_v4.5-stable_mono_win64.exe`)
    - Result: Not found, so updated in-game screenshot/manual visual QA remains pending.
- 2026-07-08 asymmetric-frame split after screenshot feedback:
  - Root cause: `Kb_base.png` is an asymmetric fantasy decoration sheet, not a symmetric nine-patch frame. Treating it as a whole `StyleBoxTexture` still distorts the left dragon, top strip, and right tail when the tactical pause panel changes size.
  - Corrected the structure so `battle_runtime_pause_scroll_panel.tres` is a simple stretchable `StyleBoxFlat` background, `battle_runtime_pause_scroll_inner_panel.tres` remains a simple content surface, and the `Kb_base` artwork is isolated in the reusable `BattleRuntimePauseDecorFrame.tscn` decoration component.
  - `BattleRuntimePauseDecorFrame.tscn` owns non-input `TextureRect` atlas slices for `LeftDragonDecor`, `TopDragonDecor`, `RightTailDecor`, and `BottomTailDecor`; the tactical pause scene instances that component over the plain background instead of using the artwork as a theme panel.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to require independent non-input decoration pieces and forbid `Kb_base` inside the panel `StyleBox`.
    - GREEN: the same guard passed after adding `BattleRuntimePauseDecorFrame.tscn`, converting the outer panel resource to `StyleBoxFlat`, and keeping the existing content node paths intact. The run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
  - Godot CLI lookup (`where.exe godot`, `where.exe godot4`, `where.exe Godot_v4.5-stable_mono_win64.exe`)
    - Result: Not found, so updated in-game screenshot/manual visual QA remains pending.
- 2026-07-08 top-left decoration cluster correction after screenshot feedback:
  - Root cause: the first split still placed decoration slices directly under the full-size decoration layer. `TopDragonDecor` stretched against the whole panel width while `LeftDragonDecor` used its own fixed offsets, so the dragon head/body could not stay aligned.
  - Corrected `BattleRuntimePauseDecorFrame.tscn` so the asymmetric `Kb_base` slices live inside `BattleRuntimePauseDecorCluster`, a fixed-size top-left control scaled as a single group with `scale = Vector2(0.82, 0.82)`.
  - `LeftDragonDecor`, `TopDragonDecor`, `RightTailDecor`, and `BottomTailDecor` now use local offsets inside that cluster instead of panel-edge anchors. This keeps the decoration pinned to the popup's left-top corner and makes later scale tuning one value instead of four separate edge positions.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to require a scaled top-left decoration cluster and forbid direct root parenting for `TopDragonDecor`.
    - GREEN: the same guard passed after moving the decoration slices into `BattleRuntimePauseDecorCluster`. The run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
- 2026-07-08 outer frame nine-patch correction after screenshot feedback:
  - Root cause: the top-left cluster still treated `Kb_base.png` as floating decoration over the panel. The intended player-facing result is one outer frame around the whole tactical-pause popup, with the popup background and inner information blocks kept simple.
  - Corrected `BattleRuntimePauseDecorFrame.tscn` to remove all atlas-sliced decoration nodes and use one full-rect `NinePatchRect` named `OuterFrameNinePatch`.
  - The outer frame now references the project-owned keyboard panel sheet, crops the blue-dragon frame with `region_rect = Rect2(340, 0, 320, 120)`, uses patch margins to keep the left/top/right/bottom frame readable, and sets `draw_center = false` so the artwork is not reused as the popup fill.
  - `WorldSitePeacetimeHud.tscn` keeps `BattleRuntimePauseBackgroundPanel` as the simple base surface, overlays the outer frame component, and enlarges/insets `PauseDetailMargin` so tactical details do not sit under the frame.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to require `OuterFrameNinePatch` and forbid `BattleRuntimePauseDecorCluster`, `LeftDragonDecor`, `TopDragonDecor`, `RightTailDecor`, and `BottomTailDecor`.
    - GREEN: the same guard passed after replacing the cluster with the full outer-frame `NinePatchRect`. The run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
  - Godot CLI lookup (`where.exe godot`, `where.exe godot4`, `where.exe Godot_v4.5-stable_mono_win64.exe`)
    - Result: Not found, so updated in-engine screenshot/manual visual QA remains pending.
- 2026-07-09 modular fantasy HUD atlas replacement:
  - Replaced the Kb_base/travel-book tactical pause visual repair with project-owned slices from `assets/textures/ui/fantasy-hud-generated/fantasy_hud_modular_atlas.png`.
  - Added battle-runtime StyleBoxTexture resources for parchment panels, inner panels, fantasy slots, wood buttons, and HP/MP bars under `resource/ui/themes/battle-runtime/`.
  - `BattleRuntimePauseDecorFrame.tscn` owns fixed non-input `TextureRect` decoration pieces while `battle_runtime_pause_scroll_panel.tres` remains the stretchable parchment panel. The later screenshot follow-up keeps only the useful left scroll roll and removes the unused top plaque.
  - `WorldSitePeacetimeHud.tscn`, `BattleRuntimeSkillSlot.tscn`, `BattleRuntimeHeroSwitchButton.tscn`, and `BattleRuntimeHeroTroopSummaryRow.tscn` now reference the same battle-runtime fantasy atlas skin for runtime-only panels, buttons, slots, and bars without changing command submission or battle rules.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to require modular fantasy atlas resources.
    - GREEN: the same guard passed after adding the atlas-sliced resources and rewiring runtime HUD scenes. The run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
- 2026-07-09 bottom-detail correction after screenshot feedback:
  - Corrected the modular fantasy HUD pass so `BattleRuntimePauseDecorFrame.tscn` keeps only `LeftScrollRoll`; the right roll and top wood plaque were removed to avoid forced symmetry and unused decoration.
  - Removed the old round command wheel texture, overlay radial command menu, radial positioner helper, and regroup button. Runtime skills now bind through `BattleRuntimeSkillCommandPanel` inside `BattleRuntimePauseDetailPanel`.
  - Reworked `BattleRuntimeHeroSwitchButton.tscn` from a text-primary framed button into a frameless plinth/unit idle preview with a secondary bottom name label. The redundant selected-hero preview and preview selection-outline path are removed.
  - RED/GREEN:
    - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed `battle runtime hud uses reference driven map first command flow` after the guard was tightened to forbid the command wheel/radial menu, require bottom-detail skill binding, require left-roll-only decoration, and require plinth-preview hero switching.
    - GREEN: the same guard passed after the scene and binding correction. The full run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
- 2026-07-09 screenshot follow-up for bottom-detail cleanup:
  - RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed the updated HUD guards while `BattleRuntimeRegroupButton`, `BattleRuntimePauseCollapseButton`, `TopWoodPlaque`, framed hero-switch slots, selected-preview outline calls, and the redundant selected hero preview still existed.
  - GREEN: the same run passed all battle-runtime HUD guards after removing those controls, keeping hero switching to a frameless plinth/idle/name-label component, and making skill slots use the original frame until hover. The full run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
- 2026-07-09 screenshot follow-up for hero switch scale and skill slot states:
  - Root cause: the frameless hero switch reused the plinth/unit preview at `scale = Vector2(0.23, 0.23)` inside a 76x92 button, so the unit read at roughly label scale. The selected state also depended on a yellow treatment that disappeared against the parchment/gold UI background.
  - Corrected `BattleRuntimeHeroSwitchButton.tscn` to a 104x118 authored button, enlarged the `HeroPlinthPreview` to `scale = Vector2(0.38, 0.38)`, and added scene-authored `SelectedBackplate` plus `SelectedSideMark` nodes for a dark/cool selected state instead of a yellow outline.
  - Corrected `WorldSitePeacetimeHud.tscn` tactical-pause detail sizing so the enlarged hero switch buttons are not clipped by the parent hero panel.
  - Corrected `BattleRuntimeSkillSlot.tscn` so the dark `battle_runtime_fantasy_slot_selected.tres` frame is the normal state and the gold `battle_runtime_fantasy_slot.tres` frame appears only through the existing hover style path.
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
    - Result: The updated battle-runtime HUD guard passed, including the enlarged plinth/unit switch and dark-default/gold-hover skill slot assertions. The full run still failed only on the known unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
    - Result: Passed, 0 warnings, 0 errors.
  - `git diff --check`
    - Result: Passed with CRLF normalization warnings only; no whitespace errors.
- Manual QA remains pending because this proposal requires in-game confirmation of placement, destination beacon, skill targeting, cancel, and submit restoration flows.
