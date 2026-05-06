# Multi-Agent Workflow

This document defines the project's persistent agent workflow. It is a workflow
contract, not a requirement to spawn temporary runtime agents. If the runtime has
durable agent support, map each role document directly to that agent. If it does
not, the main agent still follows this state machine and handoff protocol.

## Goals

- Keep requirement analysis, implementation, review, acceptance, and documentation
  consolidation as separate responsibilities.
- Make agent handoffs explicit enough that work can resume after interruptions.
- Preserve the project's documentation routing rules and architecture boundaries.
- Avoid using process documents as a substitute for build checks or human gameplay
  testing.

## Roles

- Main Agent: workflow orchestration and final communication.
- Agent1: requirement decomposition.
- Agent2: implementation and implementation-adjacent documentation.
- Agent3: implementation review.
- Agent5: acceptance gate for requirement coverage, architecture, C# quality,
  Godot quality, risk, and performance.
- Agent4: documentation consolidation after acceptance.

File numbers follow workflow order, so Agent5's acceptance spec is numbered
before Agent4's final documentation spec.

Role specs:

- `agents/01-requirement-breakdown.md`
- `agents/02-implementation.md`
- `agents/03-review.md`
- `agents/04-acceptance-gate.md` (Agent5)
- `agents/05-documentation-consolidation.md` (Agent4)

## Applicability

Use the full workflow for:

- Code implementation and refactor tasks.
- Runtime logic, scene structure, UI, asset/resource, and Godot authoring changes.
- Architecture, cross-system, persistence, battle handoff, world flow, and content
  pipeline work.
- Substantial documentation changes.
- Any task where the user explicitly asks to use the workflow.

The main agent may bypass the full workflow for simple status checks, direct
answers, command outputs, small typo fixes, and clearly non-behavioral edits.

## State Machine

```text
Intake
-> Agent1_Decompose
-> Main_Decomposition_Gate
-> Agent2_Implement
-> Agent3_Review
   -> Agent2_Revise
   -> Agent3_Review
-> Agent5_Acceptance_Gate
   -> Agent2_Revise
   -> Agent3_Review
   -> Agent5_Acceptance_Gate
-> Agent4_Documentation_Consolidation
-> Main_Final_Response
```

Exceptional states:

```text
Blocked        # missing information, missing assets, architecture conflict, or tooling failure
User_Clarify   # user decision is required before a safe implementation can continue
Cancelled      # user stops or redirects the task
Deferred       # intentionally moved out of the current task scope
```

## State Responsibilities

### Intake

Main agent reads the user request, project rules, relevant docs, and working tree
context. It decides whether the full workflow applies.

Exit criteria:

- Workflow applicability is known.
- Obvious blockers or required user decisions are identified.

### Agent1_Decompose

Agent1 breaks the request into functional points, affected systems, architecture
risks, implementation boundaries, and acceptance criteria.

Exit criteria:

- The implementation task is specific enough for Agent2.
- Risks that need a technical change note or user decision are surfaced.

### Main_Decomposition_Gate

Main agent checks whether Agent1's output is actionable and aligned with project
rules. It may ask the user only when a decision is required.

Exit criteria:

- Agent2 has a bounded implementation assignment.

### Agent2_Implement

Agent2 edits code, scenes, resources, and implementation-adjacent documentation.
Agent2 may update docs while developing when that prevents stale contracts or
lost context.

Exit criteria:

- Requested behavior is implemented or a blocker is documented.
- Build and static checks that are practical in the current environment have been
  run or explicitly marked as not run.
- Changed files and known limitations are summarized for review.

### Agent3_Review

Agent3 reviews the implementation for bugs, regressions, edge cases,
maintainability, and missing tests or checks.

Exit criteria:

- Pass, or a focused list of required fixes is returned to Agent2.

### Agent5_Acceptance_Gate

Agent5 verifies requirement coverage, architecture compliance, Godot resource
authoring, C# quality, performance risk, runtime diagnostics, and remaining
manual-test gaps.

Agent5 does not claim manual gameplay testing, engine runtime testing, or visual
QA unless a human or tool actually performed those checks.

Exit criteria:

- Pass, or a focused list of acceptance failures is returned to Agent2.

### Agent4_Documentation_Consolidation

Agent4 performs the final documentation pass only after Agent5 passes. It updates
or trims routed documentation so the accepted implementation is reflected without
duplicating temporary notes.

Exit criteria:

- Relevant docs and indexes are updated.
- Obsolete or misleading notes introduced by the task are removed or routed.

### Main_Final_Response

Main agent summarizes the delivered result, verification, limitations, and
manual checks still required. Main agent does not hide review or acceptance risks.

## Handoff Format

Each agent handoff should be concise and include:

```text
State:
Input:
Output:
Files changed or reviewed:
Risks:
Next state:
```

For failures, include:

```text
Blocking issue:
Why it matters:
Required fix or decision:
Return state:
```

## Coordination Rules

- Main agent owns state transitions and resolves conflicting agent outputs.
- Agent2 must not ignore Agent3 or Agent5 findings unless Main records why the
  finding is out of scope or incorrect.
- Agent4 does not change business code. It may fix broken documentation links or
  obviously stale comments.
- Manual gameplay testing remains a human activity unless a concrete automated or
  local engine run is explicitly performed.
- Substantial cross-system changes should update `docs/technical-changes/` before
  or during implementation.
