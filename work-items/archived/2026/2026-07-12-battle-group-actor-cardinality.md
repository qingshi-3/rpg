# Battle Group Actor Cardinality And Casualty Basis

Status: Completed
Executor: executor-xhigh (gpt-5.6-sol/xhigh)
Verifier: Codex fresh independent verification context
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Repair the Strategic Management battle path so one deployed strategic participant produces exactly one battle-group commander identity with one hero actor and the accepted main-corps Runtime representation, without multiplying heroes, commanders, command state, combat contribution, or casualty basis from legacy force-count rows. Corps-strength writeback must use only the accepted 0-100 corps-strength contract.

## Confirmed Discussion Result

The user authorized the accepted current-system review remediation sequence to proceed batch by batch without repeated confirmation for ordinary scoped work. This task is remediation batch A2, immediately after the completed battle-settlement transaction-safety batch.

The confirmed contract is:

1. `battle group = 1 hero + 1 main corps` is the strategic participant and commander boundary.
2. A deployed strategic participant compiles to exactly one `BattleGroupSnapshot` and one battle-group commander identity. Legacy hero/corps force rows, `Count`, footprint, or visible presentation multiplicity must not create additional battle groups or duplicate hero commanders.
3. Runtime creates exactly one hero actor for that battle group. Main-corps Runtime representation may contain the minimum actor facts already accepted by the current Runtime, but every such actor belongs to the same commander state and must not fabricate another hero or command owner.
4. `battleUnitCount` is presentation/runtime force representation data. It is not long-term soldier identity and cannot be summed with a hero row to define corps casualties.
5. Strategic casualty writeback uses the participant's pre-battle `CorpsStrength` in the accepted 0-100 range and the deployed corps result. Hero force rows and visible-actor counts do not enter the denominator.
6. Missing, duplicated, or ambiguous participant/group/actor mappings fail explicitly. Do not hide a broken mapping behind a compatibility fallback.

The user is concurrently testing `scenes/world/preview/`. That directory and the standalone Strategic Region Preview chain are excluded from inspection, edits, validation failures, and drift conclusions for this task.

## Authority Impact

No player-facing gameplay change is authorized. Existing gameplay authority already defines one hero plus one main corps and treats visible soldiers as non-persistent presentation.

Before implementation, synchronize only durable clarifications required by the final code shape in:

- `system-design/strategic-battle-bridge-architecture.md`: one deployed strategic participant maps to one `BattleGroupSnapshot`; compatibility force rows cannot create extra battle-group or commander identity.
- `system-design/battle-runtime-architecture.md`: one hero actor per battle group and all corps/presentation actors share the single group commander state.
- `system-design/battle-result-settlement-architecture.md`: casualty conversion uses pre-battle 0-100 corps strength and deployed corps survival facts only, never hero rows or visible actor counts.

`system-design/hero-led-light-rts-system-architecture.md` already states the controlling cross-system invariant and should change only if a concise clarification is genuinely missing. If implementation requires a new player-facing soldier model, multiple heroes per group, multiple commandable corps, or a different strength scale, set this task to `Needs Discussion` and stop.

## Execution Scope

### Trace And Correct The Authoritative Data Path

- Trace the current participant through Strategic definitions/state, Bridge session/draft, compatibility request projection, final Snapshot, Runtime actor creation, Runtime result, Bridge summary, and Strategic settlement.
- Identify every place where hero and corps force rows or force `Count` expand into extra `BattleGroupSnapshot`, hero actor, commander state, or casualty denominator.
- Establish one stable participant/group identity across Snapshot, Runtime actors, results, reports, and settlement.

### Snapshot And Runtime Cardinality

- Compile one deployed strategic participant into one `BattleGroupSnapshot`.
- Preserve one hero actor and one commander identity per group.
- Represent the main corps without creating extra hero/commander copies. Reuse current accepted Runtime actor structures where possible; do not redesign formations, navigation, combat, or Presentation.
- Make duplicate or contradictory source rows fail with a named reason when they cannot be deterministically projected into the one-group contract.

### Casualty Basis

- Remove hero force rows and visible/presentation actor counts from corps casualty calculation.
- Convert surviving deployed corps facts into `CorpsStrength` using the frozen pre-battle 0-100 basis.
- Preserve A1 reserve, exact replay, candidate commit, rollback, and active-context identity behavior.

