# Bridge Draft Final Snapshot Authority

- Status: Completed
- Executor: Codex fixed executor (`gpt-5.6-sol` / `xhigh`)
- Verifier: Codex independent verifier (fresh verification context)
- Created: 2026-07-12
- Updated: 2026-07-12

## Objective

Make the Strategic Battle Bridge compile the final immutable `BattleStartSnapshot` directly from the accepted battle-preparation Draft. A compatibility `BattleStartRequest` may remain only as an explicit outbound adapter/carrier and must never rebuild, replace, overwrite, or reverse-synchronize facts into the final Snapshot.

## Confirmed Discussion Result

This is remediation batch B1 for P1-02 in `gameplay-alignment/reports/2026-07-12-current-system-code-review.md`, executed after the authorized A1-A4 remediations. The final Snapshot must preserve session and Draft lineage explicitly. Deployment subset, formation, initial destination, objective, participant identity, and the actor/cardinality facts protected by A1/A2 come from the accepted Draft. Missing, stale, duplicate, or mismatched lineage fails explicitly without partial mutation, fallback authority, or double synchronization.

The compatibility request is a subordinate outbound projection only. It may carry legacy-shaped data to a remaining consumer, but it owns no final battle facts and cannot write back into `BridgeActiveContext.Snapshot`.

## Authority Impact

Impact classification: **Medium**. Player-facing gameplay and scene/resource taxonomy do not change. The accepted cross-system ownership and failure contract is clarified in `system-design/strategic-battle-bridge-architecture.md` before implementation: the Draft is the final Snapshot compiler input; final Snapshot lineage is explicit; compatibility projection is one-way and cannot replace the Snapshot; lineage inconsistencies fail closed.

No gameplay authority change is required. The implementation must preserve A1 settlement transaction/idempotency, A2 actor cardinality, A3 command authorization, and A4 Strategic Management/map authority.

## Execution Scope

- Record exact pre-edit hashes for every touched path and preserve the dirty worktree.
- Add public behavior tests that prove Draft-to-Snapshot lineage is unique and compatibility divergence cannot overwrite the final Snapshot.
- Compile the final `BattleStartSnapshot` from the accepted Draft and its validated participant/deployment facts.
- Preserve explicit session, Draft, participant, deployment, formation, initial-destination, objective, and A1/A2 cardinality lineage.
- Keep any required `BattleStartRequest` usage as a clearly named one-way outbound compatibility adapter/carrier.
- Reject missing, stale, duplicate, or mismatched lineage explicitly and without partial active-context mutation.
- Run focused bridge, TargetBattle, and StrategicManagement affected regressions, then the final low-concurrency solution build.
- Apply the full `godot-code-review` checklist to the exact B1 diff and record exact-path `diff --check` evidence.

## Non-Goals

- No command-state consolidation.
- No regroup or retreat implementation.
- No UI redesign.
- No scene or resource taxonomy changes.
- No code slimming or unrelated cleanup.
- No changes to A1 settlement transaction/idempotency, A2 actor cardinality, A3 command authorization, or A4 strategic-map authority except compatibility needed to preserve their accepted behavior.

## Constraints And Exclusions

