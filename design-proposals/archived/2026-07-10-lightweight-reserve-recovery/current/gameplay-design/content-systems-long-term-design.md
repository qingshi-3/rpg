# Content Systems Long-Term Design

This document defines the long-term gameplay design for units, hero-led corps combat, progression, equipment, city management, and non-city strategic locations.

It is a clean design reference kept outside the older `docs/` hierarchy.

## Core Direction

The long-term content system balances three pillars:

```text
people and heroes: 40%
city and strategic-location management: 30%
combat build and command: 30%
```

The game should not become a pure hero RPG, a pure city builder, a TFT clone, or a heavy RTS. The target combat feel is hero-led light RTS inspired by Sanguo Qunying:

```text
prepare heroes, corps, equipment, professions, cities, and resources
-> enter a full authored battle map
-> command heroes and their troops separately at medium frequency
-> resolve real-time combat through automatic unit behavior and player commands
-> read a battle report that explains why the result happened
-> write consequences back to cities, resources, corps, and campaign state
```

The strategic layer uses Sanguo Qunying-style realtime world-map pacing, not Civilization-style player turns. World-map time runs while the player is on the large strategic map. Entering a city, battle preparation, battle execution, dialogue, or another modal management state pauses world-map time. City management commands are issued while paused; they may start work that completes later on world-map time, but opening or operating the city interface does not itself advance time.

## Combat Product Identity

The combat layer is a low-frequency spatial realtime tactical battle. The player is a battlefield commander who makes local war decisions through terrain, timing, reserves, and high-impact commands instead of winning through APM or individual soldier micro.

The combat experience should naturally ask the player to:

- hold or break chokepoints such as bridges, gates, passes, and alleys;
- contest terrain such as high ground, forests, routes, and multiple entrances;
- commit, delay, or preserve reserves;
- shift forces between local fronts;
- wait for cooldowns, reinforcements, flanks, or another-front breakthrough;
- respond to telegraphed enemy actions;
- retreat before local collapse becomes unrecoverable.

Combat features should be judged by whether they create these spatial decisions. Damage output, animation spectacle, and unit count are supporting tools, not the core combat identity.

## Battle Preparation And Plans

Battle preparation should express commander intent before real-time execution starts.

The default battle-preparation loop is:

```text
select a battle group
-> deploy that battle group with its selected formation
-> repeat for every participating battle group
-> start battle
```

Battle-group formation is a planning preference, not individual soldier micromanagement. Strategic management may let the player set a long-term default formation for each battle group, such as standard, assault, guard, loose, or column. Battle preparation initializes the current battle's selected formation from that default. The player may switch the selected formation before or after placement, but the selected formation belongs to the current battle deployment unless the player explicitly saves it as the battle group's default in a future management flow.

Drag deployment uses the selected formation. Formation adaptation may help the battle group fit narrow or irregular deployment zones, but it must preserve the player's tactical intent and every member footprint. The system may compress spacing or fall back to a column-like arrangement; it must not overlap members, place only part of a footprint in the zone, or silently scatter the battle group into unrelated positions.

Battle preparation no longer requires the player to choose an objective area or a battle-group state before launch. Deployment expresses where the group enters the map. Battle starts with a default attack posture.

During battle, the player expresses the main movement objective through destination beacons. The player may select one or more battle groups, then right-click a valid destination cell while battle is running or while tactical pause is active. If the destination is reachable for the selected groups, the battle places or moves a destination beacon at that cell. Multi-selected groups share that beacon and update only their own command scope; non-selected battle groups keep their current beacon, target, fallback, or local-combat state.

Destination beacons are player-facing runtime command objects, not hidden AI coordinates. A new beacon command supersedes the selected groups' previous player destination at the next valid command boundary. The default behavior after accepting a beacon is attack/assault: groups advance toward the beacon, enter relevant local combat, and resume beacon advance or finish the command according to runtime combat rules.

