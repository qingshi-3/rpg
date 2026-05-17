# Implementation Notes

Status: Phase 2 Started

The first code refactor phase has been implemented beside the legacy battle path.

The user accepted the expected architecture and `code-refactor-design.md` on 2026-05-17. The first code refactor phase is tracked in `code-refactor-implementation-plan.md`.

First implementation phase:

```text
target contracts and boundary tests
-> battle-group domain state
-> snapshot and command contracts
-> runtime event/result contracts
-> settlement and report contracts
-> legacy boundary adapters
-> minimal battle-group vertical flow
```

Engineering closure:

- `TargetBattleArchitectureRegression` is included in `rpg.sln` so solution builds compile the target architecture regression project.
- `code-refactor-implementation-plan.md` is now a completed first-phase implementation record with checked steps.
- Focused target architecture regression passes.
- Low-concurrency solution build passes.

Second phase has started with `code-refactor-phase2-entry-migration-plan.md`. The live world/site battle entry still uses the legacy `BattleSessionHandoff` / `BattleStartRequest` chain, and the new `BattleGroupBattleFlowService` remains a validated parallel skeleton until phase-2 entry migration wires it into launch flow as a side-channel probe.

Implementation work should use `expected/system-design/hero-led-light-rts-system-architecture.md` as the working architecture target. If implementation reveals an architecture change, pause and update the expected copy for user acceptance before continuing.
