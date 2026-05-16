# Product Positioning

## One-Line Pitch

```text
一款融合三国群英传式大地图战略、三国志 / 三国立志传式人物社交经营、
城池 / 战略地点经营与英雄带兵轻 RTS 战斗的 2D 像素战略 RPG。
```

## Core Gameplay Category

The game is a strategy RPG built from three player-facing gameplay pillars:

1. **Strategic map play**: forces, locations, armies, routes, attack/defense, occupation, threats, and consequences.
2. **Officer social play**: recruitment, appointments, loyalty, affinity, bonds, rank, duty, betrayal risk, and people-driven events.
3. **City / strategic-location operation and hero-led corps combat**: authored location maps, facilities, garrison/deployment, hero/corps builds, medium-frequency battle commands, battle reports, and result writeback.

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

The player should experience a 三国群英传 style strategic surface:

- Operable locations / `WorldSite` entries on a large map.
- Forces and armies moving, attacking, defending, intercepting, retreating, and occupying.
- Local victories or failures changing location, force, resource, and threat state.
- A map that gives strategic context instead of acting only as a linear level list.

### 2. Officer Social Management

The player should experience a 三国志 / 三国立志传 style people layer:

- Finding, recruiting, persuading, appointing, rewarding, and managing people.
- Relationship, affinity, loyalty, obligation, resentment, trust, and bonds affecting outcomes.
- Social choices changing who can be trusted, promoted, delegated to, or lost.
- Character value coming from politics, relationships, duty, and command role, not only combat stats.

### 3. City / Strategic-Location Operation And Hero-Led Combat

Important conflicts should resolve through hero-led light RTS battles that validate player preparation and in-battle command:

- Authored city, stronghold, ruin, dungeon, or other strategic-location maps are concrete management and battle spaces, not menu-only panels.
- Facility slots, garrison slots, entrances, deployment cells, and terrain shape the local conflict.
- Each hero company is one hero plus one main corps.
- The player selects hero companies and issues separate hero, corps, and combined commands at medium frequency.
- Soldiers and enemies fight through readable automatic behavior, while player commands steer focus, movement, retreat, and hero skills.
- Battle reports explain why the build and command decisions won or failed and write results back to strategic-map and people state.

The execution layer may borrow the readability of auto-battlers: clear formations, visible skill timing, event feed, speed controls, and final diagnosis. The target is not pure post-deployment playback, shop-roll economy, fair-board rounds, or synergy drafting. The target is strategic-location warfare where preparation, terrain, hero/corps build, command timing, and strategic context matter.

## Not The Primary Pitch

Do not lead external positioning with these as the main category:

- Summoning simulator.
- Linear chapter JRPG.
- Full 4X.
- Paradox-style grand strategy simulation.
- Open-world sandbox.
- Pure TFT-like autobattler.
- Menu-only base management game.

Some of these can describe supporting elements, but they are not the core gameplay promise.

## Steam Tag Direction

Recommended first-line tags should come from gameplay:

```text
Strategy RPG
Strategy
Simulation
Auto Battler
Base Building
```

Possible secondary tags after feature maturity:

```text
Political Sim
Party-Based RPG
Resource Management
Tactical RPG
Choices Matter
Pixel Graphics
2D
Top-Down
Fantasy
Singleplayer
```

`Turn-Based Tactics` is no longer a first-line promise while the project migrates away from manual tactical chess. `Grand Strategy` is also risky unless the implemented strategic layer supports that audience expectation.

## Target Player Promise

The player should feel:

- I am competing and surviving on a strategic map, not just clearing story nodes.
- People matter: whom I recruit, trust, appoint, reward, or alienate changes the campaign.
- My city/location layout, hero/corps build, scouting, deployment, and command choices shape how battles play out.
- Battles are readable hero-led clashes, not black-box arithmetic or pure no-command playback.
- Battles feed back into the map and people layer.
- The setting gives rich character variety, but the gameplay remains strategy RPG first.
