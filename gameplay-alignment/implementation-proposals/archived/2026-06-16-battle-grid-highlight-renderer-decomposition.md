# Battle Grid Highlight Renderer Decomposition Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Tech debt authority:
  - `gameplay-alignment/tech-debt-register.md` TD-007.
- Related accepted implementation record:
  - `gameplay-alignment/implementation-proposals/2026-06-13-battle-grid-highlight-geometry-extraction.md`.

## Goal

Finish the low-risk TD-007 renderer slice by moving procedural vector highlight node drawing out of `BattleGridHighlightOverlay` into focused renderer collaborators while keeping the existing overlay API and visual behavior stable.

## Scope

- Keep `BattleGridHighlightOverlay` as the node-facing state owner and public API for callers.
- Extract dynamic vector drawing for skill range borders, path arrows, target lock rings, and hover frames into `BattleGridVectorHighlightRenderer`.
- Keep tile-layer rendering in `BattleGridHighlightTileLayerRenderer`.
- Keep geometry calculation in `BattleGridHighlightGeometry`.
- Add architecture guards that `BattleGridHighlightOverlay.cs` stays below 350 lines, does not inline `new Polygon2D` or `new Line2D`, and delegates vector rendering to the extracted renderer.

## Non-Goals

- Do not change battle highlight colors, timings, z-index semantics, hover targeting, tile-layer draw order, or public methods.
- Do not change authored scenes or resource taxonomy.
- Do not implement the larger TD-004, TD-006, or TD-008 Presentation refactors in this slice.

## Tests

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Acceptance

- `BattleGridHighlightOverlay.cs` is below 350 lines.
- `BattleGridHighlightOverlay.cs` no longer directly constructs `Polygon2D` or `Line2D`.
- Vector drawing is isolated in `BattleGridVectorHighlightRenderer`.
- Existing highlight regression coverage remains green.

## Acceptance Evidence

- 2026-06-16: RED architecture guard initially failed because `BattleGridVectorHighlightRenderer.cs` did not exist.
- 2026-06-16: `BattleGridHighlightOverlay.cs` is 257 lines after extraction; `BattleGridVectorHighlightRenderer` owns `Polygon2D`/`Line2D` creation for skill range borders, path arrows, target lock rings, and hover frames.
- 2026-06-16: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed after updating source guards to the new renderer/geometry boundary.
- 2026-06-16: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed, including `battle grid highlight overlay delegates vector rendering`.
- 2026-06-16: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-16: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-16: `git diff --check` exited 0; it only reported line-ending normalization warnings for existing touched files.
