# Strategic World Runtime Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `gameplay-design/content-systems-long-term-design.md`: strategic play prepares cities, resources, armies, opportunities, and battle entry for hero-led light RTS combat.

## Responsibility

The strategic world runtime owns world tick advancement, player resources, site state, army movement, opportunity spawning and completion, garrison production, strategic-map fog visibility, and battle-ready notifications for normal assault or field intercept flows.

## Does Not Own

The strategic world runtime does not own site interior UI, battle simulation, battle report truth, scene root switching, manual persistence UI, site intelligence, infiltration, exploration alert state, defensive Raid progression, or autonomous world-threat planning.

## Persistent State

`StrategicWorldState` is campaign authority for:

- world tick, seed, run id, and player faction;
- strategic-map fog visible cells, explored cells, and last update tick;
- player resources;
- `WorldSiteState` ownership, control, facilities, garrison, unit placements, damage, and mode;
- `WorldArmyState` position, destination, intent, navigation path cache, units, and status;
- active/completed/expired opportunities and spawn-rule cooldowns.

## Runtime State

Runtime-only state includes scene selection, world-clock pause/accumulator, hovered/selected UI ids, navigation providers, transition handoff state, and presentation markers.

## Inputs

Inputs are `StrategicWorldDefinition`, authored site/opportunity/facility/resource definitions, player commands, navigation context, battle results, and scene-transition requests.

## Outputs

Outputs are `WorldTickResult`, `WorldArmyMovementResult`, `WorldActionResult`, `GameEvent` records, battle-start requests, UI notices, and low-noise diagnostics.

## Contracts

World tick advancement performs only durable strategic changes: aftermath cleanup, resource production, auto-garrison production, and opportunity lifecycle updates.

Army movement uses the strategic navigation provider as the authoritative path contract. Missing or invalid navigation blocks the army explicitly instead of falling back to presentation movement. Player assault armies that arrive at configured hostile sites become battle-ready; moving armies from opposing factions can trigger field intercept.

Strategic-map fog is map visibility only. It records explored and currently visible map cells from player-owned locations and non-garrisoned player armies. It must not carry site intel snapshots, infiltration facts, alert escalation, or battle-trigger facts.

Opportunities are deterministic from world tick, run seed, rule id, spawn point, and active count. Completing an opportunity applies its configured rewards and emits a world event.

## Failure Rules

Missing definitions, invalid navigation, full garrison zones, unsupported assault targets, and malformed opportunity data must fail with explicit result reasons and diagnostics. The world runtime must not fabricate battle outcomes, hidden threat plans, or hidden discovery state.

## Acceptance

This architecture is acceptable when:

- world tick produces resources, garrisons, opportunities, and map fog visibility without persistence, intel, or raid side effects;
- army movement blocks on invalid navigation and reports a deterministic reason;
- normal assault and field intercept are the only strategic battle-ready paths;
- strategic runtime state can be understood without reading scene-node state.
