# Battle Objective Planning UI Skin Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: Battle objective planning UI should use the shared strategic UI skin resources instead of local `StyleBoxFlat` blocks.
- Originating design proposal: Not required; current accepted authority already defines Presentation/UI layout and resource-backed authoring rules.
- Amendment proposals: None.
- Blocking issues: Objective planning UI carried local panel styles, and the active runtime thumbnail was embedded in `WorldSitePeacetimeHud.tscn` rather than instantiated from the standalone thumbnail template.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements `system-design/presentation-ui-layout-architecture.md`, especially Presentation-owned overlay feedback, authored UI resources, and the battle-preparation target selector boundary.
- Follows project `AGENTS.md` Godot Resource Authoring rules for `.tscn`, `.tres`, `Theme`, and reusable scene resources.
- Uses GodotPrompter skills: `godot-ui`, `responsive-ui`, `scene-organization`, `assets-pipeline`, `godot-code-review`, and `godot-testing`.

## Goal

Move the battle objective thumbnail and objective map dialog onto the existing `basic-ui/1` shared Theme and StyleBoxTexture skin without changing target-selection behavior.

After this slice, the invariant is:

```text
assets/themes/game-ui-skin/basic_ui_1_theme.tres
-> BattlePreparationObjectiveThumbnail.tscn uses WorldContextCard
-> BattleObjectiveMapDialog.tscn uses WorldContextSheet / WorldContextCard / action button variations
-> C# binders keep using the same authored scene paths
```

## Scope

- Remove local `StyleBoxFlat` sub-resources from `BattlePreparationObjectiveThumbnail.tscn`.
- Remove local `StyleBoxFlat` sub-resources from `BattleObjectiveMapDialog.tscn`.
- Align the active embedded `WorldSitePeacetimeHud.tscn` objective thumbnail with the standalone thumbnail Theme variation without changing its binding path.
- Bind objective planning panels and dialog buttons to existing shared Theme variations.
- Add regression coverage proving the objective planning UI uses shared `basic-ui/1` resources, no local `StyleBoxFlat` blocks, and enough vertical budget for Theme margins and authored controls.

## Non-Goals

- Do not change objective selection rules, target marker semantics, Application validation, Runtime launch, or battle preparation state.
- Do not change `WorldSitePeacetimeHud.tscn` global theme structure in this slice.
- Do not restyle debug panels, `BattleIntentMarker`, or world-site hover summary.
- Do not replace all node-level color/font/margin overrides.
- Do not implement any active design proposal under `design-proposals/active/`.

## Touched Systems

- Battle preparation objective planning UI scenes.
- Shared strategic UI skin usage.
- `tests/WorldSiteDeploymentCacheRegression` Presentation resource authoring coverage.

## Tests

- Add or update regression coverage proving objective planning scenes:
  - reference `assets/themes/game-ui-skin/basic_ui_1_theme.tres`;
  - do not contain `StyleBoxFlat`;
  - bind dialog/thumbnail panels and buttons through shared Theme variations;
  - cover the active embedded world-site thumbnail and the objective dialog height budget.
- Re-run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- No new runtime logs required. This is a scene resource skin binding change with static regression coverage.

## Manual QA

- Enter battle preparation, open the objective thumbnail/dialog, switch company, choose an objective, close/confirm, and confirm hover/pressed button states, panel borders, and text fit remain acceptable.

## Acceptance Evidence

- `BattlePreparationObjectiveThumbnail.tscn`, `BattleObjectiveMapDialog.tscn`, and the active embedded objective thumbnail in `WorldSitePeacetimeHud.tscn` now use the shared `basic-ui/1` Theme variations instead of local `StyleBoxFlat` panel blocks.
- `BattleObjectiveMapDialog.tscn` reduces the map preview minimum height so the shared Theme margins, 54 px action buttons, status label, and stack gaps fit inside the authored dialog height.
- `tests/WorldSiteDeploymentCacheRegression` covers standalone templates, the active embedded thumbnail, dialog action Theme variations, and the vertical layout budget.
- Passed `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` on 2026-06-18.
- Manual QA is still recommended for battle preparation objective selection hover, pressed, close, and confirm states.