Authored objective areas remain useful tactical map semantics for scenarios, AI intent, reports, route hints, tutorials, and future optional planning modes. They are no longer a mandatory battle-preparation step for the first playable battle flow.

### Local Combat Response

Automatic battle behavior should understand active local fights, not only destination beacons and direct target pursuit. When combat starts near a battle group, nearby units that satisfy the local combat rules should evaluate whether they should join the local fight, take an open attack position, hold a named support position, or remain on their beacon, command, or defense task.

This behavior should make enemies and allies read as battlefield participants with local awareness:

- front-line units try to occupy valid attack positions when available;
- support units do not idle beside a fight simply because direct attack positions are full;
- defenders reinforce local fights inside their defense scope but do not chase indefinitely;
- beacon or explicit command movement remains the main direction, but active local combat may temporarily interrupt it inside the current command scope.

Local combat response is not global per-unit target optimization. It is a scoped tactical reaction to nearby combat that preserves player commands, defensive leashes, readable support positions, and Runtime movement authority.

Local response must remain readable by role:

- shield and infantry units reinforce lines, chokepoints, and nearby attack positions;
- ranged units prefer legal firing positions and avoid blocking the front;
- cavalry or other mobile units should not clog chokepoints and should join mainly when a side or rear approach is available.

Each battle group should preserve its command identity. Local response may temporarily redirect a battle group into a nearby fight, but it should not scatter visible soldiers into independent long-term behaviors or pull every nearby battle group into one fight without budget, leash, and return rules.


### Intent-Directed Enemy Combat

Enemy battle behavior should separate scenario context, enemy intent, non-engaged movement, and engaged local combat.

Battle scenarios such as siege defense, siege assault, field battle, ambush, or site defense provide battlefield target objects and default enemy intent. They do not hardcode enemy behavior. A siege-defense scenario may default to defending gates or inner lines, but a specific enemy group can still receive an explicit intent to sally out, harass, retreat, protect a structure, or assault the player's rear.

Non-engaged enemy groups move toward stable intent-owned target objects, not toward individual moving units. Target objects may be authored regions, deployment areas, gates, bridges, breaches, defensive lines, structures, or scenario-provided group targets. Runtime-observed player clusters are tactical observations; they may influence local combat or explicit hunt/harass intent, but they must not replace the group's long-term movement target by default.

Enemy group intent is data-driven. Encounter configuration may provide an explicit enemy group intent; enemy archetypes may provide defaults; battle scenarios may provide fallback intent; and a safe fallback must exist when no configured intent resolves. Intent references target selectors and tactical policies such as leash, retarget cooldown, engagement gate, and fallback intent.

Engaged enemy groups should use local-optimal combat inside a bounded local combat region. The local region is built from perception, contact, damage, and combat-zone facts. Local intelligence has a strict cap so it does not become global per-unit optimization.

Player groups may reuse target objects, perception, local combat-region, target, slot, and Runtime validation systems. Player destination beacons, explicit commands, and later accepted planning modes remain player-sourced intent. Enemy intent must not rewrite player intent.

## Strategic Location Naming

Use **strategic location** as the design umbrella for large-map locations.

`WorldSite` can remain a technical abstraction. Player-facing and design documents should prefer concrete terms:

| Type | Role |
|---|---|
| City / Stronghold | Core long-term management, training, armament, garrison, defense, and major warfare. |
| Resource Site | Lightweight resource-producing point. |
| Gate / Pass | Route control and defensive chokepoint. |
| Ruin | Neutral special-content location with direct battles, resources, equipment, lore, unlocks, or sealing/clearing outcomes. |
| Dungeon | Combat-focused challenge location with direct battles, rewards, materials, and progression. |
| Opportunity | Short-lived map event such as ambush, caravan, refugees, rescue, or monster attack. |

Do not force every strategic location into the full city management model. Only core cities should carry the full management surface.

## Strategic Time Model

