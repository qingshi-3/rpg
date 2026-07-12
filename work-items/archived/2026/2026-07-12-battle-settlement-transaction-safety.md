# Battle Settlement Transaction Safety

Status: Completed
Executor: executor-xhigh (gpt-5.6-sol/xhigh)
Verifier: gpt-5.6-sol/high
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Make Strategic Management battle settlement and battle-entry rollback transactionally safe before later authority consolidation or code slimming. A carried but undeployed reserve must not block settlement or take casualties; launch/scene failure must restore the exact pre-expedition station; and a complete battle result must be durably, idempotently committed before its Bridge Active Context is consumed.

## Confirmed Discussion Result

The user confirmed remediation batch A1 after the independently accepted current-system code review. This task is the first state-safety slice, not a general optimization pass.

The confirmed behavior is:

1. The bridge records the stable deployed/reserve role for every carried strategic participant. Runtime result completeness applies only to deployed participants. Reserve participants are unlocked without fabricated runtime result rows or casualties and return to their recorded rollback station.
2. Expedition creation records each participant's exact valid pre-departure station before clearing `HomeCityId`. Cancellation, Bridge creation failure, and scene-transition failure restore that recorded station for every surviving participant and clear the expedition/carrier association without partial mutation.
3. Battle-result application uses a candidate-state commit boundary: validate and apply to a candidate, durably persist the complete candidate, publish it as current state, and only then consume the matching active context. Persistence failure leaves the live state and context retryable.
4. Durable save uses a same-directory staging file, flushes completed content, and atomically promotes/replaces the live save while preserving a recoverable previous complete version where the platform supports it. A load never treats `State: null` as a new campaign.
5. Battle-result idempotency is keyed by the stable expedition/session/snapshot identity. Replaying the exact already-committed result returns the original successful settlement identity without applying rewards or losses twice. A conflicting result for an already-settled expedition fails explicitly.
6. Active-context publication and consumption compare the expected context/session/snapshot identity. Stale callbacks and mismatched result attempts cannot overwrite or clear another active context.
7. Save schema changes are versioned and migrate the current version incrementally. Existing active expeditions may derive a missing rollback station from `SourceLocationId` only when current state and definitions prove that source was the valid departure city; otherwise recovery fails explicitly rather than guessing.

## Authority Impact

This task adds durable transaction detail without changing player-facing gameplay. Before implementation, update these accepted system documents:

- `system-design/battle-result-settlement-architecture.md`: candidate-state durable commit order, exact replay semantics, failure/retry behavior, and the rule that context consumption follows durable commit.
- `system-design/strategic-battle-bridge-architecture.md`: explicit deployed/reserve participant role, per-participant rollback station, and identity-checked active-context publication/consumption.
- `system-design/strategic-management-system-architecture.md`: persisted settlement identity, save-version migration, exact rollback-station ownership, and no partial mutation on failure.
- `system-design/scene-transition-router-architecture.md`: transition failure invokes the identity-matched Strategic Management/Bridge rollback boundary and cannot directly invent station or settlement state.

No gameplay authority change is authorized. If implementation requires a different player-facing reserve, casualty, stationing, recovery, or retry rule, set this task to `Needs Discussion` and stop.

## Execution Scope

### Participant And Reserve Semantics

- Add the smallest typed participant fields required to retain deployed/reserve role and exact rollback station across Strategic Management, Bridge session/draft, summary, and settlement.
- Require Runtime outcomes only for deployed participants.
- Apply casualty results only to deployed participants.
- Unlock reserve participants with unchanged strength and restore their recorded rollback station.
- Preserve stable hero/corps/participant/session/snapshot identity; do not create synthetic casualty rows for reserves.

### Rollback Safety

- Capture rollback station before expedition dispatch clears `HomeCityId`.
- Validate the complete cancellation/rollback plan before mutating hero, corps, expedition, or carrier state.
- Restore exact station and clear expedition locks for Bridge creation and scene-transition failures.
- Keep failure diagnostics low-noise and include the relevant expedition/context identity.

### Durable And Idempotent Settlement

- Introduce a focused Application orchestration boundary for candidate-state result application, durable save, live-state publication, and context consumption.
- Make persistence failure explicit and retryable; do not leave mutated live state behind.
- Make exact replay return the already-created feedback/settlement identity without duplicate rewards, losses, location capture, or records.
- Reject conflicting replays and mismatched context/session/snapshot identities.
- Replace direct live-file overwrite with staging plus atomic promotion/replacement and recovery of a previous complete save.
- Increment the save version and implement bounded migration for the fields introduced by this task. Reject null, malformed, unsupported-future, or unrecoverable save state explicitly.

