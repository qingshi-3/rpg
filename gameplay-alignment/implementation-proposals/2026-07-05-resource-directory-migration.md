# Resource Directory Migration Implementation Proposal

Status: In Progress - Batch 0 Complete, No Resource Moves Yet

## Origin

- Requirement: RES-TAX-001
- Design Proposal: `design-proposals/archived/2026-07-05-resource-directory-taxonomy/`
- Authority:
  - `system-design/resource-authoring-taxonomy.md`
  - `system-design/battle-content-progression-architecture.md`
  - `system-design/README.md`

## Scope

Create and execute a staged migration from mixed `assets/` authored-resource storage into a top-level `resource/` directory while preserving scene, config, code, and test references.

The implementation must be batch-based. Each batch moves one resource family, updates all static references in the same change, runs targeted verification, and stops if stale paths or broken loads are found.

Current inventory from the initial static scan:

| Resource Family | Current Location | Count | First Target |
|---|---:|---:|---|
| LimboAI behavior trees | `assets/ai/battle/*.tres` | 2 | `resource/battle/ai/` |
| Battle skill definitions | `assets/battle/skills/*.tres` | 5 | `resource/battle/skills/` |
| UI themes/styleboxes | `assets/themes/game-ui-skin/*.tres` | 23 | `resource/ui/themes/game-ui-skin/` |
| TileSets | `assets/tilesets/**/*.tres` | 3 | `resource/tilesets/` |
| Shaders | `assets/**/*.gdshader` | 5 | `resource/shaders/` |
| Building AtlasTexture icons | `assets/textures/world/Buildings/Foundation/*_icon.tres` | 8 | `resource/ui/icons/buildings/foundation/` |
| Unit definitions | `assets/battle/units/**/unit.tres` | 697 | Later pilot under `resource/battle/units/` |
| Unit visual definitions | `assets/battle/units/**/visual.tres` | 697 | Later pilot under `resource/battle/units/` |
| Unit audio definitions | `assets/battle/units/**/audio/audio.tres` | 2 | Later unit audio/definition slice under `resource/battle/units/` |
| Legacy unit visual support resources | `assets/battle/unit_visuals/**/*.tres` | 3 | Later visual cleanup after unit pilot |
| SpriteFrames preview packages | `assets/**/frames.tres` | 904 | No move in this proposal |

## Non-Goals

- Do not move `frames.tres` in this proposal.
- Do not move raw textures, audio, PLIST files, SVGs, fonts, or `.import` sidecars out of `assets/`.
- Do not move `.tscn` files into `resource/`.
- Do not migrate all unit `unit.tres` and `visual.tres` files in the first batch.
- Do not leave compatibility duplicate resources under old paths.
- Do not add runtime fallback logic from `resource/` back to old `assets/` paths.
- Do not start Godot editor or trigger full import/reload unless a batch specifically needs editor validation after static and .NET checks pass.

## Touched Systems

- `system-design/` authority documents.
- `project.godot` folder color metadata when `resource/` exists.
- `assets/` and new `resource/` directories.
- `scenes/**/*.tscn` ext_resource paths.
- `config/**/*.json` resource path indexes.
- Presentation C# constants that load shaders or broad unit resources.
- Regression tests that assert authored resource paths.

## GodotPrompter Skills

- `resource-pattern`
- `assets-pipeline`
- `csharp-godot`
- `godot-testing`

## Reference Surfaces

Every batch must scan and update these surfaces:

```text
scenes/**/*.tscn
assets/**/*.tres
resource/**/*.tres
config/**/*.json
src/**/*.cs
tests/**/*.cs
system-design/**/*.md
gameplay-design/**/*.md
gameplay-alignment/implementation-proposals/**/*.md
project.godot
```

The static path search for a batch must include both forms:

```powershell
rg -n "res://assets/<old-scope>" scenes assets resource config src tests system-design gameplay-design gameplay-alignment project.godot
rg -n "assets/<old-scope>" scenes assets resource config src tests system-design gameplay-design gameplay-alignment project.godot
```

For expected exceptions, the implementation must list the exact path family. The standing exception is:

```text
assets/**/frames.tres
assets/** source media and .import files
```

## Batch Plan

### Batch 0: Migration Guards And Project Metadata

Scope:

- Create `resource/` with focused subdirectories as needed.
- Add `resource/` folder color to `project.godot`.
- Add or update static regression guards so future authored resources do not default to `assets/`.

Candidate test coverage:

