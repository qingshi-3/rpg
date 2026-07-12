# Hero-Led Auto Battle V0 Playable Slice

Status: Draft
Workflow Role: Child requirement draft under `REQ-BRIDGE-SPATIAL-BATTLE-V0`; not implementation authority.
Requirement Id: REQ-BRIDGE-HERO-AUTO-BATTLE-V0
Parent Proposal: `design-proposals/archived/2026-05-19-bridge-spatial-battle-v0`
Supersedes: None
Superseded By: None
Amends: `REQ-BRIDGE-SPATIAL-BATTLE-V0`
Amended By: None
Affected Authority Documents:
- `gameplay-design/details/combat-command/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-result-settlement-architecture.md`
Related Implementation Proposals: None yet.

## Purpose

Build one playable battle slice quickly enough for hands-on feedback.

The slice should test the real target experience:

- the player feels that a raised hero enters the fight and matters early;
- battle remains mostly automatic, with low-frequency player intent changes;
- the first loop is rough but playable before building a full skill system;
- hero defeat is a battle-state consequence, not permanent loss anxiety.

This document narrows the current bridge battle proposal away from "terrain strategy first" and toward "RPG hero battlefield expression first".

## Reference Lesson

The target is not to copy Sanguo Qunyingzhuan, but to learn from its strengths and weaknesses.

Worth borrowing:

- the fantasy of a raised hero becoming visibly powerful on the battlefield;
- the satisfaction of a strong hero cutting into troop masses;
- equipment and growth making a hero feel more dangerous over time.

Problems to avoid:

- heroes waiting behind soldiers until the troop phase is mostly over;
- early or mid-game heroes dying too easily when they enter the troop line;
- different heroes converging into one generic stat and shared-skill pool;
- late-game gear being the only reliable path to the "hero cuts through troops" fantasy.

## Core V0 Rules

### Heroes Fight Early

Every player hero in this slice should be willing and able to auto-enter combat.

V0 must not rely on the player protecting a fragile backline hero through precise movement. A mage, archer, support, or warrior may have different skill shapes, ranges, and target preferences, but all should have enough battle durability to participate without constant babysitting.

### Battle-Only Down State

Hero HP reaching zero means the hero is down for the current battle or forced out of the fight.

It does not mean permanent death. The later settlement may apply recovery cost, fatigue, injury, or temporary unavailability, but this V0 only needs the battle to communicate: the hero lost contribution, not the campaign.

### Automatic By Default

The default battle loop is:

```text
start battle
-> hero company auto-advances under its current posture
-> heroes and corps choose targets and use available attacks
-> player occasionally changes intent or triggers a high-impact command
-> battle ends with result and feedback
```

The player should not need frequent precision clicks to keep a hero alive.

### Differentiation Can Wait Until The Loop Works

Hero classes or archetypes should eventually be defined by skill shape, timing, and target choice rather than by "can survive" versus "cannot survive".

The rough first implementation does not need that system. It may use existing attacks, existing ability data, simple tuned stats, and a small number of sample actors. The only requirement is that the hero visibly participates from the start and the player can judge whether automatic hero combat feels worth developing further.

Later examples for archetype behavior:

| Archetype | Battlefield expression | Later skill shape |
|---|---|---|
| Vanguard | enters the line and clears clustered troops | short-range cleave or shockwave |
| Caster | fights from mid range and punishes clusters | circular or line area burst |
| Raider | reaches priority targets faster through alternate routes | short dash, pierce, or target-switch pressure |
| Commander | makes nearby corps more stable or aggressive | aura, guard pulse, or rally burst |

Do not block the first playable loop on these skill shapes.

### Corps Support Hero Expression

Corps are not a disposable phase before the hero starts playing.

In this slice, corps should:

- hold contact so the hero can attack into a readable front;
- keep enemy soldiers from instantly surrounding one hero;
- add pressure that makes the hero's contribution matter;
- make the hero feel stronger or weaker depending on company composition.

The first implementation can keep corps rules simple. The important behavior is that hero and corps fight together from the start.

## V0 Battle Scenario

Working scenario name: Bonefield Bridgehead.

The location is not a full city. It is a small fortified pass, bridgehead, or ruined outpost that can reuse the existing Bonefield site path while acting as the first battle validation map.

The scenario should contain three readable combat areas:

| Area | Purpose | Mechanic Tested |
|---|---|---|
| Central bridge or gate | fast main contact and troop clustering | occupancy and chokepoint pressure |
| Upper pressure point | gives ranged or caster-style actors a different contact pattern | range and target access |
| Lower flank access | lets a raider or aggressive posture reach priority targets sooner | path cost, target contact, route choice |

These are not abstract "strategic routes". In the rough first loop, each route only needs to create a different target contact pattern. Skill payoff can come later.

## Required Mechanics For First Playable Version

### 1. Hero Durability Floor

Tune V0 so ordinary enemy troops do not instantly delete a hero who enters the front.

Acceptable first-pass methods:

- increase hero HP or defense relative to soldiers;
- reduce generic soldier damage against heroes;
- use enemy target selection that does not always collapse onto one hero;
- keep soldier count and attack range low enough that a hero can survive visible contact.

Avoid adding a complex survival subsystem unless tuning cannot achieve the feel.

### 2. Battle Down Feedback

