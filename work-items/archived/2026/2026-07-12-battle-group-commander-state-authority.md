# Battle Group Commander State Authority

- Status: Completed
- Executor: Codex fixed executor (gpt-5.6-sol / xhigh)
- Verifier: Codex primary independent context
- Created: 2026-07-12
- Updated: 2026-07-12

## Objective

Remediate code-review finding P1-07 by making one battle-group commander state the only Runtime authority for command scope, destination beacon, tactical plan/progression, objective intent, tactical/local-combat region, and related group-level transitions. Runtime actors consume derived execution intent and retain only actor-local action phase plus an explicitly non-authoritative revision/cache when needed.

## Confirmed Discussion Result

The user authorized continuous remediation in the review report's risk order. A1-A4 and B1 are complete; this task is the next isolated batch. Behavior must be invariant when the same battle group contains 1, 2, or N Runtime actors: one accepted group command, one group state transition sequence, and one semantic/report event sequence. Actor multiplicity must not create another commander state or advance the group state more than once per Runtime tick/boundary.

This batch consolidates existing command/beacon/plan/objective mirrors into the existing group-owned tactical/commander state. It does not add product commands or implement regroup/retreat; those remain a later P1-08 batch.

## Authority Impact

Impact classification: **Medium**. No gameplay or durable architecture direction changes. Current `system-design/battle-runtime-architecture.md`, `system-design/battle-command-architecture.md`, and `system-design/battle-group-tactical-region-architecture.md` already require one commander state per battle group and actor-local execution only. Implementation must conform to those accepted contracts; no authority edit is required unless execution discovers a contradiction, in which case set this task to `Needs Discussion` and stop.

## Execution Scope

- Capture exact pre-edit hashes for every touched path while preserving the dirty worktree.
- Add public behavior RED tests for one command applied to otherwise equivalent groups containing 1, 2, and N actors.
- Make group state the sole owner of active command identity/scope, destination beacon, tactical plan/progression, objective intent, and group tactical/local-combat region state used by decisions and events.
- Route command acceptance and state transitions through one group-level mutation boundary.
- Derive actor execution intent from the group state; keep actor-local HP, cooldown, current target, movement/attack/cast phases, and any named non-authoritative revision cache required for execution.
- Ensure decision coordination, plan-state emission, diagnostics, reports, and snapshot/view reads consume group-owned state and emit once per group transition rather than once per actor.
- Remove or explicitly demote actor-level mirrors that can act as competing authority; mismatched stale caches must not silently win.
- Preserve B1 one-participant-to-one-group identity and A1-A4 accepted behavior.
- Run focused Runtime/command regressions, TargetBattle behavior coverage, safe affected project builds, then the final low-concurrency solution build.
- Apply complete `state-machine`, `csharp-godot`, `godot-testing`, and `godot-code-review` guidance to the exact diff.

## Non-Goals

- No regroup, retreat, interrupt, move, attack, or hold production command implementation from P1-08.
- No command-authorization changes from P1-09 beyond preserving its completed behavior.
- No Bridge Active Context/FlowResult or expedition/participant mirror cleanup.
- No UI redesign, scene/resource taxonomy change, content contract work, verification-infrastructure redesign, performance rewrite, or general code slimming.
- No mechanical splitting of large partial classes or unrelated dead-code cleanup.

## Constraints And Exclusions

- Never enter, enumerate, diff, validate, or run `scenes/world/preview/` or standalone Strategic Region Preview chains/tests.
- Do not touch territory artifacts, world workbench, other active task bodies, or unrelated dirty-worktree changes.
- Do not launch, stop, or inspect Godot or user processes.
- Do not run global `git status`, whole-repository scans, staging, commits, branch changes, restores, or cleaning.
- Use `apply_patch` for repository edits and low-concurrency minimal `dotnet` commands.
- Use TDD in the order exact baseline hashes -> public behavior RED -> minimum GREEN -> REFACTOR.
- Do not let multiple write-capable agents or sessions operate on this project concurrently.
- Installed skills required: `state-machine`, `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. Exactly one authoritative commander/tactical state exists per battle group; no actor-owned command, beacon, plan, objective, or tactical-region field can independently determine group behavior.
2. Equivalent 1/2/N-actor groups accept the same command once, follow the same group transition sequence, and emit the same number and identity of group command/plan/report events.
3. Runtime decision coordination advances each group commander state once per intended tick/boundary, independent of actor count; actors consume derived intents and retain only actor-local execution state or named non-authoritative caches.
4. Destination-beacon replacement, plan/objective progression, local-combat entry/exit, return-to-objective behavior, defeat/routing boundaries, and diagnostics read the group owner and do not use actor mirrors as fallback authority.
5. Existing B1 participant/group cardinality, A1 settlement/idempotency, A2 actor/cardinality, A3 authorization, and A4 Strategic Management/map behaviors remain passing.
6. Focused Runtime/command regressions and TargetBattle behavior cases pass, except precisely classified unrelated pre-existing guards; affected WorldSite coverage is build-only if its runner may enumerate the excluded preview path.
7. `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeds.
8. Exact-path whitespace checks succeed; branch remains `main`, HEAD remains `212c69161fabe375d524adf72c13cfe9cf290d79`, and index tree remains `e88edcc7d3485c1210f24c0d195c9fa314940ceb`.
9. The exact diff passes the complete `godot-code-review` checklist with no scoped Critical or Improvement finding unresolved.