- A path taxonomy guard that permits `assets/**/frames.tres` but flags behavior trees, themes, styleboxes, tilesets, shaders, battle skill definitions, and AtlasTexture icon resources under `assets/`.
- Because existing authored resources still live under `assets/` before their batches, the initial guard may allow only the inventoried legacy buckets and exact counts. Any new scene file or unknown `.tres` / `.gdshader` under `assets/` fails until it is routed through a focused batch.
- A config guard that allows raw texture/audio paths under `assets/`, but requires moved authored resources to use `res://resource/...`.

Verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Stop condition:

- Stop if the guard cannot distinguish raw media from authored resources without broad false positives.

### Batch 1: LimboAI Behavior Trees And Task Adapter Boundary

Scope:

- Move `assets/ai/battle/battle_enemy_basic.tres` to `resource/battle/ai/battle_enemy_basic.tres`.
- Move `assets/ai/battle/battle_corps_commanded.tres` to `resource/battle/ai/battle_corps_commanded.tres`.
- Move `scripts/ai/limbo_tasks/battle/*.gd` and `.gd.uid` into a source-owned adapter location under `src/`.
- Update `scenes/ai/battle/BattleAiAgentHost.tscn`.
- Update behavior-tree script paths.
- Update tests that currently assert `assets/ai/battle` or `scripts/ai/limbo_tasks/battle`.

Recommended adapter target:

```text
src/Runtime/Battle/AI/LimboTasks/
```

Reason:

- The tasks are AI-facing plugin adapters, not UI presentation.
- The tasks must still call narrow C# facade methods and must not mutate Runtime truth.

Verification:

```powershell
rg -n "res://assets/ai/battle|assets/ai/battle|res://scripts/ai/limbo_tasks|scripts/ai/limbo_tasks" scenes assets resource config src tests system-design gameplay-alignment project.godot
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Manual QA:

- If automated checks pass, open `scenes/ai/battle/BattleAiAgentHost.tscn` only if static resource load checks or tests indicate Godot-side path uncertainty.

Stop condition:

- Stop if LimboAI cannot load GDScript task scripts from the chosen `src/` path.

### Batch 2: Battle Skill Resources

Scope:

- Move `assets/battle/skills/*.tres` to `resource/battle/skills/`.
- Update `config/battle/battle_skill_definitions.json`.
- Update tests and docs that assert old skill resource paths.

Verification:

```powershell
rg -n "res://assets/battle/skills|assets/battle/skills" config scenes assets resource src tests system-design gameplay-alignment project.godot
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Stop condition:

- Stop if battle skill catalog loading accepts stale paths or silently skips missing resources.

### Batch 3: UI Theme And StyleBox Resources

Scope:

- Move `assets/themes/game-ui-skin/*.tres` to `resource/ui/themes/game-ui-skin/`.
- Update all UI `.tscn` ext_resource paths.
- Update UI regression tests that assert `res://assets/themes/game-ui-skin`.
- Keep raw UI textures under `assets/textures/ui/...`.

Verification:

```powershell
rg -n "res://assets/themes/game-ui-skin|assets/themes/game-ui-skin" scenes assets resource config src tests system-design gameplay-alignment project.godot
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Manual QA:

- Open the strategic world and one site-management HUD only after automated checks pass. Confirm themed buttons, panels, and slots still render.

Stop condition:

- Stop if theme/stylebox references are duplicated between `assets/` and `resource/`.

### Batch 4: Shaders

Scope:

- Move `assets/battle/shaders/*.gdshader` to `resource/shaders/battle/`.
- Move `assets/world/shaders/*.gdshader` to `resource/shaders/world/`.
- Update C# shader path constants.
- Update any scenes or tests that assert shader paths.

Verification:

```powershell
rg -n "res://assets/.+\\.gdshader|assets/.+\\.gdshader" scenes assets resource config src tests system-design gameplay-alignment project.godot
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Stop condition:

- Stop if shader material creation logs missing shader resources during regression or manual run.

### Batch 5: TileSets

Scope:

- Move `assets/tilesets/world/StrategicWorldRoot.tres` to `resource/tilesets/world/StrategicWorldRoot.tres`.
- Move `assets/tilesets/battle/bone/ground.tres` and `objects.tres` to `resource/tilesets/battle/bone/`.
- Update all scene ext_resource paths.
- Keep tile textures under `assets/textures/...`.

Verification:

```powershell
rg -n "res://assets/tilesets|assets/tilesets" scenes assets resource config src tests system-design gameplay-alignment project.godot
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Manual QA:

- Open `scenes/world/StrategicWorldMap.tscn`, `scenes/world/sites/impl/BonefieldSite.tscn`, and `scenes/city/base/plains_city_base.tscn` if automated checks pass.

Stop condition:

- Stop if any TileMapLayer loses its TileSet after path rewrite.

### Batch 6: Building AtlasTexture Icons

Scope:

- Move `assets/textures/world/Buildings/Foundation/*_icon.tres` to `resource/ui/icons/buildings/foundation/`.
- Update `config/strategic_management/cities/buildings_foundation.json`.
- Update Strategic Management tests that assert icon path roots.
- Keep source building textures under `assets/textures/world/Buildings/...`.

Verification:

```powershell
rg -n "res://assets/textures/world/Buildings/Foundation/.+_icon\\.tres|assets/textures/world/Buildings/Foundation/.+_icon\\.tres" config scenes assets resource src tests system-design gameplay-alignment project.godot
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Stop condition:

- Stop if city building cards fail to load icons from config paths.

### Batch 7: Unit Resource Pilot

Scope:

- Move only first-slice indexed unit `unit.tres` and `visual.tres` files listed in `config/battle/unit_definition_index.json`.
- Do not move any `frames.tres`, PNG, or PLIST files.
- Preserve each moved unit's relative faction/unit folder shape under `resource/battle/units/`.
- Update `config/battle/unit_definition_index.json`.
- Update tests that assert first-slice unit paths.
- Update `BattleUnitFactory` only if its broad discovery fallback must point to `resource/battle/units` for moved definitions.

Initial pilot resources:

```text
莱昂纳王国/f1_宗师Zir
莱昂纳王国/f1_天蓝石狮
莱昂纳王国/f1_风刃指挥官
莱昂纳王国/f1_后排弓手
莱昂纳王国/f1_Elyx风暴刃
莱昂纳王国/f1_辉光龙骑兵
霜原部盟/f6_Draugar领主
霜原部盟/f6_灵魂狼
深渊军团/f4_Skull施法者
```

Verification:

```powershell
rg -n "res://assets/battle/units/.+/(unit|visual)\\.tres|assets/battle/units/.+/(unit|visual)\\.tres" config scenes resource src tests system-design gameplay-design gameplay-alignment project.godot
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Expected exception:

```text
assets/battle/units/**/frames.tres
assets/battle/units/**/*.png
assets/battle/units/**/*.plist
```

Stop condition:

- Stop if moved unit definitions cannot reference visual definitions or SpriteFrames without duplicating raw media.

### Batch 8: Unit Library Migration

Scope:

- Migrate remaining `unit.tres` and `visual.tres` resources only after Batch 7 proves the reference pattern.
- Use a generated move map reviewed before execution.
- Update broad discovery tests so all authored unit definitions resolve under `resource/battle/units`.
- Keep `frames.tres` and raw media under `assets/`.

Verification:

```powershell
rg -n "res://assets/battle/units/.+/(unit|visual)\\.tres|assets/battle/units/.+/(unit|visual)\\.tres" config scenes resource src tests system-design gameplay-design gameplay-alignment project.godot
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Stop condition:

