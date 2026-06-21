# Strategic City Dispatchable Company Roster

Status: Accepted And Archived

## Authority

- `gameplay-design/vertical-slices/first-playable-slice.md`
- `system-design/strategic-management-system-architecture.md`

## Scope

Fix the selected city expedition roster so it represents hero companies currently present and dispatchable from that city. When a hero company starts an expedition, Strategic Management already locks the hero and corps and moves the corps to `Expedition`; the city dashboard must no longer expose that company as part of the city's expedition roster.

The source of truth remains normalized Strategic Management state: hero assignment plus corps `HomeCityId`, `Status`, and `CurrentExpeditionId`. Do not add a second authoritative corps-id list onto `StrategicCityState`.

## Non-Goals

- Do not add full multi-city station transfer or return-home rules in this slice.
- Do not add a duplicate city-owned corps list that must be manually kept in sync.
- Do not change expedition formation, battle bridge, battle result settlement, or world-army movement adapter behavior.

## Touched Systems

- Strategic Management city dashboard view model.
- Strategic Management regression tests.

## GodotPrompter Skills

- `godot-debugging`
- `godot-testing`
- `csharp-godot`

## Tests

- Add or update a regression proving a dispatched hero company disappears from the source city's expedition roster.
- Keep coverage that the underlying corps state records `Expedition` so the company is not silently deleted.

## Diagnostics

No new runtime logging is required. This is a deterministic view-model derivation fix.

## Manual QA

Start from the strategic map, create an expedition with one hero company, select the original city again, and confirm the expedition list shows only the remaining in-city hero companies.

Status: Closed by user archive confirmation on 2026-06-20.

## Acceptance

- City expedition roster starts with three hero companies in the first playable slice.
- After dispatching one hero company, the source city's expedition roster shows two.
- The dispatched corps remains durable Strategic Management state with `Expedition` status.

## Verification

- 2026-06-19: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
- 2026-06-19: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- 2026-06-19: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
