# Strategic World Location Detail Cutover Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-world-resource-bar-cutover.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-map-site-mapping.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-non-city-location-dashboard.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic Management is the new strategic-layer authority for location summaries, city facilities, source permissions, and corps instances. The large-map left detail panel may still share the legacy map selection shell, but it must not show legacy `WorldSiteState.Facilities` or `WorldSiteState.Garrison` as management truth.

## Goal

Cut the large-map selected-location detail panel over to Strategic Management location dashboards so city and non-city strategic facts match the new management authority.

## Scope

- Change `StrategicWorldRoot.RefreshDetail(...)` to resolve the selected map site through `StrategicManagementRuntime.LocationMappings`.
- Build the selected location read model through `StrategicManagementRuntime.BuildLocationDashboard(...)`.
- Show Strategic Management location identity, control, source permissions, passive production, city facility slots, built facilities, and corps instances in the existing left detail containers.
- Keep opportunity detail, action buttons, expedition drafting, army movement, battle entry, and legacy map rendering untouched.
- Add a regression guard that prevents the selected-location detail body from reading legacy site facilities or garrison rows.

## Non-Goals

- Do not replace expedition state, old `WorldArmyState`, or army command flow in this slice.
- Do not replace battle request building, battle preparation, battle bridge, or battle result writeback.
- Do not add new city commands or new Strategic Management definitions.
- Do not implement save/load or persistent strategic lifecycle changes.
- Do not delete legacy world-site data structures globally while other transition slices still need them.

## Touched Systems

- `src/Presentation/World/StrategicWorldRoot.DetailHud.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- `gameplay-alignment/implementation-proposals/README.md`

## Tests

Primary Presentation guard:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Strategic Management guard:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Missing map-site to Strategic Management mapping should fail visibly in the detail panel with a concise player-facing message instead of falling back to legacy management facts.

## Manual QA

Optional after automated verification: open the large strategic map, select the player city and a non-city resource/source site, and confirm the left detail panel shows Strategic Management production/source/facility/corps facts.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed on `strategic world detail reads strategic management location dashboard` because `StrategicWorldRoot.DetailHud.cs` still imported only legacy world state and did not consume Strategic Management dashboard view models.
- 2026-06-14: `StrategicWorldRoot.RefreshDetail(...)` now resolves selected map sites through `StrategicManagementRuntime.LocationMappings`, builds `StrategicManagementRuntime.BuildLocationDashboard(...)`, and renders Strategic Management location, production, source permission, city facility, and corps instance facts.
- 2026-06-14: The large-map detail panel no longer reads `WorldSiteState.Facilities`, `WorldSiteState.Garrison`, legacy resource labels, or legacy facility ids as management truth. Unmapped locations fail visibly as unconfigured Strategic Management locations instead of falling back to old management facts.
- 2026-06-14: Review fix added: `RefreshDetail(...)` explicitly initializes `StrategicManagementRuntime` before reading location mappings, and `ResetWorld()` now resets `StrategicManagementRuntime` because the large-map resource bar and detail panel both read the new strategic state.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
