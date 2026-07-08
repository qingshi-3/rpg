# First Playable Vertical Slice Target

## Parent Authority

Global product rules live in `../content-systems-long-term-design.md`.

This document defines the first playable vertical slice as a product target. It is used to decide which feature cases implementation proposals must satisfy before the project calls the first slice playable.

## Boundary

This document owns:

- player-facing demo goals;
- required user cases;
- minimum feature scope;
- acceptance criteria for the first playable slice.

This document does not own:

- code architecture, module ownership, scene structure, node types, resource loading details, or implementation order;
- long-term balance values, full content breadth, or final UI polish;
- temporary progress tracking.

Implementation work should reference the case ids in this document. If implementation reveals that a case is not achievable without changing accepted gameplay or system authority, use the proposal flow before coding around the conflict.

## Product Target

The first playable vertical slice must prove that the game is more than a battle runtime preview.

The player should experience one compact loop:

```text
understand the starting stronghold
-> choose one or more named battle groups
-> travel to an enemy strategic location
-> confirm the triggered battle
-> read battle context
-> deploy at least one participating battle group
-> command destination beacons during battle
-> intervene during battle
-> resolve the outcome
-> see world, hero, and reward feedback
```

The slice is successful only if all three product pillars are visible:

| Pillar | Slice Promise |
| --- | --- |
| Faction building | The player starts from a controlled stronghold, sends an expedition through the world map, and changes the world by winning or losing. |
| Hero attachment | The player commands a named battle group with a clear role and minimal character feedback. |
| Tactical breakthrough | The battle includes at least one player-triggered intervention that can visibly change battle flow. |

## Scenario

The first slice uses one authored scenario:

```text
Player stronghold -> Bonefield assault -> post-battle world feedback
```

The scenario must be playable from the project main scene without requiring editor-only setup or debug-only commands.

## Minimum Slice Content Counts

These counts are product acceptance gates for the first playable vertical slice. They define the minimum content breadth needed to show the intended game identity; they are not implementation architecture requirements.

| Content Area | Minimum Required Count | Slice Requirement |
| --- | ---: | --- |
| Strategic locations visible on the world map | 3 | 1 player stronghold, 1 enemy assault target named Bonefield, and 1 supporting resource site or opportunity location. |
| Fully managed city / stronghold | 1 | The player stronghold must show ownership, garrison, resources, and expedition access. |
| Direct battle / reward target | 1 | Bonefield must be reachable from the world map and resolve through the authored assault battle. |
| Player selectable heroes | 3 | Each hero must have a name, battlefield role, default corps, one active skill, and minimal post-battle reaction text. |
| Carried player battle groups per Bonefield expedition | 1-3 | The player may send one strategic expedition army carrying multiple selected battle groups. |
| Deployed player battle groups per battle | 1-3 | Battle preparation may deploy any carried battle-group subset, but at least one battle group must be deployed before launch. |
| Enemy named leaders | 1 | Bonefield must have one named enemy leader or boss-like commander. |
| Player-cast hero active skills | 3 | One active skill per selectable hero. Only the selected hero's skill is available in the battle. |
| Player corps classes | 3 | The first slice uses shield, archer, and cavalry as the selectable battle groups' default corps classes. |
| Enemy regular unit types | 2 | Bonefield must include at least two readable enemy unit roles besides the named leader. |
| Visible soldiers in the deployed corps | 3-5 | The selected corps must read as troops attached to the hero, not as a single abstract stat. |
| Hero equipment samples | 3 | The slice must include named samples covering weapon, armor, and token / command item. At least one sample must appear as a reward, unlock, or report-visible contributor. |

The supporting resource site or opportunity location does not need the full stronghold surface or a battle. It exists so the world map is not just a two-node battle launcher and so post-battle feedback can point to future strategic play.

## Required User Cases

### VS-01 Start And Understand The Slice

Player intent: start the demo and understand what to do next.

Player actions:

1. Launch the game into the strategic world.
2. Look at the starting stronghold and nearby enemy target.
3. Read the current objective.

Required system response:

- The player-controlled stronghold is visually and textually identifiable.
- The enemy Bonefield target is visible or discoverable without searching the whole map.
- The UI gives one clear next goal: send a battle group to assault Bonefield.

Acceptance:

- A first-time player can identify the player's base, the enemy target, and the next action without reading development documentation.
- Unsupported strategic locations do not look equally actionable as the slice target.

