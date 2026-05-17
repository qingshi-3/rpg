# Implementation Notes

Status: First Phase Implemented

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

Implementation work should use `expected/system-design/hero-led-light-rts-system-architecture.md` as the working architecture target. If implementation reveals an architecture change, pause and update the expected copy for user acceptance before continuing.
