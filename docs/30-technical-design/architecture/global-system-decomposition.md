# Global System Decomposition

This document defines the long-term system split for the current product direction:

```text
大地图战略 + 人物社交经营 + 城池 / 战略地点经营与英雄带兵轻 RTS 战斗
```

The goal is not to erase existing code. The goal is to stop central scene roots from accumulating unrelated responsibilities.

## Top-Level Flow

```text
Run start
-> Strategic world state and map
-> WorldSite / opportunity / army contact / social event
-> Player chooses intervention, preparation, deployment, or delegation
-> Auto-resolution or battle request
-> Battle playback / result
-> World, site, people, army, resource, and threat writeback
-> Next strategic decision
```

The game is not a single battle. It is a campaign loop that moves through world state, people state, site state, conflict, and consequences.

## System Owners

### Run

Owns one campaign run:

- random seed;
- victory/failure state;
- persistent world state;
- persistent people state;
- run history and save entry.

Does not own battle rules, map scene nodes, or UI widgets.

### Strategic World

Owns large-map play:

- `StrategicWorldState`;
- `WorldSiteState`;
- `WorldArmyState`;
- threats and opportunities;
- world tick;
- battle request creation;
- battle result application.

Does not own local battle simulation or Godot battle nodes.

### WorldSite

Owns persistent operable local maps:

- authored site map identity;
- facilities and facility slots;
- garrison and unit placements;
- deployment zones and entrances;
- exploration and local memory;
- site mode transitions.

`WorldSiteState.UnitPlacements` is the site-local authority for unit positions. Runtime presentation projects from it.

### Officer / People

Owns character identity and social state:

- recruitment;
- loyalty, trust, resentment, bonds, duty, rank, talent;
- appointments and authority;
- event reactions;
- availability and loss.

It can influence battle through build, role, authority, and modifiers, but it should not directly mutate battle scene internals.

### Auto Battle

Owns local conflict resolution:

- reading `BattleStartRequest`;
- instantiating battle participants from request and site placements;
- automated real-time behavior and skill triggers;
- terrain/pathing/objective resolution;
- battle playback and readable event feed;
- `BattleResult` and battle report creation.

It must not persist world state directly.

### Content Pipeline

Owns authored definitions:

- world/site definitions;
- character definitions;
- facility definitions;
- unit and ability definitions;
- encounter/objective definitions;
- tutorial and campaign content.

Runtime systems should consume authored definitions through stable contracts, not hardcoded scenario branches.

## Scene Root Rule

Scene roots are composition shells, not domain owners.

`StrategicWorldRoot` should:

- bind strategic map scene nodes;
- display world state;
- route player input to application services;
- launch requests and consume results.

`WorldSiteRoot` should:

- load the authored site map;
- bind shared map, unit, UI, and camera nodes;
- route to management, deployment, exploration, and battle controllers;
- call handoff services.

`WorldSiteRoot` should not own:

- deployment candidate algorithms;
- facility/action business rules;
- exploration simulation;
- battle runtime phase ownership;
- result writeback rules;
- battle report construction.

## Target Runtime Split For WorldSite

The current `WorldSiteRoot` is too large and should be split along runtime responsibilities:

| Owner | Responsibility |
|---|---|
| `WorldSiteMapRuntime` | load map scene, expose grid/map context, render sort anchors |
| `WorldSiteManagementPresenter` | render facilities, garrison, threats, actions, notices |
| `WorldSiteDeploymentController` | select, drag, validate, and write unit placements |
| `WorldSiteExplorationController` | exploration movement, HUD, alert, encounter request |
| `WorldSiteBattleLauncher` | build and validate battle handoff from current site context |
| Battle runtime owner | future hero-led light RTS runtime or isolated backend auto-resolve path |
| Battle report builder | readable report and contribution summary |

Extraction should happen one owner at a time, with tests or smoke checks proving behavior did not move into a second authority.

## Battle Direction

Legacy manual battle systems are retired runtime authorities. Do not restore player turn phases, battle AP/action menus, manual move/attack command flow, or manual command UI as replacement work.

New work should target:

```text
site operation + build/deployment
-> hero-led light RTS battle inside an authored city or strategic-location battlefield
-> readable result report
-> world/site/person writeback
```

Auto-battler readability can remain an execution-layer reference for event clarity and reports. Do not import TFT-like shop rolls, fair-board economy rounds, synergy drafting, or pure no-command playback as the core loop.

## Architecture Risks

- If every new feature calls directly into `WorldSiteRoot`, the project will keep producing 6000-line root scripts.
- If battle runtime writes world state, request/result boundaries collapse.
- If battle has no readable report, the build and command loop becomes a black box.
- If facilities become heavy wall-hitting gameplay, the battle stops validating hero/corps builds.
- If `WorldSite` becomes only a right-side menu, the project loses its main distinction from ordinary node management.

## Next Landing Points

1. Keep the retired manual AP/runtime deleted.
2. Continue extracting `WorldSiteRoot` responsibilities only when a focused owner exists.
3. Create the accepted hero-led light RTS architecture before expanding combat presentation.
4. Keep retired manual battle code deleted instead of using it as a fallback.

The first auto battle migration remains a historical cleanup record in `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`.
