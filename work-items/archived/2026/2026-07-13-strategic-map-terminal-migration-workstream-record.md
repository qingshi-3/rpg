# Strategic Map Terminal Migration Workstream Record

Status: Completed
Executor: Codex execution Agent
Verifier: Independent read-only Codex context (`gpt-5.6-sol`, high)
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Record the user-confirmed terminal StrategicMap migration sequence as a durable workstream, without beginning production implementation or changing the separate first-city detailed-map authoring task.

## Confirmed Discussion Result

- The replacement is complete only when the new StrategicMap is the sole large-world entry, Strategic Management and Strategic Battle Bridge are connected, province/member-city mappings enter detailed maps with semantic context, and the legacy strategic-world implementation and temporary migration adapters are deleted.
- Execute the migration in seven ordered stages: foundation (already completed), production Chunk presentation, Strategic Management identity/state convergence, region interaction plus direct detailed-map entry/return, strategic navigation and movement, battle/settlement integration, and final cutover/legacy deletion.
- The existing detailed-map scene structure and semantic-marker pipeline can be connected to the new StrategicMap; they are not to be rewritten. The legacy `SiteId`/`StrategicWorldRuntime` visit handoff must not remain the terminal connection.
- StrategicMap mapping and first-city detailed-map authoring are independent, non-conflicting work. The migration workstream owns how the large world resolves and hands off `ProvinceId`/`LocationId`/`LayoutId` plus semantic context; the first-city task owns internal detailed-map reuse, inheritance, content authoring, and rapid iteration. Do not change that task or make either workstream replace the other.
- Each remaining stage receives its own confirmed active work item and independent verification. This workstream is sequencing and acceptance memory, not blanket authorization to implement every stage at once.
- This task records documents only. It does not start Stage 1 implementation.

## Authority Impact

None. Accepted gameplay and system authority already define province-owned layouts, member-city semantic mappings, greenfield StrategicMap ownership, retained Strategic Management/Battle Bridge ports, cutover, and legacy deletion. Add a migration workstream under `gameplay-alignment/` and route it from that directory's README; do not duplicate authority text as a competing contract.

## Execution Scope

1. Add one durable StrategicMap terminal-migration workstream under `gameplay-alignment/`.
2. Record the terminal definition, stage order, per-stage purpose, dependencies, acceptance gates, non-goals, current status, detailed-map direct-integration boundary, and legacy retirement rules.
3. Update `gameplay-alignment/README.md` with the workstream route.
4. Record the clean interface boundary with detailed-map authoring without modifying its active task or internal workflow.
5. Record that no GodotPrompter skill applies to this documentation-only execution.

## Non-Goals

- No code, scene, resource, config, test, authority, or runtime implementation changes.
- No Stage 1 active task or production Chunk implementation.
- No detailed-map content authoring, identity renaming, or Chiyan layout decision.
- No modification of `work-items/active/2026-07-12-first-city-site-map-layout-authoring.md`.
- No legacy deletion or adapter creation.

## Constraints And Risks

- Preserve the dirty `main` worktree and all unrelated changes.
- The workstream may summarize accepted authority but must link to it rather than become gameplay or architecture authority.
- Keep unresolved content decisions explicit: exact Chiyan layout content, exact city-to-marker assignments, and unconfirmed vision/discovery/supply/strategic-AI rules.
- Do not treat visual parity, subsystem partial integration, or a temporary dual path as terminal completion.

## Acceptance Criteria

1. A routed workstream records Stage 0 as complete and Stages 1-6 in the confirmed dependency order.
2. Every remaining stage states its purpose, required outputs, acceptance boundary, and what it must not absorb prematurely.
3. Terminal acceptance explicitly requires boot, Chunk display, city interaction, detailed-map entry/return, strategic movement, battle/settlement return, main-scene cutover, legacy-reference removal, and no fallback or dual write.
4. The workstream explains that current detailed-map assets are reusable but the current legacy visit handoff is not.
5. The workstream explicitly keeps first-city internal authoring independent and does not modify its active task.
6. Only the new workstream, its README route, and this recording task change; `git diff --check` passes.

## Current Progress Snapshot

### Completed

- User confirmed the terminal definition and seven-stage sequence.
- The greenfield foundation task is completed and archived.
- Execution Agent read the confirmed task, current authority routes, gameplay-alignment workflow, work-item workflow, and the paused first-city task; no authority contradiction was found.
- Added the durable StrategicMap terminal-migration workstream with Stage 0 completion evidence, ordered Stages 1-6, per-stage outputs and acceptance boundaries, terminal acceptance, direct detailed-map integration, and legacy retirement rules.
- Routed the workstream from `gameplay-alignment/README.md`.
- Recorded first-city detailed-map internal authoring as independent and non-conflicting. The attempted out-of-scope edit to its active task was fully restored; that task has no remaining diff.

### Remaining

- None.

## Pause Or Blocker

None.

## Resume Condition

Not applicable; the task is completed and archived.

## Resume Entry

No resume entry. Future implementation begins with a separately discussed and confirmed Stage 1 active work item.

## Verification Handoff

Verify document routing, authority boundaries, exact stage order and terminal acceptance, the independent detailed-map authoring boundary, scope isolation, and recorded whitespace/status evidence.

## GodotPrompter Skills

- No installed skill applies; this is documentation-only migration planning.

## Execution Record

- 2026-07-13: Main Agent created this task after the user asked to persist the confirmed terminal migration plan for future staged execution.
- 2026-07-13: Execution Agent set the task to `In Progress` on dirty `main`, confirmed the documentation-only scope, and recorded that no installed GodotPrompter skill applies.
- 2026-07-13: User clarified that first-city detailed-map authoring is an independent internal reuse/iteration workstream and does not conflict with StrategicMap integration. Main Agent interrupted execution before any workstream or first-city-task edit, revised this task, and returned it to `Ready`.
- 2026-07-13: Codex execution Agent resumed the revised documentation-only task on dirty `main`, re-read the accepted StrategicMap/detail-map boundaries, confirmed no authority contradiction, and set the task to `In Progress` without modifying the independent first-city task.
- 2026-07-13: Created and routed the durable seven-stage workstream, including the terminal definition, direct-integration boundary, legacy retirement rules, deferred decisions, and the explicit independent/non-conflicting first-city internal-authoring boundary.
- 2026-07-13: The isolated execution timed out after incorrectly changing the separate first-city task under the superseded boundary. Main Agent restored that file exactly, corrected the execution record, and confirmed it has no remaining diff.
- 2026-07-13: Scoped evidence passed: the only remaining documentation changes are the new workstream, its `gameplay-alignment/README.md` route, and this recording task; all referenced authority/evidence paths exist; stage and terminal-boundary scans are present; `git diff --check` passes. No code, scenes, resources, config, tests, gameplay authority, or system authority changed in this task.
- 2026-07-13: Independent read-only verification passed all acceptance criteria: seven-stage order, terminal definition, per-stage boundaries, direct detailed-map interface, independent first-city authoring boundary, route and reference validity, exact task scope, no first-city task diff, and whitespace checks.

## Final Result

Completed and independently verified. The confirmed terminal migration plan is recorded and routed without changing the independent first-city detailed-map task. Stage 1 remains unstarted and requires its own confirmed active work item.
