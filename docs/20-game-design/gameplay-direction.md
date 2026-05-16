# Core Gameplay Direction

This document defines the stable player-facing gameplay direction. Product positioning lives in `../10-product/positioning.md`; technical implementation lives in `../30-technical-design/`.

## Core Positioning

```text
三国群英传式大地图战略
+ 三国志 / 三国立志传式人物社交经营
+ 城池 / 战略地点经营与英雄带兵轻 RTS 战斗
```

The project may use 异世界召唤 as a worldbuilding and content expression. That expression should enrich recruitment, character variety, and relationship conflict, but it should not replace the gameplay pillars above.

## Reference Game Boundaries

Reference games clarify structure, but they do not define the final player experience.

### Versus Heroes Of Might And Magic

Heroes of Might and Magic is a useful warning case for surface similarity:

```text
large map + resources + towns/sites + heroes/armies + tactical battle
```

The project should not drift toward a Heroes-style core of map resource racing, town build-order optimization, stacked army growth, low-loss clearing, and mostly optional tactical settlement.

Key differences to preserve:

- Strategic play should be about world pressure, `WorldSite` consequences, people, duties, threats, and battle writeback, not only mine control, weekly production, and army size snowballing.
- Characters should not be only army containers, stat auras, or spell platforms. Recruitment, trust, appointment, loyalty, resentment, authority, and loss must change the campaign.
- Battles should not become full-unit manual chores where the dominant question is the single best order for minimizing losses.
- Important battles should validate preparation and command: scouting, city/location development, hero/corps build, deployment, terrain use, battle-time orders, and accepted strategic risk.
- The execution layer can borrow the readable event flow of auto-battlers, but the battle identity remains hero-led light RTS rooted in a concrete city or strategic location.
- Low-value encounters may support direct auto-resolution, but important conflicts should justify battle playback through objectives, site damage, capture, rescue, retreat, or relationship consequences.

The desired contrast is:

```text
Heroes-style battle = traditional tactical settlement of strategic advantage.
This project = hero-led light RTS validation of strategic, social, location, hero, corps, deployment, and command choices.
```

### Versus Sanguo Qunying

Sanguo Qunying remains the strategic campaign skeleton reference and the closest high-level battle-command reference, but it does not define the exact implementation or product identity.

Learn from:

- Persistent strategic state.
- Site/city ownership and army movement.
- Officer/general assignment.
- Battle entry from world actions.
- Heroes leading visible troops.
- Player command at company level rather than individual soldier micro.
- Battle results returning to world, people, resources, and faction state.

Do not copy:

- Old control density, singleton-heavy implementation style, or hardcoded skill structure.
- Pure conquest as the default player path.
- One-off hardcoded skill logic.
- Historical IP, names, content, or assets.

The useful distinction is:

```text
Sanguo Qunying provides the campaign chain and hero-led troop command reference.
This project combines that with officer-social management, concrete strategic-location operation, fantasy corps identity, and reportable battle consequences.
```

## Layer Separation

### Gameplay Layer

The player-facing game is built around:

- Moving through and contesting a strategic map.
- Recruiting, appointing, trusting, persuading, rewarding, and managing people.
- Operating authored `WorldSite` maps through facilities, garrison, deployment, exploration, preparation, and local consequences.
- Resolving important conflicts through hero-led light RTS battles that the player can command, read, and diagnose.
- Letting results flow between map state, people state, resources, site state, and battle state.

### Expression Layer

The setting can explain character arrival through summoning, rifts, historical echoes, legends, and original worlds. These are content devices.

Expression should answer:

```text
Why can these people appear together?
Why do they bring old bonds, grudges, duties, and identities?
Why can the content roster expand beyond one historical period?
```

Expression should not answer:

```text
What Steam genre is this?
What are the main player verbs?
What system owns recruitment, loyalty, appointment, site operation, or battle?
```

### Technical Layer

Implementation uses concepts such as `Faction`, `WorldSite`, `WorldArmy`, relationship state, data definitions, battle handoff, and result writeback. These are implementation owners and contracts, not product labels.

