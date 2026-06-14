# Strategic Management Map Site Mapping Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-core-foundation.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-presentation-cutover.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-dashboard-ui-binding.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-command-buttons.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Strategic Management may reuse existing large-map/site presentation assets, but it must not silently treat every legacy site scene entry as the same managed city.

## Goal

Replace the temporary hardcoded `WorldSiteRoot` mapping from any site to `LocationPlainsCity` with an explicit Strategic Management map-site mapping. The site HUD should only show and mutate city management when the active map/site id resolves to a Strategic Management city.

## Scope

- Add first-slice map-site metadata to Strategic Management location definitions.
- Add a small read-only resolver in `Rpg.Application.StrategicManagement`.
- Route `WorldSiteRoot` dashboard and command callbacks through the resolver.
- For unmapped or non-city map sites, block city-management commands and show an explicit notice instead of mutating the first city.
- Add regression coverage for known mappings and Presentation guardrails.

## Non-Goals

- Do not implement a full strategic-location dashboard for resource sites, beast sites, ruins, or enemy targets.
- Do not replace legacy world map/site scene flow.
- Do not implement save/load.
- Do not change battle Runtime, battle preparation, battle launch, settlement, or bridge contracts.
- Do not add new city content beyond the first managed city.

## Touched Systems

- Modify Strategic Management definitions and runtime wiring.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`.
- Modify `tests/StrategicManagementRegression/Program.cs`.
- Modify `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- Update `gameplay-alignment/implementation-proposals/README.md`.

## Tests

Primary Strategic Management verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Presentation architecture verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

No new diagnostics category is required. The HUD notice is enough for the first slice; later location dashboard work can add low-noise logs when non-city location actions become available.

## Manual QA

Optional after automated verification: enter the player camp and confirm the city dashboard still works; enter a non-city site and confirm the management panel does not mutate the plains city.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `StrategicManagementMapSiteResolver` and first-slice map-site IDs did not exist.
- 2026-06-14: Added first-slice map-site metadata to Strategic Management location definitions and implemented `StrategicManagementMapSiteResolver` without depending on legacy world-state types.
- 2026-06-14: Routed `WorldSiteRoot` city dashboard binding and command callbacks through explicit map-site city resolution; non-city or unmapped sites now show a notice and do not submit city-management commands.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted the existing Godot source-generator warning about `GodotProjectDir`.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The test project still emitted existing Godot source-generator and nullable warnings.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
