# Hero-Led Light RTS Code Refactor Phase 2 Entry Migration Plan

Status: Entry Migration Complete

## Purpose

Phase 1 proved the target battle architecture beside the legacy battle path:

```text
BattleGroupState
-> BattleStartSnapshot
-> BattleRuntimeSession skeleton
-> BattleOutcomeResult + BattleEventStream
-> SettlementPlan
-> BattleReportRecord
```

Phase 2 migrates the live world/site battle entry toward that target architecture without breaking existing strategic world handoff, site runtime, result writeback, or regression coverage.

## Current Entry Path

The active live path still uses:

```text
StrategicWorldRoot / WorldSiteRoot
-> WorldSiteBattleLauncher
-> BattleSessionHandoff.BeginBattle(BattleStartRequest)
-> WorldSiteRoot.ApplyBattleStartRequest
-> auto battle / legacy BattleResult
-> WorldBattleResultApplier
```

This path remains the user-facing authority until the new battle-group session path can run from the same launch data and produce equivalent safe failure behavior.

## Target Phase 2 Shape

Phase 2 should introduce a battle session boundary that can run in parallel with the legacy handoff:

```text
BattleStartRequest
-> LegacyBattleStartSnapshotAdapter
-> BattleStartSnapshot
-> BattleGroupBattleFlowService.RunMinimalBattle
-> diagnostics / target report record
```

The first phase-2 integration must not replace the player-facing result path. It should prove that the new session flow can be constructed, validated, and failed safely from real launch data.

## Non-Goals

- Do not remove `BattleSessionHandoff` in this phase.
- Do not replace `WorldBattleResultApplier` yet.
- Do not implement the final live light-RTS state machine yet.
- Do not add broad hero/corps content or balance.
- Do not edit authored scenes unless an entry-point test requires a narrow binding fix.

## Task 1: Entry Boundary Tests

Add regression coverage that proves:

- `WorldSiteBattleLauncher` still starts the legacy handoff for existing launch requests.
- The same `BattleStartRequest` can be converted into a `BattleStartSnapshot`.
- A valid converted snapshot can run through `BattleGroupBattleFlowService`.
- Invalid converted snapshots fail through explicit target-architecture rejection, not through partial world mutation.
- New target-session diagnostics do not consume or clear the active legacy handoff.

Recommended test location:

```text
tests/WorldSiteDeploymentCacheRegression/Program.cs
```

Use existing launcher tests as the entry point because they already cover launch rollback and handoff behavior.

## Task 2: Parallel Session Probe

Add an application-layer service that runs the target architecture as a side-channel probe:

```text
src/Application/Battle/BattleGroupSessionProbeService.cs
```

Responsibilities:

- accept a `BattleStartRequest`;
- convert it through `LegacyBattleStartSnapshotAdapter`;
- run `BattleGroupBattleFlowService.RunMinimalBattle`;
- return a diagnostic result object;
- never mutate `StrategicWorldState`;
- never call `BattleSessionHandoff`;
- never replace the legacy `BattleResult`.

The probe exists only to validate the new architecture against live launch data before replacement.

## Task 3: WorldSiteBattleLauncher Hook

Wire the probe into `WorldSiteBattleLauncher` behind a narrow optional dependency.

Rules:

- Legacy handoff remains the player-facing path.
- Probe failure logs diagnostics but does not cancel a valid legacy launch.
- Legacy launch failure still follows the existing rollback behavior.
- Probe success/failure should be test-visible without requiring Godot scene execution.

## Task 4: Verification

Run:

```text
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Acceptance

Phase 2 entry migration is acceptable when:

- existing battle entry behavior still passes current regressions;
- `BattleStartRequest` launch data can exercise the target battle-group flow;
- probe diagnostics are explicit and low-noise;
- no long-term world state is mutated by the probe;
- the next step can choose between replacing result/report handling or expanding the real runtime state machine.

## Completion

Status: Complete

Implemented phase-2 entry migration:

- `BattleGroupSessionProbeService` accepts real `BattleStartRequest` launch data, builds target snapshots, and runs the target battle-group flow.
- `WorldSiteBattleLauncher` runs the probe as a diagnostic side channel while keeping legacy `BattleSessionHandoff` as the player-facing path.
- Probe success and rejection are exposed through `WorldSiteBattleLaunchResult`.
- Probe failure does not cancel, consume, or replace the active legacy handoff.
- Regression coverage proves successful probe flow, rejected probe flow, and unchanged legacy rollback behavior.

The project is now ready to start the smallest business gameplay slice on top of the new architecture. The old handoff/result path still exists as the player-facing runtime until a later replacement phase.
