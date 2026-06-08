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

## First Playable Slice

The first playable slice offers three selectable hero companies. The player deploys exactly one company into the Bonefield assault, and the selected hero brings the company's default corps automatically.

Required player roster:

| Company Role | Hero Display Name | Hero Resource | Default Corps Display Name | Corps Resource |
| --- | --- | --- | --- | --- |
| Shield line | `曦盾执旗者` | `assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres` | `天蓝石狮卫` | `assets/battle/units/莱昂纳王国/f1_天蓝石狮/unit.tres` |
| Archer line | `逐日号令官` | `assets/battle/units/莱昂纳王国/f1_风刃指挥官/unit.tres` | `穿阳弓手` | `assets/battle/units/莱昂纳王国/f1_后排弓手/unit.tres` |
| Assault line | `裂光剑卫` | `assets/battle/units/莱昂纳王国/f1_Elyx风暴刃/unit.tres` | `辉光龙骑` | `assets/battle/units/莱昂纳王国/f1_辉光龙骑兵/unit.tres` |

Required Bonefield roster:

| Role | Display Name | Resource |
| --- | --- | --- |
| Enemy leader | `寒骨冢主` | `assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres` |
| Fast harassment enemy | `霜魂猎犬` | `assets/battle/units/霜原部盟/f6_灵魂狼/unit.tres` |
| Ranged pressure enemy | `颅火先知` | `assets/battle/units/深渊军团/f4_Skull施法者/unit.tres` |

Visible combatants in this slice must use authored unit resources under `assets/battle/units/`. Geometric placeholders are not acceptable for hero, corps, or enemy units in the player-facing prototype.

Each selectable hero has one first-slice active skill. Only the selected hero's skill is available during that battle; unselected hero skills must not appear as usable commands. If the current runtime lacks a richer primitive such as shield, control, or area damage, the first implementation may downgrade the skill into a simple targeted effect while preserving the player-facing tactical identity.

The expedition panel may show default corps information as read-only. Troop configuration is deferred to a later city or strategic-location configuration flow.

## Non-Goals

- Multiple main corps under one hero.
- Individual soldier long-term progression.
- Aptitude affecting command response, morale loss, formation stability, or recovery cost.
