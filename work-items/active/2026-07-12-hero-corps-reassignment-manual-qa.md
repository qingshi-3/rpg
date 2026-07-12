# Hero Corps Reassignment Manual QA

Status: Awaiting Verification
Executor: Unassigned (historical implementation completed before migration)
Verifier: Independent verifier / user
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Finish the remaining in-game verification for the implemented hero main-corps reassignment workbench without changing its accepted design or implementation.

## Confirmed Discussion Result

- Implementation and automated verification are complete.
- The recruitment surface is the first-version reassignment workflow; there is no separate corps tab.
- Recruitment cards show compact reserve-soldier and resource requirements, not refund or net-impact rows.
- Replacing a main corps refunds the old corps according to current strength and assigns the new corps; the old corps must not remain as hidden inventory.
- Only the remaining manual QA is active.

## Authority Impact

None. Current gameplay and system authority already contain the accepted behavior. This task must not change authority, code, scenes, resources, or tests unless verification finds a defect and a new confirmed execution direction is recorded.

## Execution Scope

In a playable city-management flow:

1. Confirm there is no separate `编制` tab.
2. Open `招兵`, select each hero, and confirm the selected hero's current corps is shown.
3. Confirm each troop option directly shows reserve-soldier and resource costs as compact icon/amount attributes.
4. Confirm options do not print separate refund or final net-impact rows for heroes with an existing corps.
5. Replace a hero's corps and confirm the hero shows the new corps.
6. Confirm the old corps does not appear as a hidden extra city row.
7. Confirm resources and reserve soldiers change by the expected net amount.

## Non-Goals

- Do not redesign the workbench, corps model, refund rules, or UI.
- Do not add a corps inventory, reassignment flow, or unrelated QA.
- Do not fix defects within this verification task without returning to discussion and updating this work item.

## Constraints And Risks

- Preserve the current shared worktree.
- A failed observation must include reproduction steps, expected result, actual result, and relevant logs or capture.
- Historical evidence is available through `history/README.md` only if exact implementation context is required.

## Acceptance Criteria

- All seven manual checks pass, or each failure is recorded precisely and the task returns to `Needs Discussion`.
- No state-changing repository work is performed as part of verification.

## Current Progress Snapshot

### Completed

- Implementation completed.
- Strategic Management regression passed.
- Complete Presentation regression passed after unrelated resource-taxonomy cleanup.
- Project build passed with zero warnings and errors in the recorded implementation verification.

### Remaining

- The seven in-game checks in Execution Scope.

### Pause Or Blocker

- Awaiting independent in-game verification.

### Resume Condition

- A verifier can run the city-management flow in Godot.

### Resume Entry

- Read this task and current gameplay/system authority, then perform only the seven listed checks.

### Latest Verification

- Automated implementation evidence passed on 2026-07-08 and 2026-07-10; manual QA remains unperformed.

## Execution Record

- Migrated from the retired implementation-record queue on 2026-07-12. The original record remains unchanged under `history/implementation-proposals/2026-07-08-hero-corps-reassignment-workbench.md`.

## Final Result

Pending independent manual verification.
