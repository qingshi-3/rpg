# Strategic World Detail Bottom Bounce Implementation Proposal

Status: Implemented - Pending Manual QA

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

## Scope

- Change only the strategic-world selected location/opportunity detail sheet under `StrategicWorldHud.tscn` `OverlayHost/SiteDetailPanel`.
- Keep the sheet bottom-centered and content/action layout unchanged.
- Reuse the existing authored scene and C# binding path.
- Update regression guards so the sheet must enter from a computed offscreen-below position and settle after overshoot.

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
- Confirm the operation sheet starts below the screen, pops upward past the final position, then settles.
- Click empty map or switch context and confirm the sheet retracts downward without leaving invisible input blockers.

## Acceptance

- The selected city operation sheet remains under `OverlayHost/SiteDetailPanel`.
- Opening the sheet uses offscreen-below start position, upward overshoot, and settle-back motion.
- Closing the sheet retracts below the screen and hides after the tween completes.
- The sheet layout and action visibility remain unchanged.

## Verification Evidence

- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` exited `0` with `0` warnings and `0` errors.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` was run after the red guard and implementation. The strategic-world detail sheet guard passed, including `OverlayHost/SiteDetailPanel`, offscreen-below entry, upward overshoot, settle-back motion, and downward retract. The command still exited nonzero only on the unrelated known `TileSets` resource taxonomy blocker.
- `git diff --check` exited `0`; Git reported only CRLF-to-LF normalization warnings for existing text files.