The campaign has a large-map timeline. While the player is on the world map, elapsed strategic time may move armies, allow enemy actions, settle resource-site production, refresh opportunities, and later advance construction, recovery, training, or expedition timers.

City management is a paused management mode over that timeline. The player can inspect a city, spend resources, build facilities, create corps instances, assign heroes, and adjust strategic choices without outside armies or resource production advancing in the background. Returning to the world map resumes the large-map timeline.

Battle preparation, active battles, dialogue, story scenes, reports, and other focused modal states also pause the large-map timeline unless a later accepted design explicitly introduces a timed exception.

Internal settlement ticks or pulses may be used by the implementation to batch elapsed world-map time. They are not player-facing turns, and the game should not expose a generic "end turn" or "next step" loop as the default strategic rhythm.

## Combat Model

Combat is hero-led light RTS.

```text
battle group = 1 hero + 1 main corps
```

The player selects a battle group, but command is split into three channels:

```text
hero command
corps command
combined command
```

This supports Sanguo Qunying-style decisions such as leaving a hero behind while sending the troops forward.

### Hero Command

Hero commands affect the hero directly:

- move;
- attack;
- hold position;
- retreat;
- cast hero skill;
- move closer to or away from the corps.

### Corps Command

Corps commands affect the hero's troops as a group. The player never controls individual soldiers.

- advance;
- return to guard;
- hold area;
- attack a target or area;
- protect the hero;
- retreat.

### Combined Command

Combined commands affect the hero and corps together:

- battle-group move;
- battle-group attack;
- battle-group defend;
- battle-group retreat;
- regroup.

Combat should support medium-frequency command. The player often selects battle groups, moves, focuses targets, casts hero skills, and redirects troops, but does not perform high-frequency individual soldier micro.

Long-term hero capacity may allow one hero to manage multiple corps slots, such as a `4-6` upper limit. The first playable combat slices may still use one hero plus one main corps as the battle-group shape. That is an implementation staging rule, not a rejection of later reserve or multi-corps capacity.

## Corps Presentation And Casualties

Each hero brings one visible corps.

```text
normal battle group: 1 hero + 3-8 visible soldiers
normal battle: 3-5 friendly battle groups
```

Soldiers are visible and participate in movement, attacks, formation, and death presentation. Long-term state does not track each soldier independently.

Corps casualties use shared corps strength:

```text
CorpsStrength: 0-100
VisibleSoldiers: 3-8
```

Visible soldiers disappear at strength thresholds. Example for a 5-soldier corps:

```text
100-81: 5 soldiers
80-61: 4 soldiers
60-41: 3 soldiers
40-21: 2 soldiers
20-1: 1 soldier
0: corps routed
```

The hero can survive while the corps routs. Post-battle recovery, losses, and restoration costs are resolved from corps strength, battle outcome, retreat state, and city support.

## Attributes

Use two attribute layers:

```text
hero base attributes: long-term growth and build requirements
combat stats: readable battle-facing values
```

### Hero Base Attributes

Hero base attributes are:

| Attribute | Use |
|---|---|
| Martial | Melee, assault, and physical skill direction. |
| Vitality | HP, survival, pressure resistance. |
| Technique | Precision, ranged skill, critical or technical execution. |
| Tactics | Command, tactical skill efficiency, battle planning. |
| Willpower | Control resistance, sustained fighting, morale stability. |
| Charisma | Troop leadership, morale, command presence. |
| Craft | Facilities, machines, armament, repair, engineering. |
| Mystic | Magic, rituals, special energy, supernatural skills. |

These attributes should not be the main battle report language. They support progression, profession fit, skill pools, equipment requirements, and a small number of city or strategic-location actions.

### Combat Stats

Readable combat-facing stats are:

```text
HP
Attack
Defense
AttackSpeed
Speed
Range
Mobility
Mana
ManaRegen
Cooldown
```

