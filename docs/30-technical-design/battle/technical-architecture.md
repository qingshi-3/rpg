# Battle Technical Architecture

## Authority

The battle architecture is being realigned from the first auto battle migration toward the accepted hero-led light RTS direction.

Authoritative routes:

- Current gameplay authority: `../../../gameplay-design/content-systems-long-term-design.md`
- System design route: `../../../system-design/README.md`
- Gap tracking: `../../../gameplay-alignment/gap-register.md`
- World battle contract: `../world/strategic-world-v1-battle-contract.md`
- Historical auto battle migration: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`

## Runtime Contract

The stable boundary remains:

```text
BattleStartRequest
-> battle runtime or backend auto-resolve
-> BattleResult
-> WorldBattleResultApplier
```

Rules:

- `WorldSiteState.UnitPlacements` is the site-local deployment authority.
- Battle runtime must not mutate `StrategicWorldState` or persist `WorldSiteState` directly.
- `BattleResult.ForceResults` is required for survivor and loss writeback.
- Presentation may show command feedback, playback, event feed, speed/skip controls, and report views, but it must not infer final casualties from scene nodes.

## Retired Manual Runtime

The manual battle runtime is no longer a compatibility path. Player-phase turn controllers, battle AP spending, command routers, action menus, turn queues, and preview controllers have been deleted or detached from active scenes.

If a feature needs battle execution, do not recreate the retired manual runtime as a shortcut. Either keep the backend auto-resolve path isolated or implement the feature through an accepted hero-led light RTS architecture proposal.

## Scene And Content

Reusable scene and unit references can remain while they serve the future combat architecture:

- Battle scene and map runtime: `battle-scene-architecture.md`
- Unit system: `unit-system.md`
- Unit authoring: `unit-authoring.md`
- Unit animation system: `unit-animation-system.md`

These documents should be corrected or deleted if they start routing new work toward AP, player phases, or manual action menus.
