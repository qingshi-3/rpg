# Battle Beacon Flow-Field Command Implementation Proposal

Status: Accepted implementation record; archived by user request on 2026-07-08 after focused Runtime, preparation, beacon presentation, selection-highlight, and build verification. Full regression suites still have unrelated guard failures recorded below.

## Origin

- Requirement: BATTLE-BEACON-COMMAND-001
- Design Proposal: `design-proposals/archived/2026-07-07-battle-beacon-flowfield-command/`
- Amendment Proposal: `design-proposals/archived/2026-07-07-battle-preparation-click-beacon-flow/`
- Authority:
  - `gameplay-design/details/combat-command/README.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/battle-navigation-topology-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/battle-tactical-intent-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Blocking Issues: None known before implementation.

## Requirement

Implement the accepted battle command flow where preparation uses click-to-place formation deployment, battle launch no longer requires objective-region or engagement-rule choices, deployed player battle groups receive initial destination beacons during preparation, and battle movement is commanded by selecting one or more battle groups and right-clicking a reachable destination beacon during preparation, live battle, or tactical pause.

## Scope

- Remove launch-readiness dependency on pre-battle objective-zone selection and engagement-rule selection.
- Keep deployment zones as the placement authority for battle preparation.
- Replace drag-required deployment with click-to-place formation-follow placement while keeping any drag shortcut on the same placement path.
- Show a non-blocking top-center prompt after placement that tells the player to right-click a destination.
- Accept preparation-time right-click beacon assignment for selected deployed battle groups and carry those initial beacon facts into Runtime launch.
- Add a destination-beacon command payload that can address multiple selected battle groups.
- Accept destination-beacon commands during preparation, live battle, and tactical pause.
- Validate the first multi-select beacon command atomically: if any selected battle group cannot statically reach the destination for its footprint/passability profile, reject the whole command and keep all previous destinations.
- Store accepted destination beacons as Runtime command target objects with command identity, owner groups, anchor, height, revision, and validity state.
- Build or reuse command-scoped beacon flow fields keyed by beacon id, topology version, height, and footprint/passability profile.
- Route player group movement toward the active beacon through the existing Runtime movement decision boundary while preserving neighbor movement commits, occupancy, reservations, local combat, and actor action locks.
- Display accepted or preparation-seeded destination beacons as Presentation overlays from command facts.
- During preparation, highlight the current selected or deploying player battle group and its owned destination beacon with the existing unit body outline shader; the beacon arrow remains unshaded.
- During tactical pause, highlight the currently selected battle group's visible destination beacon with the existing unit body outline shader.
- Keep Presentation from sampling flow fields or moving units along uncommitted paths.

## Non-Goals

- No partial acceptance for mixed reachable/unreachable multi-select commands.
- No large-scale RTS box-selection system beyond existing or minimal multi-select battle-group selection support.
- No new posture/engagement-rule UI; default posture is attack.
- No new authored objective-zone workflow.
- No replacement of local combat slot solving, damage, ability execution, settlement, or battle report generation.
- No Presentation-side movement interpolation changes except showing accepted beacon overlays and command feedback.

## Touched Systems

- Strategic battle bridge and battle-preparation launch readiness.
- Application command DTOs and validation result shape.
- Runtime command acceptance and battle-group command state.
- Runtime navigation flow-field cache built from immutable battle topology.
- Runtime movement continuation and anchored decision selection.
- Presentation battle-runtime selection, right-click input, command feedback, and beacon overlay.
- Regression tests under `tests/TargetBattleArchitectureRegression` and `tests/WorldSiteDeploymentCacheRegression`.

## GodotPrompter Skills

- `input-handling`
- `ai-navigation`
- `csharp-godot`
- `godot-ui`
- `hud-system`
- `godot-testing`
- `godot-code-review`

## Tests

- Add or update command-contract regression coverage for:
  - `CommandRequest` supports destination-beacon payloads with multiple battle-group ids.
  - battle launch readiness does not require objective-zone or engagement-rule choices.
  - deployed player groups require an initial destination beacon before launch.
  - preparation-seeded destination beacons become Runtime active beacons before the first movement decision.
  - destination-beacon commands submitted during tactical pause are accepted as command facts without advancing battle time.
  - multi-select beacon command rejection is atomic when any selected group cannot reach the destination.
  - accepted beacon replacement affects only the selected battle groups and leaves non-selected groups unchanged.
- Add or update navigation regression coverage for:
  - beacon flow fields are cached by beacon/topology/height/profile key.
  - unchanged beacons reuse the same flow field instead of rebuilding on every movement decision.
  - dynamic occupancy, reservations, local combat slots, actor targets, damage, and Presentation facts are excluded from flow-field keys and field construction.
  - local-combat movement hot paths still do not build new flow fields.
- Add or update Presentation regression coverage for:
  - clicking a roster row enters formation-follow placement mode without requiring drag threshold movement.
  - clicking a legal deployment position commits the placement, while invalid clicks keep the preview active.
  - after placement, a top-center non-blocking Chinese prompt asks the player to right-click a destination.
  - objective thumbnail/target selection is hidden from the mandatory preparation flow.
  - preparation right-click submits or stores an initial destination beacon for the selected deployed group or multi-selection.
  - right-click battle-runtime input submits a `CommandRequest` instead of moving actors directly.
  - Presentation beacon overlays bind to Runtime command facts.
  - preparation selected/deploying battle groups and their owned beacon reuse the existing unit body outline shader, with no shader applied to the arrow sprite.
  - tactical-pause beacon overlays highlight only the currently selected battle group's owned beacon and reuse the existing unit body outline shader.
  - Presentation does not sample beacon flow fields.
- Run:
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - `git diff --check`

## Diagnostics

- Runtime logs one low-noise event for beacon accepted, moved, superseded, rejected, unreachable, or invalidated.
- Runtime movement diagnostics distinguish missing topology, illegal footprint, unreachable beacon field, occupancy block, reservation block, local-combat override, and action-lock delay.
- Application rejection remains UI feedback/diagnostics, while Runtime rejection enters the battle event stream.

## Manual QA

- Click a battle group in the preparation roster and confirm the full formation follows the mouse without dragging.
- Click an invalid placement and confirm the preview stays active with invalid feedback.
- Click a legal deployment cell and confirm the group is placed.
- Confirm the top-center prompt appears and does not block map input.
- Right-click a reachable destination during preparation and confirm a beacon appears for the selected group.
- During preparation, switch the selected or currently deploying group and confirm only that group and its owned beacon have the yellow outline; confirm the beacon arrow itself is not outlined.
- Select multiple deployed groups during preparation, right-click a reachable destination, and confirm they share one initial beacon.
- Start a battle after deployment and preparation beacon setup without selecting an objective zone or engagement rule.
- Select one player battle group during live battle, right-click a reachable cell, and confirm an accepted beacon appears.
- Select multiple player battle groups, right-click a reachable cell, and confirm they share one beacon.
- While battle continues unpaused, select a different battle group and issue a new beacon; confirm previously commanded groups keep their old beacon.
- Press spacebar to pause, change selection, issue a beacon, and confirm battle time does not advance until unpaused.
- While paused with multiple beacons visible, switch the selected hero and confirm only that hero's owned beacon has the yellow outline.
- Try an unreachable destination for a multi-selection and confirm the command is rejected for all selected groups.
- Confirm unit movement visuals remain smooth cell-to-cell playback from Runtime events and do not jump to Presentation-computed paths.

## Acceptance

- The accepted authority documents describe the beacon command flow.
- The originating design proposal and preparation-click amendment are archived.
- Battle launch requires valid deployment, formation, and initial destination-beacon facts for deployed player groups.
- Player battle groups default to attack posture after launch.
- Preparation-seeded beacons initialize player-sourced tactical intent before the first Runtime movement decision.
- Runtime accepts, rejects, supersedes, and stores destination-beacon commands with reportable command identity.
- Multi-selected groups can share one accepted beacon.
- Non-selected battle groups are not affected by beacon replacement.
- Beacon flow fields rebuild only when beacon, topology, height, or footprint/passability profile changes.
- Runtime movement consumes beacon fields only as advisory direction input and still commits one legal neighboring anchor at a time.
- Presentation displays beacons and movement events without sampling flow fields or creating alternate movement truth.

## Verification Evidence

2026-07-08 verification:

- `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`
  - Beacon Runtime command coverage passed, including multi-group destination beacon payloads, shared reachable beacon acceptance, unreachable multi-select atomic rejection, selected-group-only replacement, orphan cleanup, pause-time command acceptance without time advance, preparation-seeded active beacon initialization, beacon-directed movement, and one-build shared-profile flow-field caching.
  - Suite exit remains blocked by an unrelated oversized-file guard: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1618`.
