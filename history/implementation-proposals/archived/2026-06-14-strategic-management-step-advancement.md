# Strategic Management Step Advancement Implementation Proposal

Status: Superseded By `2026-06-14-strategic-management-world-timeflow-boundary.md`

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-core-foundation.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-resource-production-settlement.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`

Strategic time and faction-shared passive resource production belong to the Strategic Management command layer. Presentation, Godot scene nodes, and the legacy strategic world must not become alternate owners for time advancement or production settlement.

## Goal

Add the first explicit Strategic Management step-advancement command. Advancing strategic time should move the durable step counter and settle all currently controlled passive production for the requesting faction through one command result.

## Scope

- Add an `AdvanceStrategicStep(...)` command entry to `StrategicManagementCommandService`.
- Increment `StrategicManagementState.StrategicStep` only after validation succeeds.
- Settle passive production for all player-controlled locations owned by the requesting faction.
- Emit low-noise strategic events for step advancement and resource-site production settlement.
- Expose a retained runtime helper on `StrategicManagementRuntime`.
- Add regression coverage for successful multi-step advancement, enemy-held production exclusion, invalid step rejection, and runtime retained-state mutation.

## Non-Goals

- Do not add automatic ticking, timers, background loops, or Godot process-driven time.
- Do not add UI buttons or presentation controls for ending a step.
- Do not add cross-city transport loss, route disruption, resource caps, or logistics.
- Do not add resource-site upgrades, workers, build queues, or RTS-style harvesting.
- Do not touch battle Runtime, battle bridge, scene transition, or result settlement contracts.

## Touched Systems

- Modify Strategic Management command service and runtime facade.
- Modify `tests/StrategicManagementRegression/Program.cs`.
- Update `gameplay-alignment/implementation-proposals/README.md`.

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

Use existing Strategic Management command logs for accepted and rejected step-advancement commands. This slice should produce one command-level log per explicit command call, not per-frame or timer logs.

## Manual QA

Optional after automated verification: invoke the command through a debug/admin entry or a later accepted strategic flow and confirm controlled resource-site income appears in the dashboard summary.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `StrategicManagementCommandService.AdvanceStrategicStep(...)` and `StrategicManagementRuntime.AdvanceStrategicStep(...)` were missing.
- 2026-06-14: Added command-layer strategic step advancement, global controlled passive-production settlement, command-level strategic events, and retained runtime wrapper.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-14: Superseded by `2026-06-14-strategic-management-world-timeflow-boundary.md` after accepted authority clarified that strategic time is elapsed world-map time, not player-facing steps or turns.
