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

Each city has a local identity. A plains human city naturally supports common human military routes; a special or foreign route requires the right source permission, facility, resource, or relationship. The city identity defines what is stable and cheap, while special buildings and later support-efficiency tradeoffs define what the city sacrifices development space to support.

City construction uses authored, bounded construction regions with RTS-style preview placement. The player chooses a building from a construction panel, sees a mouse-attached preview, and places it onto a legal snapped grid position inside a buildable region. Construction regions decide where buildings can be placed, not which building categories are allowed there. The first player-facing construction version uses this interaction only for defensive fortifications.

First-version construction does not include workers, road connectivity, resource pathing, gathering range, or production-efficiency simulation. Placement legality should focus on buildable region membership, footprint, bounds, overlap, resources, and simple eligibility. Later economy or capability work may let terrain, tile, resource context, or local map facts affect resource/support efficiency without becoming hard placement-category bans.

An eligible first-version defensive fortification must have a detailed-map position that materially affects a local defense battle. Its battle effect must cross the Strategic Battle Bridge as position-aware local support or as a position-aware battle entity; local support is only the Bridge representation of the eligible fortification's effect, not a support-building category. A purely strategic benefit, support building, or other non-defensive facility does not qualify even if it could later affect battle. This condition defines the intended construction slice; it does not assert that the required battle integration already exists.

The first player-facing construction list contains only defensive fortifications and currently has one confirmed baseline:

```text
Arrow Tower: baseline defensive fortification
```

Medical shrines, medical facilities, other support buildings, farms, markets, lumber camps, mines, training grounds, taverns, workshops, and every other non-defensive facility remain later strategic-management capabilities, but they are outside the first player-facing construction list even if they could later affect battle. Resource sites may still provide faction-shared income, route pressure, or later source permissions without creating a foundation city economy-building construction loop.

Walls, barricades, traps, route sealing, and other passability- or topology-changing fortifications are also outside this first version and require later focused design.

First-phase resources use faction-shared storage without cross-city transport loss. Regional logistics, route disruption, or front-line supply efficiency can be explored later only after the base conquest-management loop is playable.

Faction technology is not a first-version requirement. Corps access and growth come from city identity, facilities, resource sites, minor source sites, hero aptitude, and corps instance progression.

City management operations are not "end turn" actions. They are explicit strategic commands issued while the world-map timeline is paused. If a city action later needs duration, such as construction, training, recovery, or expedition preparation, that duration should progress after returning to the world-map timeline rather than by asking the player to advance a city turn.

## Corps Muster And Cities

Cities expose muster templates and aggregate reserve soldiers, not individual soldier records. A muster template means the city has the right conditions to create or rebuild a corps type. Conditions can include local identity, required facility, controlled source location, and resources.

City manpower follows:

```text
ActiveForces + ReserveForces <= CityForceCapacity
```

Active forces are represented by corps, battle groups, and garrison instances. Reserve forces are prepared but unassigned soldiers that recover automatically at a base rate of `2` per elapsed world-map pulse and can be spent on corps creation, replenishment, and later manpower-based local battle support. Recovery is free, requires no building, and stops at the remaining city force capacity.

The first version does not ask the player to issue a manual conscription command or select an automatic conscription intensity. City presentation may show current reserve soldiers, total force capacity, and the passive recovery rate as read-only strategic facts, but it does not need a dedicated conscription workflow.

Corps creation produces a persistent corps instance. The instance can be assigned to a hero, stationed in a garrison, travel with an expedition, recover after battle, train, or receive equipment-level upgrades.

Losing the source of a muster template does not delete existing corps instances. It stops or restricts new creation, recovery, training, or upgrades until the source returns.

The first-version recruitment UI is also the hero main-corps reassignment surface. When a hero replaces an assigned corps, the old corps is fully refunded into the city without extra reassignment loss, based on its current remaining strength. The new corps then becomes the hero's main corps. This keeps the city operation loop focused on hero-led forces instead of requiring a separate corps inventory tab before that workflow has a distinct purpose.

Troop options in the recruitment surface should show their reserve-soldier and resource costs directly. Replacement options should also expose the old-corps refund and final net reserve/resource change before the player commits.

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
- Which construction regions exist in the first core city and which terrain, tile, or local map facts should later affect resource/support efficiency.
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
- Player construction of economy, recruitment, hero, workshop, or other non-defensive facilities in the first construction version.
- Passability-changing walls, barricades, traps, roadblocks, or route-sealing structures in the first construction version.