- `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Preparation and beacon presentation coverage passed, including roster-click placement-follow mode, left-click placement commit, non-blocking top prompt, preparation right-click initial beacons, launch requiring initial beacons while hiding objective thumbnails, Runtime right-click destination beacon commands, Runtime beacon visibility during tactical pause, reusable animated beacon marker scene, selected Runtime beacon outline, selected preparation group/beacon outline, and disposed cached selection guard.
  - Suite exit remains blocked by the existing resource taxonomy guard: `legacy authored asset bucket changed before its migration batch bucket=TileSets expected=0 actual=2`.
- `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - `battle unit command selection uses unit outline shader` passed after reducing unit command-selection outline width to match beacon-scale usage.
  - Suite exit remains blocked by unrelated failures for the preview workbench scene and Runtime selected-hero spotlight assertion.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- `git diff --check` passed.
- `WorldSiteRoot*.cs` line-count guard stayed at 7273 lines.

Manual QA and follow-up fixes:

- User iteratively reviewed beacon marker scale, hero idle animation, plinth layering, viewport avoidance, tactical-pause visibility, selection outline reuse, preparation selected-group highlighting, and unit outline thickness through screenshots and runtime reports.
- Reported follow-up issues were fixed within this implementation stream; no active beacon-flow follow-up remains in this proposal at archive time.
