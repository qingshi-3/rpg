# Strategic Battle Active Context CAS

- Status: Completed
- Executor: executor-xhigh
- Verifier: Codex primary independent context
- Created: 2026-07-12
- Updated: 2026-07-13

## Objective

Remediate P2-03 by making Strategic Battle Active Context publication, snapshot advancement, result publication, failure cleanup, and final consumption use one immutable context/session/snapshot/result revision token with compare-and-set semantics.

## Confirmed Discussion Result

The user authorized continuous remediation in the code-review risk order. P2-14 is completed at main HEAD `955605a754950cb3d7c8c861a07bd426c76e5a7f`. Existing identity checks and durable settlement replay remain, but callers still retain a mutable Active Context object and reconstruct expected identity from its current fields. This batch introduces a store-owned or context-owned monotonic revision lease captured at each accepted boundary. Stale callbacks must use the lease they originally received and cannot adopt later snapshot/result identity by rereading the same mutable object.

## Authority Impact

Impact classification: **Medium / high correctness risk**. No gameplay rule changes. `system-design/strategic-battle-bridge-architecture.md`, `system-design/battle-result-settlement-architecture.md`, and `system-design/scene-transition-router-architecture.md` already require identity-matched publication, cancellation, retry, and consumption. No authority edit is required unless implementation finds a contradiction.

## Execution Scope

- Capture exact baseline and touched-path hashes before edits.
- Add public behavior RED tests for stale publication/failure/result/consume interleavings and exact retry.
- Introduce one immutable Active Context CAS token containing context ID, session ID, snapshot ID, monotonic revision, and accepted result identity when present.
- Return/capture the token at successful begin/peek and require the expected token for mutation, clear, and consume operations.
- Advance revision atomically when the authoritative final Snapshot replaces the preparation seed and when the result envelope is first accepted.
- Prevent callers from reconstructing an expected token from later mutable Context fields; scene-transition failure must clear only with the token captured when that transition began.
- Make result publication reject stale snapshot/revision, duplicate callbacks, and mismatched result identity before partial mutation.
- Make settlement commit/consume compare the exact accepted result token; preserve durable exact replay after the transient context is consumed.
- Preserve P2-14 single result envelope, A1 transaction ordering, Draft/Snapshot authority, participant cardinality, map authority, commander authority, and rollback semantics.
- Add low-noise diagnostics naming expected/current identity and revision on CAS rejection without per-frame logging.
- Run focused Strategic Management, scene-transition/build-only WorldSite, TargetBattle safe coverage, low-concurrency solution build, exact-path whitespace checks, and complete review.

## Non-Goals

- No new save/resume format for live battle contexts.
- No expedition/world-army/participant mirror retirement.
- No P2-02 result-summary expansion or P1-08 regroup/retreat commands.
- No UI, scene, resource, preview, content, or general code-slimming work.
- No replacement of the already accepted durable settlement replay boundary.

## Constraints And Exclusions

- Ignore `scenes/world/preview/` and do not run a runner that enumerates it; WorldSite may be build-only.
- Do not launch, stop, or inspect Godot/user processes.
- Work on `main`; do not create/switch branches, stage, or commit inside the executor.
- Use `apply_patch`, RED-GREEN-REFACTOR, low-concurrency minimal builds, and one fixed write executor.
- Do not hold the store lock across arbitrary external persistence or publication callbacks if a safer reservation/commit protocol is required; preserve retryability and exact-once effects.
- Any required change to accepted authority or persistence semantics returns this task to `Needs Discussion` before implementation continues.
- Required skills: `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. Every accepted Active Context state has a monotonic immutable CAS token; snapshot and result advancement produce a new revision.
2. Begin is idempotent only for the exact accepted object/token and cannot overwrite a different active identity.
3. A stale scene-transition failure, cached scene callback, or cancellation token cannot clear or roll back a newer revision, even when it holds the same mutable Context reference.
4. Final Snapshot publication and result-envelope publication compare the expected revision and identity before mutation; rejected operations leave Context/store state unchanged.
5. Duplicate result return before commit fails or returns the same accepted identity without republishing; exact durable replay after consumption still returns the original settlement identity without duplicate effects.
6. Settlement consume requires the exact context/session/snapshot/result revision token and occurs only after durable save and live publication; failure leaves the accepted context retryable.
7. Interleaving tests cover old callback after snapshot advancement, old callback after result publication, wrong revision, duplicate result return, persistence failure/retry, exact committed replay, and different-result conflict.
8. Prior A1-A4, B1-B2, P2-14 and scene-transition stale-callback behavior remains passing.
9. Safe affected builds and `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeed; no excluded preview runner is executed.
10. Exact diff passes whitespace/integrity checks and complete `godot-code-review` with no scoped Critical or Improvement finding unresolved.

