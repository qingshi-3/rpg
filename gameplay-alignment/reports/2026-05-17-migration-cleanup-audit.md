# Migration Cleanup Audit

Date: 2026-05-17

Follow-up cleanup status on 2026-05-17:

- Misleading active `docs/` routes were rewritten to point at `gameplay-design/`, `system-design/`, or historical migration records.
- Old AP/action-wheel wireframe assets under `docs/20-game-design/tactical-battle/battle-ui-wireframes/` were deleted.
- The dead `WorldSiteRoot.UseAutoBattleRuntime` exported switch was removed and regression tests now guard against restoring it.

This audit checks whether the first migration left misleading or broken residue behind. It does not evaluate missing target features such as battle-group combat, equipment depth, city attributes, or battle command UI.

## Authority Used

- Current gameplay authority: `gameplay-design/content-systems-long-term-design.md`.
- Current routing rule: `gameplay-alignment/authority-map.md`.
- Legacy `docs/` material is reference-only and must not override `gameplay-design/` or `system-design/`.

## Clean

### Legacy Manual Runtime References

Runtime, scene, asset, project, and solution scans found no active references to the retired manual battle core:

- `BattleTurnController`
- `BattleCommandController`
- `BattleIntentController`
- `BattlePreviewController`
- `BattleInputRouter`
- `ActionPointComponent`
- `BattleActionMenu`
- `BattleHudRoot`
- `TopTurnBar`
- `UnitStatusCard`
- `BattleActionDock`
- `CommandInfoPanel`
- `FloatingActionHint`
- `BattleTurnQueue`
- `ActionWheelSlot`
- legacy battle AP authoring fields such as `MaxActionPoints`, `MoveActionPointCost`, and `AttackActionPointCost`

Remaining matches for these names are primarily regression guardrails in `tests/WorldSiteDeploymentCacheRegression/Program.cs` and `tests/AutoBattleRuntimeRegression/Program.cs`, where they assert that old runtime files and scene dependencies stay deleted.

### Resource Paths

All non-test `res://` references in `.tscn`, `.tres`, `.gdshader`, `.cs`, `.gd`, and `.import` files resolve to existing project files.

The earlier suspected mojibake resource paths were a PowerShell default-decoding false positive. Reading the same files as UTF-8 shows valid paths such as `res://resource/battle/units/莱昂纳王国/f1_Baast冠军/visual.tres`.

### Build And Regression

The cleanup state compiles and the migration guard tests pass:

- `dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj`
- `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

The build completed with 0 warnings and 0 errors.

## Dirty

### 1. Active Legacy Docs Were Rerouted

Follow-up cleanup rewrote the main active `docs/` routes that presented automated tactical validation or post-deployment auto battle as the active future.

Updated examples:

- `docs/20-game-design/tactical-battle/README.md` now routes to `gameplay-design/` and warns against using auto tactics as product identity.
- `docs/20-game-design/gameplay-direction.md` now describes medium-frequency hero-led light RTS commands.
- `docs/10-product/positioning.md` now presents city/strategic-location operation and hero-led corps combat.
- `docs/30-technical-design/battle/README.md` and `docs/30-technical-design/battle/battle-scene-architecture.md` now route future runtime work through accepted light RTS architecture.
- `docs/30-technical-design/world/strategic-world-v1-battle-contract.md` now states the manual AP runtime is removed.
- `docs/40-content/tutorial/tutorial-battle-spec.md` now teaches hero-led battle-group commands instead of pure no-command auto battle.

Historical migration documents still use old terminology by design. They are records, not active authority.

### 2. Old AP / Action-Wheel Wireframe Assets Were Removed

Follow-up cleanup deleted the stale battle UI wireframe files under:

- `docs/20-game-design/tactical-battle/battle-ui-wireframes/`

The removed files contained old turn/AP/action-wheel language such as `TopTurnBar`, turn order, AP cost, move AP, attack AP, and enemy turn.

### 3. `UseAutoBattleRuntime` Was Removed

Follow-up cleanup removed the dead exported switch from:

- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

### 4. Inert Battle Intent / Action Utilities Remain

The deleted manual runtime no longer owns active flow, but some old battle support code remains compiled and mostly unwired:

- `src/Presentation/Battle/Actions/BattleActionExecutor.cs`
- `src/Presentation/Battle/Intents/BattleIntentResolver.cs`
- `src/Presentation/Battle/AI/GreedyAlliedIntentPlanner.cs`
- `src/Presentation/Battle/AI/GreedyEnemyIntentPlanner.cs`
- `src/Presentation/Battle/Flow/BattleCorpsCommand.cs`

These files are not currently active replacements for the old manual runtime. They may be reusable as future light-RTS building blocks, but they are not documented as such in `system-design/`. Until a confirmed discussion classifies them and updates current authority as required, they remain ambiguous implementation residue.

### 5. Historical Language Still Says "Auto Tactics"

Some historical migration records still use "auto tactics" because that was the first migration target. Follow-up cleanup changed active docs and test guardrails away from using it as product direction.

This is acceptable only as historical context for "legacy manual runtime retirement". It should not become the naming basis for future combat work.

## Priority

1. In the next confirmed combat-architecture discussion, classify the remaining battle intent/action utilities as reusable foundation or old runtime residue, update current authority as required, and capture execution in one active work item.
2. Keep historical migration documents as records, but do not route new gameplay or architecture work through them as product authority.

## Bottom Line

The first migration cleaned the active manual/AP runtime well enough: old controllers, AP authoring fields, action-menu UI, and scene dependencies are gone, tests pass, and resource paths resolve.

The remaining unclean areas are not missing features. They are authority and residue problems: historical migration documents still use old "auto tactics" language by design, and some old battle utility code has no accepted future ownership.