- Stop and split into faction-sized batches if the move map is too large to review or the test diff becomes hard to reason about.

## Diagnostics

Migration diagnostics are mostly static:

- stale-path `rg` searches;
- failing resource-load assertions;
- tests that name the stale path and the expected new root;
- optional Godot editor/manual open checks only after static and .NET checks pass.

Runtime logging is not required unless a batch changes a runtime resource loader.

## Manual QA

Manual QA should be narrow and batch-specific:

- Batch 1: open LimboAI host scene only if tests cannot prove task loading.
- Batch 3: open one strategic UI and one site-management UI to confirm themes.
- Batch 5: open one world map, one site map, and one city base/layout to confirm TileSets.
- Batch 7: run or open a battle/unit preview only for pilot units if automated load checks pass.

Do not use broad manual playthrough as the first validation layer.

## Acceptance

This migration proposal is accepted when:

- `resource/` exists and owns migrated authored resource families.
- No migrated family has stale `res://assets/...` references except explicitly listed raw media or `frames.tres` exceptions.
- Each batch has passing targeted tests, `dotnet build`, and `git diff --check`.
- `frames.tres` remains in `assets/` by explicit exception, not accidental omission.
- High-volume unit resources are migrated only after the first-slice pilot proves stable.

## Verification Evidence

### 2026-07-05 Batch 0

Changes:

- Created the tracked `resource/` root placeholder.
- Registered `res://resource/` in `project.godot` folder colors.
- Added a static taxonomy guard that keeps `assets/` limited to raw media plus explicitly inventoried legacy authored-resource buckets until each migration batch moves them.
- Extended the UTF-8 BOM text-resource guard to include `resource/`.
- Added the previously missed unit audio and legacy unit visual support resources to the inventory.

Verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Result:

- `WorldSiteDeploymentCacheRegression` passed. Existing nullable warnings and a non-fatal `ScriptPathAttributeGenerator` warning about `GodotProjectDir` were reported during the test build.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- `git diff --check` passed.
