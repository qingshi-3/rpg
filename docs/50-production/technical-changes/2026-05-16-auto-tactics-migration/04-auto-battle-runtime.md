# Auto Battle Runtime

## Purpose

This document defines the target automated real-time execution layer. It is the tactical runtime that replaces player-operated turn/AP battle flow after deployment.

## Runtime Identity

The runtime should read like an auto-battler clash on an authored `WorldSite` battlefield:

```text
prepare deployment
-> start battle
-> units automatically acquire targets, move, attack, use skills, and resolve objectives
-> player watches, pauses, speeds up, skips, and reads the report
```

The runtime validates preparation. It is not a manual command mode.

## Initial Scope

The first slice supports:

- one `WorldSite` map;
- one player hero;
- one player corps;
- one small enemy group;
- movement across existing grid surfaces;
- basic attack;
- one hero skill trigger;
- victory/defeat outcome;
- `BattleResult.ForceResults`;
- event stream for report building.

The first implementation step is narrower than the first playable slice. `14-auto-battle-runtime-skeleton-implementation-plan.md` creates a pure C# runtime skeleton that validates request spawning, deterministic ticks, target acquisition, basic movement, attacks, defeat, events, outcome, and `ForceResults`. It does not yet connect playback UI, battle scene entities, map pathing, or hero skill presentation.

`15-auto-battle-session-runner-implementation-plan.md` adds the next application-layer bridge: an active `BattleSessionHandoff` request can be resolved by the auto simulation and completed with the simulation's full `BattleResult`. This is still not scene/UI integration; `WorldSiteRoot` and the legacy manual battle path remain unchanged until playback and controller ownership are ready.

`17-auto-battle-runtime-controller-implementation-plan.md` adds the first `AutoBattleRuntimeController` boundary. Its initial controller is still application-layer only: it starts an active handoff through the session runner, builds a report, and exposes pause/resume/speed/skip-style playback cursor controls over the generated event feed.

`18-worldsite-auto-battle-adapter-implementation-plan.md` adds an opt-in WorldSite adapter for scene callers. It lets `WorldSiteRoot` delegate active handoff resolution to the auto battle controller without making auto battle the default path or removing the legacy manual battle runtime.

## Simulation Model

Use deterministic simulation steps with real-time presentation pacing:

- Simulation owns discrete ticks or fixed time steps.
- Presentation may interpolate movement and animation.
- Combat outcome must not depend on frame rate.
- Pause stops simulation advancement.
- Speed changes simulation pacing, not rules.
- Skip fast-forwards to the next stable result and still produces events and report data.

## Unit Behavior

Minimum behavior loop:

1. If defeated, do nothing.
2. If current target is invalid, acquire a target based on faction, objective, threat, distance, and role.
3. If target is in attack range and cooldown is ready, attack or use eligible skill.
4. If target is not in range, move toward a valid attack or objective surface.
5. If blocked or no path exists, emit a low-noise stuck/failure event and choose the next valid behavior.

Hero skill trigger for the first slice:

- one explicit ability condition;
- clear event cue;
- contribution recorded for the report.

## Grid And Board Rules

- The board is the active `WorldSite` grid.
- Deployment origins come from `WorldSiteState.UnitPlacements`.
- Terrain and water restrictions must reuse existing movement capabilities where possible.
- Facilities and site objects can influence battle only through explicit modifiers or objective rules.
- Do not make wall-hitting or facility destruction the main battle experience.

## Runtime Events

The runtime should emit structured events for:

- battle started;
- unit spawned;
- target acquired;
- movement started and completed;
- attack resolved;
- skill triggered;
- unit defeated;
- objective changed;
- battle paused, speed changed, skipped;
- battle ended.

Events are for playback and report building. They are not world persistence.

## Outcome Checks

Minimum outcome logic:

- player victory when enemy force is defeated or required objective is completed;
- player defeat when player force is defeated or required defense objective fails;
- withdraw only when an explicit future rule allows it;
- disaster only for defined severe failure conditions.

## Non-Goals

- No TFT shop, economy, round draft, or item carousel.
- No manual per-unit command UI.
- No AP/TurnSystem dependency.
- No direct mutation of `StrategicWorldState`.
- No full ability library in the first slice.

## Acceptance

- Auto battle can start from a `BattleStartRequest`.
- Runtime entities spawn from request forces and site placements.
- Units move and attack without player commands.
- At least one hero skill can trigger automatically and be reported.
- Runtime emits enough events for a readable report.
- Runtime returns `BattleResult` with `ForceResults`.
