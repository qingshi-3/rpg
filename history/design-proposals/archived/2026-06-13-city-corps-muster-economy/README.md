# City Corps Muster Economy

## Metadata

| Field | Value |
|---|---|
| Requirement id | REQ-STRAT-MGMT-001 |
| Status | Accepted and merged into authority documents |
| Parent proposal | None |
| Supersedes | None |
| Superseded by | None |
| Amends | None |
| Amended by | None |
| Related implementation proposal | None yet |
| Affected authority documents | `gameplay-design/content-systems-long-term-design.md`; `gameplay-design/details/cities-and-locations/README.md`; `gameplay-design/details/heroes-and-corps/README.md` |

## Current Design

Current authority establishes city and strategic-location management as a core pillar, with limited facility slots, resource sites, corps growth through city support, and battle-result writeback. It does not yet define a concrete first strategic-management model for how cities unlock troop options, how small-unit forces persist between battles, or how special non-city locations feed troop recruitment.

## Accepted Direction

The first strategic-management model is city-led corps muster, not heavy RTS production, full 4X technology, or city-builder simulation.

- Cities have a local identity, such as a plains human city, that defines natural military development.
- Resource sites and minor strategic sites provide passive income or special source permissions; they do not become full cities.
- City facility slots are limited, forcing military, workshop, defense, and special-route tradeoffs.
- Troops are managed as persistent corps instances created from available muster templates, not as individual soldier inventory.
- Corps instances can suffer losses, recover, train, gain equipment level, and be assigned to heroes.
- Corps instances are not permanently deleted as the first model's default; severe losses route them into scattered or rebuilding states.
- The first special route is beast taming: capture a beast minor site, build a beast pen in a city, then create beast shock corps.
- First beast corps options are wolf pack assault and great beast charge.
- First-phase resources are faction-shared and do not use cross-city transport loss.
- Faction technology is deferred.

## Non-Goals

- RTS-style production queues.
- Individual soldier stockpiles.
- City-by-city technology trees or faction technology in the first version.
- Cross-city resource transport loss in the first version.
- Full population, public-order, logistics, or diplomacy simulation.
- Upgradable minor beast sites in the first version.
- Random beast-control failure in the first version.
- Broad multi-race troop taxonomy before the first city-led loop is playable.

## Merge Notes

The expected copies were merged into the affected authority documents. Implementation must start from a focused implementation proposal under `gameplay-alignment/implementation-proposals/`.
