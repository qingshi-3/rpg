# Heroes And Corps Detail

## Parent Authority

Global rules live in `../../content-systems-long-term-design.md`, especially the hero company, corps presentation, attributes, corps definition, and aptitude sections.

## Boundary

This detail area defines the player-facing unit model:

- hero company composition;
- one hero plus one main corps;
- visible soldiers and shared corps strength;
- hero attributes and combat stats;
- corps class, form, tags, morale, strength, and soldier presentation;
- hero-corps aptitude limits.

## To Refine

- Post-v0.1 hero stat display.
- Corps strength thresholds for visible soldiers.
- Hero survival, corps routing, retreat, and recovery outcomes.
- How fantasy forms differ without breaking the combat-class model.

## V0.1 Playable Slice

The first playable slice uses one selected hero with one default corps.

Required first playable roster:

| Role | Resource | Display Name |
| --- | --- | --- |
| Player hero | `assets/battle/units/f1_将军/unit.tres` | `阿吉昂高鬃` |
| Player corps soldier | `assets/battle/units/f1_近战兵/unit.tres` | `风刃学徒` |
| Enemy leader | `assets/battle/units/首领_城域守卫/unit.tres` | `凯罗塔` |

Visible combatants in this slice must use authored unit resources under `assets/battle/units/`. Geometric placeholders are not acceptable for hero, corps, or enemy units in the player-facing prototype.

The expedition panel may show default corps information as read-only. Troop configuration is deferred to a later city or strategic-location configuration flow.

## Non-Goals

- Multiple main corps under one hero.
- Individual soldier long-term progression.
- Aptitude affecting command response, morale loss, formation stability, or recovery cost.
