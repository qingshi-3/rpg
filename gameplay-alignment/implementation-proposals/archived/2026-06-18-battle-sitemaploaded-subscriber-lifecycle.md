# Battle SiteMapLoaded Subscriber Lifecycle Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: Battle Presentation nodes that subscribe to `WorldSiteRoot.SiteMapLoaded` should explicitly unsubscribe when leaving the tree.
- Originating design proposal: Not required; current accepted Presentation/runtime authority already covers UI/Presentation ownership and failure boundaries.
- Amendment proposals: None.
- Blocking issues: `BattleGridHighlightOverlay` and `BattleDebugController` subscribe to `WorldSiteRoot.SiteMapLoaded` without matching `_ExitTree()` disconnection.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements `system-design/presentation-ui-layout-architecture.md` Presentation boundary and failure rules without changing gameplay or runtime command authority.
- Follows project `AGENTS.md` Implementation Authority and Runtime Diagnostics rules by fixing the authoritative event path instead of hiding stale callbacks behind fallbacks.
- Uses GodotPrompter skills: `csharp-godot`, `csharp-signals`, `godot-debugging`, `godot-testing`, and `godot-code-review`.

## Goal

Ensure battle Presentation subscribers do not remain referenced by `WorldSiteRoot.SiteMapLoaded` after they leave the scene tree.

After this slice, the invariant is:

```text
WorldSiteRoot.SiteMapLoaded += OnSiteMapLoaded
-> matching _ExitTree()
-> WorldSiteRoot.SiteMapLoaded -= OnSiteMapLoaded
```

## Scope

- Add `_ExitTree()` disconnection to `BattleGridHighlightOverlay`.
- Add `_ExitTree()` disconnection to `BattleDebugController`.
- Keep `BattleDeploymentZoneOverlay` as the reference pattern and regression guard.
- Add static regression coverage for direct `SiteMapLoaded` subscription/disconnection pairs.

## Non-Goals

- Do not refactor `BattleGridHighlightOverlay` rendering or dynamic overlay decomposition.
- Do not change `WorldSiteRoot.SiteMapLoaded` declaration or map-loading semantics.
- Do not change debug toggle input behavior.
- Do not add broad signal helpers or lifecycle abstractions beyond this event path.

## Touched Systems

- Battle Presentation highlight overlay lifecycle.
- Battle debug controller lifecycle.
- `tests/WorldSiteDeploymentCacheRegression` Presentation anti-rot coverage.

## Tests

- Add a regression proving direct `SiteMapLoaded` subscribers include:
  - `SiteMapLoaded += OnSiteMapLoaded`;
  - `public override void _ExitTree()`;
  - `SiteMapLoaded -= OnSiteMapLoaded`.
- Include `BattleDeploymentZoneOverlay` in the guard so the known-good pattern cannot regress.
- Re-run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- No new runtime logs required. This fixes lifecycle cleanup around an existing event subscription and should stay silent during normal operation.

## Manual QA

- Enter a world-site battle scene, reload or transition away from the site, and confirm battle highlight/debug overlays do not emit stale callback warnings or duplicate overlay updates.

## Acceptance Evidence

- `BattleGridHighlightOverlay` now disconnects from `WorldSiteRoot.SiteMapLoaded` in `_ExitTree()`.
- `BattleDebugController` now disconnects from `WorldSiteRoot.SiteMapLoaded` in `_ExitTree()`.
- `tests/WorldSiteDeploymentCacheRegression` scans battle Presentation subscribers and requires `SiteMapLoaded += OnSiteMapLoaded` to have a matching `_ExitTree()` disconnection.
- Passed `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` on 2026-06-18.
- Passed `dotnet build rpg.sln -maxcpucount:2 -v:minimal` on 2026-06-18. The build still emits pre-existing nullable/source-generator warnings in test projects.
- Manual QA is still recommended for battle scene transition/reload cases where map-loaded callbacks could previously fire on stale Presentation nodes.
