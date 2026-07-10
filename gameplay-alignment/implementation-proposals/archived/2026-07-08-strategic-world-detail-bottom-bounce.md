# Strategic World Detail Bottom Bounce Implementation Proposal

Status: Archived By User Request - Implemented; Manual QA Not Retained As Active Work

## Origin

- Requirement: UI-WORLD-DETAIL-BOUNCE-001
- Authority:
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Blocking Issues: None known.

## Requirement

Make the strategic-world selected-city operation sheet use the accepted q-bounce popup feel: clicking a city opens the bottom context sheet from below the screen, overshoots upward, then settles back into its authored bottom-center rest position.

User adjustment: the opening motion should be readable without feeling slow. The main upward travel uses `0.30` seconds, and the reverse downward retraction uses the same `0.30`-second travel after the small upward close bump.

## Scope

- Change only the strategic-world selected location/opportunity detail sheet under `StrategicWorldHud.tscn` `OverlayHost/SiteDetailPanel`.
- Keep the sheet bottom-centered and content/action layout unchanged.
- Reuse the existing authored scene and C# binding path.
- Update regression guards so the sheet must enter from a computed offscreen-below position, use matched `0.30`-second enter/retract travel, settle after overshoot, and close by bumping upward before retracting below the screen.

## Non-Goals

- Do not redesign strategic-world action content, city summary text, or action availability.
- Do not migrate every popup in the project in this slice.
- Do not change entered-city management, battle preparation, battle runtime, or modal confirmation dialogs.

## Touched Systems

- `gameplay-alignment/implementation-proposals/README.md`
- `gameplay-alignment/implementation-proposals/2026-07-08-strategic-world-detail-bottom-bounce.md`
- `src/Presentation/World/StrategicWorldRoot.DetailHud.cs`
- `src/Presentation/World/StrategicWorldRoot.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs`

## GodotPrompter Skills

- `godot-ui`
- `responsive-ui`
- `csharp-godot`
- `godot-testing`

## Implementation Plan

1. Add a focused red guard to the existing strategic-world Presentation resource regression.
2. Replace the short slide offset with an offscreen-below position derived from viewport height and sheet size.
3. Keep the current overshoot/settle and hide-on-complete lifecycle.
4. Verify build, focused Presentation regression, and diff hygiene.

## Tests

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- `git diff --check`

Known unrelated blockers may keep the full `WorldSiteDeploymentCacheRegression` suite nonzero. The strategic-world detail sheet guards must pass.

## Manual QA

- On the strategic world map, click a city.
- Confirm the operation sheet starts below the screen, uses a readable `0.30`-second upward pop, moves slightly past the final position, then settles.
- Click empty map or switch context and confirm the sheet first bumps slightly upward, then retracts downward without leaving invisible input blockers.

## Acceptance

- The selected city operation sheet remains under `OverlayHost/SiteDetailPanel`.
- Opening the sheet uses offscreen-below start position, a `0.30`-second upward travel, upward overshoot, and settle-back motion.
- Closing the sheet uses the reverse q-bounce: a short upward bump followed by a matched `0.30`-second downward retraction below the screen, then hides after the tween completes.
- The sheet layout and action visibility remain unchanged.

## Verification Evidence

- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` exited `0` with `0` warnings and `0` errors.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` was run after the red guard and implementation. The strategic-world detail sheet guard passed, including `OverlayHost/SiteDetailPanel`, offscreen-below entry, upward overshoot, settle-back motion, and downward retract. The command still exited nonzero only on the unrelated known `TileSets` resource taxonomy blocker.
- `git diff --check` exited `0`; Git reported only CRLF-to-LF normalization warnings for existing text files.
- 2026-07-09 timing adjustment: the guard first failed on the new readable-timing/reverse-close requirement, then passed after setting the main entry travel to `0.56` seconds and adding a close bump before downward retraction. The full `WorldSiteDeploymentCacheRegression` command still exited nonzero only on the unrelated `TileSets` resource taxonomy blocker.
- 2026-07-09 follow-up timing adjustment: the guard first failed on the matched-speed requirement, then passed after setting both main entry and main retraction travel to `0.36` seconds. The full `WorldSiteDeploymentCacheRegression` command still exited nonzero only on the unrelated `TileSets` resource taxonomy blocker.
- 2026-07-09 follow-up timing adjustment: the guard first failed on the `0.30`-second matched-speed requirement, then passed after setting both main entry and main retraction travel to `0.30` seconds. The full `WorldSiteDeploymentCacheRegression` command still exited nonzero only on the unrelated `TileSets` resource taxonomy blocker.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` exited `0` with `0` warnings and `0` errors after the `0.30`-second follow-up adjustment.
- `git diff --check` exited `0` after the `0.30`-second follow-up adjustment.