- Never enter or enumerate `scenes/world/preview/`.
- Never inspect or run standalone Strategic Region Preview chains or tests.
- Do not touch territory artifacts, world workbench, other active tasks, or unrelated pre-existing guards.
- Do not launch, stop, or inspect Godot or any user process.
- Do not run global `git status`, whole-repository scans, staging, commits, branch changes, restores, or cleaning.
- Use `apply_patch` for every repository edit.
- Use TDD in the order exact baseline hashes -> public behavior RED -> minimum GREEN -> REFACTOR.
- Use low-concurrency minimal `dotnet` builds and preserve all user and A1/A2/A3/A4 changes already present in the dirty worktree.
- Installed skills applied: `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. The accepted Draft directly compiles the final immutable `BattleStartSnapshot`; no compatibility request path can rebuild or replace it.
2. Snapshot/session/Draft lineage is explicit and validated; missing, stale, duplicate, or mismatched lineage fails without mutating the active Snapshot.
3. Deployment subset, formation, initial destination, objective, participant identity, and A1/A2 cardinality facts in Runtime input match the accepted Draft even when the compatibility projection intentionally differs.
4. Any remaining `BattleStartRequest` is demonstrably outbound-only and owns no final battle fact or reverse synchronization.
5. A1 settlement transaction/idempotency, A2 actor cardinality, A3 command authorization, and A4 Strategic Management/map authority remain passing in focused regressions.
6. Focused bridge, TargetBattle, and StrategicManagement regressions pass, except precisely classified unrelated pre-existing guards.
7. `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeds.
8. Exact-path `git diff --check` succeeds; branch remains `main`, HEAD remains `212c69161fabe375d524adf72c13cfe9cf290d79`, and index tree remains `e88edcc7d3485c1210f24c0d195c9fa314940ceb`.
9. The exact B1 diff passes the complete `godot-code-review` checklist with no scoped critical or improvement findings left unresolved.

## Current Progress Snapshot

Completed:

- Read project rules, authority routing, gameplay battle-preparation authority, accepted bridge architecture, work-item lifecycle, P1-02/B1 remediation order, and all three required skills.
- Confirmed authority is consistent with the B1 direction.
- Captured repository identity: branch `main`; HEAD `212c69161fabe375d524adf72c13cfe9cf290d79`; index tree `e88edcc7d3485c1210f24c0d195c9fa314940ceb`.
- Captured initial hashes for the accepted bridge architecture and known P1-02 implementation paths in the execution record below.
- Synchronized the accepted bridge architecture with final Draft compilation, explicit lineage, outbound-only compatibility projection, and fail-closed mismatch rules.
- Added the first public-behavior contract and observed RED: the current Active Context exposes no independent `PreparationDraft` and still publishes compatibility/final Snapshot state too early.
- Implemented the typed Bridge Draft, preparation seed, final Snapshot compiler, explicit session/Draft/revision lineage, one-time commit, detached outbound compatibility projection, and fail-closed missing/stale/duplicate/mismatched lineage behavior.
- Completed GREEN and scoped refactor: StrategicManagement regression passes in full; WorldSiteDeploymentCache regression project builds; TargetBattle behavior cases pass before its known unrelated oversized-file guard.
- Completed the final solution build, exact-path whitespace checks, repository identity proof, and full B1 `godot-code-review` checklist.

Remaining:

- None. Independent verification passed every acceptance criterion.

## Pause, Blocker, And Resume

- Pause/blocker: None; task completed and independently verified.
- Resume condition: None. Any follow-up requires a separate confirmed work item.
- Resume entry: Not applicable after archival.
- Latest successful verification: independent StrategicManagement regression passed all 104 cases, including B1 lineage/divergence behavior and A1/A2/A3/A4 guards.
- Latest related build: independent WorldSite regression project build and low-concurrency solution build both succeeded with 0 warnings and 0 errors; the WorldSite runner remained intentionally excluded.
- Latest unrelated failure: independent TargetBattle execution passed every behavior case and then failed only `oversized code files are tracked and no new ones are introduced` for pre-existing `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1122`; neither is a B1 path and no new oversized guard appeared.

## Execution Record

### Pre-Edit Identity

- Branch: `main`
- HEAD: `212c69161fabe375d524adf72c13cfe9cf290d79`
- Index tree: `e88edcc7d3485c1210f24c0d195c9fa314940ceb`

### Initial Exact-Path SHA-256