### Behavior-Level Tests

- Follow RED-GREEN-REFACTOR using existing focused C# regression executables; add no new framework or runner.
- Cover each of the three default strategic battle groups individually and in multi-group combinations.
- Assert one `BattleGroupSnapshot`, one hero actor, and one commander identity for every deployed participant.
- Assert force `Count` changes do not multiply heroes or battle-group commander states.
- Assert 0%, partial, and 100% corps survival map correctly to the 0-100 strategic strength basis without hero-row inflation.
- Exercise the public Strategic Management -> Bridge -> Snapshot -> Runtime -> result-summary/settlement path rather than private helpers or source-string shape alone.
- Re-run the A1 transaction-safety cases to prove no regression in reserve, rollback, persistence, replay, or context consumption.

## Non-Goals

- Do not remove legacy `BattleStartRequest`, complete the broader Draft-to-Snapshot authority cutover, or delete compatibility adapters beyond the smallest mapping correction required here.
- Do not implement command-validator cutover, enemy-skill authorization, main-map Strategic Management control cutover, regroup/retreat, destination reachability gates, or consequence-summary expansion.
- Do not consolidate runner infrastructure, split large roots, delete AutoBattle/dead resources, merge StyleBoxes, or perform general code slimming.
- Do not redesign visible formations, navigation, AI, combat balance, skill behavior, unit resources, or authored battle scenes.
- Do not modify `scenes/world/preview/`, Strategic Region Preview code/resources/tests, territory masks or generated territory outputs, world-map workbench, city-map authoring, or hero-corps manual QA.
- Do not stage, commit, switch branches, clean, restore, or normalize the shared dirty worktree.

## Constraints And Risks

- This crosses Strategic Management, Bridge, Runtime actor creation, and settlement, so use `executor-xhigh (gpt-5.6-sol/xhigh)`.
- Work on `main`; preserve all existing tracked, deleted, and untracked user changes.
- At task creation, `main` remains at `212c69161fabe375d524adf72c13cfe9cf290d79`. A1 changes and unrelated shared-worktree changes are uncommitted and must remain intact.
- `scenes/world/preview/` is a user-owned moving test target. Do not read or touch it. Ignore failures whose only evidence is that directory or the separately tracked Strategic Region Preview work item.
- Do not modify the three existing active tasks or their scoped files. Avoid the preview/mask workstream and generated territory artifacts entirely.
- Prefer pure C# Application/Runtime seams and existing typed contracts. Do not add dependencies, test frameworks, generic entity systems, or another commander-state model.
- Tests must observe public behavior. Source-shape guards may remain as unrelated legacy evidence but are not sufficient acceptance for this task.
- Run focused validation first. Use `dotnet build <project> -maxcpucount:2 -v:minimal`; do not start Godot unless independent evidence shows a scoped engine-only need and no user test is running.
- Non-trivial ownership, mapping, denominator, and failure semantics require concise nearby comments. Update stale comments with behavior.

## Acceptance Criteria

- [x] Relevant accepted system authority contains the one-participant/one-group/one-commander and corps-only casualty-basis clarification before implementation changes.
- [x] Each deployed strategic participant produces exactly one `BattleGroupSnapshot` with stable participant/group identity.
- [x] Runtime creates exactly one hero actor and one commander state per deployed strategic participant, independent of force-row count or `battleUnitCount`.
- [x] Corps/presentation actors belong to the same battle-group commander state and cannot emit duplicate group command ownership.
- [x] Strategic casualty writeback uses only frozen pre-battle 0-100 corps strength and deployed corps survival facts; hero rows and visible actor counts do not affect the denominator.
- [x] Three default groups pass individual and combined end-to-end cardinality tests.
- [x] Zero, partial, and full corps survival produce correct 0-100 strategic strength results.
- [x] Duplicate or ambiguous mapping fails explicitly without partial Runtime or Strategic state mutation.
- [x] A1 reserve, rollback, persistence failure, exact replay, conflict, and active-context tests remain green.
- [x] Focused affected runners, `dotnet build rpg.sln -maxcpucount:2 -v:minimal`, and scoped `git diff --check` pass, with unrelated pre-existing guard failures recorded precisely.
- [x] `scenes/world/preview/`, its test chain, territory artifacts, and other active-task files remained excluded from verification and untouched by the verifier.
- [x] Executor handed off at `Awaiting Verification`; a fresh independent verifier checked every criterion before `Completed` and archive.