## Current Progress Snapshot

Completed:

- P2-03 evidence, current store/settlement/router code, and accepted authority were reviewed.
- Existing protection was classified: exact context/session/snapshot comparison and durable replay exist, but no captured revision lease protects mutations of the same Context object.
- Clean baseline recorded at `main` HEAD `955605a754950cb3d7c8c861a07bd426c76e5a7f`.
- The external Codex quota reset condition was confirmed satisfied by the user.
- Mandatory authority, active-work routing, and the required `csharp-godot`, `godot-testing`, and `godot-code-review` skills were read in full.
- Execution bootstrap passed on `main` at exact HEAD `10910fdc1241f7322a685a12f22152b193a14dc6`; exact-path inspection found no pre-existing local diff in the intended existing paths.
- Behavior-first RED tests were added for same-reference stale callbacks, snapshot/result revision advancement, wrong revision, exact duplicate result identity, different-result conflict, and unchanged-state rejection.
- RED command: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -p:BuildInParallel=false -v:minimal` exited `1`; the main project built, and the test project failed only because `StrategicBattleActiveContextToken`, `TryAdvanceSnapshot`, CAS `TryPublishResultEnvelope`, and the explicit result-conflict contract did not exist.
- GREEN introduced the immutable context/session/snapshot/revision/result token, CAS-only snapshot/result/clear/consume boundaries, and a reservation protocol that executes persistence and live-publication callbacks outside the global Store lock.
- GREEN Strategic Management command: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -p:BuildInParallel=false -v:minimal` passed all registered cases, including persistence and publication failure retry, exact durable replay, different-result conflict, A1 ordering, prior participant/cardinality/result authority, and the new revision interleavings.
- Safe scene-transition command: `dotnet run --project tests\SceneTransitionCasRegression\SceneTransitionCasRegression.csproj -p:BuildInParallel=false -v:minimal` passed 3/3 without resource-directory enumeration or Godot process startup.
- WorldSite command was build-only as required: `dotnet build tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` succeeded; its runner was not executed.
- Final read-only review found a partial-publication replay leak plus CAS hardening gaps around result-summary authority, result clearing, digest integrity, duplicate Snapshot publication, participant-role atomicity, final reservation recheck, and captured-token propagation.
- Behavior-first review RED command: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -p:BuildInParallel=false -v:minimal` exited `1` with exactly five new behavior failures: wrong-token durable replay, summary/result-envelope divergence, duplicate final Snapshot advancement, null-participant partial mutation, and post-publication result mutation remaining token-valid.
- Review GREEN now binds settlement to a summary re-derived from the accepted P2-14 envelope, consumes exact still-active durable replay only with the exact result token, prevents generic accepted-result clear, validates the canonical result digest at every Store CAS, rechecks complete Store state after external callbacks, and makes Snapshot/participant updates fail before mutation.
- Review GREEN Strategic Management command passed all registered cases, including the new throw-after-live-publication replay, wrong result token callback suppression, actual result revision advancement, divergent-summary rejection, digest mutation rejection, accepted-result clear rejection, duplicate Snapshot rejection, and null-participant atomicity cases.
- A fresh executor-level conformance audit was run under the explicitly requested `executor-xhigh` profile (`gpt-5.6-sol` + `xhigh`). This context exposed no separate runtime metadata field beyond that fixed invocation, and no buffered `ultra` metadata from the prior CLI profile was relied on. The audit re-read the exact scoped implementation and evidence, found no authority or persistence-semantic conflict, and found no scoped defect requiring production or test changes.
- The fresh audit independently traced the immutable context/session/snapshot/result revision lease across begin/peek, final Snapshot publication, P2-14 result-envelope publication, stale clear, and settlement consumption. It confirmed same-reference stale callbacks retain their captured predecessor token, deterministic result identity distinguishes exact duplicate from conflict, exact durable replay remains idempotent, and reservation/commit/release keeps arbitrary persistence/publication callbacks outside the global Store lock.
- The fresh `csharp-godot`, `godot-testing`, and `godot-code-review` reviews completed with no unresolved scoped Critical or Improvement. Scene-transition and WorldSite call chains preserve the captured token; Draft/Snapshot, participant cardinality, map/commander, A1 ordering, retry, rollback, and safe TargetBattle filtering remain conformant.

Remaining:

- None.

## Execution Baseline

Pre-edit SHA256 or explicit new-path baseline at `main` HEAD `10910fdc1241f7322a685a12f22152b193a14dc6`:

| Intended path | Baseline |
| --- | --- |
| `work-items/active/2026-07-12-strategic-battle-active-context-cas.md` | `d043b1c281cd357600770e081958e5eea83d08e1942b2686df9f6d5cff0cd081` |
| `src/Application/StrategicBattleBridge/StrategicBattleBridgeModels.cs` | `364d5c3c68e172f6c4513036c5601e1b594d3949327ce6e47c09efe5cb817099` |
| `src/Application/StrategicBattleBridge/StrategicBattleActiveContextStore.cs` | `66705b4e04220a14b607040f672b520c783853e95ae2d820cbbc88df55481f40` |
| `src/Application/StrategicBattleBridge/StrategicBattleDraftSnapshotCompiler.cs` | `a52a31ac51a2f54afd5806d62c788769fb3a3fd9547860a7fb4824edb7d11be8` |
| `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs` | `75f90c2739f663f1adac4c684bebfd6c95d991635227b3fd0f498e6e75ca483d` |
| `src/Application/StrategicManagement/StrategicBattleSettlementCommitService.cs` | `d566bd187c672dd716c7a5b387986666d368928b02618d2ca42c690f29b16886` |
| `src/Application/StrategicManagement/StrategicManagementRuntime.cs` | `a7abd10f71e017596802ccee44de9f0702c4a317e08c650603b1db80a5fe82cd` |
| `src/Infrastructure/Scenes/SceneTransitionRouter.cs` | `2ff6208dda6922b0073896355a9613c0e8c5eb898f563134d1dcb9f1b709a687` |
| `src/Presentation/World/Sites/WorldSiteRoot.cs` | `07dcdd38b59ec80aa934d5ccd75d683a8ab53572e808e6dd4ca84eaa1f6433c6` |
| `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs` | `5c7b78f36926b84dae00de12af90953fecd65190fca9518878a3b80cf1e86131` |
| `tests/StrategicManagementRegression/Program.cs` | `5657481855c24023943e15e4fd72f148ad2de571fc109761f2bc85283fed435a` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.TransactionSafety.cs` | `7dd45f37c477c8efcf25194e00c54200e3757954fb7e13e5ce531bf69095cea1` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.Support.cs` | `a09db1089a73e84a09cf57f01314940048bed483b3a0e9436ae9ab0ba25ff04f` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleResults.cs` | `0cad5c68a3dd7810967e861b3de4f28ed45152cdb4e727cdb2d2a01f793725bf` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleGroupCardinality.cs` | `e859e2c56abd5828d7881809ff55aeb752f9430ac40d0bd14aceba9aa55429a3` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.ExpeditionBridge.cs` | `000ffeab8c4321da25bb7ccd3053055754fe4c3d1ddc2f4b5df1c3c009f9926b` |
| `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouter.cs` | `2b75f2ee4d07c402710532467ed5419389a63e362fd1ef2b82f5f3b92b20465c` |
| `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleSkillRuntimeRepair.cs` | `3732f56f63fb7bc9279f8a7aae3878c737e559fdb68055426f29512e66685cc7` |
| `tests/TargetBattleArchitectureRegression/Program.cs` | `c3b046d9352c9025a434ba983621fb0e8e796fd18589d0ced239e259b70bfd99` |
| `tests/SceneTransitionCasRegression/SceneTransitionCasRegression.csproj` | `MISSING` |
| `tests/SceneTransitionCasRegression/Program.cs` | `MISSING` |

