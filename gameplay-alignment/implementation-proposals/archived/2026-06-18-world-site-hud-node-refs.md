# World Site HUD Node References Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: World-site HUD deep scene paths should be centralized in a focused Presentation collaborator instead of growing `WorldSiteRoot.SiteManagementHud`.
- Originating design proposal: Not required; current accepted authority already defines Presentation/UI layout hosts and binder rules.
- Amendment proposals: None.
- Blocking issues: `BuildSiteHud()` directly resolves many deep `WorldSitePeacetimeHud.tscn` paths.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements `system-design/presentation-ui-layout-architecture.md`, especially Layout Host Model, View Model And Binder Rules, and Migration Rules For Current Code.
- Follows the current accepted architecture only. It does not implement Draft active proposals under `design-proposals/active/`.
- Uses GodotPrompter skills: `godot-ui`, `responsive-ui`, `scene-organization`, `hud-system`, `godot-code-review`, and `godot-testing`.

## Goal

Move `WorldSitePeacetimeHud.tscn` node lookup paths into one focused internal Presentation class while keeping current scene structure, UI behavior, and Application/Runtime boundaries unchanged.

After this slice, the invariant is:

```text
WorldSiteRoot instantiates the HUD scene
-> WorldSitePeacetimeHudNodeRefs resolves authored Control paths
-> WorldSiteRoot wires existing presenters, binders, and callbacks
```

## Scope

- Add a `WorldSitePeacetimeHudNodeRefs` class that resolves typed references for the authored world-site HUD scene.
- Update `BuildSiteHud()` to use the node refs class instead of directly holding long `GetRequiredNode` paths.
- Add anti-rot regression coverage that guards the new binding boundary.
- Keep the current `WorldSitePeacetimeHud.tscn` structure and names unchanged.

## Non-Goals

- Do not implement `design-proposals/active/2026-06-17-strategic-world-context-ui/expected`.
- Do not implement `design-proposals/active/2026-06-17-site-map-layout-authoring/expected`.
- Do not change strategic-world context UI information architecture.
- Do not change UI skin/theme resources in this slice.
- Do not split `WorldSitePeacetimeHud.tscn` or `WorldSiteRoot.tscn`.
- Do not change battle-preparation drag, Runtime command, settlement, or strategic-management behavior.

## Touched Systems

- World-site Presentation HUD binding.
- `tests/WorldSiteDeploymentCacheRegression` anti-rot coverage.

## Tests

- Add or update anti-rot coverage proving node lookup paths live in `WorldSitePeacetimeHudNodeRefs`.
- Re-run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- No new runtime logs required. Existing `GameUiSceneFactory.GetRequiredNode` warnings remain the explicit failure surface for missing authored HUD nodes.

## Manual QA

- Enter a world site and confirm site management HUD, battle preparation HUD, and battle runtime hero command HUD still bind and respond.

## Acceptance Evidence

- 2026-06-18: `WorldSitePeacetimeHudNodeRefs` now centralizes typed lookups for authored `WorldSitePeacetimeHud.tscn` nodes. `WorldSiteRoot.SiteManagementHud.BuildSiteHud()` instantiates the HUD, resolves refs once, and wires existing presenters, binders, and callbacks without directly owning the deep scene paths. Verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` and `dotnet build rpg.sln -maxcpucount:2 -v:minimal`. Regression runs still report existing Godot source-generator / nullable warnings in test projects.