When a hero goes down, the player should see a clear battle-state change:

- the hero stops contributing;
- the corps or company becomes less effective;
- the result screen can explain the downed hero as a battle loss.

No permanent-death implication in this V0.

### 3. Posture Commands

Keep player control low frequency.

Minimum useful postures:

| Posture | Expected behavior |
|---|---|
| Assault | auto-advance, pursue nearby valid targets, apply pressure |
| Focus Fire | prefer a selected or priority target when reachable |
| Hold Line | fight in place or near a defend point; do not chase deep |

Retreat can be added if cheap, but it is not required before the first feel test unless hero down/recovery needs it.

### 3.5 Runtime Command Inspection UI

The first runtime command UI slice is presentation-only. It exists to test selection, readability, and the Sanguo Qunying-style command panel shape before wiring real-time command execution.

Required behavior:

- after battle starts, the bottom command host remains visible instead of disappearing;
- pressing `Space` pauses battle presentation playback while keeping UI input active;
- while paused, the bottom command host shows participating hero companies;
- selecting a hero company highlights the hero and attached corps members together;
- the left panel shows the selected hero company with separate hero command, corps command, and combined command sections;
- draft command buttons may show feedback or logs, but they must not change runtime movement, damage, outcome, settlement, or battle report truth in this slice.

This pause is not a `SceneTree.Paused` global pause. It is a presentation playback wait so the UI remains clickable and the Runtime/Application authority boundary does not change.

### 4. No New Skill System In Rough V0

The first playable version should not build a new skill system.

Allowed rough implementation:

- use existing basic attacks or existing ability definitions;
- tune HP, defense, damage, range, unit count, cooldown, and target pressure;
- define one sample hero company and one enemy setup through the narrowest existing content path;
- keep slice-specific tuned values small and visible so they can later move into definitions without changing runtime ownership.

Rough does not mean a second combat model, hidden outcome bypass, or fake victory logic. Movement, damage, target choice, defeat, and settlement must still use the existing authoritative runtime path.

### 5. Map-Combat Interaction

The map is valid only if it changes combat contact enough to be felt during play.

Examples:

- central bridge creates a readable first clash and can slow or cluster units through occupancy;
- upper pressure point lets a ranged/caster-style actor affect a different target set;
- lower flank access changes which enemy becomes reachable first.

If the same hero performs almost identically on every route, the map is not yet doing its job.

### 6. Semantic Attribution

Use semantic markers for authoring and later explanation:

- `DeploymentZone` for player/enemy start areas;
- `Lane` for main, upper, and lower route labels;
- `ChokePoint` for the central bridge or gate;
- `RangedPoint` for the upper pressure point if it receives any rule or report attribution;
- `FlankRoute` for the lower access path;
- `DefendPoint` for a hold-line goal.

Markers are not hidden gameplay rules. Movement legality, damage, and target rules remain owned by the grid, occupancy, abilities, and runtime facts.

## What To Build First

Build one battle that can be replayed several times with different hero or command choices.

Recommended first implementation order:

1. Author the Bonefield Bridgehead map shape with deployment zones and three combat areas.
2. Ensure heroes and corps auto-enter the fight from battle start.
3. Tune hero durability so the hero can fight early without precise player protection.
4. Tune existing basic attacks, HP, damage, cooldowns, ranges, unit counts, and enemy pressure until the fight can be played repeatedly.
5. Add simple posture switching only if the battle is hard to evaluate without it.
6. Add enough battle feedback to tell whether the hero carried, went down, or failed to affect the fight.

## First Playtest Questions

After the first implementation, evaluate through direct play instead of more speculation.

Ask these questions after each run:

- Did the hero enter meaningful combat within the first 10-20 seconds?
- Did the player feel afraid to let the hero fight?
- Did the hero visibly contribute before any custom skill system exists?
- Did corps and hero fight together, or did soldiers play first and hero clean up later?
- Did changing posture or route change target contact or battle outcome?
- Did the player need frequent precision orders to avoid a bad result?
- Did the map amplify the hero, or did it feel like a decorative obstacle course?
- If the hero went down, did it feel like a readable battle loss rather than campaign punishment?

## Non-Goals

- No full city or siege system.
- No permanent hero death.
- No high-frequency individual soldier micro.
- No full hero roster or final class system.
- No new skill system in the rough first implementation.
- No functional full hero/corps/combined runtime command execution. A presentation-only command inspection UI is allowed for selection and feedback testing.
- No large shared skill pool design.
- No complex cover, stealth, or line-of-sight tactics unless the runtime actually consumes them.
- No broad LimboAI redesign inside this slice.

## Acceptance Criteria

The first playable version is acceptable when:

- a hero company fights from the start without waiting for soldiers to finish the battle;
- the hero can survive ordinary early contact long enough to show battle identity;
- the first playable loop works through existing attacks, movement, target choice, and tuning before any new skill system;
- one map feature changes target contact or pressure enough to be felt;
- the player can influence battle through low-frequency intent, not precision babysitting;
- the player can pause presentation, select a hero company, see the hero/corps highlighted, and inspect separated hero/corps/combined command sections without changing battle truth;
- hero down is communicated as a battle consequence, not permanent loss;
- the slice is good enough for hands-on feedback even if balance and visuals are rough.
