# Strategic Battle Active Context Cutover Implementation Proposal

Status: Implemented - Desktop Mono Verification Passed

## Origin And Authority

- Design proposal:
  - `design-proposals/archived/2026-06-14-strategic-battle-active-context/proposal.md`
- System authority:
  - `system-design/strategic-battle-bridge-architecture.md`
  - `system-design/scene-transition-router-architecture.md`
  - `system-design/strategic-management-system-architecture.md`
- Depends on:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-result-legacy-writeback-retirement.md`

## Goal

Cut Strategic Management-backed battle entry, active scene handoff, Runtime completion, and result summary generation over to a Bridge Active Context so legacy `BattleSessionHandoff`, `BattleStartRequest`, and `BattleResult` no longer serve as the active authority for Strategic Management battles.

## Scope

- Add `StrategicBattleActiveContext` and a bridge-owned runtime store for the current Strategic Management-backed battle.
- Let `StrategicBattleBridgeService` create an active context from a `StrategicBattleSession`, route metadata, and a temporary compatibility request projection.
- Route Strategic Management-backed battle scene transitions through the active context instead of `BattleSessionHandoff`.
- Let `WorldSiteRoot` boot battle preparation/runtime from the active context first, while using the compatibility request only as a removable presentation adapter.
- Complete Runtime results into the active context, build `StrategicBattleResultSummary` from context plus Runtime/settlement/report facts, and apply Strategic Management commands without legacy `BattleResult`.
- Preserve non-Strategic legacy battle paths until a later deletion slice.
- Add regression guards that Strategic Management battle paths do not begin, peek, complete, or consume static `BattleSessionHandoff`.

## Non-Goals

- Do not rewrite battle Runtime simulation, actor state machines, AI, movement, damage, skills, cooldowns, or target selection.
- Do not rebuild battle preparation UI resources in this slice.
- Do not delete `BattleStartRequest`, `BattleResult`, or `BattleSessionHandoff` globally while non-Strategic legacy paths still compile.
- Do not change Strategic Management gameplay rules, rewards, resources, corps recovery, or hero progression.
- Do not implement active battle save/resume.

## Touched Systems

- `src/Application/StrategicBattleBridge/*`
- `src/Infrastructure/Scenes/SceneTransitionRequests.cs`
- `src/Infrastructure/Scenes/SceneTransitionRouter.cs`
- `src/Presentation/World/StrategicWorldRoot.BattleEntry.cs`
- `src/Presentation/World/Sites/WorldSiteRoot*.cs`
- `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.*.cs`

## Tests

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Bridge active-context start, scene handoff begin/cancel, Runtime completion, summary application, and rejection paths should log session id, expedition id, target location, snapshot id, and failure reason at low frequency.

## Manual QA

Optional after automated verification: send a Strategic Management expedition to Bonefield, enter preparation, deploy at least one hero company, resolve battle, and confirm the strategic feedback notice appears and the strategic dashboard reflects the result.

## Acceptance Evidence

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Temporary external active-context closure validation:
  - `ACTIVE_CONTEXT_VALIDATION_PASS outcome=Defeat feedback=battle_feedback_0001 participants=1`
- Desktop Godot Mono headless smoke:
  - `C:\Users\qs\Desktop\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe --headless --path D:\godot\rpg --quit-after 180`
  - Exit code `0`; `rpg-20260614.log` recorded Strategic World initialization, Runtime activation, and one world clock settlement without project errors.
- `git diff --check`

Strategic Management-backed battle entry now creates a Bridge Active Context before scene transition. The scene router publishes/cancels that context instead of using `BattleSessionHandoff` for the strategic branch. `WorldSiteRoot` resolves active context before legacy handoff, and strategic battle result writeback is built from active context plus Runtime/settlement/report facts before applying Strategic Management commands.
