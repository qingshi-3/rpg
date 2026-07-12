# Battle Thunder Mark Lifetime Guard Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: Battle thunder-mark FX lifetime timers must not let stale await continuations free a node after a newer `Play()` cycle has started.
- Originating design proposal: Not required; current accepted authority already defines Runtime truth and Presentation-only visual feedback boundaries.
- Amendment proposals: None.
- Blocking issues: `BattleThunderMarkFx.Play()` can start multiple lifetime waits, while `_ExitTree()` only kills the Tween and does not invalidate pending timer continuations.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements `system-design/hero-led-light-rts-system-architecture.md`, especially the Presentation/UI layer boundary.
- Implements `system-design/battle-runtime-architecture.md`, especially the rule that Presentation visual interpolation and feedback do not own Runtime truth.
- Implements `system-design/presentation-ui-layout-architecture.md`, especially Presentation-owned overlay feedback and failure rules.
- Uses GodotPrompter skills: `using-godot-prompter`, `csharp-godot`, `csharp-signals`, `godot-debugging`, `godot-testing`, and `godot-code-review`.

## Goal

Protect `BattleThunderMarkFx` from stale timer and tween callbacks while preserving its current authored scene, lifetime, pause behavior, and visual loop.

After this slice, the invariant is:

```text
BattleThunderMarkFx.Play()
-> increments a local lifetime generation
-> starts one generation-scoped lifetime wait and a node-bound pulse Tween
-> only the current generation may queue-free the mark after the pause-aware timer completes
```

## Scope

- Add a lifetime generation guard to `BattleThunderMarkFx`.
- Invalidate pending lifetime waits when the FX exits the scene tree.
- Keep the existing pause-aware `CreateTimer(..., processAlways: false)` behavior.
- Bind the pulse Tween to the FX node so Godot owns its lifecycle with the node.
- Add regression coverage proving the timer is generation-guarded and the Tween is node-bound.

## Non-Goals

- Do not change thunder-mark Runtime rules, expiration truth, teleport legality, or battle outcome.
- Do not change `BattleThunderMarkFx.tscn` node structure or visual art.
- Do not rewrite all battle FX lifecycle code in this slice.
- Do not alter tactical pause semantics or timer durations.
- Do not implement any active design proposal under `design-proposals/active/`.

## Touched Systems

- Battle Presentation FX lifecycle.
- `tests/BattleHitFeedbackRegression` anti-rot coverage.

## Tests

- Add a regression proving `BattleThunderMarkFx` lifetime waits are generation guarded and Tween ownership is node-bound.
- Re-run:
  - `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- No new runtime logs required. This is a local Presentation FX lifecycle guard; failure is covered by regression tests.

## Manual QA

- In battle runtime, trigger thunder-tag mark creation repeatedly and confirm the visible mark persists for its current lifetime without stale timer cleanup cutting off a new play cycle.

## Acceptance Evidence

- 2026-06-18: `BattleThunderMarkFx` now scopes each `Play()` lifetime wait with a local generation, invalidates pending waits on `_ExitTree()`, rechecks node validity/tree membership/generation after the pause-aware timer, and binds the pulse Tween to the FX node. Regression coverage now guards the generation flow, post-await `QueueFree()` ordering, `processAlways: false`, and `BindNode(this)`. Verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` and `dotnet build rpg.sln -maxcpucount:2 -v:minimal`. Regression/build runs still report existing Godot source-generator and nullable warnings in test projects.
