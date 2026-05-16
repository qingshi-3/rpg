# Legacy Battle Retirement

## Purpose

This document defines the retirement boundary for legacy manual battle systems after the auto battle migration became the active direction.

## Legacy Systems

These are retired runtime authorities:

- `BattleTurnController` player phase;
- `TurnSystem`-style flow;
- AP-driven battle decisions;
- manual move/attack action menu;
- `BattleActionMenu` as main combat UI;
- battle-time macro commands that require repeated player input.

Do not expand or restore these as future gameplay.

## Current Boundary

The active scene and runtime should not depend on these retired pieces. Unit visuals, health, movement range, attack data, hit feedback, and animation may remain because they are reusable presentation/content data, not manual runtime authority.

If a missing auto battle feature blocks progress, leave a placeholder or blank presentation surface. Do not rebuild AP, player turns, command routers, or manual battle HUDs.

## Removal Gates For Any Remaining Reference

Remove or detach a legacy owner only after:

- a target owner exists;
- launch from `BattleStartRequest` works;
- battle can complete without manual commands;
- `BattleResult.ForceResults` is returned;
- world writeback passes assault or defense smoke checks;
- playback UI and report cover the user-facing replacement;
- old and new owners are not both authoritative for the same responsibility.

## Retirement Order

1. Stop adding new features to manual command UI.
2. Route new battle entry to auto battle for the first supported scenario.
3. Detach manual flow from active scenes.
4. Delete manual HUD/action-menu resources and command controllers.
5. Remove battle AP dependencies from unit definitions and authored resources.
6. Delete or rewrite stale docs that describe manual battle as target direction.

## Documentation Cleanup

When legacy behavior is removed:

- update `docs/20-game-design/tactical-battle/README.md`;
- update `docs/30-technical-design/battle/README.md`;
- delete manual AP/action menu design docs when they stop serving current architecture;
- route QA through `docs/60-qa/testcases/auto-tactics-migration.md`.

Prefer deleting obsolete guidance over layering "deprecated" notes everywhere.

`20-legacy-manual-authority-deletion-implementation-plan.md` was the first deletion pass. `21-final-manual-runtime-removal-implementation-plan.md` closes the remaining cleanup by deleting active manual runtime classes, scenes, AP authoring fields, and stale tutorial/QA routes.

## Acceptance

- No runtime responsibility has two authoritative owners.
- New work no longer depends on AP or player command phases.
- Old docs no longer mislead clean-context agents into expanding manual battle.