### VS-02 Inspect The Starting Stronghold

Player intent: feel that the player owns a place, not just a battle launcher.

Player actions:

1. Select the starting stronghold.
2. Open its available actions.
3. Review available garrison, resources, and expedition options.

Required system response:

- The stronghold shows its ownership, garrison, and basic resource state.
- The available battle groups are shown as named forces, not anonymous unit counts.
- The stronghold exposes the expedition action from VS-04.

Acceptance:

- The player can explain what they control before sending an expedition.
- The stronghold screen does not require full city management, but it must communicate ownership, available heroes, available corps, and expedition access.

### VS-03 Trigger Battle Confirmation

Player intent: intentionally start the battle after the expedition reaches a hostile target.

Player actions:

1. Select or form an expedition force.
2. Right-click the hostile Bonefield target.
3. Let the expedition travel to the target.
4. Confirm the "触发战斗" dialog when the army arrives.

Required system response:

- Right-clicking a hostile battle-capable target issues the expedition attack order.
- Arrival pauses world-map time long enough for an intentional battle entry.
- The camera focuses the battle location before or while the confirmation dialog appears.
- The confirmation dialog title is "触发战斗".
- Confirming enters the battle preparation / deployment scene.
- No strategic battle-preparation choice is required in this slice.

Acceptance:

- The player can send a selected expedition force to Bonefield by right-clicking the hostile target.
- The player sees a battle confirmation at the target location after arrival.
- The player can enter the battle without making any separate strategic preparation choice.

### VS-04 Select Named Battle Groups

Player intent: choose a hero-led expedition force, not raw troops.

Player actions:

1. Start an expedition from the stronghold.
2. Select one or more available battle groups.
3. Confirm the default corps attached to each selected hero.

Required system response:

- The expedition panel shows the hero name, hero role, default corps name, and basic tactical identity.
- Each selectable hero has a distinct battlefield role and default corps class.
- Each selected hero's default corps is attached automatically for the first slice.
- The player cannot accidentally launch an empty or non-hero expedition in this slice.
- The selected battle groups travel as one strategic expedition army.

Acceptance:

- Each selected battle group can be described as "hero + corps".
- The player understands each selected hero's battlefield role before choosing the target.
- The slice offers three selectable battle groups, and the expedition can carry any available subset from one to three battle groups.

### VS-05 Send The Expedition To Bonefield

Player intent: move the selected battle groups through the world toward a hostile objective.

Player actions:

1. Choose Bonefield as the expedition target.
2. Watch the expedition army travel.
3. Arrive at the target.

Required system response:

- The selected battle groups leave the stronghold as one visible strategic expedition army.
- Arrival pauses or gates world progression long enough for the player to enter battle intentionally.
- If the player targets a location outside the slice scope, the game explains that no assault battle is available there.

Acceptance:

- The player sees a world action before battle starts.
- The transition into battle feels like the result of the expedition, not a detached debug jump.

### VS-06 Read Pre-Battle Context

Player intent: understand what is about to happen and why the fight matters.

Player actions:

1. Read the battle confirmation or pre-battle information.
2. Confirm entry into the assault.

Required system response:

- The pre-battle context names both sides.
- It identifies Bonefield as the target.
- It communicates the main objective: win the assault and take or secure the location.

Acceptance:

- The player understands the battle goal before seeing the deployment screen.
- Battle entry does not depend on a hidden or mandatory strategic preparation choice.

### VS-07 Deploy Participating Battle Groups

Player intent: choose which carried battle groups participate in this battle, place them, and understand the valid deployment area.

Player actions:

1. Enter the deployment screen.
2. Place or confirm at least one carried battle group inside a valid deployment zone.
3. Leave any non-deployed carried battle groups in reserve.
4. Inspect enemy deployment if visible.

Required system response:

- Valid player deployment area is visible.
- Invalid placement is rejected with player-facing feedback.
- Each deployed hero and corps remains recognizable after placement.
- Carried but undeployed battle groups remain out of the current battle runtime, do not take battle casualties, and are not available as mid-battle reinforcements in this slice.

Acceptance:

- The player cannot start battle until at least one carried battle group is deployed.
- The player can intentionally leave one or more carried battle groups undeployed as reserves.
- The deployment step remains short enough for first-slice play and does not become full army setup.

