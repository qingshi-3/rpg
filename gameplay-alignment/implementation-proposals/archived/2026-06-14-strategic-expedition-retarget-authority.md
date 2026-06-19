# Strategic Expedition Retarget Authority Implementation Proposal

Status: Implemented - Amended By Direct Battle Trigger Entry

## Origin And Authority

- Gameplay authority:
  - `gameplay-design/vertical-slices/first-playable-slice.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Related implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-expedition-authority.md`
  - `gameplay-alignment/implementation-proposals/2026-06-13-strategic-army-command-application-boundary.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-active-context-cutover.md`

## Goal

Fix the world-map expedition retarget bug where a Strategic Management expedition can be created as `MoveToPosition`, then its `WorldArmyState` adapter is commanded to assault Bonefield, causing battle entry to reject the strategic bridge session with `unsupported_expedition_intent` when the army arrives.

Correction: a selected moving expedition right-clicking Bonefield must update the Strategic Management expedition to `AssaultLocation` before the large-map army reaches the target. Retargeting owns travel intent; battle entry is now confirmed by the direct "触发战斗" world-map dialog after arrival.

Amendment: `gameplay-alignment/implementation-proposals/2026-06-14-direct-battle-trigger-entry.md` supersedes the earlier strategic battle-preparation gate. Do not reintroduce `missing_battle_preparation_choice`, arrived-assault preparation buttons, or strategic-preparation bridge metadata from this historical proposal.

## Scope

- Add a Strategic Management command for retargeting an active expedition's target location and intent.
- Validate retargets through Strategic Management rules before mutating the expedition.
- Route selected-army map and site commands through that command when the army carries a `StrategicExpeditionId`.
- Keep `WorldArmyState` as the large-map movement adapter; it must not become strategic expedition authority.
- Keep battle bridge entry strict: it should continue to reject non-assault strategic expeditions.
- Do not gate initial assault expedition creation or retargeting behind strategic battle-preparation.
- When an assault expedition arrives, show the direct "触发战斗" confirmation instead of a strategic-preparation choice panel.

## Non-Goals

- Do not change battle Runtime, deployment UI, or battle result settlement.
- Do not add strategic AI, diplomacy, logistics, or new route-planning behavior.
- Do not make unsupported targets silently fall back to battle entry.
- Do not remove legacy non-Strategic world-army command compatibility in this slice.

## Touched Systems

- `src/Application/StrategicManagement/*`
- `src/Presentation/World/StrategicWorldRoot.SelectionInput.cs`
- `src/Presentation/World/StrategicWorldRoot.ArmyCommands.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Tests

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Strategic retarget command logs should include expedition id, target location, intent, and explicit failure reason. Existing world-army command logs should remain the movement-adapter trace.

## Manual QA

Select one or more hero companies, create an expedition, command the selected strategic army to Bonefield, and confirm arrival focuses Bonefield and opens the "触发战斗" confirmation instead of resetting the army or drifting the camera into an invalid map view.

## Acceptance Evidence

- RED: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Failed before implementation because `StrategicManagementCommandService.RetargetExpedition(...)` did not exist.
- RED: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Failed before implementation because `StrategicWorldRoot` had no `TrySyncStrategicExpeditionCommand(...)` path.
- Superseded correction: earlier missing-preparation gate and arrived-assault preparation-button evidence was removed by `2026-06-14-direct-battle-trigger-entry`.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with `0` warnings and `0` errors.
- Desktop Godot Mono headless smoke:
  - `C:\Users\qs\Desktop\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe --headless --path D:\godot\rpg --quit-after 180`
  - Exit code `0`; existing Control anchor warning remains.
- GREEN: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed.
- GREEN: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed. Existing nullable/source-generator warnings remain outside this slice.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with `0` warnings and `0` errors.
- Desktop Godot Mono headless smoke:
  - `C:\Users\qs\Desktop\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe --headless --path D:\godot\rpg --quit-after 180`
  - Exit code `0`; only the existing Control anchor warning appeared.
