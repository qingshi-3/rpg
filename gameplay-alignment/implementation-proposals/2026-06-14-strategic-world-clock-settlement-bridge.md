# Strategic World Clock Settlement Bridge Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposal:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-world-clock-presentation-terminology.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic time is Sanguo Qunying-style realtime world-map time. The current legacy strategic world clock may remain as a Presentation/runtime driver during the rebuild, but elapsed strategic-time settlement must be routed through Strategic Management commands.

## Goal

When the large-map world clock completes one world-map settlement, bridge that event into `StrategicManagementRuntime.SettleElapsedWorldTime(1)` so first-version Strategic Management resource-site production advances while the world map is running.

## Scope

- Import the Strategic Management Application boundary into the strategic world clock partial.
- Call `StrategicManagementRuntime.SettleElapsedWorldTime(1)` exactly from the world-clock settlement path after the legacy `WorldTickService` advances.
- Append a concise production-settlement notice when Strategic Management emits settlement events.
- Log failed Strategic Management settlement explicitly without replacing legacy world-clock behavior.
- Add a regression guard that prevents the world-clock settlement path from bypassing the Strategic Management Runtime facade.

## Non-Goals

- Do not replace the legacy `WorldTickService` or rename `WorldTick` internals in this slice.
- Do not introduce a new Godot timer, `_Process` loop, or separate automatic Strategic Management clock.
- Do not directly mutate Strategic Management resources from Presentation.
- Do not modify battle Runtime, battle preparation, battle bridge contracts, or battle-result settlement.
- Do not add enemy strategic AI, construction timers, training timers, recovery timers, or opportunity refresh.

## Touched Systems

- `src/Presentation/World/StrategicWorldRoot.WorldClock.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`

## Tests

Primary Presentation bridge guard:

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

Use the existing world-clock tick log for normal ticks. If Strategic Management settlement rejects a running-map clock tick, log a warning with the failure reason so the bridge failure is visible without adding per-frame noise.

## Manual QA

Optional after automated verification: open the large strategic map, let one `大地图结算` complete, and confirm Strategic Management resource production changes are reflected the next time the city/resource dashboard is opened.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed on `strategic world clock settles strategic management elapsed time through runtime` because the world-clock partial did not import Strategic Management or call `StrategicManagementRuntime.SettleElapsedWorldTime(1)`.
- 2026-06-14: `AdvanceWorldClockTick()` now keeps the legacy `WorldTickService` as the local cadence driver, then bridges one elapsed world-map pulse through `StrategicManagementRuntime.SettleElapsedWorldTime(1)`. Successful production settlement appends a concise notice; failed settlement logs the rejection reason.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
