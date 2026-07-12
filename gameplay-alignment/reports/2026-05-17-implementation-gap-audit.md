# 2026-05-17 Implementation Gap Audit

## Purpose

This report compares the current first-pass migration implementation with the accepted long-term gameplay direction in `gameplay-design/content-systems-long-term-design.md`.

Follow-up note: `gameplay-alignment/reports/2026-05-17-migration-cleanup-audit.md` and the same-day cleanup pass rerouted the main active legacy `docs/` entries away from auto-tactics product authority. Remaining auto-tactics language should be treated as historical migration context unless a current authority document says otherwise.

The main finding is structural: the completed migration successfully retires the old AP/manual tactical runtime and builds an automated battle-resolution backbone, but the accepted target has since moved to hero-led light RTS inspired by Sanguo Qunying. The current code is therefore a useful foundation, not a playable replacement for the target battle experience.

## Authority Used

Current governance route:

1. `gameplay-design/` supplies accepted player-facing rules.
2. `system-design/` supplies accepted implementation architecture.
3. A user-confirmed discussion result is captured in one active work item for execution scope and evidence; it is not gameplay or architecture authority.
4. Legacy `docs/` material is historical evidence only.

`docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md` and related old docs are useful migration evidence, but they target "automated tactical validation" and must not override the newer hero-led light RTS authority.

## Verification

Commands run:

