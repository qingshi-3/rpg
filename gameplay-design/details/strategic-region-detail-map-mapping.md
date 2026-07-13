# Province And City Detailed-Map Relationship

## Parent Authority

This detail refines `../content-systems-long-term-design.md` for the player-facing relationship between large-world province geography, member cities, and authored detailed maps.

## Player Promise

Approaching, contesting, or controlling a main or auxiliary city remains relevant after the game enters that province's detailed map. Different member cities may resolve different entrances, attacker deployment contexts, route contexts, or other explicit battle-start conditions without pretending that the detailed map is a scaled copy of the large world.

## Concept Boundaries

| Concept | Meaning |
|---|---|
| Province | The strategic aggregate containing exactly one main city and zero or more auxiliary cities. It owns one authoritative detailed-map `LayoutId`. |
| Main or auxiliary city | A stable strategic location identified by `LocationId` and belonging to exactly one province. It owns mutable city facts through Strategic Management, but it does not select a separate layout. |
| Large-world visual region | The one geometry associated with a city's `LocationId`. It supports presentation and geographic queries but has no independent gameplay identity, control, or persistent state. |
| Detailed-map semantic region or marker | An authored entrance, deployment area, route, objective, construction area, event position, or other semantic reference inside the province layout. |
| City construction region | A detailed-map buildable area used by city placement rules. It is not a large-world city region. |
| Battle tactical region | A battle-scoped authored or runtime area used for intent, movement, local combat, support, or diagnostics. It is not campaign geography and does not own strategic control. |

Shape overlap does not merge these concepts or their authority.

## Core Rules

- A province owns exactly one detailed-map `LayoutId`; member cities cannot choose different layouts.
- Every main or auxiliary city corresponds one-to-one with one large-world visual geometry keyed by `LocationId`.
- The visual geometry is not a separate `RegionId`, campaign location, control record, or settlement target. Its visible state derives from the corresponding city and province.
- A mapping may connect a member city's `LocationId` to an entrance, attacker deployment area, route context, scenario condition, or another authored battle-start fact inside the province layout.
- A large-world polygon is not scaled, rasterized, or projected into detailed-map cells. The detailed map remains an authored gameplay space whose topology and legal cells are authoritative inside that map.
- If two city approaches are meant to play differently, their authored semantic mappings make the difference explicit rather than infer it from screen coordinates.
- Battle entry and settlement preserve the same `ProvinceId`, battle `LocationId`, and province-owned `LayoutId`. Detailed-map and battle Runtime systems never own campaign control.
- City construction regions and battle tactical regions participate only through explicit semantic references. Neither becomes large-world geography.

## Player Decisions And Feedback

When city approach matters, pre-battle context should communicate the resolved entrance, deployment side, route pressure, or scenario implication before launch. Battle reports and strategic return feedback retain enough province and city context to explain where the battle occurred and where consequences were applied.

Several member cities may intentionally resolve the same authored context within the province layout. A semantic mapping can vary without changing the province's layout.

## Cross-System Links

- Canonical geography defines provinces, member cities, and one visual geometry per city.
- Strategic Management owns mutable province and city facts plus campaign consequences.
- Detailed-map authoring defines stable semantic markers and legal topology.
- The Strategic Battle Bridge resolves and validates city-to-marker mappings for battle entry.
- Battle settlement returns consequences through the accepted Bridge and Strategic Management command path.

Implementation ownership and failure rules live in `../../system-design/strategic-region-detail-map-mapping-architecture.md`.

## Reference Boundary

The standalone strategic-region Preview remains frozen reference evidence for presentation quality only. It is not production Runtime authority, a campaign-region model, a mapping implementation, or a required coordinate-conversion technique.

## Still Unconfirmed

- vision and discovery rules;
- exact province and city capture rules;
- supply, logistics, and strategic AI use of geography;
- exact marker mappings for individual cities;
- final auxiliary-city display names and balance identities.

## Non-Goals

- Literal large-world-polygon-to-grid scaling.
- Independent mutable campaign state for visual regions.
- Per-city detailed-map selection inside one province.
- Automatic generation of detailed maps from large-world polygons.
- Reclassifying city construction regions or battle tactical regions as large-world city regions.
