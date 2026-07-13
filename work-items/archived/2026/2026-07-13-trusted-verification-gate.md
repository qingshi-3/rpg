# Trusted Verification Gate

- Status: Completed
- Executor: `executor` (isolated Codex execution, `gpt-5.6-sol` + `high`)
- Verifier: Codex primary context (independent from executor)
- Created: 2026-07-13
- Updated: 2026-07-13

## Objective

Establish one reliable, low-noise validation entry for the current Godot C# system before final code slimming: include every maintained C# regression runner, replace the clearest stale path/source-shape assertions with behavior checks, cover the four strategic battle flows at their highest-risk boundaries, and add a safe Godot headless/import/scene smoke that never enters the excluded preview workspace.

## Confirmed Discussion Result

The user confirmed the code-review remediation order and authorized completing all remaining batches. State-corruption, single-authority, and product-contract batches are complete; the next required batch is the review report's trusted verification gate. This batch improves verification confidence without redesigning gameplay, replacing the existing lightweight console-runner approach, or treating unmeasured performance concerns as defects.

## Authority Impact

Impact: Medium test and verification infrastructure. No gameplay or system authority changes are required because production ownership, persistence, Bridge, and transition contracts remain unchanged. If execution discovers that a behavior fixture requires changing an accepted production contract, set this task to `Needs Discussion` and stop.

## Scope

- Add all maintained non-preview C# regression runner projects to the single `rpg.sln` build entry and provide one low-concurrency, concise runner command or script for them.
- Exclude the Strategic Region Preview runner and every path under `scenes/world/preview/` from this task and from the unified validation entry.
- Repair clearly stale assertions that target retired document paths, moved source files, or private source shape; retain only small explicit architecture guards whose contract cannot be exercised behaviorally.
- Ensure behavior fixtures cover the four review flows at the existing authoritative boundaries: strategic expedition/arrival, preparation with carried versus deployed subsets, runtime command/simulation, and result/settlement/save/return continuation.
- Add focused fault-injection coverage for save, Bridge/context, and scene-transition failure semantics where existing seams support it without production redesign.
- Add a safe Godot headless/import/scene smoke manifest or runner that lists explicit maintained scenes and does not scan the project tree or preview directory. Prefer `PackedScene` load/instantiate/lifecycle checks over source/path assertions.
- Run one focused RED, one focused GREEN, one final low-concurrency solution build/unified validation pass, and one exact code review. Repeat only after an actual failure.

## Non-Goals

- No broad test-framework migration to GUT/gdUnit4 and no CI redesign.
- No gameplay, persistence schema, Bridge, scene-router, UI, or production architecture redesign.
- No profiler optimization claims; only add counters/baseline hooks if an existing focused seam makes this trivial and non-invasive.
- No final dead-code/resource/helper slimming in this batch.
- No inspection, enumeration, modification, import, or smoke execution of `scenes/world/preview/`.

## Constraints