`AttackSpeed` is the readable basic-attack cadence stat. Runtime uses it to pace basic attack events, and battle presentation uses the same value to scale attack animation playback so the stat and visible swing speed do not drift.

Do not add `Discipline` as a standalone core stat. Discipline-like behavior should come from morale, current command, corps level, equipment level, profession behavior, and hero-corps aptitude.

## Corps Definition

A corps is defined by gameplay role and fantasy form.

```text
CombatClass: battlefield role
Form: fantasy body or presentation
Tags: content and rule hooks
```

Examples:

```text
dragon riders = cavalry class + flying / dragon / beast tags
armored crabs = shield class + armored / aquatic / beast tags
treants = shield or infantry class + plant / giant tags
mechanical artillery = archer or mage class + construct / siege tags
```

Combat class is for player-readable battlefield responsibility. Form and tags provide fantasy identity, special resources, and limited special rules.

### Strategic Corps Muster

Strategic management treats corps options as city-supported muster templates and persistent corps instances.

A muster template is the right for a city to create or rebuild a corps type. It comes from the city's identity, controlled source locations, facilities, special resources, relationships, or later accepted systems. A template is not a free unit and does not bypass resource, time, facility, or capacity costs.

A corps instance is an actual persistent force attached to a city, garrison, expedition, or battle group. It tracks readiness facts such as strength, training, equipment level, experience, state, and current assignment. Long-term state still does not track individual soldiers independently.

Severe losses should not permanently delete a corps instance by default in the first strategic-management model. A wiped or shattered corps should enter a routed, scattered, or rebuilding state that requires city support and resources to restore if the relevant muster template remains available.

Losing a template source, such as a special site or required facility, should not immediately erase existing corps instances. It should prevent new creation and restrict recovery, training, or upgrades until the source is restored.

Hero-directed main-corps replacement uses the city recruitment workbench as its first-version player surface. When the player replaces a hero's current main corps with a newly mustered corps, the old corps is settled back into the city with full refund and no extra replacement loss. The refund is based on the old corps' current remaining strength, returning the recoverable reserve soldiers and resource value represented by that strength. Strength already lost in battle is not restored by reassignment. This replacement flow should not silently create a hidden city corps inventory by parking the replaced corps as another player-managed corps instance.

## Professions And Bonds

The first profession layer is combat class:

| Class | Battlefield Role |
|---|---|
| Infantry | Balanced advance. |
| Shield | Frontline protection, guarding, holding. |
| Spear | Anti-charge and line pressure. |
| Archer | Ranged fire and focus pressure. |
| Cavalry | Mobility, charge, pursuit. |
| Mage | Area damage, control, burst. |
| Medic | Healing, cleansing, protection. |
| Assassin | Flank, dive, hero threat. |

Bond systems should be profession/tag-based rather than specific historical relationship-based, because the setting can combine historical, fictional, and fantasy content.

Bonds should not mainly be "deploy N of class X for +Y%". They should modify tactics, behavior, and interaction:

```text
shield + archer: safer ranged firing line
spear + archer: anti-charge firing line
cavalry + assassin: stronger flank pressure and pursuit
mage + shield: better protection during casting
medic + infantry: stronger sustained advance and post-battle recovery
```

Tag bonds can express fantasy interaction:

```text
flying: ignores some terrain, but is vulnerable to anti-air pressure
aquatic: stronger in water-heavy locations
undead: morale-resistant, but limited by healing type
construct: requires repair instead of normal healing
dragon: high shock value and cost
```

## Hero-Corps Aptitude

Heroes can lead any corps class, but aptitude determines how well the combination works.

```text
S: strong corps stat modifier, advanced or unique skill link, better corps auto-skill efficiency
A: good corps stat modifier, standard skill link, better corps auto-skill efficiency
B: normal performance
C: light stat penalty, lower auto-skill efficiency
D: poor fit, most skill links unavailable
```

Hero-corps aptitude only affects:

