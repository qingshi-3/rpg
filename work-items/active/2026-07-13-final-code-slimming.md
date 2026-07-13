# Final Code Slimming

- Status: In Progress
- Executor: `executor` (isolated Codex execution, `gpt-5.6-sol` + `high`)
- Verifier: Codex primary context (independent from executor)
- Created: 2026-07-13
- Updated: 2026-07-13

## Objective

Finish the confirmed code-review remediation with evidence-based slimming: remove independently dead code and unused resources, merge byte-identical resources, then reduce exact Snapshot/test-support duplication and test-only legacy intermediaries without weakening current behavior coverage or changing accepted gameplay/architecture.

## Confirmed Discussion Result

The user authorized completing all remaining review remediation in planned order and requested logical batches with local commits. The trusted verification gate is complete at `6a079983`. Slimming must optimize for one authoritative path and maintainability, not deleted-line count. Anything requiring a product or architecture choice returns to discussion instead of being guessed.

## Authority Impact

Impact: Medium implementation and test-maintenance cleanup. No gameplay or system authority change is currently required. Removing a candidate is allowed only after static and dynamic/resource-reference evidence proves it is outside the current authoritative path. If a candidate still owns production behavior, persistent facts, scene wiring, or a cross-system contract, set `Needs Discussion` and stop that candidate.

## Scope And Batch Order

### Batch A: independent deletion and identical resources

- Audit and remove the standalone legacy AutoBattle stack and disabled adapters/runners only where production, reflection, editor tooling, C# literal, scene, resource, and project references prove it dead. Retain current Battle Runtime and any still-live compatibility boundary.
- Audit and remove unused scenes/scripts such as the reported `WorldCorpsInstanceRow` pair only when scene/resource/dynamic references are absent.
- Audit and remove the test-only legacy result builder only after migrating any valuable behavior fixture to the current result/snapshot boundary.
- Deduplicate the five reported byte-identical StyleBox pairs by selecting one authored resource per byte-identical group and updating every explicit reference. Do not collapse visually or semantically different resources.

### Batch B: exact structural duplication

- Consolidate the three exact skill Snapshot deep-copy implementations behind the current authoritative compiler/copy utility without changing ownership, identity, or immutability behavior.
- Reduce repeated runner/helper infrastructure now that `run-trusted-validation.ps1` is authoritative. Prefer small shared test support with unchanged runner isolation; do not introduce a new test framework.

### Batch C: test-only intermediaries

- Audit `BattleGroupSessionProbeService` and related legacy adapters for production, reflection, editor, scene/resource, and dynamic use. Move or delete only portions proven test-only; preserve required test seam interfaces and any live compatibility adapter.
- Remove unregistered obsolete source-shape test methods/helpers left behind by the trusted verification gate when they have no behavior value or callers.

## Non-Goals

- No deletion or redesign of Emotion, LimboAI/C# behavior trees, content factories/migration, objective-planning UI, legacy World/Request/Result/Handoff production boundaries, or root-owner architecture unless a separate confirmed discussion authorizes it.
- No gameplay, save schema, battle result, command authorization, scene taxonomy, or visual redesign.
- No broad formatter, framework migration, speculative performance optimization, or mechanical partial-class splitting.
- No inspection, enumeration, modification, import, or execution of `scenes/world/preview/`.

## Constraints

- Work on `main`; do not create or switch branches.
- Use one isolated executor fixed to `gpt-5.6-sol` + `high`; do not spawn nested agents or parallel writers.
- Execute one batch at a time. The executor must stop at `Awaiting Verification`; the independent verifier reviews, runs proportionate validation, archives only after all batches, and commits each verified batch before the next begins.
- Never read, modify, hash, diff, stage, or commit `src/Runtime/Battle/BattleGroupCommanderTransitionCoordinator.cs.uid`.
- Preserve and never stage `work-items/active/2026-07-13-codex-default-reasoning-high.md`.
- Use `csharp-godot` and `godot-code-review`. Record any additional GodotPrompter skill actually used.
- Prefer static reference audits and focused runners. Run the unified gate once per completed logical batch, only rerunning after a real failure. Godot smoke must use the explicit trusted allowlist.
- Keep authored Godot resources authoritative; do not replace reusable `.tscn`/`.tres` assets with code-built UI or styles.

## Acceptance Criteria

1. Every deletion has recorded evidence covering compile references, dynamic/reflection/editor use, scene/resource paths, and project registration as applicable; no candidate is deleted merely because a simple symbol search is empty.
2. Batch A removes only independently dead production/test artifacts and merges only byte-identical StyleBoxes with all references updated and no missing `res://` path.
3. Batch B leaves one authoritative skill Snapshot copy path and materially reduces exact runner/helper duplication without weakening behavior fixtures or runner isolation.
4. Batch C removes or relocates only test-only intermediary code; current Bridge/Runtime production startup, settlement, command, and compatibility behavior remain unchanged.
5. Emotion, AI, content migration, preview work, live legacy boundaries, test-seam interfaces, and the two excluded untracked files remain untouched.
6. Each batch passes its focused checks, `git diff --check`, exact `godot-code-review` with no unresolved Critical/Improvement, and the trusted unified validation with zero errors.
7. Each verified batch is committed on `main`; final task evidence names commits, changed responsibilities, removed/merged assets, and any candidates retained with reasons.

