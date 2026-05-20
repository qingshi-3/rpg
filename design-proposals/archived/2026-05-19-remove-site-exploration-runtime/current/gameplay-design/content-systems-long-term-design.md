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

## Strategic Location Naming

Use **strategic location** as the design umbrella for large-map locations.

`WorldSite` can remain a technical abstraction. Player-facing and design documents should prefer concrete terms:

| Type | Role |
|---|---|
| City / Stronghold | Core long-term management, training, armament, garrison, defense, and major warfare. |
| Resource Site | Lightweight resource-producing point. |
| Gate / Pass | Route control and defensive chokepoint. |
| Ruin | Neutral exploration site with special resources, equipment, lore, unlocks, or sealing/clearing outcomes. |
| Dungeon | Combat-focused exploration site with battles, rewards, materials, and progression. |
| Opportunity | Short-lived map event such as ambush, caravan, refugees, rescue, or monster attack. |

Do not force every strategic location into the full city management model. Only core cities should carry the full management surface.

## Combat Model

Combat is hero-led light RTS.

```text
hero company = 1 hero + 1 main corps
```

The player selects a hero company, but command is split into three channels:

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

- company move;
- company attack;
- company defend;
- company retreat;
- regroup.

Combat should support medium-frequency command. The player often selects hero companies, moves, focuses targets, casts hero skills, and redirects troops, but does not perform high-frequency individual soldier micro.

Long-term hero capacity may allow one hero to manage multiple corps slots, such as a `4-6` upper limit. The first playable combat slices may still use one hero plus one main corps as the battle-group shape. That is an implementation staging rule, not a rejection of later reserve or multi-corps capacity.

## Corps Presentation And Casualties

Each hero brings one visible corps.

```text
normal company: 1 hero + 3-8 visible soldiers
normal battle: 3-5 friendly hero companies
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

These attributes should not be the main battle report language. They support progression, profession fit, skill pools, equipment requirements, and a small number of city or exploration actions.

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

First-phase city attributes are:

```text
Control
Population
Food
Money
BuildingMaterials
SpecialResources
GarrisonCapacity
TrainingCapacity
WorkshopCapacity
DefenseValue
FacilitySlots
```

Do not include public order, intelligence, or damage as first-phase core city attributes.

### City Attribute Roles

| Attribute | Role |
|---|---|
| Control | Who owns the city and whether it can be built, trained, defended, or upgraded. |
| Population | Labor, recruits, tax base, facility staffing. |
| Food | Sustains population, garrison, training, and long defense. |
| Money | Construction, recruitment, training, maintenance, equipment upgrades. |
| BuildingMaterials | Construction, defense, workshops, repair-like costs when added later. |
| SpecialResources | Fantasy corps and equipment upgrades. |
| GarrisonCapacity | How many hero companies, corps, or guards can be stationed. |
| TrainingCapacity | Corps level cap increase and training efficiency. |
| WorkshopCapacity | Corps equipment level cap and upgrade efficiency. |
| DefenseValue | City defense, walls, gates, towers, and defensive auto-resolution. |
| FacilitySlots | Constrained city development choices. |

### City And Corps Growth

City management should directly feed corps growth:

```text
city training capacity -> corps level cap
battle -> corps level experience
city workshop capacity + resources -> corps equipment level
city garrison capacity -> how many companies can be stationed
city defense value -> defensive battle conditions and auto-resolution
```

## Facilities

Cities use limited facility slots instead of freeform city building.

Facility categories:

| Category | Examples | Role |
|---|---|---|
| Resource | farms, mines, quarries, markets, special gathering points | Food, money, materials, special resources. |
| Military | barracks, training grounds, stables, beast nests, archery ranges, mage towers | Garrison, training, class unlocks. |
| Workshop | smithy, armor forge, machine workshop, arcane workshop, beast gear workshop | Corps equipment level and special processing. |
| Defense | walls, towers, gates, barricades, wards | Defense value and defensive battle support. |
| Storage | granary, warehouse, quartermaster | Resource caps or cost reduction. |
| Special | summoning circle, ruin laboratory, purification altar, dragon nest, graveyard, portal | Unique content, special corps, resources, or story hooks. |

Cities should develop different identities:

```text
main city: balanced
frontier city: garrison and defense
resource city: production
workshop city: equipment upgrades
special city: unique corps or special resources
```

## Non-City Strategic Locations

### Resource Sites

Resource sites provide specific resources and can be occupied, guarded, raided, or linked to nearby cities.

They should not carry full city systems such as population, broad facilities, and full training or workshop layers.

### Gates And Passes

Gates and passes control routes and defensive chokepoints.

Minimal attributes:

```text
Control
GarrisonCapacity
DefenseValue
RouteLinks
LimitedFacilitySlots
```

### Ruins

Ruins are exploration and special-content locations.

Possible states:

```text
unexplored
exploring
cleared
occupied
sealed
depleted
```

Ruins can provide special equipment, resources, corps unlocks, story clues, summoning materials, or facility blueprints.

Only important ruins should become managed strategic locations.

### Dungeons

Dungeons are combat-focused exploration locations.

They can track:

```text
exploration progress
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
- hero company contribution;
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
ranged company lacked protection
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
1 ruin or dungeon
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

First-phase city attributes:

```text
Control
Population
Food
Money
BuildingMaterials
SpecialResources
GarrisonCapacity
TrainingCapacity
WorkshopCapacity
DefenseValue
FacilitySlots
```

First-phase commands:

```text
hero: move / hold / attack / retreat / cast skill
corps: advance / return to guard / hold / attack target / retreat
combined: company move / company attack / company retreat / regroup
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