## Pause, Blocker, And Resume

- Pause/blocker: None; task completed.
- Resume condition: Satisfied on 2026-07-13 by explicit user confirmation that the quota reset condition had passed.
- Resume entry: None.
- Latest verification: Independent verification passed all acceptance criteria: Strategic Management 113/113, SceneTransition CAS 3/3, safe TargetBattle 5/5, WorldSite build-only with 0 errors, solution build with 0 warnings/errors, exact-path hygiene, and complete scoped code review.

## Execution Record

- 2026-07-12: Primary discussion context confirmed the remaining defect is mutable-identity reconstruction rather than absence of all identity checks. No authority contradiction was found.
- 2026-07-12: Attempted to start the required fixed `executor-xhigh` (`gpt-5.6-sol`, `xhigh`), but the local Codex execution service returned its usage-limit error before repository work began. The task was marked `Blocked` with an exact resume condition; no code, test, authority, preview, scene, or resource file changed.
- 2026-07-13: The user confirmed the external quota reset condition was satisfied. The fixed executor completed mandatory bootstrap, verified `main` at exact HEAD `10910fdc1241f7322a685a12f22152b193a14dc6`, enumerated the exact intended scope, captured every existing-path SHA256 and both new-path `MISSING` baselines, and legally transitioned `Blocked -> Ready` without combining execution start.
- 2026-07-13: After the separate Ready checkpoint was recorded, the fixed executor legally transitioned `Ready -> In Progress`. The first implementation milestone is behavior-first RED coverage; no production file had been edited at this transition.
- 2026-07-13: `godot-testing` RED completed with exit `1` for the intended missing token/CAS APIs after the main project built. A test-only missing namespace import found on the first attempt was corrected before the valid RED rerun and is not counted as defect evidence.
- 2026-07-13: Minimal GREEN completed. Strategic Management and the isolated scene-transition CAS runner passed; WorldSite was build-only. REFACTOR added exact Begin idempotency, explicit A1 ordering coverage, duplicate-result exact identity reuse, and Store-lock concurrency coverage while remaining green.
- 2026-07-13: Safe TargetBattle verification required adding a name-filter entry to its existing runner so the invocation cannot execute its recursive whole-repository source gate. Before touching that later-added intended path, its clean exact-path baseline was captured as SHA256 `c3b046d9352c9025a434ba983621fb0e8e796fd18589d0ced239e259b70bfd99` and added to this evidence.
- 2026-07-13: Read-only review identified a publish-side-effect-then-throw replay leak and further CAS integrity gaps. New behavior tests reproduced five unresolved boundaries before production hardening. GREEN now preserves durable replay while requiring the exact still-active result token, rejects caller summaries divergent from the accepted P2-14 envelope, validates the accepted result digest, forbids result-bearing generic clear, blocks repeat final Snapshot publication, and rechecks participant and Store state atomically.
- 2026-07-13: A new explicitly fixed `executor-xhigh` audit (`gpt-5.6-sol` + `xhigh`) superseded the prior buffered CLI profile ambiguity. Its pre-audit task-document SHA256 was `e63131fad4e29c79ca6aeac65a8cc9cb16d9b2d4bae043bc3eca9357cf45d14d`. It made no production or test correction because the current implementation passed the complete executor conformance review; only this task evidence was updated.
- 2026-07-13: Independent verification accepted P2-03. Strategic Management passed 113/113, isolated SceneTransition CAS passed 3/3, the documented safe TargetBattle filter passed 5/5, WorldSite build-only completed with 0 errors, and `rpg.sln` built with 0 warnings/errors. Exact-path whitespace/scope checks and independent `csharp-godot`, `godot-testing`, and `godot-code-review` review passed with no unresolved Critical or Improvement finding.

