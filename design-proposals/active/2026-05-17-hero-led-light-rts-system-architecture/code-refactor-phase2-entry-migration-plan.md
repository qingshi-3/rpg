# Hero-Led Light RTS Code Refactor Phase 2 Entry Migration Plan

Status: Live Replacement V0 In Progress

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

The original active live path used:

```text
StrategicWorldRoot / WorldSiteRoot
-> WorldSiteBattleLauncher
-> BattleSessionHandoff.BeginBattle(BattleStartRequest)
-> WorldSiteRoot.ApplyBattleStartRequest
-> auto battle / legacy BattleResult
-> WorldBattleResultApplier
```

This path must not remain the long-term player-facing authority. The live `WorldSiteRoot` start-battle activation is being moved to the battle-group runtime adapter first, while legacy request/result objects remain only as transition adapters until settlement writeback is replaced.

## Target Phase 2 Shape

Phase 2 first introduced a battle session boundary that could run in parallel with the legacy handoff:

```text
BattleStartRequest
-> LegacyBattleStartSnapshotAdapter
-> BattleStartSnapshot
-> BattleGroupBattleFlowService.RunMinimalBattle
-> diagnostics / target report record
```

That probe is complete. The current replacement step moves live start-battle activation to:

```text
BattleSessionHandoff active request
-> WorldSiteBattleGroupRuntimeAdapter
-> BattleGroupBattleFlowService
-> BattleRuntimeState
-> BattleOutcomeResult + BattleEventStream
-> SettlementPlan / BattleReportRecord
-> legacy result adapter only for remaining world writeback bridge
```

The remaining legacy result bridge is not authority. It must be removed when settlement writeback fully owns strategic-state mutation.

The live replacement now includes an autonomous combat v0 inside `BattleRuntimeSession`: runtime actors advance, find opposing corps by faction, apply damage, and terminate only after a side is defeated. Player command intervention remains out of scope for this v0.

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

Status: Probe complete; replacement incomplete

Implemented phase-2 entry probe:

- `BattleGroupSessionProbeService` accepts real `BattleStartRequest` launch data, builds target snapshots, and runs the target battle-group flow.
- `WorldSiteBattleLauncher` runs the probe as a diagnostic side channel while keeping legacy `BattleSessionHandoff` as the player-facing path.
- Probe success and rejection are exposed through `WorldSiteBattleLaunchResult`.
- Probe failure does not cancel, consume, or replace the active legacy handoff.
- Regression coverage proves successful probe flow, rejected probe flow, and unchanged legacy rollback behavior.

Live replacement has started:

- `WorldSiteRoot` no longer resolves player-facing start battle through `WorldSiteAutoBattleAdapter`.
- `WorldSiteBattleGroupRuntimeAdapter` resolves the active launch through target battle-group runtime flow.
- `BattleGroupSessionProbeService` snapshots both player and enemy forces into the target battle-group flow with faction and source-force attribution.
- `BattleRuntimeSession` no longer returns immediate placeholder victory for valid opposed battles; it runs deterministic autonomous movement and corps attacks until player victory or defeat.
- `LegacyBattleResultAdapter` converts runtime actor survival back into bridge `BattleForceResult` records so remaining world writeback consumes runtime facts instead of guessing force losses.
- `BattleStartRequest`, `BattleResult`, and `WorldBattleResultApplier` remain as bridge objects for world writeback and must not receive new business authority.

The project is not yet fully migrated. The next replacement step is moving strategic-state writeback from `WorldBattleResultApplier` to `SettlementPlan` / `StateDeltaSet`, then replacing the bridge result path completely.
