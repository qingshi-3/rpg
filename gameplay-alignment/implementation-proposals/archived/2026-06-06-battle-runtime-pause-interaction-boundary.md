# Battle Runtime Pause Interaction Boundary Implementation Proposal

Status: Accepted

## Authority

- User request: tactical pause already freezes battle logic and unit animation frames; pause must now keep observation and command interaction usable.
- `gameplay-design/vertical-slices/first-playable-slice.md` VS-09: player can pause or continue watching a readable real-time battle.
- `system-design/battle-runtime-architecture.md`: Runtime owns live battle truth; Presentation owns hover, selection, visual interpolation, and feedback.
- `system-design/presentation-ui-layout-architecture.md`: Presentation/UI owns input focus, interaction-mode presentation, transient overlays, hover tooltips, and command UI.
- `system-design/battle-command-architecture.md`: command UI creates intent and Runtime consumes accepted orders at valid decision boundaries.

## Scope

Refine the tactical pause process boundary so battle time remains frozen while player observation and command preparation continue.

Paused battle time must freeze:

- Runtime fixed-tick advancement, perception/action facts, AI decisions, cooldowns, damage, defeat, and settlement progression.
- Unit movement lanes, tweens, AnimationPlayer/AnimatedSprite playback, hit reactions, damage-reaction timers, battle-event presentation, and map/environment animation.
- Runtime-driven camera follow and any follow tween that would express ongoing battle playback.
- Runtime perception overlays and similar battle-fact overlays; they may remain visible as frozen data but must not keep time-based animation or state recomputation running.

Paused interaction must remain usable:

- Space-key unpause and authored command HUD controls.
- Manual battle camera keyboard movement, middle-mouse pan, mouse-wheel zoom, viewport constraint refresh, and camera clamp.
- Mouse picking, tile hover frame, unit footprint hover frame, selected command-group highlight, and static range/target/path/area preview updates caused by pointer movement or UI selection.

Overlay animation rule:

- Paused battle overlays may update when player input changes the selected command, hovered cell, target set, or preview area.
- Paused battle overlays must be visually static after each update: no pulsing range fills, no floating target markers, and no looping path/target animation.
- Pause-state readability can later be handled by a separate pause shader or UI filter, not by keeping battle overlay animations alive.

## Non-Goals

- Do not add new live battle commands, skills, targeting semantics, or Runtime order consumption.
- Do not change battle result, AI, perception, or settlement rules.
- Do not implement the future pause shader/filter in this slice.
- Do not change strategic-world clock pause behavior.

## Touched Systems

- `WorldSiteRoot` tactical-pause process-mode application and restoration.
- `BattleCameraController` / `MapCameraController` pause-safe manual navigation boundary.
- `BattleGridHighlightOverlay` dynamic overlay animation policy while tactical pause is active.
- Source regression coverage under `tests/WorldSiteDeploymentCacheRegression/`.

## Tests

- Source regression: tactical pause keeps world-site root, HUD, viewport host, SubViewport, battle camera, and grid highlight overlay processable while keeping map, units, and active site map pausable.
- Source regression: tactical pause explicitly freezes unit presentation and uses pause-safe timers for unpause waiting.
- Source regression: manual camera input remains routed through the pause-safe battle camera while runtime-driven follow is blocked during tactical pause.
- Source regression: battle highlight overlay can still rebuild hover/preview cells but disables pulse/float tween creation while tactical pause is active.

## Diagnostics

- Preserve low-noise `BattleRuntimeCommandPauseToggled`, `BattleRuntimeScenePauseApplied`, and `BattleRuntimePresentationWaitPaused` logs.
- If the pause boundary changes process modes, log enough context to identify the reason without per-frame spam.

## Manual QA

1. Start Bonefield battle and press Space during visible movement/attack.
2. Verify units, attack/impact cues, and environment animation freeze on the current frame.
3. Move the mouse across tiles and units; verify hover frames update.
4. Use `W/A/S/D`, middle-mouse drag, and wheel zoom; verify manual camera navigation works while paused.
5. Select the paused command UI group; verify selection/highlight and static command previews update.
6. Verify range/target/path previews do not pulse, float, or loop while paused.
7. Press Space again; verify battle resumes from the frozen state and auto-follow can resume only after player navigation state permits it.

## Acceptance

Implementation is accepted when automated regressions and manual QA confirm that tactical pause freezes battle facts and battle-time visuals while keeping player observation, camera movement, and command-preparation UI responsive.

## Verification Evidence

Automated red check:

- `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- Expected failure before implementation:
  - `battle runtime pause keeps observation input alive`
  - `battle runtime pause keeps highlights static but input refreshable`

Automated green checks:

- `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - Result: passed.
  - Note: preserved existing Godot source-generator warning about missing `GodotProjectDir`.
- `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`
  - Result: passed.
  - Note: preserved existing Godot source-generator warning about missing `GodotProjectDir`.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`
  - Result: succeeded with 0 errors.
  - Note: existing warnings remain in `TargetBattleArchitectureRegression` nullable analysis and Godot source-generator setup.

Cleanup:

- `dotnet build-server shutdown`
  - Result: MSBuild and C# compiler servers shut down.

Manual QA:

- Passed by user acceptance on 2026-06-06 after Godot playback.