## Skills Used

- `using-godot-prompter`: Codex skill routing and mandatory Godot implementation workflow.
- `csharp-godot`: typed C# conventions, Godot boundary safety, and hot-path collection guidance.
- `godot-testing`: RED-GREEN-REFACTOR and public-behavior test guidance, including the TDD and testing-pattern references.
- `godot-code-review`: final scoped review of C# ownership, lifecycle, error semantics, and performance-sensitive patterns.

## Current Progress Snapshot

### Completed

- User authorized the accepted remediation sequence to proceed batch by batch and explicitly excluded `scenes/world/preview/` while user testing continues.
- Selected A2 as the next highest-risk slice because A1 makes settlement safe but cannot correct an inflated Runtime actor/result input.
- Read repository/work-item routing, relevant gameplay and system authority, and the four named GodotPrompter skills/references.
- Recorded the confirmed scope, non-goals, authority impact, risk controls, and acceptance contract in this task.
- Executor verified `main` at `212c69161fabe375d524adf72c13cfe9cf290d79`, listed only active task names, inspected the shared dirty scope without cleanup, and recorded scoped SHA-256 baselines.
- Synchronized the three required system authority documents with one participant/one snapshot/one commander, one hero actor per group, shared commander ownership, and the corps-only frozen 0-100 casualty basis.
- Added public-path A2 regression coverage and proved RED for inflated snapshot/actor cardinality, non-unique participant corps actors, and duplicate-row acceptance.
- GREEN implementation now aggregates compatibility rows into one participant snapshot, rejects invalid strategic identity before Runtime actor construction, and converts the unique corps outcome HP fraction against frozen pre-battle strength; the full Strategic Management runner is green after scoped refactor.
- Completed affected-runner validation, final solution build, scoped diff checks, final hash/status comparison, and `godot-code-review` review.

### Remaining

- None.

## Pause Or Blocker

- Current pause/blocker: None; independent verification completed.
- Resume condition: Not applicable.
- Resume entry: Not applicable; any follow-up requires a separate confirmed work item.
- Stop condition: Not applicable to this completed task.

## Latest Verification

Fresh independent verification passed every acceptance criterion. Strategic Management public-path and A1 transaction-safety cases are green; affected Runtime and WorldSite behavior cases are green apart from the same precisely attributed unrelated guards; solution build and scoped whitespace checks pass. The task is `Completed` and archived.

## Execution Record

### 2026-07-12 - Executor Baseline And Authority Synchronization

- Branch/HEAD: `main` / `212c69161fabe375d524adf72c13cfe9cf290d79`.
- Pre-edit SHA-256: bridge authority `71fcb6ed5ac98edd249ddb6ae08c75707d7275a9f5bbf1744de352fe11ca0052`; Runtime authority `1637829e3806a4f02e12d6cce6fc91211b69a063019a4725ea6d931ec8c4a6e9`; settlement authority `f93070172f6be7dfb930affcc1cef85ef3be11dd3d7d33358c47ab0229a363d3`; launch sync `e6620fcbb12838ad950cba5388e99eb8615372589e31d413fc207c5c1fdcb1ab`; bridge service `a318dc1da208674aa69e86f7c5080bc3ab938eb1687f4f303895dd213d8acbba`; Runtime session `5e4c79ebae1c88d10d288668d044511d43ce32287858545968ba8785892ae6f5`; Runtime actor `a7e710f5488bb41a9d1dd95ce6b16a53ffb84723df12f18207a8770a78a4f331`; outcome contract `483b0df6fb63a3a86d74f7733d5ae1660735f68596e98d85e5ce291260cf89c8`; runner program `1dd25551fade8f3a6c7efee52b6edff484cb892fdd1fbd69ec827187f99b6a4e`; expedition tests `6ca31d98c0a8c497b57b4141791bae3227e15cdfd58ab84724c728fabd3a8429`; shared test support `ab7d16d3437f9a5c09233bc4c41460813ec1ad0b0995e22646aea5964340651b`; new A2 case file absent.
- Existing dirty A1 and user changes in the launch sync, bridge service, runner, support, and authority files were retained as the edit baseline.
- Architecture check: Strategic Battle Bridge owns participant-to-snapshot projection; Runtime owns one hero actor and commander state per snapshot; Bridge/Settlement owns corps-only 0-100 writeback. No player-facing authority change or hero-led index change was required.

