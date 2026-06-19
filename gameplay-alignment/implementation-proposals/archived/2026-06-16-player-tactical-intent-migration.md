# Player Tactical Intent Migration Implementation Proposal

Status: Accepted - Archived

## Origin And Authority

- Originating design proposal: `design-proposals/archived/2026-06-16-player-tactical-intent-migration/`
- System authority:
  - `system-design/battle-tactical-intent-architecture.md`
  - `system-design/battle-ai-boundary-architecture.md`
  - `system-design/battle-group-tactical-region-architecture.md`
  - `system-design/battle-command-architecture.md`

## Goal

Migrate player-commanded battle-group movement into the same Runtime Tactical Intent path used by enemy movement, while keeping deployment and battle preparation as upstream intent-input workflows.

## Scope

- Generalize AI-named intent DTOs and state fields into side-neutral tactical intent names.
- Convert accepted player `BattleGroupPlan` values into player-sourced tactical intent and selected tactical region state at Runtime start.
- Prefer selected tactical region movement over the old player-only objective-anchor movement path when active tactical intent exists.
- Preserve player intent priority over enemy policy and autonomous fallback.
- Keep player-scoped autonomous fallback available only when no player command region is active.
- Update regression tests and diagnostics to assert player and enemy movement use the same tactical selected-region path.

## Non-Goals

- Do not redesign battle-preparation or deployment UI.
- Do not change Presentation interpolation, animation, selection, hover, or debug drawing.
- Do not replace Runtime movement legality, topology, occupancy, reservations, or local steering validation.
- Do not introduce campaign-persistent strategic AI.
- Do not keep a second player-owned movement authority after the tactical intent path covers player battle plans.

## Touched Systems

- `src/Application/Battle/Snapshots/*`
- `src/Application/Battle/BattleStartRequest.cs`
- `src/Application/Battle/BattleForceRequest.cs`
- `src/Application/Battle/BattleGroupSessionProbeService.cs`
- `src/Runtime/Battle/BattleRuntimeSession.cs`
- `src/Runtime/Battle/BattleAiActionRequestBuilder.cs`
- `src/Runtime/Battle/BattleMovementContinuationPlanner.cs`
- `src/Runtime/Battle/Tactics/*`
- `tests/TargetBattleArchitectureRegression/*`
- `tests/WorldSiteDeploymentCacheRegression/*`

## Test Plan

Targeted RED/GREEN tests:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Deployment/input boundary regression:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final build:

```powershell
dotnet build rpg.sln -maxcpucount:2 -v:minimal
```

## Diagnostics

Runtime tactical-state diagnostics should identify player-sourced tactical intent separately from enemy-sourced intent and autonomous fallback. Movement events for player plan movement should use tactical region reason codes instead of the old player-only objective advance reason when a selected region exists.

## Manual QA

Passed on 2026-06-16. User entered battle and confirmed player movement after migration is smooth: "很丝滑".

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

Notes:

- Player battle plans now seed command-owned tactical regions during Runtime tactical-state initialization.
- Player plan movement with a selected region now emits tactical region movement (`region_fixed_advance`) instead of the old player-only objective movement reason.
- Move-first route-blocking local response remains intact by keeping command-scoped target candidates while using tactical region movement for ordinary objective advance.
- AI-specific intent DTO names were generalized to side-neutral tactical intent plan names.
- Manual visual QA confirmed player unit movement is smooth after entering battle.
