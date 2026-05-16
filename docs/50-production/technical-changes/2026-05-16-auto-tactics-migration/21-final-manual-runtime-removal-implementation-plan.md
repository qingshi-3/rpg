# Final Manual Runtime Removal Implementation Plan

## Goal

Close the first auto tactics migration by removing active legacy manual battle authority. This is a cleanup slice, not final auto battle feature work.

## Scope

- Delete or detach manual battle command controllers, turn controllers, intent controllers, input routers, preview controllers, manual HUD/action-menu resources, and battle AP components.
- Remove battle AP and per-turn movement authoring fields from unit definitions and unit resources.
- Rewrite or delete tutorial, QA, and technical-change routes that teach manual commands, AP spending, player turns, or action menus as active direction.
- Keep reusable unit visuals, HP, movement range, attack data, damage feedback, animation, grid, deployment, and `BattleResult.ForceResults` contracts.

## Non-Goals

- Do not build final auto battle UI.
- Do not add a complete hero skill system.
- Do not polish playback animation beyond existing placeholder behavior.
- Do not use old manual battle code as a fallback when auto battle presentation is incomplete.

## Architecture

The active battle direction is:

```text
WorldSite deployment
-> BattleStartRequest
-> auto battle runtime/controller
-> AutoBattleReportBuilder
-> BattleResult.ForceResults
-> world/site writeback
```

`WorldSiteRoot` remains a composition shell. Missing auto battle features should be blank or placeholder surfaces until a focused owner is added.

## Acceptance

- No active scene references the retired manual battle nodes.
- Deleted manual battle C# and UI scene files stay deleted.
- Battle unit definitions and resources do not serialize AP or per-turn movement fields.
- Tutorial and QA routes describe automated tactical validation, not player-phase command play.
- Migration regression tests guard against restoring the retired files and fields.