## Final Result

P2-03 is independently verified and completed. Active Context lifecycle changes now require one immutable context/session/snapshot/result revision token; stale same-reference callbacks cannot adopt newer identity, external persistence/publication callbacks execute outside the Store lock, failures remain retryable, and exact durable replay remains idempotent. Remaining risks: None. Follow-up mirror retirement and later remediation require separate active work items.

## Verification Handoff

Executor verification completed on 2026-07-13:

- Strategic Management full runner: passed every registered case after the final review fixes. Coverage includes prior A1-A4/B1-B2/P2-14, stale same-reference Snapshot/result callbacks, wrong revision/result token callback suppression, actual Snapshot/result revision advancement, exact duplicate result return, different-result conflict, persistence/publication failure retry, publish-side-effect-then-throw durable replay, exact committed replay, divergent-summary rejection, digest mutation rejection, accepted-result clear rejection, duplicate Snapshot rejection, and participant-role atomicity.
- Safe isolated scene-transition runner: passed 3/3. It covers initial token retention, exact failure clear, and same-reference advanced revision surviving stale scene failure.
- WorldSite project: build-only succeeded with 0 errors and 17 pre-existing nullable warnings. Its preview-enumerating runner was not executed.
- Safe TargetBattle filter: passed 5/5 for invalid handoff, incomplete settlement, invalid complete result/event boundary, report/settlement event identity, and Runtime vertical-slice settlement/report. The recursive repository guard was filtered out.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: succeeded with 0 errors and 23 pre-existing nullable warnings in unchanged TargetBattle test code; no scoped warning was introduced.
- Exact-path `git diff --check`: passed. Exact-path text integrity found no NUL, missing final LF, or trailing whitespace. Scope review found exactly 19 intended modified existing paths and two intended new scene-transition test paths.
- `dotnet build-server shutdown`: completed successfully after verification.

