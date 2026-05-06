# Core Loop

## World Loop

```text
Observe the moving world
-> Choose a destination, army, WorldSite, or opportunity
-> Travel or enter the local space
-> Intervene through site operation, event choice, negotiation, or battle
-> Resolve objective, withdrawal, or failure
-> Write results back to world, site, character, resource, and threat state
-> WorldClock / WorldTick advances
```

The main loop is not "fight -> upgrade -> next story node". The player should feel that the world keeps moving and that local intervention changes persistent state.

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
   - Control heroes through movement and skills.
   - Use support, commands, or site modifiers when available.
4. Enemy and rule-driven units execute.
5. Objectives, casualties, terrain state, and withdrawal state resolve.
6. Battle returns a structured `BattleResult`.

## Core Decision Axes

- Identity: what role the player has in this situation.
- Position: where the player party, armies, and WorldSites are.
- Objective: what the player wants to achieve before leaving.
- Risk: who will react and what can be lost.
- Resource: AP in battle, resources and time in world/site play.
- Information: enemy intent, site state, faction movement, and opportunity details.

## Combat Pace

- Normal combat: 3 to 5 turns.
- Elite combat: 6 to 8 turns.
- Site or objective battles may end earlier if the player extracts, rescues, destroys, or captures the target.
