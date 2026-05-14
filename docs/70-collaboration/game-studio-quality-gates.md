# Game Studio Quality Gates

This document adapts useful process ideas from
`Donchitos/Claude-Code-Game-Studios` into this project's documentation workflow.
It is not a runtime dependency and does not import that repository's Claude
Code agents, skills, hooks, or commands.

## Source

- Reference repository: https://github.com/Donchitos/Claude-Code-Game-Studios
- License: MIT.
- Adaptation rule: keep the workflow principles, rewrite them for this Godot RPG,
  and let this project's `../../AGENTS.md` and `docs/README.md` remain authoritative.

## Adoption Boundary

Use these gates as checklists during planning, implementation, review, and
verification.

Do not:

- Copy `.claude/agents`, `.claude/skills`, hooks, slash commands, or settings into
  this project.
- Let external workflow documents override local architecture boundaries.
- Add process documents to `../../AGENTS.md` unless they are short, stable routing rules.
- Treat a checklist pass as a substitute for local build, runtime, or manual
  verification.

## Gate 1: Intake

Before implementation, clarify:

- Player-visible outcome.
- Systems affected.
- Whether the change is code, authored content, scene configuration, or docs.
- Whether the work stays inside existing extension points.
- Whether a technical change note or testcase update is needed.

Stop and document the risk if the change touches Battle flow, AP, TurnSystem, or
cross-system contracts.

## Gate 2: Architecture

Check:

- Concepts use project terminology, especially `WorldSite`, `WorldArmy`,
  `ThreatPlan`, and battle/site runtime boundaries.
- Gameplay variation is data-driven where practical.
- Runtime code stays generic and avoids one-off plot branches.
- New systems communicate through service/result/request objects instead of UI
  node coupling.
- Save-relevant state is explicit in domain state objects.
- Runtime diagnostics are low-noise and tied to state transitions.

## Gate 3: Godot C# Review

Use `godot-csharp-review-checklist.md` for detailed review. At minimum, check:

- Exported paths and scene nodes fail or warn explicitly; core runtime logic must not hide broken contracts behind fallback paths.
- Godot lifecycle order is respected.
- Scene files and script names match current terminology.
- Per-frame logic avoids expensive allocations and noisy logs.
- C# null handling is explicit around Godot node lookups.

## Gate 4: QA

For code or scene changes:

- Run `dotnet build rpg.sln`.
- Run `git diff --check`.
- Update or create a focused testcase when behavior changes.
- Smoke test the critical path when a local Godot run is practical.
- Record unverified runtime checks in the relevant testcase when manual testing is
  blocked by missing authored content.

Use `docs/60-qa/testcases/smoke-check-template.md` for the shape of a smoke pass.

## Gate 5: Milestone Review

At the end of a substantial step, record:

- What is now implemented.
- What remains placeholder behavior, and whether any fallback is limited to non-authoritative presentation only.
- What requires authored content, map configuration, assets, or user decisions.
- Build and verification results.
- The next intervention point.
