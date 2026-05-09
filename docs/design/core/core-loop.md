# Core Loop

## World Loop

```text
Observe the moving world
-> Choose a summon, appointment, destination, army, WorldSite, or opportunity
-> Travel or enter the local space
-> Intervene through site operation, event choice, negotiation, or battle
-> Resolve objective, withdrawal, or failure
-> Write results back to world, site, character, relationship, rank, resource, and threat state
-> WorldClock / WorldTick advances
```

The main loop is not "fight -> upgrade -> next story node". The player should feel that the world keeps moving and that local intervention changes persistent state.

The main loop is also not "manage every character -> optimize every battle". The player has limited direct control and must choose which people, sites, and crises deserve personal intervention.

## Summoning And Relationship Loop

```text
World pressure or resource creates a summoning opportunity
-> Player chooses whether and how to summon
-> Summoned character enters with authored origin flavor and abstract social data
-> Existing bonds, grudges, obligations, and traits seed relationship state
-> World, site, battle, or appointment events change relationship metrics
-> Relationship state affects loyalty, cooperation, availability, promotion, and event outcomes
```

Origin flavor never owns gameplay logic. Gameplay logic reads relationship kinds, metrics, tags, rank, talent, duty, and control authority.

## Command And Control Loop

```text
Player appoints a small number of direct-control units
-> Other units receive duties, behavior packages, or strategic assignments
-> Automatic units expose intent or rule-driven plans
-> Player can issue limited macro commands, spend resources, or intervene personally
-> Automatic resolution writes back casualties, merit, loyalty, resentment, and site/world changes
```

Direct control is a scarce progression resource. More control authority can be unlocked later, but it should never turn the game into full-army manual operation.

## Site Loop

```text
Enter WorldSite
-> Read current mode: Peacetime / Alert / Wartime / Aftermath
-> Inspect facilities, NPCs, threats, entrances, and local resources
-> Operate, repair, build, deploy, negotiate, or trigger battle
-> Save site memory and return to the big map
```

`WorldSite` is a playable local space. It should not become only a modal management panel.

## Battle Loop

1. Battle starts from a structured `BattleStartRequest`.
2. Enemies generate Intent, either public or semi-public.
3. Player turn:
   - Allocate AP.
   - Control appointed direct-control units through movement and skills.
   - Use support, macro commands, cards, or site modifiers to influence non-direct units when available.
4. Enemy and rule-driven or intent-driven non-direct units execute.
5. Objectives, casualties, terrain state, and withdrawal state resolve.
6. Battle returns a structured `BattleResult`.

## Core Decision Axes

- Identity: what role the player has in this situation.
- Position: where the player party, armies, and WorldSites are.
- Objective: what the player wants to achieve before leaving.
- Risk: who will react and what can be lost.
- Resource: AP in battle, resources and time in world/site play.
- Information: enemy intent, site state, faction movement, and opportunity details.
- Attention: which crises, characters, and battles the player can directly handle.
- Authority: who can be directly controlled, appointed, delegated, or left to autonomous behavior.

## Combat Pace

- Normal combat: 3 to 5 turns.
- Elite combat: 6 to 8 turns.
- Site or objective battles may end earlier if the player extracts, rescues, destroys, or captures the target.