## Current Progress Snapshot

Completed:

- User confirmed continuous execution of the review remediation order.
- B1 was independently verified and archived.
- P1-07 evidence and the current Runtime/command/tactical-region authority were reviewed; accepted authority already defines the target ownership.
- Required skills `state-machine`, `csharp-godot`, `godot-testing`, and `godot-code-review` were applied.
- `BattleGroupTacticalStateStore` now owns command, beacon reference/revision, plan progression, engagement rule, objective intent, and existing tactical/local-combat region state once per commander group.
- Actor command/beacon/plan/objective fields are explicitly non-authoritative execution caches and are overwritten from group authority before observations and decisions.
- Destination-command mutation and post-resolution plan transitions run once per group; initial command/plan and plan-state events use commander identity rather than actor identity.
- Public 1/2/4-actor coverage, stale-cache rejection, focused regressions, affected builds, exact-path review, and final solution build passed.

Remaining:

- None. Independent verification passed every acceptance criterion.

## Pause, Blocker, And Resume

- Pause/blocker: None; task completed and independently verified.
- Resume condition: None. Any follow-up requires a separate confirmed work item.
- Resume entry: Not applicable after archival.
- Latest verification: Independent review confirmed group-owned command/plan/beacon/objective authority, stale actor-cache overwrite, 1/2/N event invariance, removal of temporary test filtering, and a successful low-concurrency solution build.

## Execution Record

- 2026-07-12: Fixed executor started. Required project authority and the complete `state-machine`, `csharp-godot`, `godot-testing`, and `godot-code-review` skills were read before implementation. Pre-edit task-document SHA-256: `F52610176DB2D63E08DD88190690CA9E715D9C78C5123EF2B529BEC8EBA4936F`.
- 2026-07-12: Exact pre-edit SHA-256 baseline was captured before each touched path; the new coordinator path was absent:

