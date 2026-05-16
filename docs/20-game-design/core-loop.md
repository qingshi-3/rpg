# Core Loop

## Primary Campaign Loop

```text
Read strategic-map situation
-> Choose location, force, person, relationship, site, or threat to act on
-> Recruit / appoint / move / scout / build / deploy / attack / defend / negotiate / prepare
-> Enter local operation, social event, encounter, auto-resolution, or hero-led battle
-> Resolve objective, withdrawal, failure, or social outcome
-> Read battle/site report when relevant
-> Write results back to map, location, people, resources, and threats
-> World and factions continue toward the next decision point
```

The main loop is not "fight -> upgrade -> next story node". The player should feel that map situation, people management, site operation, and conflict results are connected.

The main loop is also not "summon -> collect -> optimize everyone". Summoning may express recruitment, but the gameplay value is deciding who matters, who can be trusted, who gets authority, and how their actions change the campaign.

## Strategic Map Loop

```text
Observe forces, locations, threats, and opportunities
-> Choose attack, defense, movement, interception, scouting, or delay
-> Commit people, troops, resources, or time
-> Trigger site operation, encounter, auto-resolution, or hero-led battle
-> Update control, garrison, damage, resources, threat, and faction pressure
```

The strategic map should provide pressure and context for people management, site management, and battle choices.

## Officer Social Loop

```text
Find or summon a person
-> Read relationship, loyalty, duty, affinity, talent, and rank
-> Recruit, persuade, appoint, promote, reward, bind, or delegate
-> Person acts in map, site, social event, or battle context
-> Outcome changes trust, obligation, resentment, loyalty, authority, and availability
```

Origin flavor never owns gameplay logic. Gameplay logic reads relationship kinds, metrics, tags, rank, talent, duty, and control authority.

## Site / Location Loop

```text
Enter or inspect WorldSite
-> Read control, facilities, garrison, NPCs, threats, entrances, terrain, and local resources
-> Operate, repair, build, scout, deploy, negotiate, recruit, or trigger battle
-> Save site memory and return to the strategic map
```

`WorldSite` is the current implementation term for an operable location. It should support strategic and social decisions, not become only a modal management panel.

## Hero-Led Battle Loop

1. Battle starts from strategic, site, or social context.
2. A structured `BattleStartRequest` carries objective, map, participants, modifiers, site-state snapshot, and placement references copied from site-authoritative deployment state.
3. Player enters with prior choices from build, appointment, facility, scouting, and deployment.
4. The player selects hero companies and issues hero, corps, and combined commands at medium frequency.
5. Unit behavior remains mostly automatic: soldiers move, attack, form up, and die through readable battle rules rather than individual soldier micro.
6. The player reads battle feedback through clear events, skill cues, damage/source feedback, command state, and formation changes.
7. Objectives, casualties, retreat, capture, destruction, and special outcomes resolve.
8. A structured `BattleResult` writes back to map, site, people, resources, and threats.
9. A battle report explains contribution and failure causes so the next build and command decision is actionable.

## Core Decision Axes

- Strategy: where to attack, defend, intercept, scout, retreat, or invest.
- People: whom to recruit, trust, appoint, reward, promote, or constrain.
- Authority: who can lead, act autonomously, be delegated to, or carry a corps.
- Site: which facilities, deployment cells, entrances, terrain advantages, and defensive preparations matter.
- Build: which hero/corps composition, skills, roles, and upgrades are committed.
- Position: where forces, parties, locations, deployment slots, and objectives are.
- Risk: what can be lost politically, socially, strategically, tactically, or locally.
- Resource: time, troops, money/economy, supplies/materials, attention, and site capacity.
- Information: enemy intent, location state, faction movement, site intel, and relationship signals.
- Consequence: how this result changes map state, people state, and future options.

## Combat Pace

- Normal battle: about 45 to 120 seconds of readable command and playback, with skip or fast-forward where appropriate.
- Elite or site-objective battle: longer only if the battle has visible phase changes, objective pressure, or meaningful build diagnosis.
- Low-value conflicts should support direct auto-resolution without battle playback.
