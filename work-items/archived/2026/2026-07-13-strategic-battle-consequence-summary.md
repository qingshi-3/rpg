# Strategic Battle Consequence Summary

- Status: Completed
- Executor: Codex (single executor; model/reasoning configuration is not visible in this interface)
- Verifier: Codex primary context (independent from executors)
- Created: 2026-07-13
- Updated: 2026-07-13

## Objective

Complete P2-02 by making the accepted result envelope's Runtime outcome/event stream, settlement plan, battle report, frozen participant snapshot, and strategic target definitions compile into one complete `StrategicBattleResultSummary`, then make settlement feedback and return presentation consume that summary without fixed-text or target-reward fallback authority.

## Confirmed Discussion Result

The user authorized completion of the remaining review plan in dependency order. Accepted architecture already requires `StrategicBattleResultSummary` to carry identity, termination/outcome, objective, participant disposition/result, losses/recovery, rewards/location changes, report reference, and major attribution facts. The current Bridge only maps outcome plus remaining strength, while Strategic Management reconstructs rewards and narrative from target definitions and fixed fallback text.

This batch keeps the existing accepted result envelope as the sole source. Bridge may compile strategic target consequence definitions only after the envelope's Runtime, settlement, and report facts are complete; Strategic Management must consume the compiled summary rather than re-resolve a parallel reward/narrative truth. Categories unsupported by authoritative source facts remain explicitly empty/unknown and are never fabricated.

## Authority Impact

Impact: Medium across Strategic Battle Bridge, Battle Report/Settlement consumption, Strategic Management feedback, and return presentation. No authority document change is required because `system-design/strategic-battle-bridge-architecture.md` and `system-design/battle-result-settlement-architecture.md` already define the required mapping and ownership.

## Scope

- Add one behavior RED fixture containing objective/termination facts, deployed and reserve participants, corps loss, hero outcome, destination/regroup/retreat or command events, skill use/effect/failure facts, accepted settlement deltas, report identity/failure attribution, target reward/resources/equipment samples, and frozen equipment level.
- Extend the typed strategic summary only with durable consequence/report fields required by accepted authority; do not copy raw Runtime models wholesale or add parallel envelope authority.
- Compile the summary exclusively from `StrategicBattleActiveContext.ResultEnvelope`, its Runtime result/event stream, settlement plan, report, Bridge session/snapshot, and accepted target definitions.
- Preserve stable participant identity and deployed/reserve semantics. Map hero survived/defeated/retreated/unavailable, remaining strength/loss, routed/scattered/recovery indicators only when supported by authoritative facts.
- Map termination reason, objective facts, settlement changed identities, report ID/source events, command/skill contribution, failure candidates, equipment-level contribution, reward/resource/equipment consequence facts, and world/progression text.
- Validate cross-source identity and source-event consistency before summary acceptance. Missing, contradictory, or fabricated mappings reject without strategic mutation.
- Remove Strategic Management's target-definition/fixed-text fallback when a result is applied. Build durable feedback and the return notice from the compiled summary; retain explicit empty/unknown text only where authority has no fact.
- Preserve result-envelope CAS, replay fingerprint, settlement transaction, reward idempotency, participant casualty mapping, strategic control writeback, save/load, and legacy non-strategic report behavior.

## Non-Goals

- No new reward economy, equipment mechanics, support gameplay, experience progression, battle UI redesign, or report-screen redesign.
- No changes to regroup/retreat behavior, battle simulation, strategic travel, validation infrastructure, or general slimming.
- No read or modification under `scenes/world/preview/`.

## Constraints

