# Technical Changes

This directory stores lightweight planning notes for changes that can affect architecture, shared rules, or multiple modules.

## Recent Notes

- 2026-05-14: `2026-05-14-world-runtime-state-mutation.md`
- 2026-05-14: `2026-05-14-resident-unit-definition-cache.md`
- 2026-05-13: `2026-05-13-battle-audio-slice.md`

Do not use this directory for ordinary todos, temporary progress logs, or module implementation details.

## When To Write One

Create a technical change note before implementation when a task may:

- Modify Battle flow, AP, or TurnSystem behavior.
- Change shared concepts such as `BattleAction`, `Effect`, `Condition`, `TargetRule`, or Intent.
- Affect multiple systems, scenes, or documents.
- Require a staged migration of data, scenes, or resources.
- Introduce a known architecture risk that needs explicit acceptance.

Small additions that stay inside existing extension points do not need a technical change note.

## Suggested Template

Use filenames like `YYYY-MM-DD-topic` with the `.md` extension and cover:

- Background and goal.
- Non-goals.
- Affected systems and files.
- Shared rule or data impact.
- Implementation steps.
- Risks and rollback plan.
- Documentation updates.
- Test case updates.
- Manual acceptance checks.

## Current Policy

- If the change fits only `Effect`, `Condition`, `TargetRule`, or Definition data, prefer a module document or test case update.
- If the change breaks those boundaries, document the risk here before coding.
