# Strategic Management Site Entry Timeflow Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposal:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-world-timeflow-boundary.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic time is Sanguo Qunying-style realtime world-map time. Entering city or site management leaves the large world-map timeline and must pause Strategic Management elapsed-time settlement. Returning to the world map resumes that timeline. Scene nodes and UI panels may request this boundary, but Strategic Management timeflow remains the owner of whether elapsed world-map time can settle.

## Goal

Connect the first Strategic Management timeflow gate to the existing site-detail scene transition boundary: entering the site management scene pauses Strategic Management world time, and returning to the world map resumes it after the return scene is entered.

## Scope

- Add entered callbacks to site-detail and return scene transition requests.
- Invoke the site-detail callback only after the destination site scene is entered, and avoid invoking it when scene change fails.
- Invoke the return callback only after the world-map scene is entered, and avoid invoking it when return scene change fails.
- Wire `StrategicWorldRoot` site entry to pause Strategic Management world time.
- Wire `WorldSiteRoot` return-to-map to resume Strategic Management world time.
- Add regression coverage for callback timing, failure behavior, and Presentation wiring.

## Non-Goals

- Do not add automatic elapsed-time settlement, Godot `_Process` ticking, timers, or frame-driven Strategic Management time.
- Do not alter battle Runtime, battle preparation, battle bridge contracts, or battle-result settlement.
- Do not replace the legacy strategic world clock in this slice.
- Do not add UI controls for time settlement.
- Do not change city commands, resource production rules, or corps/facility behavior.

## Touched Systems

- `src/Infrastructure/Scenes/SceneTransitionRequests.cs`
- `src/Infrastructure/Scenes/SceneTransitionRouter.cs`
- `src/Presentation/World/StrategicWorldRoot.SiteEntry.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouter.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`

## Tests

Primary scene boundary verification:

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

Use existing scene-transition logs plus Strategic Management timeflow command rejection diagnostics. This slice should not add per-frame logs.

## Manual QA

Optional after automated verification: enter the player city from the large map, confirm the city management screen opens, return to the large map, and confirm the world-map clock resumes normally.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `SceneTransitionSiteVisitRequest.OnEntered` and `SceneTransitionReturnRequest.OnEntered` were missing.
- 2026-06-14: Added entered callbacks to site-detail and return scene transition requests. The callbacks run only after the destination scene entered callback fires; failed scene changes do not run them.
- 2026-06-14: Wired `StrategicWorldRoot` site-detail entry to `StrategicManagementRuntime.PauseWorldTimeForCityManagement` and `WorldSiteRoot` return-to-map to `StrategicManagementRuntime.ResumeWorldMapTime`.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
