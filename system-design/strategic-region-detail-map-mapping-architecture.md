# Province And City Detailed-Map Mapping Architecture

Status: Accepted Architecture

## Gameplay Authority

This document implements `gameplay-design/details/strategic-region-detail-map-mapping.md` and the concise province/city rules in `gameplay-design/content-systems-long-term-design.md`.

The accepted contract is semantic stable-id mapping. Large-world visual geometry may inform authored detailed-map layouts, but it is never scaled or projected into detailed-map cells.

## Responsibility

This architecture owns the cross-system contract that connects:

- one province and one of its main or auxiliary city locations;
- the province's authoritative authored `LayoutId`;
- stable detailed-map semantic marker references used for entrance, deployment, route, scenario, or other battle-start context;
- the same province, battle-location, and layout identities carried through result settlement and Strategic Management writeback.

It defines ownership, identity, resolution, handoff, validation, and failure boundaries. It does not own individual map content or battle outcomes.

## Does Not Own

This architecture does not own:

- province or city visual-geometry authoring and presentation;
- province/location control, expedition movement, or campaign mutation;
- detailed-map topology, cell coordinates, construction legality, or marker authoring;
- battle tactical-region generation, local combat, AI, or Runtime movement legality;
- exact vision, discovery, supply, balance, or strategic-AI rules;
- a production implementation of the standalone strategic-region Preview.

## Concept And Ownership Boundaries

| Concept | Identity And Owner | Boundary |
|---|---|---|
| Province | Stable `ProvinceId`; canonical geography owns membership and the province-owned `LayoutId`, while Strategic Management owns mutable campaign facts. | Contains exactly one main city and zero or more auxiliary cities. It is the sole detailed-layout selector. |
| Main or auxiliary city | Stable `LocationId` plus its `ProvinceId`; canonical geography owns static role and position, Strategic Management owns mutable city facts. | A battle or approach may target this location, but it cannot select a different layout. |
| Large-world visual geometry | Exactly one polygon or multipolygon bound directly to `LocationId`. | Presentation/query data only; it has no independent `RegionId`, control, or persistent state. |
| Detailed-map semantic region or marker | Province `LayoutId` plus stable `MarkerId`; Site Map Layout and Semantic Map Marker authoring own the authored reference and extracted pure data. | Supplies entrance, deployment, route, objective, construction, event, or tactical meaning to named consumers. |
| City construction region | Strategic Management construction-region identity matched to a detailed-map `ConstructionRegion` marker. | Owns buildable placement context only; it is not large-world geography or a battle tactical region. |
| Battle tactical region | Snapshot- or Runtime-scoped target, action, combat, deployment, objective, or temporary region. | Owns battle intent/local-combat meaning only; it does not own campaign control or large-world geometry. |

Shape overlap does not create identity or authority between these concepts.

## Mapping Definition Contract

Definitions / Content own one authoritative semantic binding keyed by stable identities. Each binding expresses:

```text
ProvinceId
LocationId
one or more semantic references: MarkerId + battle-entry role
optional authored scenario-context tags
```

`ProvinceId` resolves the sole authoritative `LayoutId`. `LocationId` must be one of that province's main or auxiliary cities and identifies the battle or approached location. A serialized mapping may repeat `LayoutId` only as a validation assertion equal to the province definition; it cannot select another layout. `ApproachRegionId`, independent gameplay `RegionId`, and `DetailMapId` are not part of the accepted contract.

Battle-entry roles may identify an entrance, attacker deployment context, route context, or another explicitly supported start condition. Several locations may intentionally resolve the same context within the same province layout.

Bindings reference authored identities; they do not embed copied marker footprints, large-world polygons, scaled coordinates, or mutable strategic state. Display names, centroids, colors, mask numbers, and scene-node paths are not mapping keys.

## Authoritative Resolution Path