- corps automatic skill efficiency;
- hero and corps skill links;
- corps stat modifiers.

It must not affect command response, morale loss, formation stability, or post-battle recovery cost.

## Progression

### Hero Progression

Hero progression uses low level cap plus rank breakthroughs.

```text
level: 1-20
rank: ordinary / elite / renowned / legendary / mythic or unique
profession mastery: per hero profession
```

Hero level provides base growth. Rank unlocks stage goals such as:

- skill slots;
- equipment slots;
- stronger corps access;
- advanced profession branches;
- unique skill mechanics;
- higher equipment-grade eligibility.

### Hero Skills

Hero skills are player-cast.

```text
skill availability = cooldown + mana cost
```

Skill tiers:

- normal active skill: short cooldown, low mana cost;
- core tactical skill: medium cooldown, medium mana cost;
- ultimate: long cooldown, high mana cost, limited per battle by resource pressure.

Skill assignment is a long-term loadout decision, not a hardcoded battle shortcut. Heroes, battle groups, equipment, or progression may grant skill slots that point to stable skill definitions. Those grants may carry level, source, modifier, or slot facts, but they should not duplicate the full skill definition.

Skill availability should remain readable to the player through mana, cooldown, charges, limited per-battle use, and explicit disabled reasons. Adding a new skill by content should usually mean composing authored skill definitions and reusable effect primitives; adding a new kind of effect, target rule, cost rule, or cross-system mechanic is code/system work.

### Corps Progression

Corps growth has two axes:

```text
corps level
corps equipment level
```

Corps level represents training and battle experience:

```text
training raises level cap and can provide limited experience
battle provides primary level-up experience
```

Corps equipment level represents standardized arms, gear, tools, beasts, magical components, or equivalent fantasy upgrades:

```text
city resources + required facility capacity -> equipment level upgrade
```

Examples:

```text
dragon cavalry: leather, dragon scale, sky crystal, stable or nest support
armored crab shield corps: building material, shell, water resource
mechanical archer corps: metal, core crystal, workshop capacity
undead infantry: grave soil, soul dust, black stone
```

Corps do not equip individual items.

## Equipment

Hero equipment uses light slots and a deep collection pool:

```text
weapon
armor
token / command item
```

Equipment depth comes from:

- grade;
- series;
- profession fit;
- hero fit;
- corps interaction;
- location or origin theme;
- limited fixed affixes or template variants.

Equipment grades:

```text
common -> fine -> rare -> epic -> legendary -> artifact / unique
```

Do not make random affix farming the main loop. High-grade equipment should support collection desire, identity, and build direction without becoming the only progression axis.

The token or command item slot is important because it connects equipment to hero-led corps play: banners, seals, horns, manuals, relics, contracts, or command artifacts can improve troop interaction and tactical identity.

## City Management

Cities are the core long-term managed strategic locations.

First foundation city state is:

```text
Control
Money
Food
Wood
Ore
CityForceCapacity
ReserveForces
ActiveForces (derived from corps, battle groups, and garrison instances)
ConstructionRegions
BuildingInstances
```

Do not include population, public order, intelligence, civilian demographics, or city damage as first-foundation core city attributes.

Cities should have a local identity, such as plains human city, forest settlement, dwarf mine city, undead grave city, frontier pass, or beast-border stronghold. City identity defines the natural military and economic routes that are cheap, stable, and easy to recover there. Foreign or special corps routes can be added through facilities, source permissions, and later support-efficiency tradeoffs, but they should compete with the city's normal development space and carry higher support requirements.

First-phase resources use faction-shared storage. Cross-city transport loss, regional logistics, and supply efficiency may be added later, but they are not first-phase requirements.

### City Attribute Roles

