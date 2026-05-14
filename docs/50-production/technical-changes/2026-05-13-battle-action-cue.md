# 2026-05-13 Battle Action Cue

## Context

Battle unit actions were visually dense because the next unit could begin moving or attacking immediately after the previous unit finished. The turn system now owns a short pre-action cue so pacing is consistent across player, allied auto, and enemy units.

## Contract

- `BattleTurnController` is the timing authority: before a unit acts, it shows the active-unit cue, waits 0.5 seconds, hides the cue, then runs the action.
- `BattleUnitRoot` is the presentation owner for cue instances. It loads `scenes/battle/feedback/BattleActionCue.tscn`, attaches the cue to the unit, and removes it after the hold.
- The cue is not a grid highlight and does not reuse movement path arrows. It combines a foot pulse ring, a small overhead chevron, and temporary unit focus raise.
- Player unit selection also shows the cue and temporarily locks commands during the cue window; automatic allied and enemy units wait for the cue before executing their action.

## Verification

- `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`
