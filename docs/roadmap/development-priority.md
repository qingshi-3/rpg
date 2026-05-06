# Development Priority

Current product target:

```text
玄幻大世界游历 RPG。
三群式势力生态作为底层世界骨架。
玩家默认以角色 / 小队身份介入局势，后期可选择建立势力。
```

## Current Priority

Continue hardening Strategic World V1 before expanding more battle content or emotion features.

V1 的目标不是把项目做成纯战略征服游戏，而是先把大地图生态、可进入场域、部队移动、战斗入口和结果回写稳定下来。

Primary target:

- `docs/design/world/strategic-world-v1.md`
- `docs/design/world/strategic-world-v1-implementation.md`
- `docs/design/world/strategic-world-rts-navigation-and-armies.md`
- `docs/technical-changes/2026-05-02-strategic-world-v1.md`
- `docs/technical-changes/2026-05-03-world-progression-map-surface.md`

## Current State

The first implementation pass now exists:

- V1 world state, resources, facilities, garrison, threats, world tick, and save/load.
- V1 strategic map and action UI.
- Battle handoff/result writeback.
- Bonefield occupation and Graveyard Raid pressure.
- `WorldSiteRoot` mode switch from tactical battle to non-battle site operation.
- In-site operation UI with resource bar, facility status list, garrison/threat/action lists, and return-to-map.
- `WorldClock` first pass: strategic world can auto-advance `WorldTick` while unpaused.
- Strategic map supports `WorldMapRoot` TileMap surface anchors and moving enemy Raid markers.
- Current scene structure is cleaned: the generic site runtime shell lives at `scenes/world/sites/WorldSiteRoot.tscn`, authored site implementations live under `scenes/world/sites/impl/`, and site interaction placeholders live in `scenes/world/site_interactions/`.

Next priority is to replace the fallback painted strategic map with an authored TileMap, deepen RTS-style `WorldArmy` movement, and prepare the first interception / wild opportunity slice so the big map becomes an active world surface rather than a site menu.

## Phase 1: Strategic World State

1. Done: add generic resource, facility, scene, garrison, threat, and world tick state.
2. Done: add V1 definitions for population, economy, stone, barracks, mine, defense tower, player camp, bonefield, and graveyard.
3. Done: add generic action resolution for build, train, occupy, and wait.

## Phase 2: Strategic UI

4. Done: add a strategic map UI for the three V1 site nodes.
5. Done: show resources, selected scene state, buildings, garrison, threats, and available actions.
6. Done: generate action buttons from action view models, including disabled reasons.

## Phase 3: Battle Handoff And Writeback

7. Done: add or extend `BattleStartRequest`.
8. Done: add structured `BattleResult`.
9. Done: make bonefield assault victory change bonefield into `PlayerHeld`.
10. Done: make defeat change world state without ending the game.

## Phase 4: Buildings Affect Battle

11. Done first pass: defense tower and militia enter defense battle request as modifiers or forces.
12. Done first pass: apply battle results back to facility, garrison, and scene state.

## Phase 5: Enemy Raid

13. Done: generate a graveyard raid after bonefield is held.
14. Done: advance raid by WorldTick.
15. Done: let the player defend or auto-resolve.
16. Done first pass: defense tower and garrison improve raid outcome.

## Phase 6: Persistence

17. Done first pass: save and load `StrategicWorldState`.
18. Pending manual QA: verify resources, buildings, ownership, garrison, threats, and WorldTick restore correctly.

## Next Phase: RTS Strategic Map Movement

19. Configure authored `StrategicWorldRoot` TileMap under `WorldMapRoot`.
20. Add the `StrategicNavigation` scene contract using RTS-style continuous-space navigation, not battle-grid A*.
21. Add `WorldArmyState` / expedition runtime state and save/load coverage.
22. Upgrade Graveyard Raid from a threat marker into an enemy `WorldArmy`.
23. Upgrade player assignment and assault flow from instant transfer into player expedition movement.
24. Add first interception / wild encounter trigger when player and enemy armies meet on the map.
25. Deepen `BattleStartRequest` / `BattleResult` so battle can write back army, threat, and site changes.

## Following Phase: Site Operation

26. Add authored site interaction entities for facilities under `scenes/world/site_interactions/`, bound to stable slot ids.
27. Add explicit build / demolish / upgrade / repair flows on site tiles.
28. Keep peacetime site interaction on the map surface, not behind full-screen modal UI.
29. Add persistent site memory for NPC/object deaths and facility/map-state deltas.
30. Run manual QA for `StrategicWorldRoot -> WorldSiteRoot wartime -> WorldSiteRoot peacetime -> StrategicWorldRoot`.

## Deferred

- More emotion features.
- Full card deck construction.
- Full open-world exploration.
- Pure faction-ruler conquest as the default player path.
- Large resource chains.
- Full site economy.
- Full multi-faction AI.
- Large content production.

## Success Criteria

- The player can operate population, economy, and stone.
- Barracks, mine, and defense tower are implemented through generic definitions and actions.
- Bonefield can be occupied and persist as a player-held scene.
- Mine production changes player resources.
- Graveyard raid creates pressure without real-time stress.
- Defense tower and militia change defense outcomes.
- Battle result changes world state.
- WorldSites remain enterable operable maps, not only management panels.
- The strategic map can support moving parties, interception, and later wild opportunities.
