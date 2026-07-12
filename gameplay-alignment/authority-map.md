# Authority Map

This document records which sources control gameplay, architecture, and execution under the active work-item workflow.

## Authority Order

1. `gameplay-design/`: accepted player-facing gameplay and content-system rules.
2. `system-design/`: accepted implementation architecture, system ownership, data flow, and contracts.

Code, resources, active work items, gap registers, workstream records, and historical records are evidence or execution context. They do not override accepted gameplay or system authority.

## Discussion And Execution Rule

A user-confirmed discussion result controls the current task through one self-contained document in `work-items/active/`. If the result changes a durable gameplay or architecture rule, synchronize the relevant authority document at the start of execution before modifying implementation.

When sources conflict:

```text
identify the conflict
-> return to discussion
-> obtain user confirmation
-> create or update the active work item
-> update gameplay or system authority when durable rules changed
-> execute and verify against the confirmed active work item and current authority
```

Do not reinterpret accepted design from local code, resources, work records, or historical proposals. If implementation exposes a missing or wrong rule, stop execution and return to discussion.

## Work And History Routes

`work-items/README.md` defines the active task lifecycle. Execution requires a confirmed active work item, and direction conflicts return that item to `Needs Discussion`.

`history/README.md` is the only route to records created under retired workflows. Historical records are not active authority or execution gates. Do not read their bodies unless the user explicitly requests history, resumes that exact subject, or asks for a historical investigation requiring a named record.
