# Direct Battle Trigger Entry Implementation Proposal

Status: Automated Verification Passed

## Origin And Authority

- Design proposal:
  - `design-proposals/archived/2026-06-14-direct-battle-trigger-entry/`
- Gameplay authority:
  - `gameplay-design/vertical-slices/first-playable-slice.md` VS-03 and VS-06.
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

## Goal

Remove the mandatory Strategic Management battle-preparation choice from the first playable slice and route hostile target arrival through a direct "触发战斗" confirmation. The player selects or forms an expedition force, right-clicks Bonefield, waits for arrival, confirms the battle trigger, and then enters the existing battle preparation / deployment scene.

## Scope

- Remove first-slice strategic battle-preparation definitions, state, rules, commands, view-model exposure, and UI buttons.
- Stop requiring a selected strategic preparation before creating a Bonefield assault expedition or bridge session.
- Remove strategic-preparation metadata from bridge sessions, legacy battle requests, pre-battle text, result summaries, and strategic feedback.
- Change the large-map battle confirmation dialog title to "触发战斗" and keep camera focus on the target location before confirmation.
- Keep battle deployment preparation, company placement, objective selection, engagement-rule selection, and Runtime launch readiness intact.

## Non-Goals

- Do not remove the battle deployment / preparation screen.
- Do not add scouting, siege methods, support reservations, alternate entry routes, or battle modifiers.
- Do not change battle Runtime simulation, skill behavior, AI, or battle result settlement beyond deleting strategic-preparation metadata.
- Do not add a replacement strategic choice in this slice.

## Touched Systems

- `src/Definitions/StrategicManagement/*`
- `src/Domain/StrategicManagement/*`
- `src/Application/StrategicManagement/*`
- `src/Application/StrategicBattleBridge/*`
- `src/Application/Battle/BattleStartRequest.cs`
- `src/Presentation/World/StrategicWorldRoot.*`
- `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/*`

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

Battle-entry logs should continue to include army id, expedition id, target, bridge session id, and battle focus position. They should no longer report `missing_battle_preparation_choice` for Bonefield assault.

## Manual QA

In the desktop Mono build, form or select an expedition, right-click Bonefield, wait for the army to arrive, confirm the "触发战斗" dialog, and verify the battle preparation / deployment scene opens.

## Acceptance Evidence

- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-14: `Godot_v4.5.1-stable_mono_win64_console.exe --headless --path D:\godot\rpg --quit-after 180` exited successfully. It still reports the pre-existing Control anchor size warning from `StrategicWorldRoot.MapSetup.cs:212`.
- 2026-06-14: Post-review verification confirms assault-site battle trigger focus prioritizes the hostile target site over the arrived source army.
- 2026-06-14: Residual strategic-preparation scan found only negative assertions, accepted no-preparation authority text, and battle-deployment preparation code. Deleted the stale no-op `SelectDefaultBattlePreparation` test helper.
- 2026-06-15: Fixed battle presentation actor identity alignment for Strategic Management-backed battles. Runtime events now address the same actor IDs used by displayed player-company entities after deployment.
- 2026-06-15: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`, `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`, `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`, and Godot headless startup passed after the actor-identity fix.
- Desktop Mono manual QA remains pending: the interactive right-click expedition path was not completed in this pass.
