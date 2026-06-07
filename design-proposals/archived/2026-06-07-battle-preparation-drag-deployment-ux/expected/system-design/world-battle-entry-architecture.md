# World Battle Entry Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports the accepted loop of strategic preparation, direct battle entry, hero-led light RTS runtime, settlement, and campaign writeback.

## Responsibility

World battle entry owns conversion from strategic context into `BattleStartRequest`, pre-battle presentation, scene-transition handoff, deployment preparation, battle-group plan drafting, and post-battle writeback for the currently implemented world battle kinds.

## Does Not Own

Battle entry does not own combat simulation, tactical AI, report truth, manual persistence, fog/intel reveal, infiltration, alert escalation, defensive Raid requests, or world battle phase progression.

## Persistent State

Persistent facts used by battle entry come from `StrategicWorldState`, `WorldSiteState`, `WorldArmyState`, and definitions. Battle entry may mutate site mode to `Wartime` before launch and `Aftermath` after settlement, with rollback restoring the previous mode on failed handoff.

## Runtime State

Runtime state includes pending battle request, pending rollback token, pre-battle dialog state, `BattleSessionHandoff`, site deployment cache, battle-preparation placement state, battle-group plan draft state, and active battle runtime adapter state.

## Inputs

Inputs are assault-ready player armies, field intercept results, world/site definitions, current site and army state, scene paths, semantic deployment markers, semantic objective markers, and completed `BattleResult` values.

## Outputs

Outputs are `BattleStartRequest`, `BattleEntranceRequest`, force requests, preferred placement requests, battle-group plan requests, battle-launch diagnostics, `WorldActionResult`, `GameEvent` records, site/army mutations, and world-clock tick advancement after settlement.

## Contracts

Normal assault entry is explicit: an assault-intent player army arrives at a configured target, the world pauses, the request builder snapshots the target site and forces, and the scene transition router enters the authored battle site scene.

Field intercept entry is explicit: two moving opposing armies collide within the intercept threshold, a field-intercept request is built from those army states, and settlement resumes or defeats the relevant armies.

Site entrances are authored battle-entry anchors. They are not stealth or infiltration entrances, and they are not gated by intelligence reveal state.

Deployment preparation consumes side-aware semantic deployment zones, creates or updates site-local placement rows for non-resident forces, applies preferred placements to force requests, records each participating battle group's selected objective area and engagement rule, and exports the current navigation snapshot before runtime starts.

Hero-company drag deployment is a Presentation input surface over Application-owned plan drafting. Application must validate the proposed full company formation placement, including hero anchor, corps formation footprint, deployment-zone side, terrain legality, and collision with other committed placements. Presentation may preview legality continuously, but committed placement and launch readiness come only from accepted draft state.


World battle entry also seeds enemy tactical mode and initial region facts when enough strategic context exists. Enemy offense should target player defensive deployment regions. Enemy active defense should target player offensive deployment regions. Enemy hold defense should start in its held region and rely on Runtime engagement triggers before switching to active assault. These initial facts are battle-only intent and do not become campaign persistence.

Battle-group plan drafting is Application-owned state derived from the current battle request. UI may display and edit the draft through requests, but it must not create a second battle snapshot or unit pool. A draft is launchable only when every required player-side battle group has valid hero-led formation placement, objective-zone selection, and engagement rule selection.

The battle-start handoff carries accepted plan facts into Runtime as battle-only intent. Runtime may supersede or interrupt those facts through later accepted hero, corps, or combined commands, but it must emit command or plan-transition events so reports can explain whether a battle group followed its original plan, changed objective, held position, protected the hero, or retreated.

## Failure Rules

Unsupported assault targets, missing site definitions, empty deployment caches, missing required objective zones, invalid placement terrain, incomplete battle-group plans, battle handoff failure, and battle result/request mismatch fail explicitly. Failed battle launch must cancel handoff, disable runtime, clear temporary battle entities, and restore the previous site mode.

## Acceptance

This architecture is acceptable when:

- `BattleKind.AssaultSite` and `BattleKind.FieldIntercept` are the only world-driven battle kinds;
- no defense Raid, alert escalation, or world battle phase modifier path exists;
- pre-battle UI can show forces, site status, facilities, and garrison without intel dependencies;
- battle preparation can validate battle-group plan completeness before launch;
- settlement writes normal assault or field-intercept consequences back to world state.
