# Product Positioning

## One-Line Pitch

```text
一款融合三国群英传式大地图战略、三国志 / 三国立志传式人物社交经营、回合制战棋战斗的 2D 像素战略 RPG。
```

## Core Gameplay Category

The game is a strategy RPG built from three player-facing gameplay pillars:

1. **Strategic map play**: forces, locations, armies, routes, attack/defense, occupation, threats, and consequences.
2. **Officer social play**: recruitment, appointments, loyalty, affinity, bonds, rank, duty, betrayal risk, and people-driven events.
3. **Tactical battle play**: turn-based grid combat used to resolve key conflicts through units, terrain, skills, objectives, and battle results.

These pillars are the source of truth for Steam tags, external pitch, and feature prioritization.

## Expression Layer

The project can express recruitment and character arrival through 异世界召唤, historical figures, legendary figures, original characters, and inter-world conflicts.

This expression layer exists to increase content capacity and character variety. It does not change the gameplay category.

```text
召唤 = 招募 / 人物登场 / 内容扩展的世界观表达
不是独立玩法品类
```

A summoned character should still be evaluated by gameplay facts such as relationship, loyalty, duty, talent, rank, command authority, battle role, faction value, and event hooks.

## Product Pillars

### 1. Strategic Map

The player should experience a 三国群英传-style strategic surface:

- Operable locations / `WorldSite` entries on a large map.
- Forces and armies moving, attacking, defending, intercepting, retreating, and occupying.
- Local victories or failures changing location, force, resource, and threat state.
- A map that gives strategic context instead of acting only as a linear level list.

### 2. Officer Social Management

The player should experience a 三国志 / 三国立志传-style people layer:

- Finding, recruiting, persuading, appointing, rewarding, and managing people.
- Relationship, affinity, loyalty, obligation, resentment, trust, and bonds affecting outcomes.
- Social choices changing who can be trusted, promoted, delegated to, or lost.
- Character value coming from politics, relationships, duty, and command role, not only combat stats.

### 3. Tactical Battle

The player should resolve important conflicts through tactical battles:

- Grid-based, turn-based combat.
- Units, terrain, objectives, skills, enemy intent, and limited resources.
- Battle results that write back to strategic-map and people state.
- Objectives beyond clearing all enemies: attack, defense, rescue, escort, capture, delay, evacuation, and sabotage.

## Not The Primary Pitch

Do not lead external positioning with these as the main category:

- Summoning simulator.
- Linear chapter JRPG.
- Full 4X.
- Paradox-style grand strategy simulation.
- Open-world sandbox.
- Menu-only base management game.

Some of these can describe supporting elements, but they are not the core gameplay promise.

## Steam Tag Direction

Recommended first-line tags should come from gameplay:

```text
Strategy RPG
Strategy
Tactical RPG
Turn-Based Tactics
Simulation
```

Possible secondary tags after feature maturity:

```text
Political Sim
Party-Based RPG
Resource Management
Base Building
Choices Matter
Pixel Graphics
2D
Top-Down
Fantasy
Singleplayer
```

`Grand Strategy` is a risky Steam tag. The project can be described in Chinese as having 大地图战略 / 战略玩法, but Steam's `Grand Strategy` tag may create expectations closer to Paradox-style national simulation. Use it only if the implemented strategic layer supports that audience expectation.

## Target Player Promise

The player should feel:

- I am competing and surviving on a strategic map, not just clearing story nodes.
- People matter: whom I recruit, trust, appoint, reward, or alienate changes the campaign.
- Key conflicts become tactical battles where positioning and unit choices matter.
- Battles feed back into the map and people layer.
- The setting gives rich character variety, but the gameplay remains strategy RPG first.