| Attribute | Role |
|---|---|
| Control | Who owns the city and whether it can be built, trained, defended, or upgraded. |
| Money | Construction, recruitment, training, maintenance, equipment upgrades. |
| Food | Recruitment, reserve recovery, garrison upkeep when added later, and long defense when added later. |
| Wood | Basic construction, city development, defensive structures, and some equipment or repair-like costs when added later. |
| Ore | Military construction, corps creation, equipment upgrades, and defense support. |
| CityForceCapacity | Total city manpower capacity for active corps, garrison, battle groups, and reserve soldiers. |
| ActiveForces | Soldiers already committed into corps, battle groups, or garrison; derived from owned or stationed military instances when possible. |
| ReserveForces | Prepared but unassigned soldiers available for recruitment, replenishment, and later manpower-based local support. |
| ConstructionRegions | Authored buildable areas that constrain where city buildings may be placed; they do not restrict building category. |
| BuildingInstances | Built city structures with level, construction state, placed region position, strategic effects, and later support state. |

### City And Corps Growth

City management should directly feed corps growth:

```text
city development and training buildings -> city force capacity
world-map time -> reserve soldier recovery up to remaining capacity
reserve soldiers + resources -> corps creation and replenishment
battle -> corps level experience
city training support -> corps training and recovery efficiency
city defense/support buildings -> later local defensive battle support
```

City support should also feed corps muster:

```text
city identity + controlled source locations + facilities -> muster templates
muster template + resources + reserve soldiers + capacity -> corps instance creation or rebuilding
city training support -> corps training and recovery efficiency
city workshop support + resources -> later corps equipment-level upgrades
hero-corps aptitude -> whether a hero can use the corps well
```

The first-version recruitment workbench is also the hero main-corps reassignment surface. Troop options should show the reserve-soldier and resource requirements directly as compact card attributes. Replacement still consumes the selected corps requirements and refunds the old corps through the Strategic Management command, but normal troop cards should not print separate consume, refund, and net-cost rows.

## Facilities

Cities use authored, bounded construction regions with RTS-style preview placement instead of pure menu slots or unrestricted full-map RTS construction.

The player chooses a building from a construction panel, sees a mouse-attached preview, and places it onto a legal snapped grid position inside an authored construction region. First-foundation legality checks cover footprint, overlap, region bounds, ownership/control, resources, and simple eligibility; construction regions do not ban building categories. The first foundation does not include workers, road connectivity, gathering range, resource pathing, or production-efficiency simulation. Later economy/capability work may let terrain, tiles, resource context, or local map facts modify production or support efficiency without turning those modifiers into hidden placement bans.

Building categories:

| Category | Examples | Role |
|---|---|---|
| Economy | farm, market, lumber camp, mine | Foundation income for food, money, wood, and ore. |
| Military | training ground | City force capacity, reserve support, common corps creation, replenishment, and later reinforcement support. |
| Hero / Administration | tavern or hero recruitment office | Hero access and later city administrative functions. |
| Defense / Support | arrow tower, medical shrine or medical facility | Later local battle fire support, emergency aid, and other bounded support effects. |
| Special | beast pen, racial sanctuary, mage tower, graveyard, portal | Later route-specific corps, resources, or story hooks. |

Cities should develop different identities:

```text
main city: balanced
frontier city: garrison and defense
resource city: production
workshop city: equipment upgrades
special city: unique corps or special resources
```

City buildings are not full RTS economy-production buildings. They open, stabilize, and improve city-supported muster templates, reserve recovery, training, garrison, workshop, and defense capabilities. A basic training ground may support common local troops, but it should not unlock every fantasy corps type. Specialized facilities such as archery ranges, stables, beast pens, siege workshops, mage towers, sanctuaries, graveyards, or unique local structures should define city roles and force tradeoffs.

The first building batch is deliberately small:

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

Resource sites may still provide passive income, route pressure, or source permissions as non-city strategic locations. Foundation city economy buildings can also produce faction-shared resources; mines and lumber camps are no longer restricted to resource sites only.

## Non-City Strategic Locations

### Resource Sites

Resource sites provide specific resources and can be occupied, guarded, contested through direct battles, or linked to nearby cities.

