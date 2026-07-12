# Battle Regroup And Retreat Product Commands

- Status: Completed
- Executor: Codex (current execution context)
- Verifier: Codex primary context (independent from executor)
- Created: 2026-07-13
- Updated: 2026-07-13

## Objective

Complete P1-08 by making combined battle-group `Regroup` and `Retreat` real production commands from the authored battle HUD through the existing Application submission boundary into Runtime-owned group state, events, feedback, and result attribution.

## Confirmed Discussion Result

The user confirmed continuous remediation in review priority order and asked for lean batch verification. VS-10 requires a live regroup command; accepted gameplay and command architecture also require combined battle-group retreat. Both commands must use the existing command request, Application authorization, Runtime controller, commander-group state, and event/report chain. Ordinary tactical commands must never be routed through the hero-skill resolver.

Regroup is a recoverable group intent: selected owned groups break or reduce local engagement, clear incompatible local assignments, and visibly converge on a group-owned rally/action-zone target before returning to a coherent group state. Retreat is a stronger group intent: selected owned groups break contact and move toward the accepted retreat/fallback target; completion is explicit and may produce `PlayerRetreat` battle termination when no deployed player group remains in the fight. Runtime, not Presentation, owns these transitions and completion decisions.

## Authority Impact

Impact: Medium across Battle Command, Runtime State, Input, and Presentation UI. No authority edit is required: `gameplay-design/vertical-slices/first-playable-slice.md`, `system-design/battle-command-architecture.md`, and `system-design/battle-runtime-architecture.md` already define the accepted behavior and ownership.

## Scope

- Add behavior RED coverage for live and tactical-pause regroup/retreat submission, authorization, Runtime dispatch, group-state transition, effect, events, and readable rejection.
- Extend the existing Application validator/submission service for combined `Regroup` and `Retreat`, including active battle, deployed group, player ownership, channel, selection, and atomic multi-select checks.
- Add a focused Runtime tactical-command resolver and dispatch path. Do not pass regroup/retreat or any ordinary command to `BattleRuntimeHeroSkillCommandResolver`.
- Store accepted regroup/retreat intent only in authoritative battle-group commander/tactical state; actors retain only derived execution phase/cache facts.
- Give regroup a deterministic, visible convergence behavior using existing group/formation/action-zone facts. Give retreat a deterministic fallback target from accepted snapshot/runtime topology; reject explicitly when no legal target exists.
- Emit accepted, rejected, superseded/interrupted where applicable, completed, and failed command facts with command/group identity. Preserve low-noise logging and report attribution.
- Add authored HUD controls and Chinese disabled/rejection feedback for current selected player battle groups. Route clicks and any shortcut through the existing Application submission boundary; commands must work while tactically paused without advancing simulation time.
- Preserve destination beacon, hold, hero skill, pause, combat, outcome, and settlement behavior.

## Non-Goals

- No new hero/corps channel vocabulary beyond combined regroup/retreat.
- No formation editor, AI redesign, navigation rewrite, report-content expansion beyond consuming the new command facts, or general code slimming.
- No changes or reads under `scenes/world/preview/`.

## Constraints

