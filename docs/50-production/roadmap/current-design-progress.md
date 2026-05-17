# Current Design Progress

This document tracks durable design state. Historical implementation details live in `docs/50-production/technical-changes/`.

## Completed

- Product positioning is now governed by `gameplay-design/`: Sanguo Qunying-style strategic map, officer social management, strategic-location operation, and hero-led light RTS combat.
- Legacy `docs/` routes are being cleaned so they no longer treat auto-tactics playback as the product battle identity.
- Legacy battle prototype exists with grid reading, unit composition, movement, attacks, intent markers, AP/action flow, battle HUD, hit feedback, and request/result handoff.
- Strategic World V1 direction and implementation plan exist.
- Character race and character definition foundation exist.
- Emotion backend foundation exists with race baselines, NPC variance, events, conditions, effects, relationship state, and state export.
- Emotion gameplay query contracts exist for recruitment, task assignment, loyalty, battle support, relationship gates, and event reaction.
- `StrategicWorldState` V1 exists with resources, site states, facilities, garrison, threats, world tick, and save/load.
- Strategic world action resolver handles occupation, building, training, assignment, waiting, raid defense, and auto-resolve.
- Strategic map V1 first slice exists: Player Camp, Bonefield, Graveyard as site nodes on a map surface.
- Battle start/result handoff exists between strategic world and site battle.
- Battle result writeback exists for Bonefield assault and defense raid.
- Graveyard Raid V1 exists with countdown, attacking state, auto-resolve, and battle entry.
- `WorldSiteRoot` site shell replaced old `BattleRoot`.
- First in-site operation UI exists: resource bar, operation panel, clickable facility slots, garrison/threat/action lists, and return-to-map.
- `WorldProgression` naming exists: `WorldClock` drives unpaused `WorldTick`; `SiteProduction` and `ThreatProgression` are the first consumers.
- Strategic map TileMap contract exists: `WorldMapRoot/SiteVisualLayer`, `WorldMapRoot/MapAnchors/Sites`, and `WorldMapRoot/StrategicNavigationTileLayer` with Godot 2D navigation for world-army movement.
- Enemy Raid appears as an enemy `WorldArmyState` on the strategic map and moves through strategic navigation.
- Scene structure cleanup split strategic world, site runtime shell, authored site implementations, and site interaction placeholders into `StrategicWorldRoot`, `sites/WorldSiteRoot`, `sites/impl`, and `site_interactions`.
- Hero-led light RTS architecture proposal has design acceptance and first-phase engineering closure. The target architecture skeleton now covers hero/corps/battle-group state, snapshot contracts, command validation, runtime event/result contracts, settlement/report contracts, legacy boundary adapters, minimal battle-group vertical flow, and solution-level target architecture regression coverage.

## Confirmed Principles

- World and Battle communicate through request/result contracts.
- The strategic world should read as a map surface, not as a list of location options.
- Enemy plans should be visible on the map when possible; text panels explain state but should not be the only representation.
- A `WorldSite` is not only a battle scene. It must persist as an operable map with management, deployment, battle, and memory.
- Battle failure should write world consequences, not directly end the RPG run.
- Player identity should support the strategic-map, officer-social, strategic-location operation, and hero-led battle loop.
- Sanguo Qunying is a gameplay reference for large-map strategy, movement, battle entry, and result writeback; do not reduce it to a technical backend only.
- Short-lived wilderness content should be modeled as `Opportunity` / `EncounterRequest`; only persistent operable locations should become `WorldSite`.
- Legacy manual battle systems are implementation references during migration, not the target battle identity.
- Battle execution may borrow auto-battler readability for cues and reports, but the target is hero-led light RTS on authored locations and the project should not inherit TFT-like economy as its main loop.

## Pending Design

- Hero-led light RTS second-phase migration plan: wire the live world/site battle entry from the legacy `BattleSessionHandoff` / `BattleStartRequest` chain toward the new battle-group session flow without breaking existing result writeback.
- Real hero-led light RTS runtime state machine and battle report contract beyond the current first-phase skeleton.
- Hero/corps role split for commanded battles.
- Strategic-location deployment UX for battle preparation.
- Facility effects that influence battles without making wall-hitting the main experience.
- RuleEngine.
- Intent template library and richer target policies for automatic soldier behavior.
- Authored site maps with real facility slot anchors and readable base layout.
- Authored strategic world TileMap, final site visual art, and `MapAnchors/Sites` placement.
- Richer site operation UX for construction, upgrade, demolition, repair, and unit assignment.
- Persistent WorldSite battle memory for killed NPCs, destroyed objects, facility damage, and map-state deltas.
- Large-world AI beyond V1 tick-based raid pressure.
- Manual QA pass for strategic map -> strategic-location operation -> battle -> result writeback -> strategic map loop.
- First interception or wild opportunity slice using `WorldArmy` contact and unified encounter request.
- Player identity, organization role, party / officer composition, and strategic authority entry flow.

Detailed first migration workstreams are retained as historical records under `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/`.

## Out Of Scope For Now

- Full progression system.
- Equipment system.
- Large-scale content production.
- Full site economy.
- Full open-world exploration.
- Pure strategic conquest as the default main loop.
- Expanding the legacy manual AP/action-menu battle loop as the future combat identity.