They should not carry full city systems such as population, broad facilities, and full training or workshop layers.

Some lightweight non-city sites may provide source permissions for special muster templates in addition to or instead of normal income. For example, a beast lair, old hunting ground, or abandoned taming yard can provide the source permission needed before a city with a beast pen can create beast corps. These sites remain smaller than cities: first versions track control, simple rewards or income, and whether their source permission is active.

Special-route validation, including beast taming, follows after the foundation city-operation loop is playable. Capturing a beast minor site can later provide beast source permission and small passive rewards. A city should still need a beast pen or equivalent special facility before it can create beast corps instances.

### Gates And Passes

Gates and passes control routes and defensive chokepoints.

Minimal attributes:

```text
Control
GarrisonCapacity
DefenseValue
RouteLinks
LimitedConstructionRegions
```

### Ruins

Ruins are special-content locations resolved through direct strategic actions, direct battles, rewards, sealing, clearing, occupation, or depletion.

Possible states:

```text
unknown
available
cleared
occupied
sealed
depleted
```

Ruins can provide special equipment, resources, corps unlocks, story clues, summoning materials, or facility blueprints.

Only important ruins should become managed strategic locations.

### Dungeons

Dungeons are combat-focused challenge locations.

They can track:

```text
challenge progress
depth or floors
enemy strength
reward pool
reset rules
```

Dungeons should not use full city management.

### Opportunities

Opportunities are short-lived map events. They become strategic locations only if they turn into persistent content.

## Battle Reports

Battle reports explain command, build, and resource outcomes.

Minimum report facts:

- outcome;
- battle group contribution;
- corps strength loss;
- hero skill use and impact;
- corps automatic skill performance;
- profession or tag bond triggers;
- equipment and command item contribution;
- city or facility influence;
- main failure reason;
- post-battle rewards, corps experience, equipment materials, city/resource changes.

Failure reasons should be actionable:

```text
frontline collapsed
hero overextended during assault
ranged battle group lacked protection
cavalry was countered by spear or chokepoint pressure
mana ran out before key skill timing
corps equipment level was too low
defense value was insufficient
retreat was ordered too late, causing high corps loss
```

## First-Phase Scope

The first implementation slice should validate the core loop before expanding breadth.

Recommended first-phase content:

```text
1 core city
1-2 resource sites
1 new area to occupy or develop through expedition
1 direct ruin or dungeon battle/reward site
3 heroes
3 corps classes
1 city light-RTS battle
1 equipment-grade sample set
1 corps level and equipment-level progression sample
basic battle report
```

Recommended first corps classes:

```text
shield: holding and protection
archer: ranged pressure
cavalry: charge and pursuit
```

After the foundation loop is playable, the first special strategic-management extension can validate one special corps route rather than a broad taxonomy. The current preferred route is beast taming:

```text
source: captured beast minor site
city facility: beast pen / beast camp
corps identity: shock assault
first examples: wolf pack assault, great beast charge
cost identity: high creation cost, slow recovery, and dependence on both source site and city facility
excluded first-version rule: random beast-control failure
```

First-phase city attributes:

```text
Control
Money
Food
Wood
Ore
CityForceCapacity
ReserveForces
ActiveForces (derived)
ConstructionRegions
BuildingInstances
```

First-phase commands:

```text
hero: move / hold / attack / retreat / cast skill
corps: advance / return to guard / hold / attack target / retreat
combined: battle-group move / battle-group attack / battle-group retreat / regroup
```

## Non-Goals

Do not treat these as first-phase requirements:

- complex national diplomacy or government simulation;
- public order, intelligence, and damage as core city attributes;
- multiple main corps under one hero;
- individual soldier long-term progression;
- individual corps equipment items;
- large-scale RTS box selection;
- heavy random-affix gear farming;
- many simultaneous battlefronts on one map;
- pure post-deployment autobattler playback with no player command authority.