## Current Progress

Completed:

- Trusted verification gate completed and committed at `6a079983`.
- Batch A was independently accepted and committed on `main` as `871acf27`.
- Batch B consolidated all three reported skill Snapshot deep-copy bodies into the compiler-owned internal `BattleSkillSnapshotCopy.DeepClone` path. Template compilation still assigns grant identity and excludes template caster bindings; Draft still rejects blank definition IDs and preserves caster rows; Runtime still normalizes definition IDs, tags, and caster IDs. Nested typed snapshots and all collection instances remain independently copied.
- Batch B added one bounded shared runner shell compiled as linked source into Battle Hit Feedback, Scene Transition CAS, Strategic Management, Target Battle Architecture, World Army Movement, and World Site Deployment Cache executables. It centralizes only PASS/FAIL/ExitCode handling; Target and WorldSite retain their distinct local filtering rules, assertions remain local, and every runner remains a separate executable.
- No Batch C source-shape helpers or intermediary services were removed or entered; `BattleGroupSessionProbeService` and all excluded/non-goal systems remain untouched.
- Batch B focused verification passed: low-concurrency solution build completed with 0 errors and 43 existing warnings; Target Battle Architecture and Strategic Management passed; WorldSite initially failed one obsolete source-shape assertion, which was updated to verify the compiler-owned copy utility, then the full runner passed on its real-failure rerun.
- Batch B exact `godot-code-review` result: Critical 0, Improvements 0. No scene/node, input, signal, Godot lifecycle, resource-loading, interop, thread, or hot-path behavior changed; shared copy and runner support are stateless and preserve caller ownership. `git diff --check` passed.
- The single Batch B invocation of `tools/validation/run-trusted-validation.ps1` passed with the required Godot 4.7 console binary: solution build 0 warnings/0 errors, all seven maintained non-preview runners passed, and both explicitly allowlisted scene smokes passed.
- Batch B implementation is complete and stopped at `Awaiting Verification`; no branch, stage, commit, archive, Batch A rework, or Batch C work was performed.
- Independent Batch B verification accepted the implementation: the former Template, Draft, and Runtime copy bodies all delegate to the single compiler-owned utility; their distinct identity, owner, filtering, and normalization policies remain at each entry; typed nested objects and collections are independently copied. The runner shell is linked into exactly six independent executables, while Target and WorldSite filters and all assertion helpers remain local. No unresolved `godot-code-review` finding remains.
- Review report and confirmed slimming order re-read; current worktree contains only the two excluded untracked files.
- Required `csharp-godot` and `godot-code-review` skills selected.
- Batch A executor re-read the required authority routes, task contract, code-slimming review section, and both selected skills; confirmed `main` at `6a079983` and began the constrained candidate audit.
- Batch A reference audit proved the legacy AutoBattle source/test stack and `WorldSiteAutoBattleAdapter` have no production, dynamic/reflection/editor, scene/resource, or project consumers beyond their own test registration; `WorldCorpsInstanceRow` has no external scene/resource consumer; `ResolveAutonomousCombat` has no caller.
- Five initially reported StyleBox groups were re-hashed byte-for-byte and their unreferenced copies removed. Independent verification then identified a sixth byte-identical inner-panel group that must also be consolidated by updating its live references.
- All six discovered byte-identical StyleBox groups are now handled. For the sixth group, `battle_runtime_fantasy_inner_panel.tres` is canonical; `WorldSitePeacetimeHud.tscn` and its behavior/resource assertions were redirected to it, and `battle_runtime_pause_scroll_inner_panel.tres` was deleted. The removed path has 0 remaining references and the canonical path has three scene consumers.
- The legacy request/result summary overload and its private result fixture were proven test-only. Equivalent current-boundary protection remains in the active-context result-envelope identity/mirror regression plus missing actor/participant consequence tests.
- Focused verification passed: low-concurrency `dotnet build rpg.sln` completed with 0 errors and 43 pre-existing warnings; Strategic Management, Target Battle Architecture, and World Site Deployment Cache runners all passed; UTF-8 scanning of 1,789 non-preview scene/resource files found 0 missing explicit `res://` paths.
- Exact `godot-code-review` result for the Batch A diff: Critical 0, Improvements 0. The review found no new node/scene coupling, C# source-generator/API misuse, hot-path lookup/load, input/signal lifecycle issue, resource ownership regression, or error-prone deferred/free pattern. Removing the unused scene and autonomous loop reduced obsolete ownership while authored `.tres` resources remain authoritative.
- `git diff --check` passed. The single Batch A invocation of `tools/validation/run-trusted-validation.ps1` passed with the required Godot 4.7 console binary: solution build 0 warnings/0 errors, all seven maintained non-preview runners passed, and both explicitly allowlisted Godot scene smokes passed.
- Batch A returned to `In Progress` for the verifier's scoped sixth StyleBox-group finding, completed that correction, and stopped again at `Awaiting Verification`; no branch, stage, commit, archive, or Batch B/C work was performed.
- Corrective focused verification passed: `WorldSiteDeploymentCacheRegression` completed successfully with only its existing nullable warnings; `git diff --check` passed; exact `godot-code-review` reported Critical 0 and Improvements 0. The correction changes only an authored resource path and its assertions, without scene-structure, runtime-loading, node-coupling, input, signal, lifecycle, or performance changes.
- The single trusted unified rerun authorized by the real independent-verification finding passed: solution build 0 warnings/0 errors, all seven maintained non-preview runners passed, and both explicitly allowlisted Godot 4.7 scene smokes passed.
- Independent renewed verification accepted Batch A: all removed positive references remain absent, the sixth duplicate resource has zero remaining references, its canonical replacement has exactly three scene consumers, and the final diff has no unresolved Critical or Improvement.