- `system-design/strategic-battle-bridge-architecture.md`: `98b8a64e1667e26ebf4b8911cfc1176672bf82c3037df8f6a8ac7e6c90fc88cd`
- `src/Application/StrategicBattleBridge/StrategicBattleBridgeModels.cs`: `cb45660d2003ffb7a900d42607c517595b57d747e46a260dbb9019ce22f2296d`
- `src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs`: `36b907a6f1285f7584c194b523467a30e0583354eb3628de8811fa8c3b321020`
- `src/Application/StrategicBattleBridge/StrategicBattleLaunchSnapshotSyncService.cs`: `6c100f4714ef39d76963304cd5d657bc8530eef96ea10a0cd497a3be6fc3d6ae`
- `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs`: `234780b8fd18f2b38237a566fbfd8ef6957cd6c8e851b8913c4c29ba2d110708`
- `src/Presentation/World/StrategicWorldRoot.BattleEntry.cs`: `ddab49fbe6e434176a7eaee34730448280e579ad1777bd278a5ed7d92ae8b660`
- `src/Application/Battle/BattleStartRequest.cs`: `2565f31dccd0e770a7af88c57a848ef476cbc15246b99b61ec0984ebfc80c659`
- `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`: `246a45cd32eec6e3ad4818d1b228364bd44be5599d366c0e7b180cc2f9445d24`
- `src/Infrastructure/Scenes/SceneTransitionRouter.cs`: `351ab07a9a508db35cafd1cf1945ae223de7a378c17cbc5eecd4c764bb2f07c7`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`: `5e802e8a447b32e1a64ffbb738bb7569d3f7a0c00d914d0169b89f9338ef46cc`
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.ExpeditionBridge.cs`: `977dc6f4506b8bf91cc45f61f92fe63ee9c4eac526f082831f5cf338df2b6023`
- `tests/StrategicManagementRegression/Program.cs`: `c73f949f8add0b8c44b725cca186e39d5039e82809d9389b43dac9bd48ee6853`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleSkillRuntimeRepair.cs`: `bcf22e7621639b2aae423243e2aeefbef12b6077257db71be2cadd09cea9dfac`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs`: `d9afba2ea60f352982f201f580247c3dbb63aaa2c295537edfd30ad45ba4fc1f`

These implementation and authority paths were already modified in the preserved dirty worktree except `WorldSiteBattleGroupRuntimeAdapter.cs`. B1 changes will be isolated by saved byte-for-byte baselines and exact-path post-edit comparisons rather than by assuming `HEAD` is the task baseline.

### RED

- Command: `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj -p:BuildInParallel=false -v:minimal`
- Result: expected failure after all earlier cases passed.
- New behavior failure: `strategic battle bridge creates active context: type Rpg.Application.StrategicBattleBridge.StrategicBattleActiveContext should expose property PreparationDraft`.
- Interpretation: current production behavior still lacks an independent Draft lineage and therefore cannot satisfy the outbound-only compatibility boundary.

### GREEN And Refactor

- The active context now owns `StrategicBattlePreparationDraft` plus a stable preparation seed; no compatibility projection exists before final compilation.
- The compiler reads deployment subset, placement, formation, initial destination, objective, navigation, participant mapping, and A1/A2 cardinality only from the accepted Draft plus the strategic preparation seed for immutable strategic/skill facts.
- Final Snapshot fields preserve `StrategicBattleSessionId`, `StrategicBattleDraftId`, and `StrategicBattleDraftRevision`.
- The compiler validates Draft object identity, session identity, Draft ID, revision, seed identity, participant mapping, row roles, combat facts, and duplicate finalization before committing.
- Participant roles freeze only after the complete final Snapshot validates; failed missing/stale/mismatched/all-reserve/duplicate mappings preserve the preparation seed and prior roles.
- Outbound `BattleStartRequest` is a detached deep projection created after successful compilation. A deliberate post-launch divergence cannot restart compilation or modify placement, formation, destination, objective, participant, or cardinality facts in the final Snapshot.

### Final Files And SHA-256