```text
src/Runtime/Battle/BattleActionController.cs 87C79FE166115F3184D944C67E84319C76C133F3197A8B5399AFA1CF761DA0C8
src/Runtime/Battle/BattleCommitBuffer.cs DAA0396FC13E833554102BB9B235DB7ED47F0220B4719FA91EDF735798EC7DD5
src/Runtime/Battle/BattleDecisionOutcomeApplier.cs A3941BBA9FEADBEB1525BF6F6CD55337AE6D60EEC22403E6791181FAEE2A1F40
src/Runtime/Battle/BattleGroupCommanderTransitionCoordinator.cs ABSENT
src/Runtime/Battle/BattleMovementCommitBoundary.cs 06A3D8457C862AE0B69934559B4DE1485BF0F8C83A74776C517DB2BC07741451
src/Runtime/Battle/BattlePlanStateEmitter.cs 865F57531FEC8A17D3994B8A95F8357B3C3B1682A7920D39326F365B646F83C3
src/Runtime/Battle/BattleRuntimeActor.cs A7E710F5488BB41A9D1DD95CE6B16A53FFB84723DF12F18207A8770A78A4F331
src/Runtime/Battle/BattleRuntimeDecisionPhaseCoordinator.cs DC42DFC18682A6E77FE191B432E76FED11BE3E4D1F3B25DBDB1AC1F8501C6B2B
src/Runtime/Battle/BattleRuntimeDestinationBeaconCommandResolver.cs 4CBEEF0BBCAD97F2B1FBE8DB0E57A39DCFD812087D2F8AAD491A2104B145B456
src/Runtime/Battle/BattleRuntimeResolutionPhaseCoordinator.cs 639F335D6F9349B27E96C75CF558573D91F06BFE8255D04E98F835F2A42B77F9
src/Runtime/Battle/BattleRuntimeSession.cs 5E4C79EBAE1C88D10D288668D044511D43CE32287858545968BA8785892AE6F5
src/Runtime/Battle/BattleRuntimeState.cs 7A351413B408669719762FE2F0EAEDC81631AD9489FE31C6E4A725DB20E5F98F
src/Runtime/Battle/BattleTacticalAreaDiagnosticLogger.cs 5024DE1AE85F151799CED7535305CD582E2AE7872ED1974CDFDF9ACA02F67D0B
src/Runtime/Battle/Tactics/BattleGroupTacticalState.cs ED996F676732BBCE22420A5A63ACFD3511A8C716B5B19F1C39D709C05037075C
src/Runtime/Battle/Tactics/BattleGroupTacticalStateStore.cs F795BC17BBF7B1D4E0129CA3469EC9094157907D549CF9484B76C419808D86FB
tests/TargetBattleArchitectureRegression/TargetBattleCommitBufferRegressionCases.cs 4F4E0A93826FD5DDE0ECB4870F49B7443D426D6636F6F643399CAD3859CF617C
tests/TargetBattleArchitectureRegression/TargetBattleContinuousStepHandoffRegressionCases.cs 080AF6698153C29BBBF3168D84112802710DE3783D9361E94358EEE913FC80FE
tests/TargetBattleArchitectureRegression/TargetBattleEventOrderGoldenRegressionCases.Td002SliceC.cs 9F6F45919A5A8BEC06FDECB1CDC551F5F92FECAAD15DE015F7DE20E5DE67D19A
tests/TargetBattleArchitectureRegression/TargetBattleEventOrderGoldenRegressionCases.Td003.cs CDF79B70560866F88C392AA1831EEE98FA4FF22F74144273F5223E262D20FD2A
tests/TargetBattleArchitectureRegression/TargetBattleEventOrderGoldenRegressionCases.cs DA6D5035E4F5B4BB4A245C60686913BD85B5FED6ABEF58401EC494F50EE23978
tests/TargetBattleArchitectureRegression/TargetBattleLayeredRuntimeRegressionCases.cs 2D0165660C0F134B3DB015DCE86F6CC27433C13710B27B66DE22AC587BC99591
tests/TargetBattleArchitectureRegression/TargetBattleMovementCommitBoundaryRegressionCases.cs 41F25651DFADBC58E3521F5B811C16DA1AF80CC4EA471C97DC4B8709C5E429BD
tests/TargetBattleArchitectureRegression/TargetBattleMovementIntentRegressionCases.cs A87C53B6826B6574D706D99C33B039D8922ABC758B34176B68CB27203EEFD8CD
tests/TargetBattleArchitectureRegression/Program.cs C3B046D9352C9025A434BA983621FB0E8E796FD18589D0CED239E259B70BFD99
```

- 2026-07-12 RED: the new public behavior case failed with `two actors must not duplicate the initial commander plan event: expected=1 actual=2`.
- 2026-07-12 GREEN/REFACTOR: the 1/2/4-actor case passed; stale actor command/beacon/plan caches were overwritten from group authority; complete TargetBattle behavior coverage passed except the intentionally skipped whole-repository size guard that would violate the preview exclusion. Temporary runner filtering was removed and `Program.cs` returned to its exact baseline hash.
- 2026-07-12 validation: AutoBattle Runtime, Strategic Management, and WorldArmy Movement builds and runners passed; TargetBattle and WorldSite projects built; WorldSite runner was not executed; `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 23 pre-existing TargetBattle nullable warnings and no errors.
- 2026-07-12 repository checks: exact-path whitespace checks passed; branch/HEAD/index matched acceptance. Complete `godot-code-review` found no scoped Critical or Improvement item. No Godot/user process inspection occurred.
- 2026-07-12 exclusions held: no read, enumeration, diff, validation, build, or run of `scenes/world/preview/` or standalone Strategic Region Preview; no territory artifact, world workbench, other active task body, ActiveContext/FlowResult, expedition/participant mirror, regroup/retreat, staging, commit, branch change, restore, or clean operation.

### Independent Verification

- The primary independent context did not implement B2 and reviewed the exact task contract, group transition coordinator, plan-state emitter, tactical-state owner, stale-cache behavior test, and temporary-filter cleanup.
- The implementation has one group mutation boundary for command/beacon/plan/objective state; actor fields are explicitly non-authoritative caches synchronized before observation/decision and after grouped transitions.
- Public 1/2/4-actor coverage proves initial plan, command acceptance, commander version changes, transition event identities, and stale-cache rejection are independent of actor multiplicity.
- Executor evidence for TargetBattle behavior, AutoBattle Runtime, Strategic Management, WorldArmy Movement, WorldSite build-only coverage, exact-path whitespace checks, and repository identity was accepted after inspection.
- Independent `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors.
- Complete `state-machine`, `csharp-godot`, `godot-testing`, and `godot-code-review` criteria were applied. Scoped Critical findings: None. Scoped Improvement findings: None.

## Final Result

Completed and independently verified. One authoritative commander/tactical state now owns each battle group's command, beacon, plan, objective, and tactical-region progression; actor multiplicity cannot multiply group transitions or semantic events. Scoped findings: None. Remaining task risk: None.