Remaining:

- Commit the independently accepted Batch B on `main`.
- Execute, independently verify, and commit Batch C.
- Mark `Completed`, archive, and report retained decision-bound candidates.

## Pause And Resume

- Blocker: None.
- Resume condition: Commit accepted Batch B, then start the Batch C executor from that checkpoint.
- Resume entry: Commit only the accepted Batch B implementation, tests, and this task record; preserve both excluded files. Then audit `BattleGroupSessionProbeService`, related adapters, and unregistered obsolete source-shape helpers without changing live compatibility boundaries.
- Latest verification: Independent Batch B review accepted the exact copy and runner boundaries without rerunning the already-passing unified gate. Executor evidence remains: 0-error focused build, affected runners passed after one corrected obsolete assertion, `git diff --check`, Critical 0 / Improvements 0, all seven trusted runners, and both Godot 4.7 smokes passed.

## Execution Record

- 2026-07-13: Primary context classified the final slimming as medium implementation/test cleanup, created this confirmed work item, and queued Batch A for the fixed isolated executor.
- 2026-07-13: Batch A executor confirmed `main` at `6a079983`, observed only the two known untracked work-item files outside excluded paths, and set the task to `In Progress` before implementation mutation.
- 2026-07-13: Batch A removed only audited independent candidates, removed the retired AutoBattle runner from the solution/trusted suite, preserved current Runtime/Bridge compatibility boundaries, and redirected resource-authoring checks to the five retained byte-identical StyleBoxes.
- 2026-07-13: `csharp-godot` compliance and the complete `godot-code-review` checklist were applied to the final Batch A code/resource diff; no unresolved review finding remains before the unified gate.
- 2026-07-13: Batch A unified validation passed on its first and only invocation. Executor handed off at `Awaiting Verification` without staging or committing.
- 2026-07-13: Independent verification returned one scoped finding: consolidate the sixth discovered byte-identical inner-panel group. Executor returned the task to `In Progress` before corrective mutation.
- 2026-07-13: Executor redirected the pause HUD and behavior/resource assertions to the canonical fantasy inner-panel resource, deleted the duplicate, and passed the affected runner, `git diff --check`, and exact review before the authorized trusted unified rerun.
- 2026-07-13: The one trusted unified rerun authorized for the verifier finding passed completely. Executor handed corrected Batch A back at `Awaiting Verification` without staging or committing.
- 2026-07-13: Independent renewed verification accepted Batch A and returned the overall multi-batch task to `In Progress` for the next committed checkpoint.
- 2026-07-13: Batch A was committed as `871acf27`. Batch B executor confirmed `main`, observed only the excluded default-reasoning work item, re-read the task and required skills, and began Batch B without modifying Batch A.
- 2026-07-13: Batch B executor replaced three structural skill Snapshot copies with one compiler-owned deep-copy utility while retaining each caller's identity/normalization policy, and centralized the exact runner execution shell across six isolated executables. Focused checks and review pass after correcting one obsolete source-shape assertion.
- 2026-07-13: Batch B trusted unified validation passed on its first and only invocation. Executor handed off at `Awaiting Verification` without staging or committing and stopped before Batch C.
- 2026-07-13: The independent verifier compared each consolidated copy entry with its pre-Batch-B behavior, confirmed exactly one structural copy path and six linked runner consumers with local filters/assertions intact, accepted Batch B, and returned the multi-batch task to `In Progress` for its commit and Batch C.

## Final Result

Batch A is independently verified and committed as `871acf27`. Batch B is independently verified and ready to commit: one compiler-owned skill Snapshot deep-copy path serves Template, Draft, and Runtime without merging their entry policies; one bounded runner shell serves six isolated executables without centralizing filters or assertions. Batch C remains unstarted.
