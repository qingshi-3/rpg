# Sanguo Qunying Reference Architecture

This document defines how to use 三国群英传 as a structural reference without copying its combat, content, IP, assets, or code style.

## Reference Role

Sanguo Qunying is useful because it closes this product chain:

```text
strategic world state
-> player chooses force, site, officer, or army action
-> army movement / site change / encounter generation
-> battle entry
-> battle result
-> world, character, resource, and faction writeback
-> next strategic choice
```

This project should preserve that chain.

## Current Project Direction

```text
大地图战略 + 人物社交经营 + 城池 / 战略地点经营与英雄带兵轻 RTS 战斗
```

The reference provides the campaign skeleton and high-level hero-led troop command reference. It is not an implementation blueprint or content source.

## Learn From

- The large map is a persistent state container, not decoration.
- Sites, factions, officers, armies, and resources are stateful.
- Player actions create armies, change the map, trigger encounters, and open battle entry.
- Battle is not an isolated scene; it returns results to world state.
- Scenario data lets one runtime support multiple openings.
- Domestic preparation, deployment, battle, save/load, and world progression form one product chain.

## Reject

- Historical IP, character names, city names, portraits, audio, text, and assets.
- Old Unity singleton-heavy implementation style.
- Hardcoded arrays and magic indices as long-term rules.
- One skill as one hardcoded script path.
- Strategic layer directly mutating battle internals or battle layer directly mutating world state.
- Copying Sanguo Qunying control density, old combat pacing, or content-specific skill behavior as-is.

## Mapping To This Project

| Sanguo concept | Project abstraction | Notes |
|---|---|---|
| City | `WorldSite` / 场域 | Persistent operable world location, not necessarily a city. |
| Faction / ruler | `Faction` | Control, hostility, ownership, diplomatic pressure. |
| General | `Character` / hero | Long-term person identity; may instantiate battle unit or commander role. |
| Army | `WorldArmy` / party / expedition | Moves on strategic map, enters sites, triggers conflicts. |
| Domestic affairs | `WorldAction` / `WorldSiteOperation` | Preparation, construction, repair, training, scouting, appointment. |
| Skill | `AbilityDefinition` + effect/condition/target rules | Avoid one-off hardcoded skill logic. |
| Scenario MOD | `StrategicWorldDefinition` / run definition | Initial state and rule set. |
| Battle result | `BattleResult` | Writes back world, site, people, army, resources, facilities, threats. |
| Save | run save state | Save runtime state, not presentation-only caches. |

## Default Development Rule

When a request says "make world map", "make site", "make battle entry", "make expedition", or "make garrison" without enough detail, default to this minimum chain:

```text
persistent state
-> player/world action
-> request object
-> local operation or battle
-> structured result
-> state writeback
-> next world tick or choice
```

Do not implement isolated screens that cannot feed this chain.

## Battle Responsibility

Sanguo Qunying battle is a useful reference for heroes leading troops and player command at company scale. This project battle target is hero-led light RTS with readable automatic soldier behavior and structured reports.

Required continuity:

- battle consumes characters, corps, facilities, site state, terrain, intel, and modifiers from world/site context;
- battle produces structured results;
- battle remains readable enough that the player can diagnose build, deployment, and command choices;
- battle does not directly mutate world state.

## Minimum Product Bar

Any major new feature should answer:

- What persistent state does it read or write?
- What action creates or changes it?
- Does it connect to site, people, army, threat, or battle entry?
- What structured result comes back?
- What future decision changes because of this result?

If the answer is only "it displays a panel" or "it starts a scene", it is not enough.