- Work on `main`; do not create or switch branches.
- Use one isolated executor explicitly fixed to `gpt-5.6-sol` + `high`; do not spawn nested agents.
- Preserve `src/Runtime/Battle/BattleGroupCommanderTransitionCoordinator.cs.uid`: do not read, modify, delete, hash, diff, stage, or commit it.
- Preserve `work-items/active/2026-07-13-codex-default-reasoning-high.md`; it belongs to another task and must not be staged or committed.
- Keep validation lean: static inspection first; Godot headless only through an explicit safe manifest; `.NET` builds use `-maxcpucount:2 -v:minimal`.
- Required skills: `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. `rpg.sln` contains the main project and every maintained non-preview C# regression runner; one documented low-concurrency entry builds and executes the maintained suite without manually invoking eight projects.
2. The unified entry and Godot smoke use an explicit allowlist and never enumerate, import, load, hash, or inspect `scenes/world/preview/`.
3. Known stale retired-document and moved-file assertions are removed or replaced by public behavior/contract assertions; remaining source-shape guards are few, intentional, and documented.
4. Behavior fixtures cover carried/deployed subset semantics and the return-to-map continuation, and preserve existing strategic expedition, runtime command, settlement, replay, and recovery behavior.
5. Save failure, Bridge/context failure, and scene-transition failure each have a deterministic fixture proving no partial authoritative state is published or consumed beyond the accepted failure contract.
6. A safe Godot headless/import/scene smoke loads and instantiates explicit maintained scene(s), exercises one lifecycle frame where practical, reports failures with scene identity, and exits deterministically.
7. The maintained regressions, safe smoke, and `dotnet build rpg.sln --no-restore -maxcpucount:2 -v:minimal` pass with zero errors; only known existing warnings remain.
8. Exact `godot-code-review` reports no unresolved Critical or Improvement in the changed scope, and `git diff --check` passes.

## Current Progress

Completed:

- Recovered the accepted review sequence after P2-02 and confirmed the worktree has only the two explicitly excluded untracked files.
- Read the current authority route, work-item lifecycle, review report, and required `csharp-godot`, `godot-testing`, and `godot-code-review` skills.
- Confirmed nine C# runner projects exist, while the current solution historically covered only the main project and one runner.
- Executor re-read the required bootstrap, exact task, current-system review report, and all three required skills; confirmed `main` and no authority conflict before mutation.
- Added all eight maintained non-preview runners to `rpg.sln` and added `tools/validation/run-trusted-validation.ps1` as the single low-concurrency build/run/smoke entry.
- Added an explicit two-scene `PackedScene` load/instantiate/one-frame lifecycle smoke with deterministic scene-identified failures.
- Removed maintained-suite registrations for stale retired-path, private-helper, line-budget, legacy-authority, and removed-UI assertions; corrected current fixture identities so supplemental Runtime actors share an existing commander state.
- Confirmed existing behavior fixtures cover expedition/arrival, every non-empty carried/deployed subset, Runtime commands/simulation, durable settlement/save/replay, Bridge/context CAS, scene-transition failure, and return continuation.
- Used `csharp-godot`, `godot-testing`, and `godot-code-review`.
- Completed the required RED/GREEN cycle, final unified validation, and exact review.
- Independent verification restored and repaired the current-path starting-hero skill snapshot fixture, then confirmed every acceptance criterion against the final diff.

Remaining:

- None.

## Pause And Resume

- Blocker: None.
- Resume condition: Not applicable; task completed.
- Resume entry: Not applicable.
- Latest verification: Independent final unified validation passed with solution build at zero warnings and zero errors, all eight maintained runners passing, and both allowlisted Godot 4.7 scene lifecycles passing. Exact changed-scope `godot-code-review` found no unresolved Critical or Improvement; `git diff --check`, solution membership, explicit smoke allowlist, and PowerShell syntax checks passed.

## Execution Record

- 2026-07-13: Primary context classified the batch as test/verification infrastructure with no durable authority change and created this confirmed work item.
- 2026-07-13: Executor set the task to `In Progress`; implementation inventory and the single RED/GREEN cycle are next.
- 2026-07-13: Focused RED proved seven runner memberships and the unified validation/smoke entry were missing; focused GREEN passed after the minimal infrastructure implementation.
- 2026-07-13: Unified validation exposed and drove removal/correction of stale retired-path, private-source-shape, legacy-authority, line-budget, removed-UI, carrier-field, and tactical-fixture assumptions. Each rerun followed an actual timeout, build, or runner failure.
- 2026-07-13: Final unified validation passed all eight maintained runners and both allowlisted Godot 4.7 scene lifecycles. Exact `godot-code-review` found no unresolved Critical or Improvement, and `git diff --check` passed.
- 2026-07-13: Independent review challenged two removed behavior registrations. The legacy-garrison case proved tied to the retired `WorldArmyState.GarrisonUnits` strategic roster mirror and remains unregistered; current Strategic Management participant/subset fixtures cover the authoritative path. The starting-hero skill case exercises the current Bridge and final Draft compiler, so its registration was retained and its fixture faction lineage was updated to use the participant's authoritative faction id.
- 2026-07-13: Independent final verification passed the complete unified entry and exact diff review. The task was marked `Completed` for archive and exact-batch commit.

## Final Result

Completed and independently verified. The repository now has one explicit, low-noise validation entry for all maintained non-preview C# runners plus safe Godot scene smoke. Stale private-source-shape and line-budget assertions no longer gate the suite, while the current Bridge-to-launch hero skill behavior remains covered. No gameplay/system authority or production runtime behavior changed. Remaining risks: None within scope.
