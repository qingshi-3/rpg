# Battle Preparation Deployment Polish Implementation Proposal

Status: Implemented - Automated Verification Passed

## Authority

- Implements `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-07 and VS-09 readability prerequisites.
- Follows `system-design/world-battle-entry-architecture.md` for battle-preparation placement authority.
- Follows `system-design/world-site-management-architecture.md` for site-local placement ownership.
- Follows `system-design/battle-navigation-topology-architecture.md` for footprint-aware occupancy.

## Scope

- Ensure automatic battle deployment treats existing site placements as full-footprint occupancy, not anchor-only occupancy.
- Keep enemy and player battle-preparation map drags backed by the active `BattleStartRequest` placement data.
- Avoid full battle-preparation entity rebuilds after a single map-placement drag succeeds.

## Non-Goals

- No new deployment-zone authoring.
- No new combat runtime movement rules.
- No removal of the current enemy map-drag test affordance.
- No broad UI layout or animation refactor.

## Touched Systems

- World site deployment allocation and resident defender placement preparation.
- World site battle-preparation drag handling and presentation refresh.
- World-site deployment regression tests.

## Tests

- Add mixed-footprint enemy deployment coverage so resident `2x2` defenders and `1x1` defenders cannot overlap.
- Add presentation architecture regression coverage that battle-preparation single-placement drops use a lightweight refresh path.
- Add presentation architecture regression coverage that map drags cannot mutate site placement state before finding a request-backed placement.

## Diagnostics And QA

- Existing low-noise placement move logs remain the diagnostics source.
- Manual QA should enter Bonefield battle preparation, inspect enemy spacing, drag one visible enemy test placement, and confirm the drop no longer stalls from full entity reconstruction.

## Acceptance Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed on 2026-06-08.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-08.
- The runs still report existing Godot source generator / nullable warnings in test projects; they did not block compilation or execution.
- Manual Godot UI QA was not run in this session.
