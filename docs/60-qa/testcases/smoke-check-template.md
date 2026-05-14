# Smoke Check Template

Use this template when a change needs a quick manual verification pass. Keep the
filled check concise and attach it to the relevant testcase document when the
behavior should be tracked long-term.

## Context

- Change:
- Build:
- Scene or entry point:
- Save state used:
- Known fallbacks or missing authored content:

## Preflight

- `dotnet build rpg.sln` passes.
- `git diff --check` has no new relevant warnings.
- Required scenes open without missing script/resource errors.
- Required authored data exists or the fallback behavior is documented.

## Critical Path

- Start from the intended entry scene.
- Perform the primary player action.
- Observe the expected runtime state change.
- Confirm UI text, marker, button, or scene transition reflects the change.
- Confirm no unrelated system is blocked.

## Regression Checks

- Existing save/load still works for touched state.
- Existing world progression still advances when unpaused.
- Existing battle handoff still returns to the strategic world.
- Existing actions that share conditions/effects still show correct enabled state.

## Logs And Diagnostics

- Key state transition has one clear log entry.
- No per-frame log spam appears during normal play.
- Failure path produces a useful warning or message.

## Result

- Passed:
- Failed:
- Unverified:
- Follow-up:

