# Strategic Management Dashboard UI Binding Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-core-foundation.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-presentation-cutover.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Strategic Management presentation must consume view models and submit commands. The old world-site management binder must not become the new city-management rule or display authority.

## Goal

Bind the existing Godot site management HUD to the new Strategic Management dashboard read model for the first city-management screen. The slice replaces the old management panel's displayed facility/garrison/action facts with Strategic Management dashboard facts while preserving battle entry, map entities, and scene flow.

## Scope

- Add a thin `StrategicManagementRuntime` entry in `Rpg.Application.StrategicManagement` to hold first-slice definitions, state, rules, command service, and view model service.
- Add a focused `StrategicManagementDashboardPanelBinder` in Presentation to render dashboard view models into the existing authored HUD containers.
- Update `WorldSiteRoot` site-management binding to:
  - build the dashboard from `StrategicManagementRuntime`;
  - set top-bar resource and overview text from the dashboard;
  - delegate facility/build/corps/template/hero display to `StrategicManagementDashboardPanelBinder`;
  - keep old world-site state only for map entities, battle/deployment preparation, and legacy scene flow until those systems receive separate slices.
- Add source-regression tests that prevent the new dashboard binder from referencing legacy world state or world actions.

## Non-Goals

- Do not create new UI scenes in this slice.
- Do not add working construction, muster, or assignment buttons yet; display-only rows are enough.
- Do not replace battle entry, battle preparation, settlement, expedition, or scene transition logic.
- Do not implement save/load.
- Do not delete old `WorldSiteManagementPanelBinder` if battle/site legacy code still references it; stop routing the new city-management panel through it.
- Do not implement strategic battle bridge changes.

## Touched Systems

- Create `src/Application/StrategicManagement/StrategicManagementRuntime.cs`.
- Create `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs` and `WorldSiteRoot.SiteManagementHud.cs`.
- Modify `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`.
- Update `gameplay-alignment/implementation-proposals/README.md`.

## Tests

Primary UI architecture verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Focused Strategic Management verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

No new runtime diagnostics are required. This slice is display-only. Later command-button slices should rely on `StrategicManagementCommandService` command diagnostics.

## Manual QA

Optional after automated verification: open the site scene and confirm the left management panel displays Strategic Management resources, city slots, facility options, corps templates, corps instances, and hero assignment rows.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `StrategicManagementDashboardPanelBinder.cs` did not exist and the old `WorldSiteManagementPanelBinder.cs` still existed.
- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `StrategicManagementRuntime` did not exist.
- 2026-06-14: Implemented `StrategicManagementRuntime` as the first in-memory Strategic Management entry for definitions, state, rules, commands, and dashboard view models.
- 2026-06-14: Implemented `StrategicManagementDashboardPanelBinder` and routed `WorldSiteRoot.BindSiteManagementPanel` through `StrategicManagementRuntime.BuildDashboard(...)` and `_strategicManagementDashboardPanelBinder.Bind(...)`.
- 2026-06-14: Removed the old `WorldSiteManagementPanelBinder` display authority and deleted legacy `WorldSiteRoot` resource/overview display helpers for the management panel.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir` not being visible to the generator.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