```text
canonical geography provides ProvinceId + LocationId
-> the province definition supplies its authoritative LayoutId
-> Strategic Management validates current control, expedition, and target-location facts
-> mapping definitions resolve semantic MarkerId roles inside that LayoutId
-> Site Map Layout extraction provides matching marker data and topology
-> Strategic Battle Bridge validates the resolved entry context
-> BattleStartSnapshot carries ProvinceId + BattleLocationId (= LocationId) + LayoutId and battle-facing context
-> battle Runtime validates map legality and emits outcome/events
-> Settlement and the Bridge preserve ProvinceId + BattleLocationId + LayoutId
-> Strategic Management commands apply consequences against that same lineage
```

No layer may infer the mapping from large-world geometry, mask values, pixels, marker position, naming convention, or nearest-cell heuristics.

## Persistent State

Strategic Management owns mutable campaign facts keyed by `ProvinceId` and/or member `LocationId`. There is no independent persistent record for the large-world visual geometry and no separate region-control owner. Mapping definitions and detailed-map marker catalogs are static content.

The province-owned layout is a reusable authored template. Persistent city facts remain keyed by city `LocationId`; layout- or marker-specific facts additionally carry the province's `LayoutId` or stable `MarkerId`. Reusing authored layout content must not share control, buildings, battle results, or other city state.

## Runtime And Snapshot State

The Strategic Battle Bridge may hold resolved mapping context for one session:

- `ProvinceId`;
- `BattleLocationId`, equal to the validated member `LocationId` where the battle occurs;
- authoritative `LayoutId` copied from the province definition;
- resolved semantic marker IDs and their entry roles.

`BattleStartSnapshot` carries only battle-facing facts Runtime needs plus this identity lineage for events, reports, and settlement attribution. Runtime receives resolved marker-derived data; it does not query large-world polygons or Strategic Management live state. Any expedition source-location identity remains a separate participant or rollback field.

Authored detailed-map topology remains the final authority for deployment, footprint placement, movement, and cell legality. A valid semantic binding cannot make an illegal cell or marker footprint legal.

## Settlement And Writeback

Runtime reports battle facts and never mutates strategic control. Settlement and the Strategic Battle Bridge return consequences with the same session, snapshot, `ProvinceId`, `BattleLocationId`, and `LayoutId` lineage used at launch. Strategic Management applies accepted control, ownership, reward, loss, or other campaign changes only through commands against the province or city identities named by that handoff.

A result may cite detailed-map marker IDs for report context, but marker IDs and visual geometry never become campaign-control owners.

## Reference Boundary

The standalone strategic-region Preview is frozen reference evidence for presentation quality. Its scene, shader, masks, compiler behavior, and resources are not production Runtime authority or a campaign-state model.

## Failure Rules

- Missing or duplicate `ProvinceId`, `LocationId`, authoritative `LayoutId`, or required `MarkerId` fails validation with the stable identities involved.
- A location that is not a member of the named province, or a province without exactly one main city, fails before mapping or launch.
- A mapping, marker record, or Bridge context whose carried map identity differs from the province-owned `LayoutId` fails validation; it must not select or load the differing layout.
- A binding whose marker does not exist in the resolved detailed-map layout fails before battle launch; the Bridge must not choose a nearby marker or the whole map as fallback.
- A marker with the wrong semantic type or battle-entry role fails validation.
- A stale or mismatched province/location, bridge session, or snapshot identity blocks launch or writeback without partial mutation.
- Invalid detailed-map topology or placement remains a detailed-map/Runtime failure; mapping must not override it.
- Missing optional scenario context is ignored only when the binding and battle kind explicitly mark it optional.
- No consumer may derive a fallback by scaling large-world geometry into grid coordinates.

## Acceptance

This architecture is acceptable when:

- one stable-id path connects province/member-city geography and Strategic Management state to detailed-map battle context;
- member cities can resolve different authored entrance or deployment contexts inside one province-owned layout;
- the province's `LayoutId` is the sole layout selector;
- one visual geometry is bound directly to each city `LocationId` without independent campaign state;
- detailed-map markers, city construction regions, and battle tactical regions remain distinct from large-world visual geometry;
- mappings reference semantic identities rather than copied geometry or coordinate projection;
- the Bridge carries validated `ProvinceId` and `BattleLocationId` through settlement while Strategic Management remains the sole campaign mutation authority;
- the standalone Preview remains reference-only.
