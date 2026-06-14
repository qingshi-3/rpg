# Strategic World Clock Presentation Terminology Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposal:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-site-entry-timeflow.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic time is Sanguo Qunying-style realtime world-map time. Internal settlement ticks may remain implementation granularity, but player-facing Presentation text must not describe the strategic layer as a turn, step, or manual advance loop.

## Goal

Clean old player-facing world-clock text in the strategic world Presentation so it uses `大地图时间` / `大地图结算` terminology instead of `世界步`, `世界推进`, or `世界时钟` wording.

## Scope

- Replace old player-visible world-clock, resource, opportunity, and pause notices in strategic world Presentation.
- Add a regression guard that scans the strategic world Presentation text for retired realtime-strategy terminology.
- Keep the guard scoped to string literals and comments so internal class, method, and field names such as `WorldTick` and `WorldClock` can be retired in later architecture slices.

## Non-Goals

- Do not replace the legacy `WorldTick` implementation in this slice.
- Do not rename `WorldClock` classes, fields, methods, or logs.
- Do not add automatic Strategic Management elapsed-time settlement.
- Do not modify battle Runtime, battle preparation, battle bridge contracts, or battle-result settlement.
- Do not change Strategic Management core APIs.

## Touched Systems

- `src/Presentation/World/StrategicWorldRoot.WorldClock.cs`
- `src/Presentation/World/StrategicWorldRoot.DetailHud.cs`
- `src/Presentation/World/StrategicWorldRoot.UiBootstrap.cs`
- `src/Presentation/World/StrategicWorldRoot.BattleEntry.cs`
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

No new runtime diagnostics are needed. Existing world-clock and strategic transition logs keep internal tick names until a later implementation slice replaces the legacy clock internals.

## Manual QA

Optional after automated verification: open the large strategic map and confirm the top-bar time label, pause tooltip, speed notice, resource line, and opportunity remaining-time line use `大地图时间` / `大地图结算` wording.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed on `strategic world clock presentation uses world map time terminology` because strategic world Presentation still exposed `世界步`, `世界推进`, `推进到`, `推进已暂停`, `继续推进`, `暂停世界推进`, `继续世界推进`, and `世界时钟`.
- 2026-06-14: Replaced player-facing strategic world-clock, resource, opportunity, and pause notices with `大地图时间` / `大地图结算` terminology while leaving internal `WorldTick` / `WorldClock` implementation names untouched for later slices.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
