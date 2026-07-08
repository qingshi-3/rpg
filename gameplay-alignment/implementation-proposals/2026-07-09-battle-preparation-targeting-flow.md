# Battle Preparation Targeting Flow Implementation Proposal

Status: Draft - Ready For Implementation

## Origin

- Requirement: UI-BATTLE-PREP-TARGET-001
- Design Proposal: `design-proposals/archived/2026-07-09-battle-preparation-targeting-flow/`
- Authority:
  - `gameplay-design/details/combat-command/README.md`
  - `gameplay-design/vertical-slices/first-playable-slice.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Parent Implementation Proposal:
  - `gameplay-alignment/implementation-proposals/2026-07-08-battle-map-operation-hud-suppression.md`
- Supersedes: None
- Superseded By: None
- Amends:
  - `gameplay-alignment/implementation-proposals/2026-07-08-battle-map-operation-hud-suppression.md`
- Amended By: None
- Blocking Issues:
  - The working tree already contains active UI and battle-HUD changes. Before implementation, inspect current diffs and avoid overwriting unrelated work.

## Requirement

Implement the accepted battle-preparation targeting flow:

```text
compact roster row click
-> formation-follow placement
-> left-click legal placement
-> immediate destination-targeting state
-> curved guide arrow from placed group to pointer, system cursor hidden, grid hover visible
-> left-click reachable cell to seed the initial destination beacon
-> restore battle-preparation HUD with a lower-right start-battle button when not in map operation
```

## Scope

- Remove the persistent bottom battle-preparation plan bar from the player-facing target UI.
- Add a lower-right start-battle button that is visible only outside map-operation suppression states.
- Keep the start-battle button disabled with a player-readable reason until launch readiness is satisfied.
- After successful formation placement, enter destination-targeting automatically.
- During destination-targeting, keep battle HUD suppression active, hide the system cursor, keep grid hover active, and draw a curved tapered arrow from the selected placed battle group toward the pointer.
- Confirm the initial destination beacon with left-click during destination-targeting.
- Keep invalid destination feedback local and do not emit Runtime events for rejected clicks.
- Restore the previous battle-preparation HUD state after accepted destination or cancel.

## Non-Goals

- Do not change live-battle or tactical-pause beacon command semantics beyond any necessary routing guards.
- Do not redesign the global UI theme-routing taxonomy.
- Do not add objective-region or engagement-rule selection back into battle preparation.
- Do not change Runtime movement, beacon flow-field caching, battle settlement, or report attribution.
- Do not create a second deployment or destination authority in Presentation.

## Touched Systems

- `WorldSitePeacetimeHud.tscn` battle-preparation docks and launch button placement.
- Battle-preparation HUD node references and binder/presenter code.
- Battle map operation HUD suppression and restoration flow.
- Battle-preparation deployment drag/placement completion flow.
- Destination beacon selection input handling.
- Battle-grid highlight hover integration.
- New or extracted map overlay presenter for the curved destination-targeting guide.
- Static regression guards under `tests/WorldSiteDeploymentCacheRegression`.

## GodotPrompter Skills

- `godot-ui`
- `input-handling`
- `csharp-godot`
- `godot-testing`
- `godot-code-review`

## Tests

- Add or update focused regression guards in `tests/WorldSiteDeploymentCacheRegression` to prove:
  - battle preparation does not expose the old bottom plan bar as the launch/current-objective authority;
  - the lower-right start-battle button is the launch control and remains disabled when launch readiness fails;
  - placing a battle group enters preparation destination-targeting instead of restoring normal HUD immediately;
  - preparation destination-targeting accepts left-click for destination confirmation;
  - preparation destination-targeting keeps grid hover active while suppressing blocking HUD;
  - the curved guide arrow is owned by a battle map overlay/presenter and not by generic tooltip UI or Runtime command logic.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` after focused guards pass or failures are confirmed unrelated.
- Run `git diff --check`.

## Diagnostics

- Add low-noise logs for entering/exiting preparation destination-targeting, accepted initial beacon, rejected initial destination, and cancellation.
- Do not log per-frame pointer movement or guide arrow geometry.

## Manual QA

- Enter battle preparation with at least one carried battle group.
- Hover roster rows and confirm hover/click feedback is readable.
- Click a roster row and confirm persistent UI hides while the formation follows the pointer.
- Left-click a legal deployment cell and confirm destination-targeting starts immediately.
- Confirm the system cursor is hidden, the curved guide arrow points from the placed group to the pointer, and grid hover remains visible.
- Left-click an invalid or unreachable destination and confirm local feedback appears while targeting remains active.
- Left-click a reachable destination and confirm the beacon appears and normal battle-preparation UI returns.
- Confirm only the lower-right start-battle button is used for launch and it is disabled until launch readiness is satisfied.
- Cancel destination-targeting and confirm no new beacon is committed and the previous HUD state restores.

## Acceptance

- Authority documents contain the accepted left-click preparation targeting flow.
- The design proposal is archived.
- The implementation keeps Presentation as input/feedback owner and Bridge/Runtime as validation/command authority.
- Focused regression guards cover launch button routing, HUD suppression, left-click targeting, grid hover retention, and overlay ownership.
- Manual QA confirms the post-placement flow no longer leaves the player without guidance.