### Behavior-Level Tests

- Add RED-first behavior tests to the existing focused C# regression runners; do not add a new test framework in this task.
- Cover one, two, and three carried groups across every non-empty deployed subset, proving reserve zero-casualty unlock and complete cleanup.
- Cover cancellation, Bridge creation failure, and scene-transition failure for exact station restoration and absence of partial mutation.
- Cover persistence failure before promotion, exact replay after durable commit, conflicting replay, stale callback, and mismatched context/session/snapshot.
- Cover save migration from the current version, `State: null`, malformed staging/live files, and complete-old-or-complete-new recovery semantics.

## Non-Goals

- Do not fix battle-group-to-actor cardinality or casualty denominator expansion beyond the deployed/reserve participant rule required here.
- Do not remove legacy `BattleStartRequest`, rebuild the final Snapshot authority, or perform the broader single-authority batch B.
- Do not implement command validator cutover, map-control display cutover, regroup/retreat, skill configuration, or site-map authoring fixes.
- Do not consolidate test runners, split large roots, delete compatibility code, optimize performance, or perform general code slimming.
- Do not change Strategic Region Preview, world-map workbench, territory masks, city-map authoring, or hero-corps manual QA.
- Do not introduce active-battle save/resume.

## Constraints And Risks

- This is a high-correctness-risk persistence and cross-system transaction change. Use `executor-xhigh (gpt-5.6-sol/xhigh)` or an explicitly equivalent fixed execution context.
- Work on `main`; do not create or switch branches.
- Preserve the existing dirty worktree. At task creation the repository is `main` at `212c69161fabe375d524adf72c13cfe9cf290d79`; the accepted review recorded 43 modified, 619 deleted, and 12 untracked entries before this task file. Do not clean, restore, stage, or rewrite unrelated changes.
- Three generated territory outputs and the standalone Strategic Region Preview are known unrelated moving targets. Exclude them unless drift reaches a file in this task's evidence set.
- Existing active work items concern first-city authoring, hero-corps manual QA, and the standalone strategic-region overlay. Do not modify their scope or files.
- Use existing project architecture and focused seams. Do not add a generic unit-of-work framework, database, new dependency, or new test framework.
- Tests must exercise public behavior and injected I/O/commit seams, not assert private method bodies or source-string shapes.
- Run only focused tests first. Any `.NET` build uses `-maxcpucount:2 -v:minimal`; do not start Godot unless a later verification need is recorded and no editor/import activity is competing.
- Non-trivial behavior and failure semantics require concise nearby comments. Treat stale comments as defects.

## Acceptance Criteria

- [x] Accepted authority documents contain the confirmed commit, retry, reserve, rollback, and context-identity contracts before implementation changes.
- [x] Every carried participant has a stable deployed/reserve role and exact rollback-station fact at the authoritative boundary.
- [x] Every non-empty deployed subset of one to three carried groups settles successfully; reserves keep pre-battle strength, unlock, and return to the recorded station.
- [x] Deployed participants still require complete matching Runtime result rows; missing deployed results fail without mutating live state.
- [x] Cancellation, Bridge creation failure, and scene-transition failure restore all valid participant stations and clear locks/carriers without partial mutation.
- [x] Save failure leaves the live strategic state and matching Bridge Active Context retryable and unchanged by the attempted settlement.
- [x] A durable save is always a complete old or complete new document; `State: null`, malformed, unsupported-future, and unrecoverable documents fail explicitly.
- [x] Exact replay of a committed expedition/session/snapshot is an idempotent success returning the existing settlement identity; conflicting replay is explicitly rejected and applies no duplicate effects.
- [x] Stale or mismatched context/session/snapshot callbacks cannot overwrite or clear the active context.
- [x] Save version migration covers the pre-task version and preserves existing valid strategic state.
- [x] Focused behavior tests, affected regression runners, `dotnet build rpg.sln -maxcpucount:2 -v:minimal`, and `git diff --check` pass or unrelated failures are recorded precisely.
- [x] No out-of-scope code, resources, authority, tests, configuration, active tasks, branch, or index state is changed.
- [x] Executor hands off as `Awaiting Verification`.
- [x] An independent verifier checks every criterion before `Completed` and archive.

## Skills Used

- `save-load`: versioned JSON persistence, explicit I/O failure, stable identity, and migration guidance. Project authority controls the concrete transaction boundary.
- `godot-testing`: RED-GREEN-REFACTOR and behavior-level fault-injection guidance. The existing C# regression executables remain the scoped test harness.
- `godot-code-review`: final C# Godot boundary review for static runtime ownership, lifecycle cleanup, failure semantics, and non-hot-path behavior.

