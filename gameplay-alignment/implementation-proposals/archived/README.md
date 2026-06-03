# Archived Implementation Proposals

These implementation proposals are historical records, not active work queues. Some are accepted implementation evidence; others are retained only as superseded or cleanup history. Do not use them as current implementation instructions unless the user explicitly asks for archive investigation.

| Proposal | Summary |
|---|---|
| `2026-05-21-battle-footprint-navigation-attack-slots.md` | Accepted Runtime footprint, reservation, target-choice, and orthogonal attack-slot implementation record. |
| `2026-05-21-battle-live-movement-queueing.md` | Accepted live Presentation movement queueing and timing dependency implementation record. |
| `2026-05-21-battle-presentation-motion-integrator.md` | Accepted actor-local visual movement lane implementation record, superseded in timing detail by live movement queueing. |
| `2026-05-22-continuous-rts-movement.md` | Historical fixed-clock continuous RTS movement implementation record; manual feel QA was not retained as active work. |
| `2026-05-23-battle-plan-state-machine.md` | Historical battle-group plan state-machine record; enemy tactical-region behavior is superseded by the 2026-05-29 authority. |
| `2026-05-26-resident-defender-deployment-zone.md` | Historical resident defender deployment-zone alignment implementation record. |
| `2026-05-28-local-combat-situation-ai.md` | Historical local-combat implementation record; final enemy AI acceptance requires the 2026-05-29 tactical-region authority. |
| `2026-05-29-enemy-region-directed-combat-ai.md` | Accepted enemy region-directed combat AI implementation record: group-owned tactical regions, temporary regions, local combat regions, engagement state, facade observations, and player-policy separation. |
| `2026-05-31-td-003-plan-state-emitter.md` | Accepted TD-003 minimal Runtime extraction slice for moving plan-state emission into `BattlePlanStateEmitter` without behavior changes. |
| `2026-06-01-td-003-attack-movement-resolver-extraction.md` | Accepted TD-003 record: extracted attack/movement resolution into `BattleAttackResolver` / `BattleMovementCommitResolver` (plus event factory, shared DTOs, decomposition guard) byte-for-byte; resolver is now orchestration-only for attack/movement. |
| `2026-06-01-td-002-target-selection-tactical-observation-extraction.md` | Accepted TD-002 record: extracted target selection (`BattleTargetSelectionService`), AI request shaping (`BattleAiActionRequestBuilder`), and tactical observation/engagement (`BattleTacticalObservationUpdater` + `BattleTargetLockLifecycle`) byte-for-byte; tactical-store writes stay internal. |
| `2026-06-01-battle-continuous-step-handoff.md` | Accepted Runtime movement continuity record: completed movers may same-tick hand off to the next segment only when the stored movement intent, live HP, stop boundaries, topology, and reservation authority still allow it. |
