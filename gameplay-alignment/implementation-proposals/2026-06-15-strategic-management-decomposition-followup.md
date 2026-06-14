# Strategic Management Decomposition Follow-Up Implementation Proposal

Status: Pending

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
