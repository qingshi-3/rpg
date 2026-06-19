# Strategic Management Decomposition Follow-Up Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Related implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-core-foundation.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-multi-company-expedition.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-bridge-identity-writeback.md`

## Goal

Split the first Strategic Management command and regression-test files after the current integration push so the long-term codebase does not keep growing around single large files.

## Scope

- Split `src/Application/StrategicManagement/StrategicManagementCommandService.cs` by command family, preserving `StrategicManagementCommandService` as the public command boundary.
- Split `tests/StrategicManagementRegression/Program.cs` into focused regression case files while keeping the same test registrations and coverage.
- Keep the oversized-file guard budgets fixed until the split lands; budgets must not grow.

## Non-Goals

- Do not change Strategic Management gameplay rules.
- Do not change strategic battle bridge contracts.
- Do not add new UI behavior.

## Tests

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Acceptance

- `StrategicManagementCommandService.cs` is below the general 1000-line guard or has a smaller tracked budget tied to a remaining focused follow-up.
- `tests/StrategicManagementRegression/Program.cs` no longer owns the full Strategic Management regression body.
- The oversized-code guard no longer needs Strategic Management-specific large-file allowances.

## Acceptance Evidence

- 2026-06-16: Split `StrategicManagementCommandService` into focused partial files for resources, city/corps commands, expeditions, battle results, and shared command helpers. `StrategicManagementCommandService.cs` now only preserves the public command boundary constructor and shared helper surface.
- 2026-06-16: Split `tests/StrategicManagementRegression/Program.cs` into the registration runner plus focused `StrategicManagementRegressionCases.*.cs` files for state, city/corps, expedition bridge, battle results, dashboard/timeflow, and support helpers.
- 2026-06-16: Removed the Strategic Management-specific oversized allowances from `TargetBattleArchitectureRegression`; the global oversized-code guard now passes without allowances for `StrategicManagementCommandService.cs` or `StrategicManagementRegression/Program.cs`.
- 2026-06-16: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The existing Godot source-generator warning about `GodotProjectDir` remained.
- 2026-06-16: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. Existing nullable warnings and the existing Godot source-generator warning remained.
- 2026-06-16: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
