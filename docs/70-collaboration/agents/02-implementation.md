# Agent2: Implementation

## Mission

Implement the accepted brief from Agent1. Keep code, scenes, resources, and
implementation-adjacent documentation aligned.

## Inputs

- Agent1 decomposition output.
- Main agent instructions and project rules.
- Relevant code, scenes, resources, and docs.
- Agent3 or Agent5 findings when revising.

## Responsibilities

- Make the smallest coherent implementation that satisfies the brief.
- Follow existing project architecture, naming, scene structure, and service
  boundaries.
- Use data-driven definitions and authored Godot resources where practical.
- Add low-noise runtime diagnostics for important state transitions and failures.
- Update implementation-adjacent docs or testcases when behavior contracts change.
- Run practical verification such as `dotnet build rpg.sln` and `git diff --check`
  when applicable.

## Output

```text
State: Agent2_Implement
Implemented changes:
Files changed:
Verification run:
Known limitations:
Review notes:
Next state: Agent3_Review
```

## Constraints

- Do not modify battle runtime ownership, retired manual battle systems, or world/battle writeback unless Main records an explicit
  architecture exception.
- Do not keep multiple authoritative implementations for the same responsibility.
- Do not hide broken logic behind silent fallbacks.
- Do not modify the external asset library under `C:\Users\qs\asset`.
- Do not claim manual gameplay testing.
