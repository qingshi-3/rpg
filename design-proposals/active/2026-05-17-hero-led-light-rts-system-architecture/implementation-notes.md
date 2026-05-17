# Implementation Notes

Status: Business Development Entry Ready

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

Second-phase entry migration is complete. `WorldSiteBattleLauncher` now runs the target battle-group session flow as a side-channel probe from real `BattleStartRequest` launch data while preserving the legacy `BattleSessionHandoff` / `BattleStartRequest` chain as the player-facing path.

Business development can start on the smallest hero/corps gameplay slice. The old handoff/result path still exists and must not be removed until a later replacement phase migrates result/report handling and the real live light-RTS runtime.

Implementation work should use `expected/system-design/hero-led-light-rts-system-architecture.md` as the working architecture target. If implementation reveals an architecture change, pause and update the expected copy for user acceptance before continuing.
