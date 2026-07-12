# World Site Management Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports the accepted city and strategic-location management rules in `gameplay-design/content-systems-long-term-design.md`.

## Responsibility

World site management owns the Application contract for a managed location: resources, facilities, facility slots, garrison capacity, unit placements, build actions, site mode, and the site-management HUD's data binding.

## Does Not Own

Site management does not own strategic fog, intelligence reveal state, infiltration, exploration patrols, defensive Raid threats, normal battle simulation, root scene transitions, or long-term battle report truth.

## Persistent State

`WorldSiteState` owns:

- site id, owner faction, control state, damage level, last visited tick, and mode;
- resource storage/production facts represented by strategic resources and facility state;
- facility instances, assigned population, and active/damaged/destroyed state;
- garrison units and site-local `WorldSiteUnitPlacement` rows;
- active tags and battle-relevant state snapshots.

`WorldSiteUnitPlacement` stores site-local placement identity, unit type, faction, source kind/id, army id, zone, entrance, attack direction, cell, and height. It does not store threat ids.

## Runtime State

Runtime state includes selected slot, selected placement, drag preview, deployment cache, active grid map, terrain reconciliation results, and temporary battle-preparation UI state.

## Inputs

Inputs are site definitions, facility definitions, action definitions, semantic map markers, authored deployment zones, current strategic state, battle requests, and player UI commands.

## Outputs

Outputs are action view models, facility and garrison UI rows, placement movement results, build/action results, terrain diagnostics, and battle-preparation preferred placements.

## Contracts

Facility actions are resolved through `WorldActionResolver` against definitions and current state. The UI displays available actions and disabled reasons but does not invent costs or effects.

Garrison placement rows are site-local authority. Deployment code may create or update placements, but removal must come from explicit domain events such as battle result, death, retreat, transfer, or disband.

Semantic building-slot and deployment-zone markers are authored scene data extracted into pure marker data before Application consumption. Business logic must not depend on editor node instances.

## Failure Rules

Missing site state, missing definitions, full garrison zones, invalid placement cells, water restrictions, occupied cells, and empty deployment caches fail explicitly. Site management must keep existing authoritative state intact when a UI refresh or battle handoff fails.

## Acceptance

This architecture is acceptable when:

- site detail viewing, facility display, garrison display, and build actions work without intel gates;
- garrison capacity and placement mutations go through Application services;
- authored semantic markers drive building slots and battle deployment;
- site management exposes no exploration, infiltration, threat, or manual persistence surface.