### 2026-07-12 - RED Behavior Evidence

- Added `StrategicManagementRegressionCases.BattleGroupCardinality.cs` to the existing executable runner; no framework or engine process was added.
- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal`: initial test-author typo failed (`HeroBeastTamer` does not exist), corrected to the accepted cavalry default; second build passed with 0 warnings/errors.
- `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj --no-build`: expected RED. All pre-existing cases passed; A2 failed for the intended behavior: one participant produced 6 snapshots, casualty lookup found multiple matching corps actors, and a duplicate participant hero row launched instead of rejecting.

### 2026-07-12 - GREEN And Refactor Evidence

- Bridge launch now resolves every positive compatibility row to one stable participant and one unique hero/corps role pair, then emits one `BattleGroupSnapshot` whose group/source/commander id is the participant id. Force `Count` no longer controls strategic group cardinality.
- Strategic Bridge validates duplicate or ambiguous participant/role mappings before Runtime receives or constructs actors. A broader generic Runtime rejection was tested, found incompatible with retained non-strategic multi-member snapshots, and removed as out of scope.
- Bridge result mapping requires one exact snapshot, hero outcome, and corps outcome per deployed participant. Remaining strategic strength is `round(frozen pre-battle CorpsStrength * remaining corps HP / snapshot corps HP)`; hero rows and presentation counts are excluded.
- A1 synthetic outcome support now projects its legacy fixture ratio into one authoritative corps outcome; transaction behavior itself was not changed.
- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal`: passed after GREEN and after refactor; only two pre-existing `UnitAnimationTimingPolicy.cs` obsolete-API warnings.
- `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj --no-build`: passed all cases after GREEN and again after refactor, including all A1 reserve/rollback/persistence/replay/context cases and all A2 cases.

### 2026-07-12 - Affected Validation, Review, And Final Baseline

- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal`: passed; warnings were pre-existing nullable-test warnings. The first runner attempt exposed scoped compatibility regressions from an over-broad generic Runtime identity rejection plus the unrelated size guard; that Runtime change was removed. The rerun reported 364 passes and only the unrelated existing oversized-file guard failure for `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1088`.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal`: passed with pre-existing nullable-test warnings. The runner reported 280 passes and only the unrelated shared-worktree documentation guard failure caused by the already deleted `design-proposals/active/README.md`.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- Scoped `git diff --check` plus explicit trailing-whitespace checks for the untracked A2 test/task files: passed.
- `dotnet build-server shutdown`: successfully stopped the MSBuild and Roslyn compiler servers.
- `godot-code-review`: no scoped critical findings. Node/scene, signal, input, resource-lifecycle, and per-frame performance sections are not applicable to these pure Application/Runtime seams. Typed C# conventions, explicit failure semantics, stable identity ownership, and low-frequency collection use pass. One improvement was applied: Runtime authority wording and code were narrowed to the Strategic Bridge boundary instead of changing retained non-strategic compatibility semantics.
- Final SHA-256: bridge authority `98b8a64e1667e26ebf4b8911cfc1176672bf82c3037df8f6a8ac7e6c90fc88cd`; Runtime authority `881a33af167d1d637e8c25f26db9e2389b6181588a7911025df6342e3c425108`; settlement authority `03f2d528d08ef14e54636be25e5d02a7826908113b7f17bb248801c887c85b51`; launch sync `6c100f4714ef39d76963304cd5d657bc8530eef96ea10a0cd497a3be6fc3d6ae`; bridge service `36b907a6f1285f7584c194b523467a30e0583354eb3628de8811fa8c3b321020`; runner program `a41f73c99af7e252338b346cabcfea30c19d69b36a3fde90a57b61bde494c362`; expedition tests `977dc6f4506b8bf91cc45f61f92fe63ee9c4eac526f082831f5cf338df2b6023`; shared support `bb13d83bcf56ad0cc5338c1e3616f2f7dd043192e8967d9572ae06733348a277`; new A2 tests `c4f9c2a5261bd3f68e29df854a23bd9c7b8510cea1a4d4f114bb2e9fd0363051`.
- `BattleRuntimeSession.cs`, `BattleRuntimeActor.cs`, and `BattleOutcomeResult.cs` match their recorded starting hashes; the attempted generic Runtime validation was fully removed. No gameplay authority or hero-led architecture index changed.
- Exact execution delta is limited to the three authority documents, the launch sync, Bridge result mapping, the existing Strategic Management runner/support/bridge expectations, the new A2 regression case file, and this task. Existing A1/user dirt was preserved.
- `scenes/world/preview/`, the standalone Strategic Region Preview chain, territory masks/artifacts, world workbench, city-map authoring, hero-corps manual QA, other active-task bodies/files, and `history/` were excluded from execution edits and validation conclusions. No executor patch targeted an excluded area; scoped searches and validations used explicit exclusions or exact task paths.