- Work on `main`; do not create or switch branches.
- Use one explicitly configured `gpt-5.6-sol` + `high` executor; it must not spawn nested executors or subagents.
- Preserve the unrelated untracked `src/Runtime/Battle/BattleGroupCommanderTransitionCoordinator.cs.uid`; do not read, modify, delete, stage, or commit it.
- Prefer existing authored HUD scenes/resources and containers; do not construct a parallel command panel in C#.
- Lean gate: one RED, one focused GREEN regression set, one affected build and one low-concurrency solution build, then one exact review. Repeat only after actual failure.
- Do not launch Godot or run any runner that enumerates the ignored preview directory.
- Required skills: `csharp-godot`, `input-handling`, `godot-ui`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. Selected owned deployed battle groups can submit combined regroup and retreat from the production HUD in live battle and tactical pause through `BattleCommandSubmissionService`.
2. Missing selection, enemy/non-deployed group, wrong battle/channel, invalid action lock, and missing/unreachable regroup or retreat target reject atomically with stable reason codes and Chinese UI feedback; rejected commands do not mutate Runtime or emit accepted events.
3. Runtime dispatches regroup/retreat to a dedicated tactical resolver, never the skill resolver, and group commander state is the only command-intent owner for 1/2/N actors.
4. Regroup visibly changes group behavior by clearing incompatible local combat intent and converging toward a deterministic group rally target; it reaches an explicit completed or failed state.
5. Retreat visibly breaks contact and moves toward an accepted fallback/retreat target; group completion is explicit, and all-player-groups-retreated termination uses `BattleTerminationReason.PlayerRetreat` without fabricating victory/defeat.
6. Accepted/rejected/interrupted-or-superseded/completed/failed events carry command and battle-group identity and remain attributable to runtime feedback/report facts.
7. Tactical pause accepts or rejects commands without advancing combat time; resume executes accepted intent.
8. Destination beacon, hold, hero-skill authorization, combat, normal outcome, settlement, and existing command-state/cardinality regressions remain passing.
9. Authored HUD resources expose usable focusable controls and disabled reasons; no runtime-built replacement control tree is introduced.
10. Focused regressions, affected build, low-concurrency `rpg.sln` build, and exact `godot-code-review` pass with no unresolved Critical/Improvement.

## Current Progress

Completed:

- Recovered P1-08 evidence and confirmed accepted authority already defines regroup/retreat ownership and event rules.
- Confirmed the reusable path is Presentation -> `BattleCommandSubmissionService` -> `BattleRuntimeSessionController` -> group commander/tactical state.
- Confirmed current controller dispatches destination beacons separately but otherwise falls through to the hero-skill resolver.
- Loaded the required implementation skills in the primary context.
- Added Application and Runtime atomic authorization for combined regroup/retreat, with a dedicated tactical resolver that never enters the hero-skill resolver.
- Added commander-owned targets, state progression, supersession/interruption, completion/failure facts, regroup convergence, retreat exit facts, and `PlayerRetreat` termination.
- Added authored live and tactical-pause HUD controls, focus/disabled behavior, Chinese feedback, and Application-bound submission for current multi-selection.
- Added focused behavior coverage for live/pause submission, ownership/channel/battle/action-lock/target rejection, convergence, completion, event identity, HUD authoring, and retreat termination.
- Completed the required exact `godot-code-review`; one all-member defensive authorization improvement was resolved, with no remaining Critical or Improvement findings.

Remaining:

- None.

## Pause And Resume

- Blocker: None.
- Resume condition: None; scoped execution is complete.
- Resume entry: No work remains in this batch. Later review work must use a separate confirmed active task.
- Latest verification: Independent primary-context verification passed the 5/5 focused behavior cases, confirmed dedicated tactical dispatch and atomic validation, and found no unresolved Critical/Improvement. Executor compatibility cases passed 12/12; affected project and low-concurrency `rpg.sln` builds succeeded with 0 errors.

## Execution Record

- 2026-07-13: Primary context classified the task as Battle Command/Input/UI/Runtime State, confirmed existing architecture is reusable, and created this execution contract from already accepted authority and P1-08 review evidence.
- 2026-07-13: Executor started work, confirmed the accepted Application -> Runtime commander-state boundary, and loaded `csharp-godot`, `input-handling`, `godot-ui`, `godot-testing`, and `godot-code-review`.
- 2026-07-13: RED failed as expected because regroup fell through the skill path and the authored HUD controls were absent.
- 2026-07-13: GREEN added the dedicated tactical path, commander-owned execution/completion, authored HUD controls, and `PlayerRetreat`; focused cases passed 5/5 and compatibility cases passed 12/12.
- 2026-07-13: Affected and solution builds passed with 0 errors. Exact `godot-code-review` resolved its only Improvement (all-member Runtime authorization) and found no unresolved Critical/Improvement. Executor handed off at `Awaiting Verification`.
- 2026-07-13: Independent verification passed 5/5 focused cases without rebuilding, checked the dedicated Runtime dispatch and authored UI boundary, and accepted all criteria.

## Final Result

Verified complete. Regroup and retreat are production combined commands from authored live/pause HUD controls through Application into Runtime-owned commander state, events, movement effects, explicit completion/failure, and `PlayerRetreat` termination. Remaining scoped risks: None.