## Current Progress Snapshot

### Completed

- User confirmed A1 as the first remediation slice.
- Read the repository bootstrap, work-item lifecycle, accepted Bridge, Settlement, Strategic Management, and Scene Transition authority.
- Read `save-load`, its JSON/version-migration references, `godot-testing`, and its TDD/testing-pattern references.
- Recorded the confirmed architecture and verification contract in this task.
- Executor `executor-xhigh (gpt-5.6-sol/xhigh)` completed the mandatory bootstrap and began execution on `main` at `212c69161fabe375d524adf72c13cfe9cf290d79`.
- Scoped production and regression files were clean at execution start and remained hash-stable across the baseline inspection. Existing unrelated dirty-worktree changes were preserved.
- Baseline overlap: the Settlement, Bridge, and Scene Transition authority files already contained unrelated workflow-wording edits replacing retired proposal language. Those edits predate A1 and must be preserved; Strategic Management authority was clean. No active scoped-file writer or authority contradiction was found.
- Synchronized the four accepted authority documents with the confirmed A1 contracts: deployed/reserve completeness, exact rollback station, candidate-save-publish-consume order, exact replay/conflict semantics, atomic versioned save recovery, and identity-matched active-context/transition rollback.
- Completed RED-GREEN implementation through the existing focused regression executables. Added public-behavior coverage for every non-empty deployed subset of one to three carried groups, complete rollback planning, carrier cleanup, persistence fault injection, exact/conflicting replay, save migration/recovery, and stale/mismatched active-context identities.
- Implemented typed participant role and rollback-station facts, deployed-only Runtime completeness/casualties, reserve unlock/return, focused battle-entry rollback coordination, candidate-state durable settlement, atomic staging/promotion/recovery, save version 2 migration, committed settlement identity, and identity-locked active-context publication/commit/consumption.
- Completed REFACTOR and scoped diff review. Final launch now freezes roles only after a valid non-empty deployed set; exact replay also fingerprints the full result payload; the settlement boundary independently revalidates complete Runtime/Settlement/Report facts; production code exposes no test-only context reset.
- Completed all required validation and recorded unrelated shared-worktree failures precisely.
- Resumed after independent verification, re-read the required bootstrap, named authority, and `save-load`, `godot-testing`, and `godot-code-review` instructions, and confirmed the defect is confined to the production settlement commit boundary.
- RED public-behavior regression now replays through `StrategicBattleSettlementCommitService.Commit`: the focused project built with 0 warnings/errors, 94 other existing behaviors plus the new uncommitted-context guard passed, and exact replay alone failed with `active_battle_context_mismatch` after the first commit consumed Active Context.
- GREEN: the production commit boundary now resolves a persisted expedition settlement through the existing full-payload replay check before requiring transient Active Context. Exact replay returns the original feedback identity without save/publish/effect duplication; conflicting session, snapshot, or payload returns `battle_result_conflict`; new settlement without matching Active Context still returns `active_battle_context_mismatch`.
- Focused Strategic Management regression passed 96/96. Affected builds and the solution build passed with 0 warnings/errors; World Army passed 8/8. The World Site and Target Battle runners retained only their two already-recorded unrelated failures, with both oversized-file hashes still equal to baseline.
- Applied `godot-code-review` to the small Application/test diff with no critical or improvement findings requiring changes. `git diff --check` passed, branch remained `main`, the index remained untouched, and Godot was not started.

### Remaining

- None.

## Pause Or Blocker

- Current pause/blocker: None. Independent verification passed and the task is complete.
- Resume condition: None.
- Resume entry: None.
- Stop condition: Any required player-facing rule change, authority contradiction, or need to expand into batches B-E sets `Needs Discussion` and stops mutations.

## Latest Verification

### 2026-07-12 Independent Verification After Scoped Replay Repair - gpt-5.6-sol/high