### VS-08 Command A Destination Beacon

Player intent: redirect one or more battle groups during real-time battle without individual soldier micro.

Player actions:

1. Start battle after deploying at least one battle group.
2. Select one deployed battle group during battle.
3. Right-click a reachable destination cell.
4. Optionally select multiple deployed battle groups and right-click one shared destination cell.
5. Optionally press space to pause, change the selected group or groups, then right-click a new destination while paused.

Required system response:

- Battle starts with deployed player battle groups in default attack posture; no objective-region or engagement-rule choice is required before launch.
- A valid right-click destination creates or moves a visible destination beacon.
- Multi-selected battle groups share the same destination beacon and update only their own command scope.
- Non-selected battle groups continue their existing command, local-combat, fallback, or autonomous behavior.
- Commands may be issued while battle is running or while tactical pause is active; pause-time commands update intent but do not advance battle simulation until unpaused.
- Invalid or unreachable destinations are rejected with player-facing feedback and do not change the previous destination.
- The beacon command affects movement behavior or battle report explanation.

Acceptance:

- Battle can start after at least one carried battle group is deployed; no target-area or unit-state selection blocks launch.
- The player can redirect a selected battle group by right-clicking a reachable destination.
- The player can select multiple battle groups and assign one shared destination beacon.
- A player can see that only the selected battle group or groups react to the new beacon.

### VS-09 Follow A Readable Real-Time Battle

Player intent: watch the battle and understand who is winning locally.

Player actions:

1. Start battle.
2. Observe movement, contact, damage, defeat, and local collapse or stabilization.
3. Pause or continue watching as needed.

Required system response:

- Units move and fight with readable identity and faction ownership.
- The battle shows enough health, damage, or defeat feedback for the player to read battle flow.
- The battle does not require individual soldier micro.
- If the battle stalls, blocks, or rejects movement, the player-facing state must not look like silent idling.

Acceptance:

- A player can tell when the battle group is advancing, engaged, losing strength, or winning.
- The battle reaches a clear outcome without debug-only intervention.

### VS-10 Use One Live Regroup Command

Player intent: intervene when the battle group is scattered, overextended, or locally pressured.

Player actions:

1. During battle, select the battle group.
2. Trigger regroup.
3. Continue or resume battle.

Required system response:

- Regroup is available as a live battle command for the selected battle group.
- Regroup changes the battle group behavior during the current battle.
- The command does not require individual soldier selection.
- The command event is visible in battle feedback or the final report.

Acceptance:

- The player can issue regroup after battle starts.
- Regroup can change positioning, target priority, or local survival in a visible way.
- If regroup is unavailable, the UI explains why.

### VS-11 Use One Hero Active Skill

Player intent: create a hero-led breakthrough moment.

Player actions:

1. Select the battle group during battle.
2. Trigger the hero's first active skill.
3. Observe its effect.

Required system response:

- Each selectable hero has exactly one first-slice active skill.
- The selected hero exposes exactly one active skill during battle.
- The skill has a clear name, availability state, and player-facing effect.
- The skill affects battle flow through space, timing, survivability, control, or target pressure.
- The skill use is recorded in the battle report.

Required first-slice skill identity:

```text
Skill role: stabilize or break one local fight
Targeting: simple enough for one-battle-group battle
Use count: limited enough that timing matters
Report: records whether the skill was used and what it affected
```

Acceptance:

- The player can intentionally use the skill during battle.
- The skill is not just a hidden passive stat change.
- The player can understand whether the skill is ready, unavailable, or already used.

### VS-12 Resolve Victory

Player intent: win the assault and see the world change.

Player actions:

1. Win the battle.
2. Return to the strategic world or post-battle state.
3. Inspect Bonefield or the stronghold.

Required system response:

- Bonefield changes ownership, state, or availability after victory.
- The player receives a clear reward or unlock.
- The battle group survives or records losses according to the battle result.
- The report summarizes the meaningful causes of victory.

Acceptance:

- Victory is not only a popup; it changes the strategic world.
- The player has a reason to continue after victory, even if later content is outside this slice.

### VS-13 Resolve Defeat Or Heavy Loss

Player intent: understand failure without the game becoming incomprehensible.

Player actions:

1. Lose the battle or suffer severe losses.
2. Return to the strategic layer or result screen.
3. Read what went wrong.

Required system response:

