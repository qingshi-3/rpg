# Strategic Management Expedition Authority Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-world-location-detail-cutover.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-command-buttons.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Strategic Management owns hero-corps assignment and expedition formation. The legacy `WorldSiteState.Garrison` and `WorldExpeditionService` may remain as historical code until removed, but they must not remain the authority for selecting or locking player expeditions.

## Goal

Move first-version player expedition formation to Strategic Management: a dispatchable hero company is an assigned strategic hero plus one persistent corps instance, and creating an expedition locks that hero/corps in Strategic Management before the large map creates a temporary `WorldArmyState` movement adapter.

## Scope

- Add Strategic Management expedition state, intent/status values, allocation, validation, and command mutation.
- Add city dashboard hero-company rows so the large-map expedition draft can read dispatchable companies from Strategic Management.
- Update the first strategic map expedition draft to select a Strategic Management hero company instead of old garrison unit counts.
- Update expedition creation to call `StrategicManagementRuntime.Commands.CreateExpedition(...)` first, then create a `WorldArmyState` adapter for map movement and legacy battle-transition compatibility.
- Explicitly treat `WorldArmyState` as an adapter carrying strategic expedition IDs, not the long-term expedition authority.
- Map the current first enemy map site to the Strategic Management beast minor site so attack expeditions have a strategic target.

## Non-Goals

- Do not replace army movement internals in this slice.
- Do not replace `WorldBattleRequestBuilder`, `WorldBattleResultApplier`, or legacy battle handoff in this slice.
- Do not apply battle results back to Strategic Management yet.
- Do not add save/load or strategic AI.
- Do not model multi-corps heroes or multiple corps per expedition.

## Touched Systems

- `src/Domain/StrategicManagement/*`
- `src/Definitions/StrategicManagement/*`
- `src/Application/StrategicManagement/*`
- `src/Application/World/StrategicExpeditionWorldArmyAdapter.cs`
- `src/Domain/World/WorldArmyState.cs`
- `src/Presentation/World/StrategicWorldRoot.cs`
- `src/Presentation/World/StrategicWorldRoot.Expedition.cs`
- `src/Presentation/World/StrategicWorldRoot.ExpeditionHud.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.*.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Tests

Strategic Management behavior:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Presentation and legacy-adapter guard:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Add low-noise command logs through existing Strategic Management command logging. The world-map adapter should log expedition creation with strategic expedition id, hero id, corps id, source, target, and intent.

## Manual QA

Optional after automated verification: create a corps in the city panel, assign it to a hero, start expedition from the large map, and confirm the draft lists the assigned hero company instead of old garrison unit stock.

## Acceptance Evidence

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed.
  - Known Godot source-generator warning appeared in the test project: `GodotProjectDir is null or empty`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed.
  - Known Godot source-generator warning and pre-existing nullable warnings appeared in the test project.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with `0` warnings and `0` errors.
