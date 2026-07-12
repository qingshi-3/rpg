# Work Items

This directory is the active execution route for all state-changing work. A work item is a self-contained contract between confirmed discussion, execution, and independent verification; it is not gameplay or architecture authority.

## Required Workflow

```text
discussion
-> user confirmation
-> create or update one work item in active/
-> synchronize durable gameplay or system authority when required
-> execution with progress and evidence updates
-> Awaiting Verification
-> independent verification -> Completed -> archive under archived/YYYY/

Any active state -> Cancelled -> archive under archived/YYYY/
```

An execution Agent must have a user-confirmed active work item before changing state. It reads that item, applicable `AGENTS.md` files, current authority, and repository state. It must not rely on conversational memory, create parallel proposal documents, or silently change confirmed conclusions.

## States And Transitions

| State | Meaning | Legal next states |
|---|---|---|
| `Ready` | Confirmed and complete enough to execute. | `In Progress`, `Needs Discussion`, `Paused`, `Blocked`, `Cancelled` |
| `In Progress` | An execution Agent is actively applying the confirmed scope. | `Awaiting Verification`, `Needs Discussion`, `Paused`, `Blocked`, `Cancelled` |
| `Needs Discussion` | Execution found a direction conflict, missing decision, or authority contradiction. No state-changing work continues. | `Ready`, `Cancelled` |
| `Paused` | Work intentionally stopped at a safe boundary with a known resume condition. | `Ready`, `Cancelled` |
| `Blocked` | Work cannot continue until a named external condition is satisfied. | `Ready`, `Cancelled` |
| `Awaiting Verification` | Scoped execution is complete and evidence is recorded; independent verification remains. | `In Progress`, `Needs Discussion`, `Completed`, `Cancelled` |
| `Completed` | Scope and acceptance are independently verified. | Archive only |
| `Cancelled` | The user or confirmed direction ended the task without completion. | Archive only |

`Ready`, `In Progress`, `Needs Discussion`, `Paused`, `Blocked`, and `Awaiting Verification` remain in `active/`. Only `Completed` and `Cancelled` move to `archived/YYYY/`.

## Required Document Shape

Every work item must contain:

- title, `Status`, `Executor`, `Verifier`, created date, and updated date; fill executor and verifier when known and otherwise mark them unassigned;
- objective and confirmed discussion result;
- authority impact, including which durable authority must change before implementation or an explicit statement that none changes;
- execution scope and non-goals;
- constraints and important risks or unresolved constraints;
- acceptance criteria;
- current progress snapshot with completed work and remaining work;
- pause or blocker, resume condition, resume entry, and latest verification;
- execution record and final result.

The document must be concise but sufficient for a fresh execution Agent. Do not embed full transcripts, authority bodies, code diffs, or complete logs.

## Progress, Pause, And Resume

The execution Agent sets `In Progress` when work begins and maintains the current progress snapshot after meaningful milestones. A safe handoff must name:

- what is complete;
- what remains;
- the exact pause reason or blocker;
- the condition that permits resumption;
- the first files, commands, or checks the next Agent should use;
- the latest successful and failed verification, including whether failures are related.

Use `Paused` for an intentional stop with no external failure. Use `Blocked` only for an external dependency or condition that prevents progress. Use `Needs Discussion` when continuing would require changing confirmed conclusions, scope, authority, or acceptance. The execution Agent must stop mutations in all three states.

After discussion resolves `Needs Discussion`, the discussion Agent revises the same work item, records the newly confirmed conclusion, and returns it to `Ready`. Resuming `Paused` or `Blocked` also returns the item to `Ready` after its resume condition is met; the next execution Agent then sets `In Progress`.

## Authority And Verification

Work items authorize task execution but never override `gameplay-design/` or `system-design/`. Durable gameplay, architecture, persistent-state, runtime-ownership, cross-system, scene/resource-taxonomy, or future-Agent decisions must update the appropriate authority at the start of execution. Any contradiction returns the work item to `Needs Discussion`.

When scoped execution is complete, the execution Agent may only set `Awaiting Verification`; it must not self-assign `Completed`. Record commands, outcomes, and known unrelated failures. The verifier must not be the same Agent or context that completed the current execution changes. A parent Agent that delegated execution may verify in its independent context. If neither an independent context nor user verification is available, the item remains `Awaiting Verification` and must not be set to `Completed`.

The independent verifier either returns the item to `In Progress` with actionable scoped findings, returns it to `Needs Discussion` for a direction issue, or sets `Completed` after every acceptance criterion is satisfied.

## Archive Rules

Completed or cancelled items move without rewriting their evidence to `archived/YYYY/`. The final result must state the disposition, verification conclusion, remaining risks or explicitly `None`, and any follow-up work that requires a separate confirmed work item. Archived items are historical evidence, not active authority or automatic instructions.

Use `archived/README.md` as the archive boundary and organize archived tasks by completion or cancellation year.
