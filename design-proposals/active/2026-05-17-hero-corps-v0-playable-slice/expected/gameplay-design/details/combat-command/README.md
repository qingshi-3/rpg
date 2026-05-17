# Combat Command Detail

## Parent Authority

Global rules live in `../../content-systems-long-term-design.md`, especially the combat model and battle report sections.

## Boundary

This detail area defines player-facing battle command rules:

- selecting a hero company;
- separating hero command, corps command, and combined command;
- hero hold/attack/retreat/skill behavior;
- corps advance/guard/hold/attack/retreat behavior;
- command cadence for medium-frequency light RTS;
- command feedback and battle-report implications.

## To Refine

- Post-v0.1 command list beyond battle start.
- Command UI expectations after the first playable slice.
- What commands are allowed while the hero or corps is routed, casting, stunned, retreating, or separated.
- How command state is explained in the battle report.

## V0.1 Playable Slice

The first playable slice proves entry into hero-led battle without requiring the full light RTS command UI.

Required player flow:

1. The player clicks `出征` on the world map.
2. The player selects a hero in a Chinese expedition panel.
3. The selected hero brings a default corps.
4. The player right-clicks an enemy strategic location.
5. The hero company travels on the world map toward the enemy strategic location.
6. After arrival, the player chooses to enter the assault battle.
7. The game enters pre-battle deployment.
8. The player confirms or places the hero company in a valid deployment zone.
9. The player clicks `开战`.
10. Real-time battle starts.

V0.1 does not require post-start hero/corps command controls. Those controls remain the next combat-command iteration after this playable path is usable.

## Non-Goals

- Individual soldier micro-control.
- Large-scale RTS box selection.
- AP/turn-based command flow as the future combat identity.