## Strategic Map Pillar

The strategic map provides the Sanguo Qunying style campaign surface:

- Locations / `WorldSite` entries with control, resources, garrison, facilities, damage, and local memory.
- Forces, parties, armies, raids, or expeditions moving across the map.
- Attack, defense, interception, retreat, occupation, and threat pressure.
- World state that changes because of player action, NPC action, or battle results.

The strategic map should not become only a static mission list.

## Officer Social Pillar

The people layer provides a 三国志 / 三国立志传 style social and officer-management layer:

- Recruit, summon, persuade, visit, appoint, promote, reward, punish, delegate, and dismiss.
- Loyalty, trust, obligation, affinity, resentment, bonds, duty, talent, and rank.
- Relationship-driven events, cooperation, refusal, betrayal risk, faction drift, and promotion opportunities.
- Characters matter as political/social actors, not only as combat units.

Summoning is one possible expression for acquiring people. Its runtime gameplay facts should still be abstract relationship, duty, rank, talent, authority, and social state.

## Strategic-Location Operation And Hero-Led Combat Pillar

`WorldSite` is the current implementation name for authored local spaces. Player-facing language should prefer concrete names such as city, stronghold, ruin, dungeon, resource site, or pass.

These spaces are not freeform city builders and not generic fair battle boards.

The player-facing loop is:

```text
inspect site and intel
-> operate facilities / repair / scout / appoint / deploy
-> commit hero and corps build
-> enter an authored battle map
-> command hero companies through hero, corps, and combined orders
-> read battle report
-> accept writeback consequences
```

Battle should use:

- grid cells for facility placement, garrison/deployment, pathing, terrain, and local combat readability;
- hero companies made of one hero plus one main corps;
- visible soldiers for corps presentation, with shared corps strength rather than individual soldier persistence;
- player-cast hero skills with cooldown and mana pressure;
- corps roles as formation, durability, pressure, and sustained-output expression;
- facilities and terrain as light but meaningful modifiers, not heavy wall-hitting chores.

During execution, battle should feel like medium-frequency hero-led light RTS. The player commands hero companies, redirects troops, chooses hero skills, retreats, and regroups, but does not control individual soldiers.

Battle must not become a black box. If the player cannot tell why a build won or failed, the core loop fails.

## Player Identity

The exact narrative identity can be defined by setting, but the stable system identity is:

```text
战略势力核心 / 人物经营者 / 场域经营与战斗构筑者
```

The player should make decisions such as:

- Which location or threat should I attack, defend, delay, scout, or ignore?
- Who should I recruit, trust, appoint, reward, or keep away from authority?
- Which people should lead, guard, negotiate, scout, or fight?
- How should this site be developed and defended?
- Which hero/corps build, deployment, and command plan should carry this conflict?
- Which conflicts deserve battle playback, and which can be directly resolved?
- What strategic and social consequences can I accept?

## First-Phase Focus

The current Strategic World V1 implementation remains valid as a foundation slice, but its role should be understood as implementation support for the strategic-map and WorldSite pillars, not as the whole product identity.

Short-term validation should prioritize:

- A strategic-map loop with locations, resources, threats, and attack/defense.
- `WorldSite` operation with facilities, garrison/deployment, and local state.
- A minimal battle slice that receives strategic/location context, supports hero-led command, and returns meaningful results.
- People or unit authority that affects deployment, build, battle behavior, and consequences.
- Location state and social/resource state persisting after conflict resolution.

## Explicit Non-Goals For Now

The first phase should not attempt:

- A full 4X ruleset.
- A Paradox-style grand strategy simulation.
- A full open-world simulation.
- A purely linear chapter JRPG.
- A summoning-only collection game.
- Full manual control of every unit and every person.
- TFT-like shop/economy/synergy drafting as the main battle identity.
- Heavy production chains that overshadow people, site pressure, and conflict.
- One-off hardcoded scripts for specific characters, events, or locations.
