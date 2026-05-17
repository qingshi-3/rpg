# Auto Tactics Migration

Historical record. This document orchestrated the first migration away from legacy manual/AP tactical battles. It is no longer the active product battle identity.

Current authority lives in `gameplay-design/`, `system-design/`, and `gameplay-alignment/authority-map.md`. Read this migration only when investigating the first cleanup, backend auto-resolve/report infrastructure, or legacy manual runtime retirement.

## Impact

Large historical migration. At the time, the project direction changed from player-operated turn-based tactical battles to:

```text
strategic world
-> authored WorldSite management and deployment
-> hero/corps build
-> TFT-like automated real-time tactical execution inside a WorldSite battlefield
-> readable report and structured world writeback
```

This was not a cosmetic battle-mode change. Since then, accepted gameplay direction has moved again to hero-led light RTS with battle-time hero/corps/combined commands. Do not treat pure no-command auto battle playback as current product authority.

## Historical Battle Identity

At the time of this migration, the execution layer was planned around a readable auto-battler shape inspired by 云顶之弈 / TFT:

- pre-battle placement and composition matter;
- units fight automatically after battle starts;
- target selection, movement, attack cadence, skills, and survival are readable;
- speed, pause, skip, event feed, and result report help the player diagnose the battle.

Even then, the project was not intended to become a TFT clone:

- no shop-roll economy as the main loop;
- no fair mirrored board as the primary battle space;
- no synergy drafting replacing site, people, officer authority, and strategic pressure;
- no battle-time macro chores that recreate manual tactical control.

This is historical context. Current battle identity is hero-led light RTS with battle-time hero, corps, and combined command channels.

## Historical Compatibility Contract

The first migration kept the strategic world launch/result bridge on `BattleStartRequest` and `BattleResult`.

```text
WorldActionRequest
-> WorldActionResolver
-> WorldBattleRequestBuilder
-> BattleStartRequest
-> BattleSessionHandoff
-> AutoBattleRuntimeController
-> AutoBattleReportBuilder
-> BattleResult
-> WorldBattleResultApplier
-> StrategicWorldState / WorldSiteState changes
```

The following boundaries remain useful compatibility constraints, but they are not the final target architecture:

- `WorldSite` remains the persistent location authority.
- `WorldSiteState.UnitPlacements` remains the site-local deployment authority.
- Deployment caches derived from the active grid are candidate lists only.
- Battle runtime must not mutate `StrategicWorldState` or persist `WorldSiteState` directly.
- Legacy world result writeback uses `BattleResult.ForceResults` when resolving survivors, losses, retreat, transfer, and garrison counts.
- Scene roots are composition shells, not domain owners.
- Broken core paths should fail with low-noise logs instead of being hidden by layered fallbacks.

## Reused Systems

- Strategic world movement, threats, site ownership, battle request creation, and battle result writeback.
- `WorldSiteDefinition` for facility slots, deployment zones, entrances, anchors, tags, and authored site metadata.
- `WorldSiteState` for facilities, garrison, unit placements, damage, memory, and pending threats.
- `WorldSiteState.UnitPlacements` as the authoritative deployment record.
- Grid map reading, terrain tags, movement surfaces, render sorting, and pathing helpers.
- Existing unit visuals, health, movement, attack, damage feedback, and animation components where they are not tied to manual player commands.

## Retired Direction

Do not add new gameplay features to the retired manual battle loop:

- `BattleTurnController` player phase.
- Manual battle action menu as the main combat UI.
- AP-driven per-unit player command flow.
- Macro corps commands implemented as battle-time player chores.

The first migration pass has deleted or detached the active manual runtime, old HUD/action-menu scenes, battle AP component, and legacy AP authoring fields. Missing battle presentation is not permission to rebuild manual/AP commands; future-facing work should follow the accepted hero-led light RTS architecture.

## Historical Runtime Ownership Plan

`WorldSiteRoot` should become a thin scene shell:

- load the authored site map;
- bind scene nodes and shared presentation services;
- route management, deployment, exploration, and battle controllers;
- call world/battle handoff boundaries;
- avoid owning domain rules.

Historical target owners from this migration:

| Owner | Responsibility |
|---|---|
| `WorldSiteRuntimeDeploymentCacheBuilder` | Derive ordered deployment candidates from the active grid. |
| `WorldSiteManagementPresenter` | Render facilities, garrison, threats, actions, and notices. |
| `WorldSiteDeploymentController` | Handle placement selection, drag validation, and state writes. |
| `WorldSiteExplorationController` | Own exploration HUD, movement, alert, and encounter launch. |
| `WorldSiteBattleLauncher` | Build and validate battle handoff from site context. |
| `AutoBattleRuntimeController` | Own automated battle pacing, unit behavior ticks, outcome checks, and playback. |
| `AutoBattleReportBuilder` | Convert runtime events into contribution and failure summaries. |

