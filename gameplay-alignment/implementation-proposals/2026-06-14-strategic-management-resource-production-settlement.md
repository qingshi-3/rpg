# Strategic Management Resource Production Settlement Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-core-foundation.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-presentation-cutover.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-dashboard-ui-binding.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-command-buttons.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-map-site-mapping.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-non-city-location-dashboard.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Resource sites should contribute to the faction-shared first-version economy without becoming cities, building queues, or worker-managed RTS points.

## Goal

Add the first Strategic Management resource-production settlement slice. Controlled resource sites should define passive income and a command should settle that income into faction-shared resources. Non-city location dashboards should show the current production summary.

## Scope

- Add production amounts to strategic-location definitions.
- Give the first mapped timber resource site a small building-materials production value.
- Add read-only rules for location production eligibility and projected production.
- Add a Strategic Management command that settles production for one location over a bounded positive step count.
- Surface production information in `StrategicLocationDashboardViewModel` and the non-city HUD binder.
- Add regression coverage for production projection, command settlement, enemy-held rejection, and presentation guardrails.

## Non-Goals

- Do not add automatic world ticking, timers, or background production loops.
- Do not add cross-city transport loss, route disruption, resource caps, or logistics.
- Do not add resource-site buildings, upgrades, or facility slots.
- Do not add Strategic Management UI buttons for production settlement in this slice.
- Do not touch battle Runtime, battle bridge, settlement, or scene transition contracts.

## Touched Systems

- Modify Strategic Management definitions.
- Modify Strategic Management rules and command service.
- Modify Strategic Management view models and view-model service.
- Modify `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`.
- Modify `tests/StrategicManagementRegression/Program.cs`.
- Modify `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- Update `gameplay-alignment/implementation-proposals/README.md`.

## Tests

Primary Strategic Management verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Presentation architecture verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Use the existing Strategic Management command logging path for accepted and rejected production settlement commands. No high-frequency production logs are needed because this slice has no automatic tick.

## Manual QA

Optional after automated verification: inspect the mapped resource site and confirm the dashboard shows production while city-only commands remain unavailable.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `StrategicLocationDefinition.ProductionPerStep` was missing.
- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because the Strategic Management dashboard binder did not expose production display fragments.
- 2026-06-14: Added resource-site `ProductionPerStep` definitions, first timber-site building-materials production, production projection rules, and `SettleLocationProduction(...)` command settlement.
- 2026-06-14: Added production summary view models and non-city HUD production display while keeping the binder callback-only and city-only commands guarded behind city resolution.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