Fresh fixed-profile executor audit on 2026-07-13:

- Strategic Management full runner passed every registered case, including all revision interleavings, retry/replay, P2-14 authority, A1 ordering, cardinality, map/commander, Draft/Snapshot, and rollback coverage.
- Isolated `SceneTransitionCasRegression` passed 3/3; the same-reference advanced revision survived the stale scene failure without rollback.
- `WorldSiteDeploymentCacheRegression` was built only and succeeded with 0 errors and 17 pre-existing nullable warnings; its runner was not executed.
- The documented TargetBattle filter executed exactly the five safe cases and passed 5/5; the recursive repository source guard was not executed.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors in this incremental audit build.
- Exact-path scope, whitespace, text-integrity, touched-file hash, HEAD, and no-staging checks passed after the evidence-only update. The excluded coordinator UID and `scenes/world/preview/` were not read, hashed, diffed, or included in any command scope.
- Status remains **Awaiting Verification**, Verifier remains **Unassigned**, and independent verification is still pending.

Required skill reviews:

- `csharp-godot`: complete. Immutable token value semantics, Godot/C# call-chain propagation, low-noise diagnostics, and Store lock boundaries comply; no scoped Godot lifecycle/API issue remains.
- `godot-testing`: complete. Initial contract RED plus review-driven behavior RED were captured before their production fixes; final focused coverage is green with no unresolved scoped coverage Improvement.
- `godot-code-review`: complete. CAS/concurrency, settlement authority, retry/replay, presentation propagation, and tests were independently read-only reviewed; no scoped Critical or Improvement finding remains unresolved.

Independent verification entry:

1. Re-run the Strategic Management runner and isolated `SceneTransitionCasRegression` runner.
2. Build, but do not run, `WorldSiteDeploymentCacheRegression`.
3. Re-run only the documented five-test TargetBattle filter and the low-concurrency solution build.
4. Recheck the exact touched pathset, hashes, and whitespace; do not inspect the excluded UID or `scenes/world/preview`.
5. If all acceptance criteria hold, set `Completed` and archive according to `work-items/README.md`; otherwise return this task to `In Progress` for scoped defects or `Needs Discussion` for authority/direction conflict.