- The battle reports defeat or heavy loss clearly.
- The player learns at least one actionable reason, such as:
  - poor destination timing or beacon placement;
  - frontline collapsed;
  - regroup was too late or unused;
  - hero skill was unused or mistimed;
  - corps was overextended.
- The player can restart or recover through the slice flow without editor intervention.

Acceptance:

- Failure is explained as a gameplay consequence, not as a system error.
- The player knows what to try differently.

### VS-14 Show Minimal Hero Feedback

Player intent: feel that the hero is a character.

Player actions:

1. Complete battle with victory, defeat, or heavy loss.
2. Read post-battle feedback.

Required system response:

- The hero has a named response line or result reaction.
- The reaction can be simple, but it must reflect the outcome for the selected hero.
- The reaction must not require a full relationship system.

Acceptance:

- The hero feels like a named participant in the slice.
- The response is not generic system narration only.

### VS-15 Show Minimal Progression Feedback

Player intent: feel that the run moved forward.

Player actions:

1. Complete the battle.
2. Review reward, loss, or unlock.

Required system response:

- The slice shows at least one progress marker after battle:
  - new location control;
  - resource gain;
  - facility unlock;
  - hero or corps readiness change;
  - next objective preview.
- The marker is visible in player-facing UI or result text.

Acceptance:

- The player can answer: "What did I gain or lose?"
- The slice does not end with only raw runtime debug output.

### VS-16 Show A Minimal Equipment Sample

Player intent: understand that hero equipment is part of build identity and reward feedback.

Player actions:

1. Inspect the selected battle group before battle or read the post-battle result.
2. See at least one named equipment sample tied to the hero, reward, unlock, or battle report.
3. Complete the battle and review whether equipment changed, contributed, or was unlocked.

Required system response:

- The slice includes three named hero equipment samples covering weapon, armor, and token / command item.
- At least one equipment sample is visible in player-facing UI or result text before the slice ends.
- At least one equipment sample appears as a Bonefield reward, unlock, or report-visible contributor.
- Equipment text explains the item role in simple player language, not raw debug fields.

Acceptance:

- The player can name at least one equipment item and understand whether it was held, gained, unlocked, or contributed.
- The equipment sample does not require full inventory management, random affix farming, or item comparison UI.

## Required Slice Feature Set

The following features are required for the first playable vertical slice:

| Area | Required Feature Cases |
| --- | --- |
| Content breadth | Minimum Slice Content Counts |
| Strategic orientation | VS-01, VS-02 |
| Battle trigger confirmation | VS-03 |
| Battle group | VS-04 |
| Expedition | VS-05 |
| Battle entry | VS-06 |
| Deployment and live destination command | VS-07, VS-08 |
| Live battle readability | VS-09 |
| Player intervention | VS-10, VS-11 |
| Result and continuation | VS-12, VS-13, VS-14, VS-15 |
| Equipment sample | VS-16 |

Implementation proposals may split these cases across several work items, but a public playable slice is not accepted until every required case is satisfied or explicitly replaced by an accepted gameplay proposal.

## Slice Non-Goals

The first playable vertical slice does not require:

- full city construction;
- full economy simulation;
- full hero relationship system;
- full skill trees beyond one active skill per selectable hero;
- full equipment inventory beyond the required sample set;
- procedural world generation;
- save/load;
- broad map content beyond the minimum first route and supporting location;
- final balance or final tutorial copy.

The slice must still leave clean expansion paths for these systems.

## Acceptance Checklist

Before the first playable vertical slice is considered product-complete:

- A new player can start from the main scene and reach the slice without debug commands.
- The implemented content satisfies the minimum slice content counts.
- The player selects at least one named battle group for the expedition.
- The expedition travels from stronghold to Bonefield.
- Arrival at Bonefield focuses the battle location and opens a "触发战斗" confirmation.
- The player deploys at least one carried battle group and can start battle without target-area or engagement-rule selection.
- The battle starts and resolves in real time.
- The player can issue a destination beacon command to one or more selected battle groups.
- The player can issue one live regroup command.
- The player can use the selected hero's one active skill.
- The battle result changes the strategic world.
- At least one named equipment sample is visible as held, gained, unlocked, or report-contributing.
- The report explains the outcome using at least command, skill, beacon/destination behavior, and losses.
- The hero gives minimal post-battle feedback.
- Victory and failure both produce understandable next steps.
