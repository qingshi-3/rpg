# Product Slice

## Goal

Deliver a v0.1 playable prototype that proves the main game loop can move from strategic world interaction into hero-led battle preparation and battle start.

## Player Flow

1. The player is on the existing world map.
2. The player clicks `出征`.
3. A hero expedition panel opens.
4. The player selects a hero and confirms.
5. The world enters expedition targeting mode.
6. The player right-clicks a destination.
7. If the destination is player-owned, the army moves to reinforce it.
8. If the destination is enemy-owned, the army moves to attack it.
9. After the assault army reaches the enemy strategic location, the player chooses to enter the assault battle.
10. The game opens a pre-battle deployment view.
11. The player places or confirms the hero company in the allowed deployment zone.
12. The player clicks `开战`.
13. The battle begins in real time.

## V0 Roster

| Role | Resource | Display Name |
| --- | --- | --- |
| Player hero | `assets/battle/units/f1_将军/unit.tres` | `阿吉昂高鬃` |
| Player corps soldier | `assets/battle/units/f1_近战兵/unit.tres` | `风刃学徒` |
| Enemy leader | `assets/battle/units/首领_城域守卫/unit.tres` | `凯罗塔` |

Additional existing resources may be used if implementation needs a ranged or guard variant, but no first-slice combatant may use a geometric placeholder.

## Expedition Panel

- Player-visible text is Chinese.
- V0.1 requires hero selection only.
- The panel may show default corps information as read-only.
- The panel should be structured so later iterations can add troop configuration without replacing the flow.

## Deployment

- The player must see where their side can deploy.
- V0.1 can allow either explicit placement or a clear confirmable default placement.
- Enemy placement may be fixed.
- Deployment must show authored unit visuals.

## Battle Start

- `开战` transitions from preparation into live battle.
- V0.1 does not require manual light RTS commands after battle start.
- The battle result/report may remain minimal, but it must reflect the selected hero, default corps, enemy target, and outcome facts when those are available.

## Acceptance Criteria

- A player can complete the full flow without console-only steps.
- At least one player hero, one player corps visual, and one enemy visual are visible from existing assets.
- No visible combatant is represented by a generic shape.
- The prototype is simple but testable and can be iterated into command controls later.