- Work on `main`; do not create or switch branches.
- Use one explicitly configured `gpt-5.6-sol` + `high` executor; it must not spawn nested executors or subagents.
- Preserve the unrelated untracked `src/Runtime/Battle/BattleGroupCommanderTransitionCoordinator.cs.uid`; do not read, modify, delete, hash, diff, stage, or commit it.
- Lean gate: one RED, one focused Strategic Management GREEN regression, one affected build and low-concurrency `rpg.sln` build, one exact review. Repeat only after actual failure.
- Do not launch Godot or run a runner that enumerates the ignored preview directory.
- Required skills: `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. A valid Active Context summary retains exact session/snapshot/expedition/target identity, Runtime termination/outcome, objective result, report identity, and accepted settlement identity/source-event facts.
2. Every carried participant has one disposition; only deployed participants require Runtime casualty rows. Deployed results expose stable hero/corps identity, hero state, remaining strength and loss plus supported recovery/routed/retreated facts; reserves remain unchanged and do not receive fabricated outcomes.
3. Command/beacon/regroup/retreat, hero skill use/effect/failure, equipment level, major failure attribution, and changed hero/corps/group/location facts are mapped from their authoritative event/report/settlement/snapshot sources and remain traceable by IDs.
4. Victory target consequences compile reward claim, resource reward, equipment sample, world-change, progression, and player-readable reward lines once; defeat/retreat compile their accepted consequence text without granting victory rewards.
5. Strategic Management applies and persists only summary consequence facts. It no longer re-resolves target reward definitions or hardcodes generic victory/defeat/failure narrative during result application.
6. Presentation return notice consumes the persisted feedback built from summary facts and does not fall back to a second report/result truth for Strategic Management battles.
7. Missing/mismatched envelope components, source-event divergence, duplicate participant mapping, or contradictory consequence facts reject before candidate-state publication, persistence, context consumption, or reward mutation.
8. Exact replay remains idempotent; conflicting replay fails. Result fingerprint/digest includes every newly authoritative consequence field.
9. Existing casualty/cardinality, reserve, control, settlement/CAS, reward idempotency, save/load, and legacy non-strategic report regressions remain passing.
10. Focused regressions, affected build, low-concurrency solution build, and exact `godot-code-review` pass with no unresolved Critical/Improvement.

## Current Progress

Completed:

- Recovered P2-02 review evidence and accepted Result/Settlement/Bridge authority.
- Confirmed current Bridge summary maps only outcome, disposition, and remaining corps strength.
- Confirmed Strategic Management currently reconstructs target reward and fixed narrative when `HasConsequenceFacts` is false.
- Confirmed existing envelope already carries Runtime result/event stream, accepted `SettlementPlan`, and `BattleReportRecord`.
- Loaded the required implementation skills in the primary context.
- Added one complete Active Context RED fixture with deployed/reserve roles, objective/termination, command/beacon/regroup, skill effect/failure, settlement/report lineage, target consequences, and frozen equipment level.
- Extended the strategic summary with typed termination, objective, report, settlement, contribution, participant-state, recovery, equipment, reward, and changed-identity facts.
- Made Bridge validate source-event, participant, snapshot, settlement, report, and target-consequence consistency before setting the summary acceptance boundary.
- Removed Strategic Management target-reward and fixed narrative fallback; feedback, rewards, replay fingerprint, and return presentation now consume compiled/persisted summary facts.
- Completed the lean RED-GREEN-build-review gate with `csharp-godot`, `godot-testing`, and `godot-code-review`.

Remaining:

- None.

## Pause And Resume

- Blocker: None.
- Resume condition: None; scoped execution is complete.
- Resume entry: No work remains in this batch. Later review work must use a separate confirmed active task.
- Latest verification: Independent primary-context verification passed the full Strategic Management regression, confirmed empty dispositions reject at the compiled boundary, verified replay/conflict semantics and serialized fingerprint coverage, and found no unresolved Critical/Improvement.

## Execution Record

- 2026-07-13: Primary context classified the batch as Result/Settlement/Bridge content-contract completion and confirmed no authority conflict.
- 2026-07-13: Executor started on `main`, loaded `csharp-godot`, `godot-testing`, and `godot-code-review`, and set the task to `In Progress`.
- 2026-07-13: RED failed only at the intended missing complete-consequence boundary (`accepted envelope should compile consequence facts`).
- 2026-07-13: Affected Strategic Management build initially found one discarded hero-outcome local; the scoped correction was applied and the retry passed with only three existing warnings.
- 2026-07-13: GREEN `dotnet run --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj --no-build --no-restore` passed every registered case, including the new complete consequence summary case.
- 2026-07-13: `dotnet build rpg.sln --no-restore -maxcpucount:2 -v:minimal` passed with 26 existing warnings and zero errors.
- 2026-07-13: Exact `godot-code-review` and whitespace review passed with Critical 0 / Improvements 0. No Godot editor or preview-enumerating runner was used.
- 2026-07-13: Independent verification returned one scoped Critical: empty participant dispositions still activated the old impoverished direct-summary compatibility path.
- 2026-07-13: Corrective executor resumed the task at `In Progress` and loaded `csharp-godot`, `godot-testing`, and `godot-code-review`.
- 2026-07-13: Removed the empty-disposition compatibility branch. Every non-replay application now reaches unconditional full compiled-summary validation; the replay boundary also rejects missing consequence, identity, termination, report, or disposition facts before idempotent return.
- 2026-07-13: Added an explicit direct-summary test helper that supplies snapshot/report/termination identity, deployed dispositions, participant IDs, hero state, frozen strength/loss, equipment level, and consequence acceptance facts. Invalid direct summaries now assert compiled-contract rejection, and the complete duplicate fixture asserts exact replay idempotency.
- 2026-07-13: First corrective build exposed one helper nullable warning and the first regression run exposed unknown fixture roles; both scoped helper defects were corrected. A later replay-order attempt changed conflict failure semantics and was refined so compiled-boundary rejection precedes replay while full consequence validation remains after conflict classification.
- 2026-07-13: Final focused `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj --no-restore -maxcpucount:2 -v:minimal` passed with zero errors and only three pre-existing Godot API obsolescence warnings; full `StrategicManagementRegression` passed every registered case.
- 2026-07-13: Corrective `git diff --check` passed. Exact `godot-code-review` found Critical 0 / Improvements 0; no Godot editor or preview-enumerating runner was used.
- 2026-07-13: Single unreachable-line cleanup audit found the duplicate second `return null;` already absent from `ResolveCommittedReplay`'s `!exact` branch. The one remaining return is required to preserve conflict behavior, so implementation was left unchanged; handoff remains `Awaiting Verification`. Exact `godot-code-review` found Critical 0 / Improvements 0, and no build, test, Godot editor, or preview inspection was run.
- 2026-07-13: Final independent verification passed the full Strategic Management regression, confirmed no old empty-disposition acceptance branch remains, checked exact replay/conflict behavior and serialized fingerprint coverage, and accepted every criterion.

## Final Result

Verified complete. Runtime, settlement, report, snapshot, and accepted target facts compile once in Bridge; Strategic Management and return presentation consume the compiled/persisted result without target-reward, fixed-narrative, or empty-disposition compatibility fallback. Exact replay/conflict behavior remains intact. Remaining scoped risks: None.
