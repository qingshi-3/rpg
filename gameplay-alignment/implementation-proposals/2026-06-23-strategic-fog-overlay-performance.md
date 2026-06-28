# Strategic Fog Overlay Performance

Status: Automated Verification Passed - Manual QA Pending

## Origin

- Requirement: STRATEGIC-FOG-001
- User Report: Large-map army movement stalls when fog-of-war visibility expands; revealed edge appears noisy instead of circular.
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/vertical-slices/first-playable-slice.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals: none.

## Goal

Keep strategic fog as map visibility only while making exploration expansion cheap enough for per-frame army movement and making the revealed boundary match the unit-centered circular vision rule.

## Current State

Strategic army movement calls fog refresh every frame while an army is moving.

The current direction is binary fog visibility only. Presentation updates only the current visible circles; there is no explored-history layer, no revealed residual, and no incremental expansion path to maintain. This removes the expansion-frame stall by deleting the extra fog state instead of trying to preserve it more efficiently.

## Architecture Direction

The fog service remains the Strategic World map-visibility authority. It must not regain site intel, threat, raid, or battle-trigger facts.

Presentation should own only the current fog rendering. Durable visibility facts are the current `StrategicWorldFogState.VisibleCells` snapshot; there is no player-facing explored-history state in this slice.

Current visible circles remain real-time shader parameters. Outside the visible circle, the map is unknown again.

## Scope

- Keep the fog overlay limited to current visible-circle rendering.
- Keep camera movement and zoom limited to shader parameter updates where possible.
- Reset the fog overlay state when the strategic runtime state is reset.
- Add low-noise diagnostics for unusually expensive full fog refreshes.

## Non-Goals

- No gameplay-rule changes to fog radius, faction ownership, army movement, opportunity spawning, battle entry, or strategic site visibility authority.
- No persistence-model changes.
- No site intel, threat, raid, or exploration-system revival.
- No GPU compute or multithreaded Godot API work in this slice.
- No authored scene change unless the overlay helper proves reusable outside this overlay.

## Touched Systems

- `src/Presentation/World/StrategicWorldRoot.Fog.cs`
- `src/Presentation/World/StrategicWorldFogOverlay.cs`
- `tests/WorldSiteDeploymentCacheRegression/`

## GodotPrompter Skills

Used for this implementation:

- `csharp-godot`
- `godot-debugging`
- `godot-testing`
- `scene-organization`

No additional GodotPrompter skill applies unless the fix expands into UI controls or authored reusable scene resources.

## Tests

Add focused regression coverage that verifies:

- the strategic fog overlay exposes only current visible-circle rendering and no explored-history update path;
- `StrategicWorldRoot.Fog` updates current visible circles without queuing explored cells;
- `StrategicFogOfWarService` clears revealed-history state and returns binary visibility only;
- the fog shader no longer samples an explored mask texture;
- moving exploration expansion no longer queues pending explored cells;
- strategic world reset invalidates the fog overlay state.

Recommended verification commands:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

## Diagnostics

Add low-noise logs only when:

- a full fog refresh exceeds a small frame-budget threshold.

Do not log every movement frame or every single fog cell.

## Manual QA

Desktop Mono QA should confirm:

1. Send an expedition across fogged large-map terrain.
2. While the visible circle moves inside already explored space, movement remains smooth.
3. When the expedition reveals new terrain, movement does not visibly stall.
4. The visible boundary remains a circular frontier around the army.
5. Camera pan/zoom does not force repeated expensive fog refreshes.

## Acceptance

This implementation proposal is accepted when:

- automated regression and build verification pass;
- the normal exploration-expansion path does not queue explored history;
- the expansion path does not use a stamp canvas, render-target redraw path, or explored mask texture;
- current visible circles remain the real-time visual authority;
- the visible/unknown edge follows circular reveal geometry;
- Strategic fog remains map visibility only and does not regain retired intel or raid concepts;
- manual QA evidence is recorded here after desktop verification.

## Verification Evidence

Automated verification:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed after removing the explored-history path because old flush calls still referenced deleted methods.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after collapsing fog to binary visibility only, clearing revealed-history state in `StrategicFogOfWarService`, and routing strategic world rendering through `SetFog` / `SetVisibleCircles` only.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors; it reported only existing CRLF normalization warnings in unrelated `tests/TargetBattleArchitectureRegression/` files.

Manual QA:

- Pending desktop Mono playthrough.

Third-pass automated verification:

- RED evidence: stale incremental fog tests failed after the explored-history path was removed.
- GREEN evidence: refreshed regression coverage passed once fog was reduced to binary visibility only.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors; it reported only existing CRLF normalization warnings in unrelated `tests/TargetBattleArchitectureRegression/` files.