- Verdict: Passed every acceptance criterion. The task is `Completed` and archived under `work-items/archived/2026/`.
- Reviewed the full scoped diff and applied `save-load`, `godot-testing`, and `godot-code-review`. The production `StrategicBattleSettlementCommitService.Commit` path resolves a persisted settlement before transient Active Context validation, delegates exact identity plus full-payload comparison to the existing command replay boundary, returns the original feedback identity, and does not save, publish, or reapply strategic consequences on exact replay.
- The public-boundary regression commits once through `Commit`, confirms Active Context consumption, then proves exact replay success; conflicting session, snapshot, and full payload return `battle_result_conflict`; a new uncommitted result without matching Active Context returns `active_battle_context_mismatch`. Live state and durable save remain unchanged in every replay rejection.
- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner passed 96/96.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner passed 280/281. The sole failure is unrelated: the shared dirty worktree deletes baseline-tracked `design-proposals/active/README.md`, so `site map layout authority is accepted` cannot open that retired path.
- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner retained only the unrelated oversized-file guard naming `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `src/Application/StrategicManagement/StrategicManagementRules.cs:1088`. Their current Git object hashes exactly match baseline `212c69161fabe375d524adf72c13cfe9cf290d79` (`5afd113ab26e137ae4115f61daae48ffc486093e` and `1f471a89edcb48290e54267c9d0f633145493c59`).
- `dotnet build tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner passed 8/8.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; `git diff --check` passed. Branch remained `main` at the baseline commit, the index remained untouched, and Godot was not started.

### 2026-07-12 Scoped Repair Executor Validation - executor-xhigh

- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner passed 96/96.
- The public regression commits once through `StrategicBattleSettlementCommitService.Commit`, confirms matching Active Context consumption, then replays through the same production boundary. Exact replay returns the original feedback identity without invoking publish, rewriting the durable save, or changing live state.
- The same public boundary rejects conflicting session, snapshot, and full payload with `battle_result_conflict`, and rejects a new/uncommitted result without matching Active Context with `active_battle_context_mismatch`; neither case mutates or persists state.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner retained only the unrelated dirty-worktree failure caused by deleted `design-proposals/active/README.md`.
- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner retained only the unrelated baseline oversized-file guard. Both named file hashes exactly match baseline `212c69161fabe375d524adf72c13cfe9cf290d79`.
- `dotnet build tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner passed 8/8.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors. `git diff --check` passed.
- `godot-code-review` found no critical or improvement issue in the small repair. Confirmed `main`, no staged changes, no unrelated scoped mutation, and no Godot editor/runtime launch.

### 2026-07-12 Independent Verification - gpt-5.6-sol/high

- Verdict: Failed one scoped acceptance criterion and returned the task to `In Progress`; no production or test fix was attempted.
- Scoped defect: `StrategicBattleSettlementCommitService.Commit` requires `StrategicBattleActiveContextStore.TryPeek` before candidate command application. The first successful commit consumes that context through `TryCommitAndConsume`, so an exact replay through the production entry (`StrategicManagementRuntime.CommitBattleResult`) returns `active_battle_context_mismatch` instead of the original successful settlement identity. The passing replay test calls `StrategicManagementCommandService.ApplyBattleResultSummary` directly after commit and therefore does not exercise the production orchestration path.
- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; its runner passed 95/95.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors in the current incremental build; its runner passed 280 tests and retained only the unrelated dirty-worktree failure caused by deleted `design-proposals/active/README.md`.
- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors in the current incremental build; its runner passed 364 tests and retained only the unrelated baseline oversized-file guard for `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1088`. Both file hashes exactly match baseline `212c69161fabe375d524adf72c13cfe9cf290d79`.
- `dotnet build tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj -maxcpucount:2 -v:minimal` passed; its runner passed 8/8.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors; `git diff --check` passed.
- Confirmed `main` at `212c69161fabe375d524adf72c13cfe9cf290d79`, with no staged changes and no Godot editor/runtime launch.

### Executor Handoff Evidence

- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` passed; `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj --no-build` passed 95/95.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` passed with 17 pre-existing nullable warnings; its runner passed 280 tests and had one unrelated failure because the shared dirty worktree already deletes `design-proposals/active/README.md` (`site map layout authority is accepted`).
- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` passed with 23 pre-existing nullable warnings; its runner passed 364 tests and had one unrelated pre-existing oversized-file guard failure naming `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1088`.
- `dotnet build tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj -maxcpucount:2 -v:minimal` passed; its runner passed 8/8.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- `git diff --check` passed.
- Godot editor/runtime was not started because behavior-level C# evidence was sufficient.

## Execution Record

### 2026-07-12 — Task Initialization

- Main discussion context created this self-contained task after user confirmation.
- Only this task file was added. No authority, code, resource, test, configuration, branch, index, or external state changed during initialization.

### 2026-07-12 — Executor Baseline

- Set the task to `In Progress` after reading every mandatory bootstrap, authority, skill, JSON/version-migration, and TDD/testing-pattern input in full.
- Confirmed `main` at `212c69161fabe375d524adf72c13cfe9cf290d79`; did not create or switch branches and did not touch the index.
- Scoped code and regression files were clean and stable. Recorded the three pre-existing authority wording overlaps above; no conflicting scoped drift or competing writer was detected.