```text
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Result:

- `AutoBattleRuntimeRegression`: pass.
- `WorldSiteDeploymentCacheRegression`: pass.
- `rpg.sln`: pass, 0 warnings, 0 errors.
- build servers were shut down after verification.

## Current Implementation Snapshot

### What Is Solid

- Strategic world to battle handoff exists through `WorldBattleRequestBuilder`, `BattleStartRequest`, `BattleSessionHandoff`, `BattleResult`, and `WorldBattleResultApplier`.
- `WorldSiteState.UnitPlacements` is now the site-local deployment authority, with deployment cache extraction, target validation, terrain reconciliation, and launch preparation services.
- The old manual/AP battle controller path is deleted or detached. Regression tests explicitly guard against `BattleTurnController`, `ActionPointComponent`, action menus, old command routers, and old HUD scene dependencies.
- `AutoBattleSimulation` can deterministically spawn forces from preferred placements, move them by simple Manhattan pursuit, resolve basic attacks, produce `BattleResult.ForceResults`, and build a minimal report.
- `WorldSiteRoot` defaults to the auto battle branch and applies result writeback back to strategic/site state.

### What This Actually Means

This is currently an immediate auto-resolution path with report summary. It is not yet a real-time battle scene where the player selects a battle group, commands hero and corps separately, casts hero skills, observes corps behavior, and then receives an explanatory report.

Key evidence:

- `WorldSiteRoot` activates battle from `_Ready`, calls `ActivateBattleRuntime`, then `ActivateAutoBattleRuntime`, applies the result, clears entities, disables battle mode, and switches back to non-battle UI.
- `WorldSiteRoot._Input` returns immediately during `WorldSiteRuntimeMode.Battle`, so there is no battle-time player command input.
- `AutoBattleSimulation.RunToEnd` resolves the whole battle in one pure C# pass and does not use Godot scene movement, player commands, terrain pathfinding, facilities, skills, mana, or corps state.
- `AutoBattleRuntimeController` only controls report event reveal speed, pause/resume, and skip. It does not control live combat units.

## Gap Analysis

### GAP-A: Battle Identity Mismatch

Current state:

- The implemented path is automated battle resolution plus event-feed playback.
- Old migration docs still describe battle as TFT-like automated tactical validation with no battle-time commands.

Accepted target:

- Hero-led light RTS.
- Player selects battle groups.
- Hero command, corps command, and combined command are separate.
- Player can leave the hero in place while sending troops forward.

Gap:

- The main implemented battle identity does not match the accepted target. It should be preserved as a backend auto-resolve/report foundation, but not treated as the future playable combat surface.

Priority:

- Critical.

Recommended workstream:

- Discuss and obtain user confirmation for `battle-group-light-rts-combat`, capture the result in one active work item, and update current authority as required before further battle UI/runtime work.

### GAP-B: No Battle Group Runtime Model

Current state:

- Battle forces are `BattleForceRequest` entries with `UnitDefinitionId`, `Count`, `FactionId`, and preferred placements.
- World-side persistent force state is mostly `GarrisonState` with `UnitTypeId`, `Count`, `Morale`, and `DamageLevel`.
- `BattleUnitDefinition` is a generic unit resource with HP/move/attack fields.

Accepted target:

- `battle group = 1 hero + 1 main corps`.
- Hero and corps are distinct runtime actors under one selectable battle group.
- Corps uses shared `CorpsStrength 0-100`; visible soldiers are presentation derived from strength thresholds.

Gap:

- There is no accepted domain or runtime object for hero, corps, battle group, corps strength, hero/corps selection, or shared battle-group ownership.

Priority:

- Critical.

Recommended workstream:

- Define `Hero`, `Corps`, and `BattleGroup` state/definition contracts before adding more battle behavior.

### GAP-C: No Battle-Time Player Command Layer

Current state:

- Battle-time input is disabled in `WorldSiteRoot`.
- Old AP/manual command UI is removed, which is correct.
- There is a small `BattleCorpsCommand` enum and allied intent planner supporting `Assault`, `FocusFire`, and `HoldLine`, but it is not connected to the active runtime or UI.

Accepted target:

- Hero commands: move, attack, hold, retreat, cast skill, move relative to corps.
- Corps commands: advance, return to guard, hold area, attack target/area, protect hero, retreat.
- Combined commands: battle-group move, attack, defend, retreat, regroup.

Gap:

- No command bus, no command state, no command UI, no selection model, and no active runtime owner for hero/corps/combined commands.

Priority:

- Critical.

Recommended workstream:

- Build a minimal non-AP command architecture around battle-group commands. Do not restore old `BattleCommandController` or action-menu UI.

### GAP-D: Combat Stats And Skills Are Too Thin

Current state:

- `BattleUnitDefinition` has `MaxHp`, `MoveRange`, `AttackDamage`, `AttackRange`, water movement, and occupancy flags.
- `AbilityDefinition` has ID/name/icon/range/target rules/effects.
- `AutoBattleSimulationConfig` supplies global health, damage, attack range, and attack cooldown.
- `AutoBattleEventKind` has spawn, target, move, attack, defeat, and end events only.

Accepted target:

- Readable combat stats: HP, Attack, Defense, Speed, Range, Mobility, Mana, ManaRegen, Cooldown.
- Hero skills are player-cast and require cooldown plus mana.
- Corps skills are automatic.
- Battle report must record hero skill use and corps automatic skill performance.

Gap:

- No mana, mana regen, defense, speed, mobility, per-unit cooldown, hero skill casting, corps auto-skill trigger, or skill event reporting.

Priority:

- High.

Recommended workstream:

- Add battle stat contracts only after hero/corps model is accepted, otherwise stat fields will land on the wrong abstraction.

### GAP-E: Corps Growth And Casualties Are Missing

Current state:

- Casualty writeback uses count-based `BattleForceResult` and garrison unit counts.
- `GarrisonState` has `Count`, `Morale`, and `DamageLevel`, but no corps strength or training/equipment progression.

Accepted target:

- Corps level: training raises level cap and provides limited experience; battle provides primary XP.
- Corps equipment level: upgraded through city resources and workshop capacity.
- Corps losses are expressed as `CorpsStrength 0-100`, with visible soldier count derived from thresholds.

Gap:

- Current force loss model can remove units, but cannot express partial corps damage, visible soldiers, routed corps, recovery cost, level XP, level cap, equipment level, or city-supported restoration.

Priority:

- High.

Recommended workstream:

- Introduce corps state and result writeback before tuning battle damage.

### GAP-F: Profession, Tags, Bonds, And Aptitude Are Not Implemented

Current state:

- Content has many unit resources and visual assets, but unit definitions do not expose `CombatClass`, `Form`, or gameplay `Tags`.
- No bond-resolution layer exists.
- No hero-corps aptitude model exists.

Accepted target:

- Corps definition = combat class + fantasy form + tags.
- Bonds are profession/tag interactions that change behavior and tactics.
- Aptitude only affects corps auto-skill efficiency, skill links, and corps stat modifiers.

Gap:

- There is no structured system to classify "cavalry but dragon" or "shield but crab", and no place to apply bonds or aptitude safely.

Priority:

- High.

Recommended workstream:

- Add content definitions and one first-phase bond only after the hero/corps contract exists.

### GAP-G: Equipment And Progression Are Absent

Current state:

- No hero equipment slots.
- No equipment grade/series/pool.
- No corps equipment level.
- City resources do not feed equipment upgrades.

Accepted target:

- Hero equipment slots: weapon, armor, token/command item.
- Deep equipment pool with grades from common to artifact/unique.
- Corps equipment level uses city resources and workshop capacity.

Gap:

- The collection/progression loop that should connect city resources, hero build, corps build, and combat is not represented.

Priority:

- High.

Recommended workstream:

- Defer full equipment breadth, but define minimum contracts for one weapon, one armor, one command item, and one corps equipment upgrade.

### GAP-H: City Attribute Model Does Not Match The Target

Current state:

- Strategic resources are currently `population`, `economy`, and `stone`.
- City/site state includes control, local resources, facilities, garrison, placements, damage, intel/memory, pending threats, and active tags.
- Facilities and tower counts produce some defense behavior, but not through explicit `DefenseValue`, `TrainingCapacity`, or `WorkshopCapacity`.

Accepted target first-phase city attributes:

- Control
- Population
- Food
- Money
- BuildingMaterials
- SpecialResources
- GarrisonCapacity
- TrainingCapacity
- WorkshopCapacity
- DefenseValue
- FacilitySlots

Gap:

- Current implementation has useful site mechanics, but the resource and capacity names/contracts are not aligned with the accepted model.
- `DamageLevel` and intel/memory exist as implementation support, but they should not be promoted as first-phase core city attributes unless a confirmed discussion updates current authority.

Priority:

- High.

Recommended workstream:

- Discuss and confirm city/resource alignment before changing resource IDs, capture the execution scope in one active work item, and update current authority because this touches persistence, UI text, action costs, facilities, and tests.

### GAP-I: Terrain, Facility, Objective, And Modifier Rules Do Not Affect Battle Simulation

Current state:

- `BattleStartRequest` carries `SiteStateSnapshot`, known tactical tags, entrances, and `BattleModifiers`.
- Deployment respects terrain and water before battle.
- `AutoBattleSimulation` ignores terrain, map pathfinding, site snapshot, objectives, and modifiers while resolving combat.

Accepted target:

- Full authored city map matters during battle.
- Terrain, facilities, local objectives, deployment, and city defense should influence battle results and report.

Gap:

- The current battle uses placement cells as coordinates but does not make the authored map meaningful after battle begins.

Priority:

- High.

Recommended workstream:

- Once the live RTS runtime exists, integrate only one terrain rule and one facility rule first.

### GAP-J: Battle Report Is Too Shallow

Current state:

- Report summarizes outcome, force survival/losses, attack/damage counts, event feed, and one defeat reason (`player_force_eliminated`).

Accepted target:

- Report explains outcome, battle group contribution, corps strength loss, hero skill impact, corps auto-skill performance, profession/tag bonds, equipment contribution, city/facility influence, actionable failure reason, rewards, XP, materials, and city/resource changes.

Gap:

- Current report proves the reporting path works, but cannot yet explain the accepted build-and-command game.

Priority:

- Medium-high.

Recommended workstream:

- Preserve the report builder shape, but expand events only after the runtime emits hero/corps/equipment/facility facts.

### GAP-K: WorldSiteRoot Is Still An Overloaded Scene Root

Current state:

- `WorldSiteRoot.cs` is about 4,883 lines.
- It still binds map/runtime nodes, builds site HUD, handles management input, deployment drag, exploration, battle launch, auto battle activation, result application, placement reconciliation, and many UI projections.
- Some services were extracted, but the root still coordinates too many domains.

Accepted architecture direction:

- Scene roots are composition shells.
- `WorldSiteRoot` should route to focused owners: map runtime, management presenter, deployment controller, exploration controller, battle launcher, battle runtime controller, and report UI.

Gap:

- The migration reduced some duplication, but not enough to make future light-RTS work safe. Adding hero/corps commands directly here would deepen the root-script problem.

Priority:

- High.

Recommended workstream:

- After discussion and user confirmation, update the relevant `system-design/` authority and capture execution in one active work item before adding new runtime owners.

### GAP-L: Historical Docs Need Authority Routing

Current state:

- `gameplay-design/` correctly states hero-led light RTS.
- Follow-up cleanup rerouted the main active legacy docs. Historical migration records still say automated tactical validation and may mention no repeated battle-time commands because they document the first migration.

Accepted target:

- Future work should follow `gameplay-design/` first and treat old `docs/` as reference only.

Gap:

- `AGENTS.md` and `authority-map.md` protect against this, but agents that read historical migration docs in isolation can still be misled.

Priority:

- Medium-high.

Recommended workstream:

- After the replacement combat architecture is confirmed and current authority is updated, capture any documentation cleanup in one active work item and update or quarantine the most misleading old docs. Do not do this before the replacement architecture is clear.

## Updated Completion Estimate Against Current Target

These estimates are relative to the accepted hero-led light RTS and content-system target, not the older auto-tactics migration target.

| Area | Estimate | Reason |
|---|---:|---|
| Documentation governance | 70% | New authority dirs and routing exist; old docs still conflict. |
| Strategic world and WorldSite foundation | 60-70% | Handoff, site state, garrison, threats, facilities, exploration, and writeback exist. |
| Auto-resolve backend/report skeleton | 65-75% | Pure C# auto battle and report path pass regression tests. |
| Hero-led light RTS battle | 10-20% | Old manual loop is gone, but new hero/corps command runtime is not built. |
| Hero/corps/equipment/progression content systems | 10-20% | Character and unit definitions exist, but accepted gameplay contracts are absent. |
| First playable replacement experience | 20-30% | Player can reach site flow and auto resolve, but cannot yet play/read the intended combat loop. |

## Recommended Migration Closure Order

1. Freeze further "auto tactics playback UI" expansion as a product target. Keep it as backend auto-resolve/report infrastructure.
2. Discuss and confirm `battle-group-light-rts-combat`, record the result in one active work item, and update the relevant current authority before execution.
3. Define the minimal domain contracts: `Hero`, `Corps`, `BattleGroup`, `CorpsStrength`, command channels, skill resource state, and result writeback fields.
4. Build the first live battle slice on a `WorldSite` map:
   - one battle group vs one enemy group;
   - select battle group;
   - hero move/hold/attack/retreat;
   - corps advance/return/hold/attack/retreat;
   - one player-cast hero skill with mana and cooldown;
   - corps auto behavior;
   - report events and `BattleResult` writeback.
5. Only after that, align city resources/capacities, corps progression, and equipment, because those systems need the hero/corps contract to attach to.

## Bottom Line

The migration did the hard cleanup work: old AP/manual tactical authority is gone, battle request/result boundaries are alive, and world writeback is test-covered. The next risk is not technical compilation; it is building more UI or simulation on the wrong battle identity. The next confirmed discussion and active work item should re-center implementation on hero-led light RTS, with current authority updated as required, before any larger feature work.
