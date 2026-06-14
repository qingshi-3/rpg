# Strategic Management World Timeflow Boundary Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposal:
  - `design-proposals/archived/2026-06-14-strategic-world-timeflow-boundary/proposal.md`
- Amends implementation proposal:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-step-advancement.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic time is Sanguo Qunying-style realtime world-map elapsed time. City management, battle preparation, battle execution, dialogue, and reports pause world-map elapsed time by default. Internal settlement granularity must not present itself as a player-facing turn or step loop.

## Goal

Replace the current step-oriented Strategic Management settlement API with elapsed world-map time semantics and add a first in-memory timeflow controller that permits settlement only while world-map time is running.

## Scope

- Rename durable Strategic Management time state from step semantics to elapsed world-time pulse semantics.
- Replace `AdvanceStrategicStep(...)` with `SettleElapsedWorldTime(...)` command semantics.
- Add a Strategic Management timeflow controller that can resume world-map time and pause it for city management or other modal states.
- Expose a retained runtime helper that settles elapsed world time only when the timeflow controller is running.
- Add regression coverage for successful elapsed-time settlement, paused-city rejection, invalid pulse rejection, enemy-held production exclusion, and removal of public step-turn naming.

## Non-Goals

- Do not add Godot `_Process`, timers, automatic ticking, or frame-driven time.
- Do not add UI controls for elapsed-time settlement.
- Do not implement army movement, enemy AI, opportunity refresh, construction timers, training timers, or recovery timers in this slice.
- Do not touch battle Runtime, battle bridge, scene transitions, or battle-result settlement.
- Do not preserve `AdvanceStrategicStep(...)` as a compatibility wrapper.

## Touched Systems

- Modify Strategic Management domain state naming.
- Modify Strategic Management command service and runtime facade.
- Add a focused Strategic Management timeflow controller.
- Modify `tests/StrategicManagementRegression/Program.cs`.
- Update implementation proposal index and superseded step-advancement record.

## Tests

Primary Strategic Management verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Presentation architecture guard:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Use existing Strategic Management command logs for accepted and rejected elapsed-time settlement commands. Paused settlement attempts should fail explicitly with a structured reason and no mutation.

## Manual QA

None required for this slice because it adds no UI and no Godot-driven clock.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `ElapsedWorldTimePulses`, `SettleElapsedWorldTime(...)`, `PauseWorldTimeForCityManagement()`, and `ResumeWorldMapTime()` were missing.
- 2026-06-14: Added elapsed world-map time state, `SettleElapsedWorldTime(...)`, first paused/running Strategic Management timeflow controller, city-management pause/resume runtime helpers, and paused-settlement rejection.
- 2026-06-14: Extended the naming cleanup with a RED check requiring `ProductionPerWorldTimePulse` and rejecting `ProductionPerStep` / `InvalidStepCount`; the Strategic Management regression failed on the old production naming as expected.
- 2026-06-14: Replaced remaining Strategic Management production-rate and dashboard naming from step semantics to world-time-pulse semantics. Production settlement events now report `elapsedPulses`.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