### 2026-07-12 — Authority Synchronization

- Updated only the confirmed durable A1 contracts in the four named accepted system documents before implementation.
- Preserved the pre-existing unrelated workflow-language edits in the three already-dirty authority files.
- No player-facing gameplay rule or out-of-scope architecture decision was introduced.

### 2026-07-12 — RED-GREEN Implementation

- RED: `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` failed on the deliberately missing save I/O seam before implementation.
- GREEN: Strategic Management focused runner passed 92 tests before the final identity/missing-deployed additions; the next focused run will record the final count.
- Affected World Site runner passed 280 tests; its only remaining failure was the unrelated pre-existing dirty-worktree deletion of `design-proposals/active/README.md` used by `site map layout authority is accepted`.
- No Godot editor/runtime was started.

### 2026-07-12 — REFACTOR And Validation

- Applied `godot-code-review` to the scoped C# diff and corrected final-launch role freezing, full-result replay fingerprinting, orchestration-level completeness validation, stale-context cleanup without a test-only production API, and malformed-state guards.
- Confirmed the final scoped file set contains only the four named authority documents, A1 Domain/Application/Infrastructure/Presentation code, the two existing affected regression executables, and this task. No resource, config, other active task, branch, or index mutation was made.
- Ran the commands and obtained the results recorded in `Latest Verification`.

### 2026-07-12 - Independent Verification

- Verifier `gpt-5.6-sol/high` read the full task, named authority, `save-load`, `godot-testing`, `godot-code-review`, and the task-named JSON, version-migration, TDD, and testing-pattern references.
- Independently reviewed the scoped baseline diff and reran all handoff commands with low-concurrency builds without starting Godot.
- Confirmed the two runner failures are unrelated baseline/worktree failures, then found the production exact-replay defect recorded in `Latest Verification`.
- Returned the task to `In Progress`. Only this work item was updated; no production, test, authority, resource, config, branch, index, or unrelated file was changed.

### 2026-07-12 - Scoped Replay Repair RED

- Resumed only the verifier-recorded defect on `main` without starting Godot or touching the index/unrelated dirty files.
- Replaced the command-only replay assertion with public behavior through `StrategicBattleSettlementCommitService.Commit`, covering exact identity plus full payload, conflicting snapshot, conflicting payload, durable save stability, no duplicate publish, and consumed Active Context.
- Added a public-boundary guard proving a new/uncommitted settlement still rejects when matching Active Context is absent.
- RED evidence: focused build passed with 0 warnings/errors; the runner failed only the exact production replay with `active_battle_context_mismatch`, while all other 95 behaviors passed.

### 2026-07-12 - Scoped Replay Repair GREEN And Review

- Added the minimal production branch that recognizes a persisted settlement by expedition before transient Active Context validation, then delegates exact identity and full-payload comparison to the existing command replay implementation against an isolated candidate.
- Preserved the normal candidate-save-publish-consume sequence for every uncommitted settlement; no fallback, new authority, save schema, or gameplay rule was added.
- GREEN public behavior covers exact replay after Context consumption, conflicting session/snapshot/payload, unchanged live state and durable save, no duplicate publish, and the uncommitted Context requirement.
- Applied `godot-code-review` and completed the low-concurrency validation recorded in `Latest Verification`.

### 2026-07-12 - Independent Verification After Scoped Replay Repair

- Fresh verifier read the required bootstrap, authority, full task, and named skill instructions and references; inspected the full scoped diff and production replay path; and reran every handoff command at the required low concurrency.
- Verified exact committed replay after Active Context consumption, original settlement identity, no duplicate save/state/publish effects, explicit session/snapshot/payload conflicts, and the uncommitted-context guard.
- Confirmed the two remaining runner failures are unrelated dirty-worktree/baseline conditions with the precise evidence recorded in `Latest Verification`.
- Set the task to `Completed` and archived it. Only this lifecycle document changed during verification.

## Verification Handoff

Independent verification completed. No further handoff remains.

## Final Result

Disposition: Completed and archived under `work-items/archived/2026/`.

Independent verification confirms that exact committed replay succeeds through the production candidate-state commit boundary after Active Context consumption and returns the original feedback identity without duplicate strategic effects, save, or publish. Conflicting session, snapshot, or full payload fails explicitly, and an uncommitted settlement still requires matching Active Context. Every other acceptance criterion remains satisfied.

Remaining risks: None within task scope. The two unrelated shared-worktree runner failures remain as recorded above.

Follow-up work: None required by this task.
