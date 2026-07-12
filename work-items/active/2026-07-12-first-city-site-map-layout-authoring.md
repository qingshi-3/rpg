# First-City Site-Map Layout Authoring

Status: Paused
Executor: Unassigned
Verifier: Unassigned
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Preserve the verified first-city map scaffold and resume detailed city-map authoring only when the operational city loop can validate real content needs.

## Confirmed Discussion Result

- The checked-in plains city base, inherited layout, and marker contracts are a structural scaffold, not accepted final map content.
- Detailed TileMapLayer painting, bridge art fitting, decoration, obstacles, resources, deployment zones, and building-slot tuning remain deferred.
- Resume only after the operational/city loop has been validated on `DemoSite`; the next discussion must decide which authored content real systems require.

## Authority Impact

None while paused. Current authority already defines the scaffold boundary. Any resumed implementation that changes authored-content requirements, map contracts, binding, or persistent state must first update the applicable gameplay or system authority after user confirmation.

## Execution Scope

When the resume gate is met, discuss and confirm a focused next slice for the authored content needed by the proven `DemoSite` loop. Retain:

- `scenes/city/base/plains_city_base.tscn` as terrain-only reusable base;
- `scenes/city/layouts/plains_city_v0_layout.tscn` as inherited layout-owned content;
- marker-driven bridge gameplay and explicit height connections;
- isolation from `DemoSite` and `BonefieldSite` unless a later confirmed task explicitly changes binding.

## Non-Goals

- Do not perform detailed map authoring before the resume gate.
- Do not bind Strategic Management locations, add persistent location state, write back destroyed bridge state, or change city-management UI under this task.
- Do not add procedural generation, automatic bridge-height inference, or same-coordinate multi-surface movement.

## Constraints And Risks

- Premature content work is likely to be reauthored against changing operational requirements.
- Resumption requires a new confirmed execution scope inside this same task or a clearly linked replacement task; historical records are evidence only.
- GodotPrompter skills for a resumed implementation should be selected then; no installed skill applies to this documentation migration.

## Acceptance Criteria

- While paused, the verified scaffold and its authority boundary remain intact.
- Before resumption, `DemoSite` operational/city-loop validation is recorded and detailed content needs are confirmed.
- A resumed execution defines proportionate scene/resource verification and manual visual QA before changing authored map content.

## Current Progress Snapshot

### Completed

- `BridgeMapMarker` contract and authoring surface implemented.
- Reusable plains base and inherited first-city layout scaffold implemented.
- Static scene/resource regression, project build, and whitespace checks recorded as passing.

### Remaining

- Validate the operational/city loop on `DemoSite`.
- Confirm the detailed city-map content required by real systems.
- Author and visually verify the confirmed content slice.

### Pause Or Blocker

- Intentionally paused to avoid premature detailed authoring.

### Resume Condition

- The `DemoSite` operational/city loop is stable and validated sufficiently to identify real map-content requirements.

### Resume Entry

- Read this task and current city/location and site-map authority. Record `DemoSite` validation evidence, return to discussion to define the next slice, and set `Ready` only after user confirmation.

### Latest Verification

- Structural scaffold regression, build, and `git diff --check` were recorded as passing; manual editor QA and detailed content authoring remain deferred.

## Execution Record

- Migrated from the retired implementation-record queue on 2026-07-12. The original record remains unchanged under `history/implementation-proposals/2026-06-19-site-map-layout-first-city.md`.

## Final Result

Paused pending the recorded resume condition.
