# Strategic Management Non-City Location Dashboard Implementation Proposal

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
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Non-city strategic locations are first-class strategic locations, but they are not full managed cities. The first resource-site dashboard should expose lightweight location facts without reopening city facility, corps creation, or hero assignment commands.

## Goal

Show a minimum Strategic Management dashboard for mapped non-city strategic locations. Resource sites and source sites should display their identity, control state, owner, source permissions, and shared faction resources while clearly saying that city-only management commands are unavailable.

## Scope

- Add a non-city strategic-location read model to Strategic Management presentation view models.
- Build that read model from Strategic Management definitions plus durable strategic state.
- Expose a runtime helper for building a dashboard by strategic location id.
- Route mapped non-city `WorldSiteRoot` site HUD refreshes through the location dashboard instead of binding an empty city dashboard.
- Keep facility construction, corps creation, and hero assignment guarded behind city resolution.
- Add regression coverage for location dashboard facts and Presentation guardrails.

## Non-Goals

- Do not add full city systems to resource sites, beast sites, ruins, dungeons, or opportunities.
- Do not implement resource production ticks, site upgrade actions, source-site battles, or beast-site map placement in this slice.
- Do not change battle Runtime, battle preparation, battle launch, settlement, or bridge contracts.
- Do not replace legacy world map/site scene flow.
- Do not add new persistent state beyond reading existing location state.

## Touched Systems

- Modify Strategic Management view models, view-model service, and runtime helper.
- Modify `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`.
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

No new diagnostic category is required. Existing HUD notices remain the low-noise player-facing feedback for city-only command rejection.

## Manual QA

Optional after automated verification: enter the mapped player city and confirm city management still works; enter the mapped non-city resource site and confirm the panel shows strategic-location facts without city command buttons.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `StrategicManagementViewModelService.BuildLocationDashboard(...)` was missing.
- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `WorldSiteRoot` did not route mapped non-city sites into a Strategic Management location dashboard.
- 2026-06-14: Added `StrategicLocationDashboardViewModel`, Strategic Management location dashboard builders, runtime helper, source-permission display labels, and callback-only HUD binding for read-only non-city location summaries.
- 2026-06-14: Routed mapped non-city `WorldSiteRoot` refreshes through `StrategicManagementRuntime.BuildLocationDashboard(...)` and `_strategicManagementDashboardPanelBinder.BindLocation(...)`; city-only commands still require city resolution before submitting Strategic Management commands.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