- `system-design/strategic-battle-bridge-architecture.md`: `b9e185e0679378bf7b57d95a835c0faae289314599afec78687ca9cc96eaa1b9`
- `src/Application/Battle/BattleStartRequest.cs`: `30d4e74fb566742a372fd4898366bb07542601457ea435307dba2c1abc5649fe`
- `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`: `915232ac0ea3de6b4a61251e4c78c08c9ec64c2ed2b108bba6876ef3438c4aa5`
- `src/Application/StrategicBattleBridge/StrategicBattlePreparationDraft.cs`: `a9f163b25b67e1d5b35e1faf15e66d9078d491dbf53111bd6ffbcad584a6fd4b`
- `src/Application/StrategicBattleBridge/StrategicBattleBridgeModels.cs`: `a24df9165b3af7632c381ec4d237592641f6b904b9c626ae6f32812918c94377`
- `src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs`: `a0fb56e7ff3325d44bd682a8b891f2b63b9aafcd2580aab2e3f6c929ae4549c1`
- `src/Application/StrategicBattleBridge/StrategicBattleDraftSnapshotCompiler.cs`: `c945fc921b1318aa19ec4c36a2aa9fee3d70d549e1fea25fb8655e497a1b9e33`
- `src/Application/StrategicBattleBridge/StrategicBattleLaunchSnapshotSyncService.cs`: deleted; pre-edit SHA-256 `6c100f4714ef39d76963304cd5d657bc8530eef96ea10a0cd497a3be6fc3d6ae`
- `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs`: `3cdc2aa2ff41859eba054860682529c820333b52b4bb4ae4207a5078448061bd`
- `src/Infrastructure/Scenes/SceneTransitionRouter.cs`: `2ff6208dda6922b0073896355a9613c0e8c5eb898f563134d1dcb9f1b709a687`
- `src/Presentation/World/StrategicWorldRoot.BattleEntry.cs`: `9803ebeec45488857e82ab4895908924d9b470b8161a6635fa6d2c9deac1eb72`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`: `07dcdd38b59ec80aa934d5ccd75d683a8ab53572e808e6dd4ca84eaa1f6433c6`
- `tests/StrategicManagementRegression/Program.cs`: `1c933f147c7ff27c0814162ac03b888cc2c3ae9e98aac12fbf2c8b9440f213e0`
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.ExpeditionBridge.cs`: `000ffeab8c4321da25bb7ccd3053055754fe4c3d1ddc2f4b5df1c3c009f9926b`
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.TransactionSafety.cs`: `7dd45f37c477c8efcf25194e00c54200e3757954fb7e13e5ce531bf69095cea1`
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.Support.cs`: `48dbab6d9a5dab86ab7a8d4ea7cd81a8b2efd63501823922ac5edbfe8e02bbf0`
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleGroupCardinality.cs`: `ee6aaadbde98a3fd11be4ded5ec9553714cd407eea5da2e28d65ecc2aae3308a`
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleResults.cs`: `65acadab7b14b9e4daba9637c7aed0dbf7887423032bcd1e608b5648a86355d8`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleSkillRuntimeRepair.cs`: `3732f56f63fb7bc9279f8a7aae3878c737e559fdb68055426f29512e66685cc7`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs`: `19c23e257d67f633f2b21ab3691fd3f8f751361028d90872d4e7501180ef90b0`

`TransactionSafety.cs` and `BattleGroupCardinality.cs` were pre-existing untracked A1/A2 fixture files and remain untracked; B1 changed only their Draft-compiler call/fixture preparation needed to preserve those accepted regressions. Their final hashes are recorded above. The new task, Draft, and compiler files are also untracked by design because staging is forbidden.

### Validation

