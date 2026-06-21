# Cities And Locations Detail

## Parent Authority

Global rules live in `../../content-systems-long-term-design.md`, especially the strategic location naming, city management, facilities, and non-city strategic location sections.

## Boundary

This detail area defines strategic-location gameplay:

- city / stronghold management;
- resource sites;
- gates and passes;
- ruins;
- dungeons;
- opportunities;
- first-phase city attributes;
- city facilities and strategic role differentiation.

## Player Promise

Strategic management should feel like Sanguo Qunying-style conquest with meaningful logistics, not a heavy RTS economy or a full city-builder simulation. The player captures territory, develops cities into distinct military and economic roles, assigns persistent corps to heroes, and sees battle losses return to the management layer.

The strategic rhythm follows Sanguo Qunying-style world-map time. The large map is the running timeline; city screens are paused management spaces. Entering a city to manage facilities, corps, heroes, or resources must not let external armies move, enemies act, or passive production continue in the background. Returning to the world map resumes elapsed strategic time.

## Core Rules

Cities are the only full long-term management locations. Non-city locations can provide income, route control, rewards, or source permissions, but they do not inherit city construction, reserve soldiers, broad facility choices, and full training or workshop systems.

Each city has a local identity. A plains human city naturally supports common human military routes; a special or foreign route requires the right source permission, facility, resource, or relationship. The city identity defines what is stable and cheap, while special buildings and construction-region limits define what the city sacrifices development space to support.

City construction uses authored, bounded construction regions with RTS-style preview placement. The player chooses a building from a construction panel, sees a mouse-attached preview, and places it onto a legal snapped grid position inside a compatible region. The first version should make city development choices matter without becoming a full RTS base economy.

First-version construction does not include workers, road connectivity, resource pathing, gathering range, or distance-based production efficiency. Placement legality should focus on region type, footprint, bounds, overlap, and simple eligibility.

The first foundation building pool is:

```text
Farm
Market
Lumber Camp
Mine
Training Ground
Tavern / hero recruitment office
Arrow Tower
Medical Shrine / medical facility
```

Resource sites may still provide faction-shared income, route pressure, or later source permissions. Mines and lumber camps are also valid foundation city economy buildings; they are not restricted to non-city resource sites.

First-phase resources use faction-shared storage without cross-city transport loss. Regional logistics, route disruption, or front-line supply efficiency can be explored later only after the base conquest-management loop is playable.

Faction technology is not a first-version requirement. Corps access and growth come from city identity, facilities, resource sites, minor source sites, hero aptitude, and corps instance progression.

City management operations are not "end turn" actions. They are explicit strategic commands issued while the world-map timeline is paused. If a city action later needs duration, such as construction, training, recovery, or expedition preparation, that duration should progress after returning to the world-map timeline rather than by asking the player to advance a city turn.

## Corps Muster And Cities

Cities expose muster templates and aggregate reserve soldiers, not individual soldier records. A muster template means the city has the right conditions to create or rebuild a corps type. Conditions can include local identity, required facility, controlled source location, and resources.

City manpower follows:

```text
ActiveForces + ReserveForces <= CityForceCapacity
```

Active forces are represented by corps, hero companies, and garrison instances. Reserve forces are prepared but unassigned soldiers that recover over world-map time and can be spent on corps creation, replenishment, and later manpower-based local battle support.

Corps creation produces a persistent corps instance. The instance can be assigned to a hero, stationed in a garrison, travel with an expedition, recover after battle, train, or receive equipment-level upgrades.

Losing the source of a muster template does not delete existing corps instances. It stops or restricts new creation, recovery, training, or upgrades until the source returns.

## Later Beast Route

Beast taming is a later special-source route, not the first foundation operation loop.

Required chain:

```text
capture beast minor site
-> build beast pen / beast camp in a city
-> create or rebuild beast corps instances
-> assign them to suitable heroes
-> pay higher recovery and support costs after battle
```

The beast minor site is a controllable non-city strategic location. It provides beast source permission and small passive rewards, but it does not become a full city and does not upgrade in the first version.

The first beast corps examples are:

| Corps | Role | Strategic Cost Identity |
|---|---|---|
| Wolf pack assault | Fast shock assault, rear pressure, pursuit | Cheaper than great beasts, still slower to recover than common troops. |
| Great beast charge | Heavy shock assault and line-breaking pressure | High creation cost, slow recovery, and stronger dependence on beast source plus beast facility. |

Beast corps should not use random loss-of-control or friendly-fire risk in their first version. Their downside is clear strategic cost, recovery friction, limited source availability, and hero aptitude requirements.

## To Refine

- First city attribute ranges and display priority for `Money`, `Food`, `Wood`, `Ore`, `CityForceCapacity`, `ReserveForces`, and derived active forces.
- Which construction regions exist in the first core city and which building categories each region permits.
- Exact first-version resource and reserve-soldier costs for common corps.
- First new area to occupy or develop through the foundation expedition loop.
- Later beast minor site name, map role, guard strength, and passive reward.
- How ruins and dungeons reward equipment, special resources, and corps progression.

## Non-Goals

- Public order, intelligence, and damage as first-phase core city attributes.
- Full city systems for every strategic location type.
- Turning every ruin or dungeon into a managed city.
- RTS-style worker harvesting or production queues.
- Individual soldier records, civilian population simulation, or population-driven recruitment.
- Cross-city resource transport loss in the first version.
- Faction technology in the first version.
