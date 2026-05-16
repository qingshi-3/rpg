# Playback UI And Battle Report

## Purpose

Automated battles are valid only if the player can read what happened. This document defines the minimum playback UI and report requirements.

## Playback Controls

Required controls:

- start;
- pause/resume;
- speed control;
- skip;
- event feed;
- final report view.

Godot UI should be resource-authored where practical:

- prefer `.tscn` controls and reusable rows;
- avoid building the full HUD through C# `new`;
- keep runtime code responsible for binding nodes and updating view models.

## Event Feed

The event feed should be concise and battle-readable. Minimum event categories:

- battle start and objective;
- deployment/entry;
- important movement or engagement;
- attack or skill highlights;
- unit defeated;
- facility/objective change;
- battle end.

Do not spam every simulation tick or every path step.

## Report Facts

Minimum report fields:

- outcome;
- objective result;
- surviving and defeated force counts;
- hero contribution;
- corps contribution;
- facility, terrain, or modifier contribution;
- top failure reason when defeated;
- site/world changes applied.

Report facts should come from runtime events, `BattleResult`, and world writeback summary. They should not be inferred only from presentation nodes.

The first implementation step is `16-auto-battle-report-builder-implementation-plan.md`. It builds a pure application-layer report model from `AutoBattleSimulationResult`, runtime events, and `BattleResult.ForceResults`. It deliberately emits stable summary and failure keys instead of final localized UI copy; later UI work owns Chinese presentation text and layout.

`17-auto-battle-runtime-controller-implementation-plan.md` adds application-layer playback control over report feed events. It is not the final Godot HUD; it provides the state contract future UI should bind to: phase, visible feed rows, pause/resume, speed, skip, report, and failure reason.

`19-auto-battle-report-summary-implementation-plan.md` adds the first player-readable report presentation without building the full HUD. It formats `AutoBattleReport` into a concise Chinese summary and appends it to the existing WorldSite management notice after auto battle writeback.

## Failure Reason Examples

Useful failure reason categories:

- frontline collapsed;
- hero defeated early;
- corps could not reach target;
- enemy ranged pressure was unanswered;
- water or terrain restricted movement;
- facility modifier was missing or disabled;
- objective timer or defense point failed;
- deployment left a key entrance uncovered.

The report should be actionable. It should suggest which preparation axis failed without prescribing one exact solution.

## UI Boundaries

Playback UI may:

- control simulation pacing;
- display events and report models;
- request skip;
- present final confirmation to return.

Playback UI must not:

- mutate world state;
- directly edit `WorldSiteState.UnitPlacements`;
- spend AP or issue manual battle commands;
- infer final casualties when `ForceResults` exists.

## Acceptance

- Player can pause, speed up, skip, and still receive a coherent report.
- The report explains at least one meaningful reason for defeat.
- The UI does not depend on the legacy `BattleActionMenu` as its main interaction model.
- Report construction can be tested without rendering the full scene.
- Before the final HUD exists, an opt-in auto battle can still return a concise Chinese summary through the existing management notice.
