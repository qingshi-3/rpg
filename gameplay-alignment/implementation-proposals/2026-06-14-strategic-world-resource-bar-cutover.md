# Strategic World Resource Bar Cutover Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposal:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-world-clock-settlement-bridge.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic Management is the new strategic-layer authority for faction-shared first-version resources. The old strategic world state may still drive map rendering and transitional clocks, but player-facing resource totals should no longer read from legacy `StrategicWorldState.PlayerResources`.

## Goal

Cut the large-map top resource bar over to Strategic Management resource view models so elapsed world-map settlement and city commands become visible on the strategic world screen.

## Scope

- Change `StrategicWorldRoot.RefreshResources()` to read shared faction resources from `StrategicManagementRuntime.BuildDashboard(...)`.
- Keep `State.WorldTick` / `大地图结算` display as transitional world-clock presentation until a later clock-internals slice retires legacy tick naming.
- Add a regression guard that prevents the top resource bar from reading legacy `ResourceStore` or legacy `StrategicWorldIds` resource labels.

## Non-Goals

- Do not replace legacy `WorldTick`, army movement, map rendering, or expedition state in this slice.
- Do not add new resource types, costs, production rules, logistics loss, storage caps, or cross-city transport.
- Do not mutate Strategic Management resources from Presentation.
- Do not modify battle Runtime, battle preparation, battle bridge contracts, or battle-result settlement.
- Do not cut over hover summaries or site detail panels in this slice.

## Touched Systems

- `src/Presentation/World/StrategicWorldRoot.UiBootstrap.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`

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

No new runtime diagnostics are needed. Missing Strategic Management initialization should still be handled by the runtime facade.

## Manual QA

Optional after automated verification: open the large strategic map, let one large-map settlement complete, and confirm the top resource bar's building-material total increases through Strategic Management state.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed on `strategic world resource bar reads strategic management resources` because `RefreshResources()` still read legacy `State.PlayerResources` and did not import Strategic Management.
- 2026-06-14: `StrategicWorldRoot.RefreshResources()` now reads faction-shared resources from `StrategicManagementRuntime.BuildDashboard(...).Resources` and keeps the transitional `大地图结算` counter.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
