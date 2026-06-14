# Strategic Battle Result Legacy Writeback Retirement Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-bridge-identity-writeback.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-bonefield-reward-and-hero-feedback.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Strategic Management battle results now write location control, expedition status, participant losses, rewards, and feedback through Strategic Management commands. The remaining runtime gap is that `WorldSiteRoot` still calls the old `WorldBattleResultApplier` after successful Strategic Management writeback. That keeps an old strategic settlement authority in the new path and risks double writes to legacy site, army, garrison, and world-clock state.

## Goal

Retire legacy world result writeback from Strategic Management-backed battle results while preserving the existing battle return notice and presentation cleanup flow.

## Scope

- For battle requests carrying a Strategic Management expedition id, apply the result only through `StrategicManagementRuntime.Commands.ApplyBattleResultSummary`.
- Build the post-battle `WorldActionResult` for Strategic Management battles from Strategic Management feedback, not from `WorldBattleResultApplier`.
- Keep `BuildBattleGroupRuntimeReturnNotice`, preparation summary, runtime report summary, battle entity cleanup, and placement reconciliation behavior intact.
- Add a narrow presentation compatibility cleanup for Strategic Management battles that clears resolved legacy `PlayerArmy` attacker/visiting placements and exits legacy site `Wartime` state without changing old ownership, garrison, resources, armies, or world time.
- Add regression coverage that the Strategic Management branch returns before `_worldBattleResultApplier.Apply`.
- Add regression coverage that the Strategic Management branch does not run the old world tick settlement path and keeps only the presentation compatibility cleanup.
- Rename or adjust the existing bridge guard that currently expects Strategic Management writeback before legacy applier so it now expects legacy applier retirement for Strategic Management requests.

## Non-Goals

- Do not delete `WorldBattleResultApplier` globally in this slice.
- Do not replace legacy non-Strategic battle result behavior.
- Do not modify battle Runtime simulation, skills, damage, AI, movement, or target selection.
- Do not remove `BattleStartRequest`, `BattleResult`, `BattleSessionHandoff`, or scene transition migration carriers in this slice.
- Do not change Strategic Management reward, equipment, hero feedback, or battle preparation rules.

## Touched Systems

- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Tests

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Strategic battle result writeback should keep the existing low-noise rejection logs for session/result mismatch and failed Strategic Management commands. The retired legacy branch does not need a new high-frequency log; the returned notice and Strategic Management feedback record are the observable outcome.

## Manual QA

Optional after automated verification: send a Strategic Management expedition to Bonefield, resolve victory or defeat, and confirm the return notice still shows strategic feedback, selected preparation, and runtime battle report summary.

## Acceptance Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed on 2026-06-14.
  - Covers Strategic Management battle result writeback returning before legacy `WorldBattleResultApplier`, while preserving a narrow presentation cleanup for old player-army attacker/visiting placements and legacy site mode exit.
  - Known Godot source-generator warning and pre-existing nullable warnings appeared in the test project.
- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed on 2026-06-14.
  - Confirms Strategic Management battle result, reward, feedback, resource, and elapsed-time behavior remains intact.
  - Known Godot source-generator warning appeared in the test project.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed on 2026-06-14 with 0 warnings and 0 errors.
