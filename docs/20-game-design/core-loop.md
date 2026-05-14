# Core Loop

## Primary Campaign Loop

```text
Read strategic-map situation
-> Choose location, force, person, relationship, or threat to act on
-> Recruit / appoint / move / attack / defend / negotiate / prepare
-> Enter local operation, social event, encounter, or tactical battle
-> Resolve objective, withdrawal, failure, or social outcome
-> Write results back to map, location, people, resources, and threats
-> World and factions continue toward the next decision point
```

The main loop is not "fight -> upgrade -> next story node". The player should feel that map situation, people management, and tactical conflict are connected.

The main loop is also not "summon -> collect -> optimize everyone". Summoning may express recruitment, but the gameplay value is deciding who matters, who can be trusted, who gets authority, and how their actions change the campaign.

## Strategic Map Loop

```text
Observe forces, locations, threats, and opportunities
-> Choose attack, defense, movement, interception, or delay
-> Commit people, troops, resources, or time
-> Trigger site operation, encounter, auto-resolution, or tactical battle
-> Update control, garrison, damage, resources, threat, and faction pressure
```

The strategic map should provide pressure and context for people management and battle choices.

## Officer Social Loop

```text
Find or summon a person
-> Read relationship, loyalty, duty, affinity, talent, and rank
-> Recruit, persuade, appoint, promote, reward, bind, or delegate
-> Person acts in map, social event, or battle context
-> Outcome changes trust, obligation, resentment, loyalty, authority, and availability
```

Origin flavor never owns gameplay logic. Gameplay logic reads relationship kinds, metrics, tags, rank, talent, duty, and control authority.

## Site / Location Loop

```text
Enter or inspect WorldSite
-> Read control, facilities, garrison, NPCs, threats, entrances, and local resources
-> Operate, repair, build, deploy, negotiate, recruit, or trigger battle
-> Save site memory and return to the strategic map
```

`WorldSite` is the current implementation term for an operable location. It should support strategic and social decisions, not become only a modal management panel.

## Tactical Battle Loop

1. Battle starts from strategic or social context.
2. A structured `BattleStartRequest` carries deployment, objective, map, participants, and modifiers.
3. Player reads units, terrain, objectives, AP/resources, and enemy intent.
4. Player uses movement, skills, command authority, support, or limited intervention.
5. Enemies and non-direct allies execute readable intent or behavior rules.
6. Objectives, casualties, retreat, capture, destruction, and special outcomes resolve.
7. A structured `BattleResult` writes back to map, site, people, resources, and threats.

## Core Decision Axes

- Strategy: where to attack, defend, intercept, retreat, or invest.
- People: whom to recruit, trust, appoint, reward, promote, or constrain.
- Authority: who can lead, act autonomously, be directly controlled, or receive macro orders.
- Position: where forces, parties, locations, and objectives are.
- Risk: what can be lost politically, socially, strategically, or tactically.
- Resource: time, troops, money/economy, supplies/materials, AP, and attention.
- Information: enemy intent, location state, faction movement, and relationship signals.
- Consequence: how this result changes map state, people state, and future options.

## Combat Pace

- Normal combat: 3 to 5 turns.
- Elite combat: 6 to 8 turns.
- Site or objective battles may end earlier if the player extracts, rescues, destroys, captures, or delays the target.
