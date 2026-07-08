# Heroes And Corps Detail

## Parent Authority

Global rules live in `../../content-systems-long-term-design.md`, especially the battle group, corps presentation, attributes, corps definition, and aptitude sections.

## Boundary

This detail area defines the player-facing unit model:

- battle group composition;
- one hero plus one main corps;
- visible soldiers and shared corps strength;
- hero attributes and combat stats;
- corps class, form, tags, morale, strength, and soldier presentation;
- hero-corps aptitude limits.

## Strategic Corps Instances

Strategic management should treat a hero's troops as persistent corps instances created from city muster templates.

A corps instance is not a count of individual soldiers. It is a durable force record with player-facing identity and state:

- corps role and fantasy form;
- current strength and full strength;
- training level or readiness;
- equipment level;
- experience;
- current state such as garrisoned, assigned to hero, expedition, recovering, routed, scattered, or rebuilding;
- home city or current strategic assignment.

The first strategic-management version should keep one hero plus one main corps as the normal battle-group shape. Multi-corps hero capacity, secondary corps, and complex mixed-battle-group editing are later expansions.

## Muster Templates And Recovery

A city can create or rebuild a corps instance only when it has the relevant muster template. Templates come from city identity, facilities, controlled source sites, and accepted future systems. The template is a right to muster, not an inventory of ready troops.

Corps instances should not be permanently deleted by default after a first-version defeat. A corps that reaches zero strength should route, scatter, or enter rebuilding. Recovery requires the relevant city support, resources, and time. If the template source is lost, existing corps can remain but new creation and full recovery are restricted.

## Hero-Corps Assignment

Heroes can lead corps according to aptitude and content tags. Aptitude should create build identity without turning unsuitable pairings into random failure.

For first strategic management:

- suitable heroes can use a corps normally and may unlock skill links or stronger automatic corps performance;
- poor-fit heroes may receive weaker modifiers or lose skill links;
- poor fit must not create random beast-control failure, command instability, or hidden recovery-cost punishment unless a later accepted proposal adds that behavior.

The first-version city recruitment surface is also the hero main-corps reassignment workbench. The player selects a hero, reviews available troop options, and chooses the hero's new main corps there.

When a hero already has a main corps, reassignment is a single Strategic Management replacement command. The previous corps is dissolved back into the city with full refund and no replacement loss. Refunds use the previous corps' current remaining strength, so surviving reserve soldiers and resource value return fully, while battle losses remain losses. The replacement flow must not leave the old corps parked as hidden inventory unless a later accepted design adds a distinct corps inventory workflow.

The first special route is beast corps. Beast corps are shock-assault forces. They should be strongest with beast tamers, hunters, wildland leaders, or similar aptitude tags, and weaker with ordinary commanders. Their downside is high creation and recovery cost plus source/facility dependency.

## Hero And Battle-Group Skill Loadouts

Hero and battle-group skills are assigned through durable loadout or grant entries. A grant points to a stable skill definition and may carry slot, level, source, progression, equipment, or modifier facts. It must not duplicate the full skill definition.

Hero-corps aptitude, equipment, profession mastery, rank, city support, or later content systems may unlock, modify, or disable granted skill links. Those systems should change grants, modifiers, or eligibility facts rather than hardcoding battle-specific skill lists.

When a battle starts, only skills granted to participating battle groups should enter the battle snapshot. Unselected heroes or reserve-only battle groups must not expose usable commands in the live battle UI unless a later accepted reinforcement or reserve-command design changes that rule.

## To Refine

- Post-v0.1 hero stat display.
- Corps strength thresholds for visible soldiers.
- Hero survival, corps routing, retreat, and recovery outcomes.
- How fantasy forms differ without breaking the combat-class model.
- First-version hero aptitude tags for beast corps.
- First-version corps instance states and recovery thresholds.

## First Playable Slice

The first playable slice offers three selectable battle groups. The player deploys exactly one battle group into the Bonefield assault, and the selected hero brings the battle group's default corps automatically.

Required player roster:

| Battle Group Role | Hero Display Name | Hero Resource | Default Corps Display Name | Corps Resource |
| --- | --- | --- | --- | --- |
| Shield line | `曦盾执旗者` | `resource/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres` | `天蓝石狮卫` | `resource/battle/units/莱昂纳王国/f1_天蓝石狮/unit.tres` |
| Archer line | `逐日号令官` | `resource/battle/units/莱昂纳王国/f1_风刃指挥官/unit.tres` | `穿阳弓手` | `resource/battle/units/莱昂纳王国/f1_后排弓手/unit.tres` |
| Assault line | `裂光剑卫` | `resource/battle/units/莱昂纳王国/f1_Elyx风暴刃/unit.tres` | `辉光龙骑` | `resource/battle/units/莱昂纳王国/f1_辉光龙骑兵/unit.tres` |

Required Bonefield roster:

| Role | Display Name | Resource |
| --- | --- | --- |
| Enemy leader | `寒骨冢主` | `resource/battle/units/霜原部盟/f6_Draugar领主/unit.tres` |
| Fast harassment enemy | `霜魂猎犬` | `resource/battle/units/霜原部盟/f6_灵魂狼/unit.tres` |
| Ranged pressure enemy | `颅火先知` | `resource/battle/units/深渊军团/f4_Skull施法者/unit.tres` |

Visible combatants in this slice must use authored unit resources under `resource/battle/units/`. Geometric placeholders are not acceptable for hero, corps, or enemy units in the player-facing prototype.

Each selectable hero has one first-slice active skill. Only the selected hero's skill is available during that battle; unselected hero skills must not appear as usable commands. If the current runtime lacks a richer primitive such as shield, control, or area damage, the first implementation may downgrade the skill into a simple targeted effect while preserving the player-facing tactical identity.

The expedition panel may show default corps information as read-only. Troop configuration is deferred to a later city or strategic-location configuration flow.

## Non-Goals

- Multiple main corps under one hero.
- Individual soldier long-term progression.
- Aptitude affecting command response, morale loss, formation stability, or recovery cost.
- Random beast-control failure in the first strategic-management version.
