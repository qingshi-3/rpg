# Development Priority

## Current Product Target

```text
大地图战略 + 人物社交经营 + 城池 / 战略地点经营与英雄带兵轻 RTS 战斗的战略 RPG。
```

三群式势力生态提供战略地图骨架。三国志 / 三国立志传式人物关系与任务提供人物经营骨架。城池 / 战略地点经营、英雄/兵团养成、部署、轻 RTS 指挥和战斗报告提供局部冲突骨架。

## Current Priority

Harden Strategic World V1 and WorldSite operation before expanding more battle content or emotion features.

V1 的目标不是完成全部产品形态，而是先把大地图生态、可进入地点、部队移动、战斗入口和结果回写稳定下来，为后续人物社交经营、战略地点经营和英雄带兵轻 RTS 战斗提供骨架。

Primary target:

- `docs/20-game-design/strategic-map/strategic-world-v1.md`
- `docs/30-technical-design/world/strategic-world-v1-implementation.md`
- `docs/30-technical-design/world/strategic-world-rts-navigation-and-armies.md`
- `gameplay-design/content-systems-long-term-design.md`
- `gameplay-alignment/gap-register.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-02-strategic-world-v1.md`
- `docs/50-production/technical-changes/2026-05-03-world-progression-map-surface.md`

## Current State

The first implementation pass now exists:

- V1 world state, resources, facilities, garrison, threats, world tick, and save/load.
- V1 strategic map and action UI.
- Battle handoff/result writeback.
- Bonefield occupation and Graveyard Raid pressure.
- `WorldSiteRoot` mode switch from battle runtime to non-battle site operation.
- In-site operation UI with resource bar, facility status list, garrison/threat/action lists, and return-to-map.
- `WorldClock` first pass: strategic world can auto-advance `WorldTick` while unpaused.
- Strategic map supports `WorldMapRoot` TileMap surface anchors and moving enemy Raid markers.
- Current scene structure is cleaned: the generic site runtime shell lives at `scenes/world/sites/WorldSiteRoot.tscn`, authored site implementations live under `scenes/world/sites/impl/`, and site interaction placeholders live in `scenes/world/site_interactions/`.

## Next Priority

1. Keep the retired manual battle runtime deleted while the future hero-led light RTS architecture is proposed and accepted.
2. Split remaining `WorldSiteRoot` responsibilities only behind focused owners for deployment, management, exploration, and battle runtime.
3. Preserve strategic world, `WorldSiteState.UnitPlacements`, battle request/result handoff, and result writeback.
4. Build the smallest hero-led battle slice: one authored location map, one hero/corps company, one enemy group, basic hero/corps/combined commands, automatic soldier behavior, and structured report/writeback.

The completed first migration remains a historical cleanup record under `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/`.

## Not Current Priority

- Restoring the legacy manual battle action menu.
- Restoring battle AP/TurnSystem as the combat identity.
- Building TFT-like shop rolls or fair-board autobattler economy.
- Adding unrelated content before the strategic-location + hero-led battle loop is readable.