- RED: StrategicManagement built, passed all prior cases, then failed the new public behavior because `StrategicBattleActiveContext` lacked `PreparationDraft`.
- GREEN/final: `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj -p:BuildInParallel=false -v:minimal` passed all 104 cases.
- TargetBattle: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -p:BuildInParallel=false -v:minimal` passed all behavior cases, then exited 1 only on the unrelated pre-existing oversized guard for `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1122`; neither is a B1 path.
- WorldSite affected compile: `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` succeeded with 17 pre-existing nullable warnings and 0 errors. Its broad runner was not executed because it includes resource enumeration that could enter the hard-excluded preview path.
- Final: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 23 pre-existing TargetBattle nullable warnings and 0 errors.
- Tracked exact-path `git diff --check` succeeded with no output. Exact untracked-path `git diff --no-index --check -- NUL <path>` returned the expected difference exit `1` with no whitespace diagnostics for the task, Draft, compiler, and the two pre-existing untracked A1/A2 fixture files.
- Repository identity after execution: branch `main`; HEAD `212c69161fabe375d524adf72c13cfe9cf290d79`; index tree `e88edcc7d3485c1210f24c0d195c9fa314940ceb` — all unchanged from pre-edit identity.

### B1 Code Review

Reviewed the exact B1 paths against every `godot-code-review` section:

- Node/scene architecture: no scene/resource edits, new node traversal, autoload, or parent-chain coupling; existing roots only select the Bridge Draft carrier.
- C# style: Godot partial classes remain partial; new plain C# types use strong typing, PascalCase members, scoped private helpers, and explicit failure results.
- Performance: Draft cloning and Snapshot compilation occur once at preparation/launch boundaries; no per-frame lookups, loads, allocations, or hot-loop `StringName` work were added.
- Input and signals: no input routing, signal connection, signal naming, or event-cycle changes.
- Resource management: no loading, authored resource, scene instantiation, or lifecycle changes.
- Error-prone patterns: no async/deferred work, fragile node paths, direct body movement, timer-after-free, or hidden fallback. Missing/stale/duplicate/mismatched lineage and participant/deployment errors fail explicitly before commit.
- Review result: no scoped Critical findings and no scoped Improvement findings remain.

### Independent Verification

- Verifier context: fresh Codex independent verification context; verifier did not implement B1 business code.
- Authority review: current battle-preparation gameplay authority and `system-design/strategic-battle-bridge-architecture.md` agree with the task contract. No direction or authority conflict was found.
- Exact-file integrity: every recorded final B1 SHA-256 matched; the deleted sync service remained absent. Branch remained `main`, HEAD remained `212c69161fabe375d524adf72c13cfe9cf290d79`, and index tree remained `e88edcc7d3485c1210f24c0d195c9fa314940ceb` before and after verification.
- Acceptance 1-4: static review and public behavior coverage confirm the typed Draft directly compiles and commits the final Snapshot once; session/Draft/revision and participant lineage are explicit; deployment subset, placement, formation, initial destination, objective, participant identity, and cardinality come from the Draft; the detached compatibility projection cannot overwrite or restart final compilation.
- Acceptance 5-6: `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj -p:BuildInParallel=false -v:minimal` passed all 104 cases, including A1 settlement/idempotency, A2 cardinality, A3 authorization, and A4 Strategic Management/map guards. TargetBattle passed all behavior cases and failed only the two precisely classified unrelated pre-existing oversized guards named above.
- Acceptance 6 safe WorldSite coverage: `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors. Its broad runner was not executed because it may enumerate the excluded preview path.
- Acceptance 7: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors.
- Acceptance 8: tracked exact-path `git diff --check` returned 0; each untracked exact-path `git diff --no-index --check -- NUL <path>` returned the expected difference exit 1 with no whitespace diagnostic. Repository identity remained unchanged.
- Acceptance 9: the exact B1 files were independently reviewed against every `godot-code-review` section plus the `csharp-godot` checklist. No scoped Critical or Improvement finding remains. Skills used: `using-godot-prompter`, `csharp-godot`, `godot-testing`, and `godot-code-review`.
- Verification process note: one intended exact-path diff command was mis-bound by PowerShell and invoked a read-only unscoped `git diff`. It changed no state and no preview runner, Godot process, or user process was launched or inspected; all subsequent checks used explicit exact-path arrays. This procedural deviation is disclosed as verification evidence and is not a B1 implementation finding.

### Exclusions Observed

- Did not enter or enumerate `scenes/world/preview/` and did not run Strategic Region Preview tests or chains.
- Did not inspect, launch, stop, or control Godot or user processes.
- Did not touch territory artifacts, world workbench, other active tasks, command-state consolidation, regroup/retreat, UI design, scene/resource taxonomy, or code-slimming work.
- Did not run global `git status`, whole-repository scans, staging, commit, branch change, restore, or clean operations.

## Final Result

Completed and independently verified. The final Snapshot is compiled and committed once from the accepted Bridge Draft with explicit lineage; compatibility is detached outbound-only state and cannot reverse-synchronize. All acceptance criteria passed. Scoped findings: None. Remaining product/code risk: None. The two TargetBattle oversized guards remain unrelated pre-existing debt outside B1.
