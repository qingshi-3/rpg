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
-> make one meaningful preparation choice
-> choose one or more named hero companies
-> travel to an enemy strategic location
-> read battle context
-> deploy and plan at least one participating company
-> intervene during battle
-> resolve the outcome
-> see world, hero, and reward feedback
```

The slice is successful only if all three product pillars are visible:

| Pillar | Slice Promise |
| --- | --- |
| Faction building | The player starts from a controlled stronghold, prepares before battle, and changes the world by winning or losing. |
| Hero attachment | The player commands a named hero company with a clear role and minimal character feedback. |
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
| Fully managed city / stronghold | 1 | The player stronghold must show ownership, garrison, resources, and the preparation action. |
| Direct battle / reward target | 1 | Bonefield must be reachable from the world map and resolve through the authored assault battle. |
| Player selectable heroes | 3 | Each hero must have a name, battlefield role, default corps, one active skill, and minimal post-battle reaction text. |
| Carried player hero companies per Bonefield expedition | 1-3 | The player may send one strategic expedition army carrying multiple selected hero companies. |
| Deployed player hero companies per battle | 1-3 | Battle preparation may deploy any carried company subset, but at least one company must be deployed before launch. |
| Enemy named leaders | 1 | Bonefield must have one named enemy leader or boss-like commander. |
| Player-cast hero active skills | 3 | One active skill per selectable hero. Only the selected hero's skill is available in the battle. |
| Player corps classes | 3 | The first slice uses shield, archer, and cavalry as the selectable hero companies' default corps classes. |
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
- The UI gives one clear next goal: prepare and send a hero company to assault Bonefield.

Acceptance:

- A first-time player can identify the player's base, the enemy target, and the next action without reading development documentation.
- Unsupported strategic locations do not look equally actionable as the slice target.

### VS-02 Inspect The Starting Stronghold

Player intent: feel that the player owns a place, not just a battle launcher.

Player actions:

1. Select the starting stronghold.
2. Open its available actions.
3. Review available garrison, resources, and preparation options.

Required system response:

- The stronghold shows its ownership, garrison, and basic resource state.
- The available hero companies are shown as named forces, not anonymous unit counts.
- The stronghold exposes the slice preparation action from VS-03.

Acceptance:

- The player can explain what they control before sending an expedition.
- The stronghold screen does not require full city management, but it must communicate ownership, available heroes, available corps, and preparation.

### VS-03 Make One Battle Preparation Choice

Player intent: make a pre-battle strategic choice that matters.

Player actions:

1. Choose exactly one preparation before launching the first expedition.
2. Confirm the choice.
3. Proceed to expedition selection.

Required system response:

- The preparation choice is mutually exclusive for this battle.
- The chosen preparation is visible in later pre-battle context and in the battle report.
- The chosen preparation has a visible gameplay effect in at least one of these places:
  - battle start condition;
  - battle intervention availability;
  - battle result or losses;
  - post-battle reward.

Required first-slice preparation options:

| Option | Player Meaning | Required Feedback |
| --- | --- | --- |
| Scout Bonefield | Learn the enemy plan before battle. | Pre-battle text reveals enemy leader role and recommended objective or rule. |
| Drill The Corps | Improve the company's readiness for the assault. | Battle or report shows reduced collapse risk, stronger opening formation, or lower corps loss. |
| Prepare Field Support | Save one tactical support option for battle. | Battle HUD or report shows the support was available or used. |

Acceptance:

- The player can choose one of the three options before the first expedition.
- The selected option affects the run in a way the player can see without reading logs.
- Non-selected options do not all silently apply.

### VS-04 Select Named Hero Companies

Player intent: choose a hero-led expedition force, not raw troops.

Player actions:

1. Start an expedition from the stronghold.
2. Select one or more available hero companies.
3. Confirm the default corps attached to each selected hero.

Required system response:

- The expedition panel shows the hero name, hero role, default corps name, and basic tactical identity.
- Each selectable hero has a distinct battlefield role and default corps class.
- Each selected hero's default corps is attached automatically for the first slice.
- The player cannot accidentally launch an empty or non-hero expedition in this slice.
- The selected companies travel as one strategic expedition army.

Acceptance:

- Each selected company can be described as "hero + corps".
- The player understands each selected hero's battlefield role before choosing the target.
- The slice offers three selectable hero companies, and the expedition can carry any available subset from one to three companies.

### VS-05 Send The Expedition To Bonefield

Player intent: move the selected hero companies through the world toward a hostile objective.

Player actions:

1. Choose Bonefield as the expedition target.
2. Watch the expedition army travel.
3. Arrive at the target.

Required system response:

- The selected companies leave the stronghold as one visible strategic expedition army.
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
- It shows the selected preparation from VS-03 if one was chosen.
- It communicates the main objective: win the assault and take or secure the location.

Acceptance:

- The player understands the battle goal before seeing the deployment screen.
- Preparation information does not disappear between strategic choice and battle entry.

### VS-07 Deploy Participating Hero Companies

Player intent: choose which carried companies participate in this battle, place them, and understand the valid deployment area.

Player actions:

1. Enter the deployment screen.
2. Place or confirm at least one carried hero company inside a valid deployment zone.
3. Leave any non-deployed carried companies in reserve.
4. Inspect enemy deployment if visible.

Required system response:

- Valid player deployment area is visible.
- Invalid placement is rejected with player-facing feedback.
- Each deployed hero and corps remains recognizable after placement.
- Carried but undeployed companies remain out of the current battle runtime, do not take battle casualties, and are not available as mid-battle reinforcements in this slice.

Acceptance:

- The player cannot start battle until at least one carried hero company is deployed.
- The player can intentionally leave one or more carried companies undeployed as reserves.
- The deployment step remains short enough for first-slice play and does not become full army setup.

### VS-08 Choose A Battle Plan

Player intent: express a tactical intention before the real-time battle starts.

Player actions:

1. Select a deployed hero company.
2. Select one target or objective region for that company.
3. Select one engagement rule.
4. Start battle.

Required system response:

- The objective choice is visible as a named or highlighted region.
- The engagement rule is displayed in player language.
- At minimum, the slice supports these rules:
  - move first;
  - attack first;
  - hold.
- The selected plan affects runtime behavior or battle report explanation.

Acceptance:

- Battle cannot start until every deployed player hero company has a valid objective and engagement rule.
- A player can see a difference between at least two supported engagement rules during play, in the report, or both.

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

- A player can tell when the company is advancing, engaged, losing strength, or winning.
- The battle reaches a clear outcome without debug-only intervention.

### VS-10 Use One Live Regroup Command

Player intent: intervene when the company is scattered, overextended, or locally pressured.

Player actions:

1. During battle, select the hero company.
2. Trigger regroup.
3. Continue or resume battle.

Required system response:

- Regroup is available as a live battle command for the selected hero company.
- Regroup changes the company behavior during the current battle.
- The command does not require individual soldier selection.
- The command event is visible in battle feedback or the final report.

Acceptance:

- The player can issue regroup after battle starts.
- Regroup can change positioning, target priority, or local survival in a visible way.
- If regroup is unavailable, the UI explains why.

### VS-11 Use One Hero Active Skill

Player intent: create a hero-led breakthrough moment.

Player actions:

1. Select the hero company during battle.
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
Targeting: simple enough for one-company battle
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
- The hero company survives or records losses according to the battle result.
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
  - poor objective choice;
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
- The reaction can be simple, but it must reflect outcome or preparation for the selected hero.
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

1. Inspect the selected hero company before battle or read the post-battle result.
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
| Preparation | VS-03 |
| Hero company | VS-04 |
| Expedition | VS-05 |
| Battle entry | VS-06 |
| Deployment and plan | VS-07, VS-08 |
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
- The player makes one strategic preparation choice before the expedition.
- The player selects at least one named hero company for the expedition.
- The expedition travels from stronghold to Bonefield.
- The player deploys at least one carried hero company and chooses battle plans for deployed companies.
- The battle starts and resolves in real time.
- The player can issue one live regroup command.
- The player can use the selected hero's one active skill.
- The battle result changes the strategic world.
- At least one named equipment sample is visible as held, gained, unlocked, or report-contributing.
- The report explains the outcome using at least preparation, plan, command, skill, and losses.
- The hero gives minimal post-battle feedback.
- Victory and failure both produce understandable next steps.
