# Strategic Battle Bridge Identity And Writeback Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposal:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-expedition-authority.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/vertical-slices/first-playable-slice.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Strategic Management now creates expedition state and locks hero/corps instances, but the current battle entry still loses that identity inside legacy world-army, battle-request, handoff, and result-applier paths. Since large-map location detail reads Strategic Management, battle victory must write location and participant consequences back through Strategic Management commands or the first playable loop does not close.

## Goal

Add the first Strategic Battle Bridge slice that preserves strategic expedition, hero, corps, and location identity across battle session preparation and applies battle victory/defeat summaries back to Strategic Management state.

## Scope

- Add battle-entry metadata to Strategic Management location definitions for the first Bonefield assault target.
- Add a bridge service that creates a transient strategic battle session from a Strategic Management expedition.
- Compile a bridge-owned `BattleStartSnapshot` whose player participant ids come from Strategic Management hero/corps instance state.
- Add a bridge result-summary DTO and a Strategic Management command that applies the summary to location control, expedition status, hero locks, and corps strength/status.
- Add migration fields to legacy `BattleStartRequest` / `BattleForceRequest` only as temporary bridge carriers while the existing battle-preparation scene still consumes legacy requests.
- Enrich the large-map arrived-assault battle request from the bridge session before scene transition.
- Apply bridge-produced Strategic Management result summary when the battle runtime resolves, before/alongside legacy world-state compatibility writeback.

## Non-Goals

- Do not modify battle Runtime simulation, actor state machines, navigation, skills, cooldowns, or damage.
- Do not replace battle-preparation UI internals in this slice.
- Do not delete `BattleSessionHandoff`, `WorldBattleRequestBuilder`, or `WorldBattleResultApplier` in this slice.
- Do not implement full rewards, equipment, report attribution, save/load, multi-corps heroes, or broad battle types.
- Do not make `WorldArmyState` a new strategic authority.

## Touched Systems

- `src/Definitions/StrategicManagement/*`
- `src/Application/StrategicBattleBridge/*`
- `src/Application/StrategicManagement/*`
- `src/Application/Battle/BattleStartRequest.cs`
- `src/Application/Battle/BattleForceRequest.cs`
- `src/Application/Battle/BattleGroupSessionProbeService.cs`
- `src/Presentation/World/StrategicWorldRoot.BattleEntry.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.*.cs`

## Tests

Strategic bridge and command behavior:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Presentation and migration-boundary guard:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Bridge session creation and Strategic Management result writeback should log session id, expedition id, target location, outcome, and participant ids at low frequency.

## Manual QA

Optional after automated verification: create/assign a hero company, send it to Bonefield, resolve the assault, return to the strategic map, and confirm Bonefield/Beast Den control is reflected by the Strategic Management location dashboard.

## Acceptance Evidence

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed.
  - Known Godot source-generator warning appeared in the test project: `GodotProjectDir is null or empty`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed.
  - Known Godot source-generator warning and pre-existing nullable warnings appeared in the test project.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with `0` warnings and `0` errors.
- 2026-06-15: Fixed Strategic Management-backed battle HUD skill command identity alignment.
  Runtime command HUD group keys now prefer the stable `StrategicParticipantId`, matching Runtime actor `BattleGroupId`; legacy probe group ids remain only for non-strategic compatibility launch paths. This keeps Godot object ids out of the bridge/runtime command contract.
- 2026-06-15: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed after adding the strategic participant HUD skill command regression.
- 2026-06-15: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-15: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with `0` warnings and `0` errors.
- 2026-06-15: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed the skill/runtime command cases, then stopped on the existing oversized-file guard for `tests/StrategicManagementRegression/Program.cs` and `src/Application/StrategicManagement/StrategicManagementCommandService.cs`.