### 2026-07-12 - Primary Context Read-Only Verification

- Independently reviewed the scoped Bridge projection, Runtime handoff, result mapping, public A2 regression cases, and retained A1 transaction boundary. No scoped correctness or Godot C# review finding remained.
- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj --no-build`: passed every case, including A2 cardinality/casualty/failure behavior and all A1 reserve/rollback/persistence/replay/context cases.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors; scoped tracked and untracked whitespace checks passed; compiler servers were shut down.
- Verification did not start Godot, inspect the excluded preview chain, or change the requested `Awaiting Verification` disposition.

### 2026-07-12 - Fresh Independent Verification

- Read the repository routing, complete task, named gameplay/system authority, and the complete `using-godot-prompter`, `csharp-godot`, `godot-testing`, and `godot-code-review` skills plus the named Codex, TDD, and testing-pattern references.
- Confirmed `main` / `212c69161fabe375d524adf72c13cfe9cf290d79`. All nine A2 target files matched the recorded final SHA-256 values; `BattleRuntimeSession.cs`, `BattleRuntimeActor.cs`, and `BattleOutcomeResult.cs` matched their recorded starting hashes. Scoped review distinguished retained A1 transaction/rollback/persistence dirt from A2 cardinality and casualty changes.
- Independently traced the public Strategic Management -> Bridge -> final Snapshot -> Runtime -> result-summary -> Strategic settlement path. One participant produces one snapshot, one hero actor, one commander identity, and one corps actor sharing that identity regardless of force `Count`; 0/50/100% corps HP maps to 0/50/100 strength from the frozen pre-battle basis; duplicate/ambiguous launch or result mappings reject before scoped mutation.
- `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal`: passed with 0 warnings/errors. Its runner passed every case, including all seven non-empty default-group subsets, Count variations, casualty boundaries, explicit mapping failures, and all A1 reserve/rollback/persistence/replay/conflict/context cases.
- `TargetBattleArchitectureRegression` build passed with 0 warnings/errors. Its behavior cases passed; only the unrelated oversized-file guard failed for unchanged-from-HEAD `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `src/Application/StrategicManagement/StrategicManagementRules.cs:1088`.
- `WorldSiteDeploymentCacheRegression` build passed with 0 warnings/errors. Its behavior cases passed; only the unrelated documentation guard failed because pre-existing shared-worktree dirt deletes `design-proposals/active/README.md` (21 deleted lines).
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: passed with 0 warnings/errors. Scoped tracked `git diff --check` and the untracked A2 test trailing-whitespace check passed. Build servers were shut down.
- Verification did not start Godot or read, enumerate, validate, or modify `scenes/world/preview/`, the standalone Strategic Region Preview chain, territory/workbench artifacts, other active-task files, or history.

### 2026-07-12 - Task Initialization

- Main discussion context created the self-contained A2 task after the user's standing batch-by-batch authorization.
- No code, authority, resource, test, configuration, branch, index, preview, or external state changed during task initialization.

## Verification Handoff

Ready for a fresh independent verifier. Verify one snapshot/hero/commander for all seven non-empty default-group subsets and Count variations; 0/50/100% corps HP writeback; named duplicate/ambiguous rejection with unchanged context/state; all A1 transaction cases; scoped diff/hashes; and the two unrelated affected-runner guard failures. Do not inspect or validate the excluded Strategic Region Preview chain.

## Final Result

Disposition: Completed and archived after fresh independent verification.

Verification conclusion: Every scoped acceptance criterion passed. The two remaining guard failures are unrelated pre-existing repository/worktree conditions and do not touch A2 implementation or authority.

Remaining risks: None within the confirmed A2 scope.

Follow-up work: None. Any cleanup of the unrelated oversized-file or deleted-document guards requires a separate confirmed work item.
