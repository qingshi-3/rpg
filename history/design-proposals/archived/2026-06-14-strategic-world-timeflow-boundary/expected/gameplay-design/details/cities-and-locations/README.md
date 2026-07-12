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

Cities are the only full long-term management locations. Non-city locations can provide income, route control, rewards, or source permissions, but they do not inherit population, broad facility choices, and full training or workshop systems.

Each city has a local identity. A plains human city naturally supports common human military routes; a special or foreign route requires the right source permission, facility, resource, or relationship. The city identity defines what is stable and cheap, while special facilities define what the city sacrifices slots to support.

City facilities use limited slots. The first version should make facility choice matter: a city cannot simply build every military, workshop, defense, and special facility. Frontline, rear resource, workshop, defensive, and special-source cities should become different strategic roles.

Resource extraction buildings live on resource sites in the first version. Mines, lumber camps, and similar sites provide faction-shared income and do not consume city facility slots by default.

First-phase resources use faction-shared storage without cross-city transport loss. Regional logistics, route disruption, or front-line supply efficiency can be explored later only after the base conquest-management loop is playable.

Faction technology is not a first-version requirement. Corps access and growth come from city identity, facilities, resource sites, minor source sites, hero aptitude, and corps instance progression.

City management operations are not "end turn" actions. They are explicit strategic commands issued while the world-map timeline is paused. If a city action later needs duration, such as construction, training, recovery, or expedition preparation, that duration should progress after returning to the world-map timeline rather than by asking the player to advance a city turn.

## Corps Muster And Cities

Cities expose muster templates, not raw soldier stockpiles. A muster template means the city has the right conditions to create or rebuild a corps type. Conditions can include local identity, required facility, controlled source location, and resources.

Corps creation produces a persistent corps instance. The instance can be assigned to a hero, stationed in a garrison, travel with an expedition, recover after battle, train, or receive equipment-level upgrades.

Losing the source of a muster template does not delete existing corps instances. It stops or restricts new creation, recovery, training, or upgrades until the source returns.

## Beast Route First Version

The first special-source route is beast taming.

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

Beast corps should not use random loss-of-control or friendly-fire risk in the first version. Their downside is clear strategic cost, recovery friction, limited source availability, and hero aptitude requirements.

## To Refine

- First city attribute ranges and display priority.
- Which facility slots exist in the first core city.
- Exact first-version resource names and costs for common and beast corps.
- First beast minor site name, map role, guard strength, and passive reward.
- How ruins and dungeons reward equipment, special resources, and corps progression.

## Non-Goals

- Public order, intelligence, and damage as first-phase core city attributes.
- Full city systems for every strategic location type.
- Turning every ruin or dungeon into a managed city.
- RTS-style worker harvesting or production queues.
- Individual soldier stockpiles.
- Cross-city resource transport loss in the first version.
- Faction technology in the first version.
