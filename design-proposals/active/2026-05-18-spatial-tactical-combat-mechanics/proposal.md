# Spatial Tactical Combat Mechanics Proposal

Status: Proposed

## Purpose

Record the next accepted combat product direction before implementing more combat features. The combat layer should become a small-scale semi-automatic realtime tactical war game built around spatial battlefield decisions.

This proposal is product-facing. It does not implement the semantic marker system, command UI, AI templates, or new skills directly. Those implementation scopes should get their own proposal or plan after this direction is accepted.

## Current Design

The accepted direction already rejects old manual tactical chess, AP growth, and pure post-deployment autobattler playback. The current authority defines hero-led light RTS combat, square-grid anchored realtime movement, actor-target attacks, ability spatial extension points, and unit footprints.

What is still underdefined:

- the product-level combat identity beyond "hero-led light RTS";
- why maps, chokepoints, reserves, and time matter;
- how far player command should go without becoming high-APM micro;
- how heroes, corps, skills, AI intent, and battle reports should be judged against the same combat goal;
- the first strategy scenario used to validate whether spatial tactics actually work.

## Expected Design

Combat identity becomes:

```text
low-frequency spatial realtime tactical battle
```

The player is a battlefield commander. The main decisions are committing forces, holding or contesting terrain, timing reserves, responding to telegraphed enemy actions, and using a small number of high-impact skills. The player should not control individual soldiers or win by APM.

Battle should be evaluated by whether it naturally creates spatial decisions:

- hold a bridge, gate, pass, or lane;
- delay for reinforcements, cooldowns, flanking, or another-front breakthrough;
- choose when to commit reserves;
- shift forces between entrances;
- protect ranged pressure and casting windows;
- interrupt or answer telegraphed enemy actions;
- retreat before a local collapse becomes unrecoverable.

## Scope Boundaries

Long-term hero capacity may allow one hero to manage several corps slots, such as a `4-6` upper limit. The first implementation slice may still use one hero plus one main corps because that is the current battle-group test shape. This is a staged implementation limit, not a contradiction of the long-term capacity rule.

The first validation slice should be one strategy scene, preferably a bridge assault/defense. It only needs enough content to test chokepoint value, reserve timing, telegraphed intent, and one or two spatial skills.

## Mechanics Direction

Maps are the first combat asset. Terrain and authored spatial structure must change tactics, not merely decorate the background. Relevant structures include bridges, gates, mountain passes, forests, high ground, alleys, water routes, and multiple entrances.

Corps should have simple tactical identities. A corps type should be readable through its battlefield responsibility rather than many active skills:

- spear: chokepoint and anti-charge pressure;
- shield: frontline stability and holding;
- archer: ranged pressure and area denial;
- cavalry: flanking, pursuit, and interruption;
- mage: battlefield area changes and timing pressure.

Hero skills should change space, time, or battle state rather than acting as plain damage buttons. Useful categories include terrain control, area suppression, formation disruption, tempo control, and frontline stabilization.

Enemy AI should be stable and readable before it is "smart." Strong enemy actions must telegraph intent so the player can respond. Reports should explain outcomes through terrain, timing, reserves, command, skills, and local collapse reasons.

## Dependency On Semantic Map Markers

The next implementation should introduce a reusable semantic map marker system. Battle maps and strategic-location interiors should both be able to define rectangular grid regions from a visible editor-placed anchor, extending right and down by `m*n` cells.

This proposal only states that semantic markers are required for future spatial tactics. The marker data model, Godot authoring workflow, runtime extraction, validation, and tests belong to the semantic marker proposal/implementation.

## Non-Goals

- No high-frequency single-soldier micro.
- No pure hands-off autobattler combat identity.
- No return to AP/turn-based tactical chess.
- No broad first-slice content expansion across many maps, heroes, skills, and AI templates.
- No complete semantic marker implementation inside this proposal.

## Acceptance Criteria

- Authority documents can describe combat as low-frequency spatial realtime tactical battle without contradicting hero-led light RTS architecture.
- Future combat work can be judged by spatial decision quality, not only by damage, animation, or unit count.
- Long-term multi-corps hero capacity and current one-main-corps implementation are explicitly staged rather than contradictory.
- The first validation scenario is scoped to one strategy scene before expanding content breadth.
- Semantic map markers are identified as the next implementation dependency, but kept out of this product proposal's direct implementation scope.
