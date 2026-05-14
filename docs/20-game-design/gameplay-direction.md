# Core Gameplay Direction

This document defines the stable player-facing gameplay direction. Product positioning lives in `../10-product/positioning.md`; technical implementation lives in `../30-technical-design/`.

## Core Positioning

```text
三国群英传式大地图战略
+ 三国志 / 三国立志传式人物社交经营
+ 回合制战棋战斗
```

The project may use 异世界召唤 as a worldbuilding and content expression. That expression should enrich recruitment, character variety, and relationship conflict, but it should not replace the three gameplay pillars above.

## Reference Game Boundaries

Reference games clarify structure, but they do not define the final player experience.

### Versus Heroes of Might and Magic

`魔法门之英雄无敌` is a useful warning case for surface similarity:

```text
large map + resources + towns/sites + heroes/armies + tactical battle
```

The project should not drift toward a Heroes-style core of map resource racing, town build-order optimization, stacked army growth, low-loss clearing, and mostly optional tactical resolution.

Key differences to preserve:

- Strategic play should be about world pressure, `WorldSite` consequences, people, duties, threats, and battle writeback, not only mine control, weekly production, and army size snowballing.
- Characters should not be only army containers, stat auras, or spell platforms. Recruitment, trust, appointment, loyalty, resentment, authority, and loss must change the campaign.
- Tactical battles should not become full-unit manual chores where the dominant question is the single best order for minimizing losses.
- Key battles should be new-style tactical command play: limited direct control, readable enemy Intent, AP/support-resource tradeoffs, objective pressure, mechanism counterplay, and consequences that cannot be fully replaced by auto-resolve.
- Low-value encounters may support delegation or auto-resolution, but important conflicts should justify manual play through objectives, mechanisms, people, site damage, capture, rescue, retreat, or relationship consequences.

The desired contrast is:

```text
Heroes-style battle = traditional tactical settlement of strategic advantage.
This project = commander-led tactical intervention that reads plans, spends scarce authority, counters mechanisms, and creates long-term consequences.
```

### Versus Sanguo Qunying

`三国群英传` remains the strategic campaign skeleton reference, not the combat target or product identity.

Learn from:

- Persistent strategic state.
- Site/city ownership and army movement.
- Officer/general assignment.
- Battle entry from world actions.
- Battle results returning to world, people, resources, and faction state.

Do not copy:

- Real-time soldier clash as the target battle feel.
- Pure conquest as the default player path.
- One-off hardcoded skill logic.
- Historical IP, names, content, assets, or old singleton-heavy implementation style.

The useful distinction is:

```text
Sanguo Qunying provides the campaign chain.
This project replaces its battle and people experience with officer-social management and new-style turn-based tactical command.
```

## Layer Separation

### Gameplay Layer

The player-facing game is built around:

- Moving through and contesting a strategic map.
- Recruiting, appointing, trusting, persuading, rewarding, and managing people.
- Resolving important conflicts through tactical grid battles.
- Letting results flow between map state, people state, resources, and battle state.

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
What system owns recruitment, loyalty, appointment, or battle?
```

### Technical Layer

Implementation uses concepts such as `Faction`, `WorldSite`, `WorldArmy`, relationship state, data definitions, battle handoff, and result writeback. These are implementation owners and contracts, not product labels.

## Strategic Map Pillar

The strategic map provides the 三国群英传-style campaign surface:

- Locations / `WorldSite` entries with control, resources, garrison, facilities, damage, and local memory.
- Forces, parties, armies, raids, or expeditions moving across the map.
- Attack, defense, interception, retreat, occupation, and threat pressure.
- World state that changes because of player action, NPC action, or battle results.

The strategic map should not become only a static mission list.

## Officer Social Pillar

The people layer provides 三国志 / 三国立志传-style social and officer management:

- Recruit, summon, persuade, visit, appoint, promote, reward, punish, delegate, and dismiss.
- Loyalty, trust, obligation, affinity, resentment, bonds, duty, talent, and rank.
- Relationship-driven events, cooperation, refusal, betrayal risk, faction drift, and promotion opportunities.
- Characters matter as political/social actors, not only as combat units.

Summoning is one possible expression for acquiring people. Its runtime gameplay facts should still be abstract relationship, duty, rank, talent, authority, and social state.

## Tactical Battle Pillar

Tactical battle resolves local conflicts:

- Turn-based grid combat.
- AP or other shared tactical resources.
- Units, terrain, skills, objectives, visible intent, and readable consequences.
- Battle results that write back to strategic-map, people, resources, threat, and location state.

Battle goals should not stay limited to clearing enemies. Stronger goals include attack, defense, rescue, escort, capture, delay, evacuation, sabotage, and boss mechanism resolution.

## Player Identity

The exact narrative identity can be defined by setting, but the stable system identity is:

```text
战略势力核心 / 人物经营者 / 关键战斗指挥者
```

The player should make decisions such as:

- Which location or threat should I attack, defend, delay, or ignore?
- Who should I recruit, trust, appoint, reward, or keep away from authority?
- Which people should lead, guard, negotiate, scout, or fight?
- Which conflicts deserve tactical battle, and which can be delegated or auto-resolved?
- What strategic and social consequences can I accept?

## First-Phase Focus

The current Strategic World V1 implementation remains valid as a foundation slice, but its role should be understood as implementation support for the strategic-map pillar, not as the whole product identity.

Short-term validation should prioritize:

- A strategic-map loop with locations, resources, threats, and attack/defense.
- People or unit authority that affects deployment, command, and consequences.
- Tactical battles that receive strategic context and return meaningful results.
- Location state and social/resource state persisting after conflict resolution.

## Explicit Non-Goals For Now

The first phase should not attempt:

- A full 4X ruleset.
- A Paradox-style grand strategy simulation.
- A full open-world simulation.
- A purely linear chapter JRPG.
- A summoning-only collection game.
- Full manual control of every unit and every person.
- Heavy production chains that overshadow people and conflict.
- One-off hardcoded scripts for specific characters, events, or locations.