## Final Touched-Path Evidence

Final executor SHA256 values for code/tests, captured after the final GREEN and reviews:

| Path | SHA256 |
| --- | --- |
| `src/Application/StrategicBattleBridge/StrategicBattleBridgeModels.cs` | `d25ae0804c49442b60533e561ad2d72d2c5c40ebfe0242db2d42241595b31c7e` |
| `src/Application/StrategicBattleBridge/StrategicBattleActiveContextStore.cs` | `f63a43f98e6ebd72ab8645fdeb14907dd80a1dfc29a7036c46d13f81c0a09b97` |
| `src/Application/StrategicBattleBridge/StrategicBattleDraftSnapshotCompiler.cs` | `e653b12a6b2d583199a511540f7559c8585e3bad8899598172b8dac150f86263` |
| `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs` | `48a68f10c0a0f16990d8f4a4f8e34a958cfc641682dd4ca36a05145aa58e55cc` |
| `src/Application/StrategicManagement/StrategicBattleSettlementCommitService.cs` | `a35ae6c801e9a9b0ffcb20965af3c9971eec1ff58eec78c595a89df01190b45e` |
| `src/Application/StrategicManagement/StrategicManagementRuntime.cs` | `1d8fa81119d796eb2a2117e600eb8f83fa73b1fab2ad65e045008cdaf7081b3e` |
| `src/Infrastructure/Scenes/SceneTransitionRouter.cs` | `0475bbfba4825e906fa9fb6807525ac62bad389e2bf5815f67e0cbe53fd6535f` |
| `src/Presentation/World/Sites/WorldSiteRoot.cs` | `98bf1878a2e8ccada854a4fed2f5bbf30e9b6725f57d8950de588bb199c43845` |
| `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs` | `5baba15a929870befdf2d2ee8e66fa0686b6ee48e6bad078bee83966771de676` |
| `tests/StrategicManagementRegression/Program.cs` | `a618af903d9720fd5343debeb57cff7a62eb14fbd9013050556143ee783ebcd1` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.TransactionSafety.cs` | `2be742901fb145d348c5accf451cdd3379b4bfc09fed4929686ce4cd7027d4c9` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.Support.cs` | `23c93204c8c9eac09c36cc26b439a289e9df720cbd21ac1401fcf7fdd23281cb` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleResults.cs` | `7e0480f1c1f122665ad9b54d841beb2d79077ac12cc96a356b499a01239ea7da` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleGroupCardinality.cs` | `d336a0282714252e9368cfbfe499bad6d9a727efe38d0cae8b3fbe886cdd5a01` |
| `tests/StrategicManagementRegression/StrategicManagementRegressionCases.ExpeditionBridge.cs` | `ef74956b0daf1083ab51d38a381e794be29a3a96796289b538c522f3ee154894` |
| `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouter.cs` | `90c7e029903c94b5c2d52ca2a853164aebc6be48512e53129ac05f3b31266335` |
| `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleSkillRuntimeRepair.cs` | `1c53666e5a45e654b7dbf2704e69e86ae489d13ba53d331d0599634ab2692390` |
| `tests/TargetBattleArchitectureRegression/Program.cs` | `87adcf9d1967d0876277a3239aec7962dc67341f3f0dbae8a1d3f4f2763c993d` |
| `tests/SceneTransitionCasRegression/SceneTransitionCasRegression.csproj` | `f930de43cd29308ecc328c75a61bbe6a915fa4b6e7bc1c02d4a4745dd9e98b86` |
| `tests/SceneTransitionCasRegression/Program.cs` | `d2ff9d96a676bc8ce0558bc65bc82a3342da0482dcda3a7a08482ba60136c216` |

The task document is self-referential evidence: its pre-handoff-update SHA256 was `fb1ce94f01fbf73f47ac3468f7828c63088d333a1a7cb9219f6503256172ea73`; the executor handoff reports its exact post-write SHA256 externally.
