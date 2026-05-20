# Remove Site Exploration Runtime Proposal

Status: Archived after acceptance

## Purpose

Remove the standalone site exploration runtime from the current game direction.

The project no longer needs a separate grid-exploration mode with party AP, patrol AP, exploration patrols, exploration HUD, or exploration-triggered battle handoff. Ruins, dungeons, and other non-city strategic locations may remain content types, but they resolve through direct strategic actions, direct battle entry, rewards, clearing, occupation, or sealing outcomes.

## Current Architecture

Accepted gameplay and system documents still describe ruins and dungeons as exploration locations. Presentation architecture still lists a `SiteExploration` UI mode, semantic markers still reserve `ExplorationPoint`, and the LimboAI migration note still proposes a future exploration patrol AI phase.

Implementation contains a complete exploration chain:

- `WorldSiteExplorationState` and `SiteExplorationPatrolState` store site exploration runtime state.
- `SiteExplorationPointDefinition`, `SiteExplorationPatrolDefinition`, and `SiteExplorationActionDefinition` author exploration content.
- `WorldSiteExplorationService` owns party movement, patrol movement, alert checks, action application, and exploration battle requests.
- `WorldSiteRoot.SiteExploration*` presents exploration flow and HUD.
- `BattleStartRequest`, `WorldSiteBattleLauncher`, and `WorldBattleResultApplier` carry exploration-specific battle handoff and settlement facts.

## Expected Architecture

Delete site exploration as a runtime subsystem:

- No `SiteExploration` runtime mode.
- No site exploration HUD, party AP, patrol AP, alert patrols, route patrols, or exploration grid movement.
- No exploration-specific battle request fields or settlement paths.
- No exploration point/action/patrol definitions on `WorldSiteDefinition`.
- No future LimboAI exploration patrol migration phase.

Preserve non-exploration concepts:

- Ruin and dungeon remain valid strategic-location content categories.
- Marker and battle-entry systems may still support entrances, deployment zones, event spawns, tactical regions, and direct site battle setup.
- Existing city/site management and battle preparation remain intact.

## Non-Goals

- Do not remove strategic locations, ruins, dungeons, resource sites, gates, or opportunities as map content types.
- Do not remove direct battle handoff for site assault, defense, or future dungeon battle entry.
- Do not rewrite unrelated strategic map navigation, fog, city management, or LimboAI battle intent migration.
- Do not edit archived proposal bodies.

## Acceptance Criteria

- Authority documents no longer describe standalone site exploration runtime, exploration patrol AI, or `SiteExploration` UI mode.
- Code no longer defines or references `WorldSiteExplorationState`, `SiteExplorationPatrolState`, `WorldSiteExplorationService`, `SiteExploration*Definition`, or `ExplorationTriggerPatrolId`.
- `WorldSiteRoot` no longer has exploration runtime-mode branches or `WorldSiteRoot.SiteExploration*` partial files.
- Battle request, launcher, and result applier no longer include exploration-specific handoff or settlement logic.
- Exploration-specific tests and HUD resources are removed or replaced by guard tests proving the subsystem is absent.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` and the relevant regression projects pass.
