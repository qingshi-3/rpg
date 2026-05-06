# Current Design Progress

## Completed

- Product positioning updated to `玄幻大世界游历 RPG` with a Sanguo Qunying-style strategic ecology underneath.
- Core loop.
- CombatSystem.
- AP ResourceSystem.
- CommandSystem.
- IntentSystem.
- Card positioning.
- Extension architecture.
- First tutorial battle design.
- Combat technical prototype for the minimal loop.
- Runtime GridSystem reading from battle map layers.
- Runtime UnitSystem composition.
- Basic movement and attack resolution.
- Basic Intent generation, marker display, hover preview, and enemy phase resolution.
- Basic battle UI command flow.
- Historical world prototypes for static campaign map, walkable overmap, and local location switching.
- Strategic World V1 direction and implementation plan.
- Character race and character definition foundation.
- Emotion backend foundation with race baselines, NPC variance, events, conditions, effects, relationship state, and state export.
- Emotion gameplay query contracts for recruitment, task assignment, loyalty, battle support, relationship gates, and event reaction.
- StrategicWorldState V1 implementation with resources, site states, facilities, garrison, threats, world tick, and save/load.
- Strategic world action resolver for occupation, building, training, assignment, waiting, raid defense, and auto-resolve.
- Strategic map V1 first slice: Player Camp, Bonefield, Graveyard as site nodes on a map surface.
- Battle start/result handoff between strategic world and site battle.
- Battle result writeback for bonefield assault and defense raid.
- Graveyard Raid V1 with countdown, attacking state, auto-resolve, and battle entry.
- `WorldSiteRoot` site shell replacing old `BattleRoot`.
- Site mode switching between tactical wartime and non-battle site operation mode.
- First in-site operation UI: resource bar, operation panel, clickable facility slots, garrison/threat/action lists, and return-to-map.
- `basic-ui/2` skin layer for strategic and site operation UI panels, buttons, and battle announcement dialogs.
- `WorldProgression` naming: `WorldClock` drives unpaused `WorldTick`; `SiteProduction` and `ThreatProgression` are the first consumers.
- Strategic map TileMap contract: `WorldMapRoot/SiteVisualLayer` for site art, `WorldMapRoot/MapAnchors/Sites` for site nodes, and `WorldMapRoot/StrategicNavigationTileLayer` + Godot 2D navigation for world-army movement.
- `SiteVisualLayer` footprint recognition: site hit areas, selection outlines, and labels now follow authored site art instead of code-drawn circular placeholders when configured.
- Abstract blue-yellow strategic site markers have been removed; formal hero / army count icons are pending the next UI asset contract.
- Enemy Raid appears as an enemy `WorldArmyState` on the strategic map and moves through strategic navigation.
- Scene structure cleanup: strategic world, site runtime shell, authored site implementations, and site interaction placeholders are split into `StrategicWorldRoot`, `sites/WorldSiteRoot`, `sites/impl`, and `site_interactions`.

## Confirmed Principles

- Single resource: AP.
- Unified action abstraction: BattleAction.
- Effect-based extension mechanism.
- Hero and minion responsibility split.
- Strategic world resources and facilities must be definition-driven.
- V1 world resources are population, economy, and stone.
- V1 facilities are barracks, mine, and defense tower.
- World and Battle communicate through request/result contracts.
- The strategic world should read as a map surface, not as a list of location options.
- Enemy plans should be visible on the map when possible; text panels explain state but should not be the only representation.
- A site is not only a battle scene. It must be able to persist as an operable scene with peacetime and wartime modes.
- Battle failure should write world consequences, not directly end the RPG run.
- The player does not default to a faction ruler; the default path is character / small-party world travel and intervention.
- Sanguo Qunying is a reference for world-state density, movement, battle entry, and result writeback, not the final player identity.
- `WorldSite` should be an enterable local map with operation, battle, and persistent memory, not a thin strategic shell or pure management UI.
- Short-lived wilderness content should be modeled as `Opportunity` / `EncounterRequest`; only persistent operable locations should become `WorldSite`.

## Pending Design

- RuleEngine.
- Card data structure.
- Intent template library and richer target policies.
- UI preview polish and non-debug iconography.
- One playable mechanism battle slice.
- Commander spell / support-card intervention in battle.
- Authored site maps with real facility slot anchors and readable base layout.
- Authored strategic world TileMap, final site visual art, and `MapAnchors/Sites` placement.
- Richer site operation UX for construction, upgrade, demolition, repair, and unit assignment.
- Persistent tactical-site memory for killed NPCs, destroyed objects, and map-state deltas.
- Large-world AI beyond V1 tick-based raid pressure.
- Manual QA pass for strategic map -> battle -> site operation -> strategic map loop.
- First interception or wild opportunity slice using `WorldArmy` contact and unified encounter request.
- Player-party identity, party composition, and non-faction-ruler entry flow.

## Out Of Scope For Now

- Progression system.
- Equipment system.
- Large-scale content production.
- Full site economy.
- Full open-world exploration.
- Pure strategic conquest as the default main loop.