## Historical Workstream Documents

Use these child documents only for cleanup archaeology and compatibility investigation. Do not use them as future battle product direction:

- Battle language and product guardrails: `2026-05-16-auto-tactics-migration/01-battle-language-and-guardrails.md`
- WorldSite runtime split: `2026-05-16-auto-tactics-migration/02-worldsite-runtime-split.md`
- First refactor slice, deployment cache extraction: `2026-05-16-auto-tactics-migration/03-deployment-cache-extraction.md`
- Auto battle runtime design: `2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- Playback UI and battle report: `2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`
- World/battle contract and writeback: `2026-05-16-auto-tactics-migration/06-world-battle-contract-and-writeback.md`
- Legacy battle retirement: `2026-05-16-auto-tactics-migration/07-legacy-battle-retirement.md`
- Subagent handoff rules: `2026-05-16-auto-tactics-migration/08-subagent-handoff.md`
- First implementation plan: `2026-05-16-auto-tactics-migration/09-deployment-cache-extraction-implementation-plan.md`
- Second implementation plan: `2026-05-16-auto-tactics-migration/10-deployment-target-evaluator-implementation-plan.md`
- Third implementation plan: `2026-05-16-auto-tactics-migration/11-deployment-terrain-reconciler-implementation-plan.md`
- Fourth implementation plan: `2026-05-16-auto-tactics-migration/12-battle-deployment-preparer-implementation-plan.md`
- Fifth implementation plan: `2026-05-16-auto-tactics-migration/13-battle-launcher-implementation-plan.md`
- Sixth implementation plan: `2026-05-16-auto-tactics-migration/14-auto-battle-runtime-skeleton-implementation-plan.md`
- Seventh implementation plan: `2026-05-16-auto-tactics-migration/15-auto-battle-session-runner-implementation-plan.md`
- Eighth implementation plan: `2026-05-16-auto-tactics-migration/16-auto-battle-report-builder-implementation-plan.md`
- Ninth implementation plan: `2026-05-16-auto-tactics-migration/17-auto-battle-runtime-controller-implementation-plan.md`
- Tenth implementation plan: `2026-05-16-auto-tactics-migration/18-worldsite-auto-battle-adapter-implementation-plan.md`
- Eleventh implementation plan: `2026-05-16-auto-tactics-migration/19-auto-battle-report-summary-implementation-plan.md`
- Twelfth implementation plan: `2026-05-16-auto-tactics-migration/20-legacy-manual-authority-deletion-implementation-plan.md`
- Final cleanup plan: `2026-05-16-auto-tactics-migration/21-final-manual-runtime-removal-implementation-plan.md`

## Historical Migration Sequence

Historical sequence:

1. Align wording and guardrails so new work stops restoring manual chess/AP runtime.
2. Extract deployment cache construction from `WorldSiteRoot`.
3. Split `WorldSiteRoot` responsibilities one owner at a time.
4. Build the first backend auto-resolve runtime that reads `BattleStartRequest`, uses site placements, and emits `BattleResult.ForceResults`.
5. Add playback controls, event feed, and a readable battle report.
6. Verify strategic writeback for assault, defense, and field-intercept style requests.
7. Keep the retired manual runtime deleted while auto battle placeholders mature; do not restore old AP or command UI to fill gaps.

## Historical Minimum Slice From First Cleanup

This was the minimum slice for the first cleanup, not the current business-development target:

- one authored `WorldSite` battlefield;
- one hero/corps force on the player side;
- one small enemy group;
- pre-battle deployment from `WorldSiteState.UnitPlacements`;
- automated real-time loop with movement, basic attacks, simple skill/event placeholders, and victory/defeat;
- `BattleResult.ForceResults` writeback;
- UI controls for start, pause, speed, skip, event feed, and final report.

## Deletion Policy

Code can be deleted when one clear owner replaces it and tests or smoke checks cover the migrated behavior. Do not leave two authoritative implementations for:

- deployment candidate ordering;
- site-local unit placement authority;
- battle outcome writeback;
- combat runtime phase ownership;
- battle HUD command ownership.

During migration, prefer deleting copied logic and private helper islands from `WorldSiteRoot` as soon as an extracted service owns them.

Already retired manual pieces must stay retired: player-phase turn controllers, battle command routers, manual action menus, turn queues, preview controllers, battle AP components, and AP authoring fields.

## Historical Docs-First Gate

This gate applied to the first migration. Current architecture and gameplay work should start from `gameplay-design/`, `system-design/`, and the active hero-led light RTS proposal.

Do not combine "change battle identity" and "clean up every old battle file" in one unbounded refactor.

## Historical QA Entry

Focused checks live in `docs/60-qa/testcases/auto-tactics-migration.md`.
