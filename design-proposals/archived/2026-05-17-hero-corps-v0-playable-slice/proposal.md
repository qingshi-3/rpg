# Hero Corps V0 Playable Slice Proposal

## Status

Archived

Accepted, implemented, verified, merged, and approved for archive by user on 2026-05-17.

## Background

The first architecture refactor phase and phase-two entry probe are in place. The project can now start business-facing prototype work, but the exact first playable product slice is not yet recorded in accepted gameplay authority.

This proposal defines the smallest playable path that proves the hero-led light RTS direction:

```text
world map -> expedition hero selection -> right-click destination
-> world travel -> arrived assault choice -> pre-battle deployment
-> start battle -> real-time battle playback
```

## Current Design

- `gameplay-design/details/combat-command/README.md` defines hero-led light RTS boundaries, but leaves the first playable command list and command UI expectations unresolved.
- `gameplay-design/details/heroes-and-corps/README.md` defines one hero plus one main corps, but leaves first-phase presentation and visible corps thresholds unresolved.
- `assets/battle/units/` already contains authored unit definitions and animated visuals, but no v0 prototype roster is locked as the required source of truth.
- The architecture refactor has a battle group probe beside the legacy handoff. It does not yet define the player-facing expedition, deployment, or start-battle experience.

## Expected Design

- V0.1 starts from the existing world map and adds a player-facing expedition flow.
- Clicking `出征` opens a hero selection panel. V0.1 only requires selecting a hero; troop configuration remains out of scope and the selected hero brings a default corps.
- Right-clicking an enemy strategic location while an expedition is active issues an assault order to that enemy strategic location. It must not immediately skip world travel.
- The pre-battle deployment view opens after the assault army reaches the enemy strategic location and the player chooses to enter the assault battle.
- The deployment view shows valid player deployment zones and uses the existing unit visual assets for the hero, corps, and enemy.
- Clicking `开战` starts the battle. V0.1 may run the battle with existing automatic runtime behavior; light RTS command controls are explicitly deferred.
- No geometric placeholder shapes may be used for units in the prototype. If a unit is visible as a combatant, it must be backed by an authored `BattleUnitDefinition` and `Visual` from `assets/battle/units/`.

## Affected Authority Copies

- `expected/gameplay-design/details/combat-command/README.md`
- `expected/gameplay-design/details/heroes-and-corps/README.md`

## Implementation Scope After Acceptance

- Add or wire expedition hero selection UI from the world map.
- Define a minimal v0 roster using existing unit resources.
- Carry selected hero and default corps into the battle start path.
- Add pre-battle deployment with visible deployment zones and `开战` after the assault army arrives.
- Preserve architecture direction by routing new work toward the battle group vertical flow where practical, without expanding light RTS command UI yet.
- Add focused regression coverage for selection, handoff/deployment facts, and start-battle behavior.

## Non-Goals

- Full light RTS command UI.
- Troop composition editing in the expedition panel.
- Individual soldier micro-control.
- New generated or hand-drawn placeholder unit visuals.
- Full campaign, economy, relationship, or city-management breadth.
- Replacing every legacy battle path in this slice unless required for the playable path.
