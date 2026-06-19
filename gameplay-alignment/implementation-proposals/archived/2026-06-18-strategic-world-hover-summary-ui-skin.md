# Strategic World Hover Summary UI Skin Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: Strategic-world hover summary should use the shared strategic UI skin resources instead of a local `StyleBoxFlat` block.
- Originating design proposal: Not required. `design-proposals/active/2026-06-17-strategic-world-context-ui/` remains Draft and is not implementation authority for this slice.
- Amendment proposals: None.
- Blocking issues: `WorldSiteHoverSummaryPanel.tscn` still defines a local `StyleBoxFlat` panel while other migrated strategic-world UI panels use the shared `basic-ui/1` Theme/StyleBoxTexture skin.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements `system-design/presentation-ui-layout-architecture.md` OverlayHost and strategic-world fullscreen map presentation rules for compact hover tooltip surfaces.
- Follows project `AGENTS.md` Godot Resource Authoring rules for `.tscn`, `.tres`, `Theme`, and reusable scene resources.
- Uses GodotPrompter skills: `godot-ui`, `responsive-ui`, `scene-organization`, `assets-pipeline`, `godot-code-review`, and `godot-testing`.

## Goal

Move the existing strategic-world hover summary panel onto the shared `basic-ui/1` Theme skin without changing hover behavior, data source, or placement logic.

After this slice, the invariant is:

```text
GameUiSceneFactory.CreateWorldSiteHoverSummaryPanel()
-> WorldSiteHoverSummaryPanel.tscn
-> assets/themes/game-ui-skin/basic_ui_1_theme.tres
-> WorldContextCard
```

## Scope

- Remove the local `StyleBoxFlat` panel sub-resource from `WorldSiteHoverSummaryPanel.tscn`.
- Bind the root hover panel to the existing shared Theme and `WorldContextCard` variation.
- Keep the existing scene factory path, script binding, child node names, text labels, and presenter placement flow stable.
- Add regression coverage proving the hover summary scene uses shared `basic-ui/1` resources and remains resource-instantiated through `GameUiSceneFactory`.

## Non-Goals

- Do not implement the Draft strategic-world context UI design proposal.
- Do not change hover summary text, data queries, pointer enter/exit behavior, or panel position calculation.
- Do not change `StrategicWorldRoot.UiBootstrap.cs` behavior except for tests that guard the existing factory path.
- Do not migrate unrelated `StyleBoxFlat` resources such as debug-only panels or battle intent markers.

## Touched Systems

- Strategic-world hover tooltip scene.
- Shared strategic UI skin usage.
- `tests/WorldSiteDeploymentCacheRegression` Presentation resource authoring coverage.

## Tests

- Add or update regression coverage proving:
  - `WorldSiteHoverSummaryPanel.tscn` references `assets/themes/game-ui-skin/basic_ui_1_theme.tres`;
  - the root panel uses `theme_type_variation = &"WorldContextCard"`;
  - the scene no longer contains `StyleBoxFlat` or `theme_override_styles/panel`;
  - `GameUiSceneFactory.CreateWorldSiteHoverSummaryPanel()` still instantiates the authored scene path.
- Re-run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- No new runtime logs required. This is a scene resource skin binding change with static regression coverage.

## Manual QA

- Hover multiple strategic locations on the large map and confirm the summary panel remains readable, stays near the marker, clamps inside the viewport, and visually matches the shared strategic UI skin.

## Acceptance Evidence

- `WorldSiteHoverSummaryPanel.tscn` now uses `assets/themes/game-ui-skin/basic_ui_1_theme.tres` with the `WorldContextCard` Theme variation instead of a local `StyleBoxFlat` panel resource.
- `GameUiSceneFactory.CreateWorldSiteHoverSummaryPanel()` remains the authored-scene instantiation path, so hover behavior and child node bindings stay unchanged.
- `tests/WorldSiteDeploymentCacheRegression` covers the hover summary skin resource, root Theme variation, no local panel override, and factory scene path.
- Passed `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` on 2026-06-18.
- Passed `dotnet build rpg.sln -maxcpucount:2 -v:minimal` on 2026-06-18.
- Manual QA is still recommended for strategic-location hover readability, viewport clamping, and visual fit.
