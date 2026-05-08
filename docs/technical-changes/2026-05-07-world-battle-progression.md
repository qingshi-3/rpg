# World Battle Progression

## Background

World threats previously forced an immediate battle entry when they reached a site. This is still correct when the player is attacking or defending, but AI-vs-AI conflicts need room to unfold over world time.

## Goal

- Keep battles with no player participation in the world layer for several world ticks.
- Split each conflict into visible phases.
- Project the automatic result at battle start, then let player intervention override it through the existing `BattleStartRequest` / `BattleResult` boundary.
- Add faction capabilities that affect world-layer battle projection.
- Expand the first undead raid to five enemy units.

## Non-Goals

- Do not change Battle flow, AP, or `TurnSystem`.
- Do not run the Battle runtime in the background for AI-vs-AI simulation.
- Do not introduce three-side tactical battles.
- Do not delay battles where the player is the attacker or defender.

## Affected Systems

- World state: `WorldBattleState`, `WorldBattlePhase`, `WorldBattleOutcome`.
- World progression: `WorldBattleProgressionService`, `WorldTickService`, `WorldArmyMovementService` integration through arrival results.
- Battle boundary: `BattleStartRequest.WorldBattleId`, `WorldBattleRequestBuilder`, `WorldBattleResultApplier`.
- World UI: active world battle display and intervention entry in `StrategicWorldRoot`.
- Faction data: `FactionDefinition`, `FactionCapabilityDefinition`.
- Content: undead raid force list now contains five units.

## Rules

- `WorldBattleProgressionService` owns projection, phase advancement, and automatic resolution.
- Battle runtime starts immediately when the player is the attacker or defender.
- Battle runtime only starts for no-player world battles after player intervention.
- The projected result is stored on `WorldBattleState`; it is not recalculated every tick.
- Player intervention resolves the world battle through the normal battle result applier.

## Manual Acceptance Checks

- Let a raid reach a player-held site and confirm it opens the battle scene immediately.
- Let a no-player raid reach its target and confirm the world clock does not auto-open the battle scene.
- Confirm the threat list shows a world battle phase, remaining world ticks, and an intervention button.
- Let the world clock advance until the battle resolves automatically and verify the site/threat state changes.
- Start a new raid, intervene before resolution, finish the tactical battle, and verify the tactical result overrides the projected result.
- Confirm the first undead raid creates five enemy force entries.
