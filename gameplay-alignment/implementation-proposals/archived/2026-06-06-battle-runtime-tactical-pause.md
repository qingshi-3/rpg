# Battle Runtime Tactical Pause Implementation Proposal

## Status

Accepted - implemented and user-QA confirmed (2026-06-06)

## Origin

- User request: harden the existing Space-key battle pause before adding player intervention commands.
- Accepted authority:
  - `gameplay-design/vertical-slices/first-playable-slice.md` VS-09: player can pause or continue watching battle.
  - `system-design/battle-runtime-architecture.md`: Runtime owns movement, damage, action readiness, event stream, and battle outcome.
  - `system-design/battle-command-architecture.md`: UI submits intent; runtime commands must respect action locks and decision boundaries.
  - `system-design/presentation-ui-layout-architecture.md`: Presentation owns UI mode and visual interaction boundaries.

## Goal

Make tactical battle pause behave like a mature real-time-with-pause boundary: the battlefield visually freezes, battle Runtime facts stop advancing, and battle command UI remains usable for future player interventions.

## Scope

- Harden the current Space-key battle pause.
- Gate Runtime live-clock advancement before each fixed tick.
- Use Godot pause/process-mode boundaries so the battlefield presentation freezes while battle HUD/input can continue.
- Explicitly freeze unit-level movement lanes, sprite/AnimationPlayer playback, and procedural unit tweens so pause/resume does not finish or restart the current cue.
- Ensure pause is cleared when battle runtime ends or the site scene exits.
- Add low-noise diagnostics for pause application and waiting.
- Add regression coverage for the pause contract.

## Non-Goals

- No hero skill implementation.
- No new command execution queue.
- No battle AI behavior changes.
- No battle balance, movement, targeting, or combat-zone changes.
- No save/load or mid-battle persistence.

## Architecture

Tactical pause has two coordinated layers:

```text
Presentation engine pause
-> freezes battle map/unit visual processing
-> keeps HUD/root input able to unpause

Runtime advance gate
-> blocks AdvanceFixedTick before the next simulation slice
-> preserves Runtime time, action locks, HP, movement, damage, events, and outcome
```

The UI pause flag remains Presentation-owned. Runtime does not own UI pause state and does not mutate long-term state. This keeps the battle Runtime deterministic while making the player's observed battlefield behave like a paused game world.

## Touched Systems

- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeIncremental.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
- `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- `src/Presentation/Battle/Entities/UnitAnimationComponent.cs`
- `src/Presentation/Battle/Entities/UnitAnimationComponent.Pause.cs`
- `src/Presentation/Battle/Entities/DamageReactionComponent.cs`
- `tests/BattleHitFeedbackRegression/`
- `tests/WorldSiteDeploymentCacheRegression/`

## Tests

- Source regression: pause applies a Godot tree/process-mode boundary and restores it on unpause/end.
- Source regression: live Runtime loop waits for pause clearance before calling `AdvanceFixedTick`.
- Source regression: pause wait timers are explicitly pause-safe so the UI can unpause while the tree is paused.
- Source regression: tactical pause explicitly reaches unit presentation and resumes the existing animation state without forcing idle, move replay, or cue restart.
- Source regression: unit presentation and damage-reaction timers respect SceneTree pause instead of completing one-shot cues while paused.
- Existing battle runtime playback and world-site UI regressions continue passing.

## Diagnostics

- Keep `BattleRuntimeCommandPauseToggled`.
- Add or preserve a low-noise log when the tactical pause is applied/restored.
- Preserve `BattleRuntimePresentationWaitPaused` as the wait-loop diagnostic.

## Manual QA

1. Start the Bonefield battle from the playable slice path.
2. Press Space during unit movement.
3. Verify units, attacks, hit feedback, and battle outcome stop advancing.
4. Verify the current movement/attack animation frame does not finish while paused.
5. Press Space again and verify movement resumes from the frozen position without replaying the move animation from the start.
6. Verify battle command HUD remains interactive and Space resumes the battle.
7. Verify battle ends normally and returning to site/world leaves the tree unpaused.

## Acceptance Evidence

- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj --no-restore`
  - Passed.
  - Covers pause-safe presentation wait timers, Godot scene/process-mode pause boundary, runtime-disabled and exit-tree unpause restoration, pre-tick Runtime advance gate, explicit unit-presentation freeze/resume, and pause-aware unit one-shot timers.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj --no-restore`
  - Passed.
  - Covers existing Space-key pause UI, hero-company selection, world-site scene bindings, and battle runtime activation routes.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with 0 warnings and 0 errors.
- `git diff --check`
  - Passed.

Manual QA note: user confirmed the pause behavior was much improved after testing the Space-key battlefield freeze/resume path. Future command work may build on this pause boundary, but should re-check active skills and command UI against the same freeze/resume contract.
