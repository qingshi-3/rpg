# Implementation Notes

## Status

Implemented; awaiting user playtest acceptance. User accepted this proposal on 2026-05-17.

## Engineering Notes

- Keep the first slice narrow: one selectable hero is acceptable if the UI and data flow support adding more.
- Use authored unit resources from `assets/battle/units/`; do not introduce procedural combatant markers as the player-facing representation.
- Prefer resource-driven definitions for hero/corps/enemy v0 content so later content expansion does not require gameplay-code edits.
- Treat the battle runtime's in-memory unit pool as the active combat authority. Persistent strategic state is updated only through settlement; `BattleStartRequest` and `WorldSiteUnitPlacement` rows are migration/presentation data, not roster authorities.
- Keep player-visible text in Chinese.
- Add low-noise logs for expedition selection, enemy target selection, deployment start, and battle start.

## Implemented Notes

- `StrategicWorldRoot` keeps v0.1 expedition selection hero-only and attaches the default corps after creating the world army.
- Assault expedition issues a world-travel attack order. Battle preparation opens from the arrived assault army flow, not directly from right-click targeting.
- `WorldSiteRoot` renders prepared battle units first, keeps player-side placements draggable, and commits the existing auto runtime only after `开战`.
- The v0.1 initial state uses authored `f1_general` and `boss_city` unit resources; the default corps uses `f1_melee`.
- Assault victory now clears resolved visiting/attacker placement rows so the captured location does not render both the old army placements and the surviving garrison placements.
- `BattleRuntimeSession` now creates a stable `BattleRuntimeState` actor pool from `BattleStartSnapshot`; `BattleOutcomeResult` carries actor outcomes, and `BattleSettlementService` derives state deltas from those runtime facts.
