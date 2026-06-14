# Strategic Management Command Buttons Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-core-foundation.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-presentation-cutover.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-dashboard-ui-binding.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

Strategic Management presentation may collect player intent and submit commands, but long-term strategic state mutation must remain owned by `StrategicManagementCommandService`.

## Goal

Turn the first Strategic Management dashboard from read-only display into a minimal usable management panel. The player can build available facilities, create available corps instances, assign an available corps to a hero, and unassign a hero's corps through the existing authored Godot HUD controls.

## Scope

- Add command callbacks to `StrategicManagementDashboardPanelBinder`.
- Render authored action buttons for:
  - buildable facility options;
  - creatable muster templates;
  - hero corps assignment or unassignment.
- Route button presses through `WorldSiteRoot` into `StrategicManagementRuntime.Commands`.
- Refresh the Strategic Management dashboard after each command and show a concise result notice.
- Keep the binder display/callback-only: it must not depend on `StrategicManagementRuntime`, legacy world actions, or direct strategic state mutation.

## Non-Goals

- Do not implement save/load for Strategic Management state in this slice.
- Do not implement full strategic-location mapping from legacy world site IDs.
- Do not change battle Runtime, battle preparation, battle launch, settlement, or bridge contracts.
- Do not implement expeditions, training, upgrades, facility removal, queueing, or multi-corps hero composition.
- Do not create a generic command schema or new UI scene taxonomy in this slice.

## Touched Systems

- Modify `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`.
- Modify `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- Modify `tests/StrategicManagementRegression/Program.cs` only if command behavior needs an additional focused regression.
- Update `gameplay-alignment/implementation-proposals/README.md`.

## Tests

Primary UI architecture verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Focused Strategic Management verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

No new diagnostics category is required. Command success and rejection logging remains owned by `StrategicManagementCommandService`.

## Manual QA

Optional after automated verification: open the site management HUD, press a facility build button, create a corps, assign it to a hero, then unassign it. The panel should refresh immediately and the recent-feedback text should explain the last command result.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because the dashboard binder did not create authored command buttons/callbacks and `WorldSiteRoot` did not route dashboard command callbacks into `StrategicManagementRuntime.Commands`.
- 2026-06-14: Implemented command buttons in `StrategicManagementDashboardPanelBinder` using authored world action button resources and display-only callbacks.
- 2026-06-14: Routed facility build, corps creation, hero assignment, and hero unassignment through `WorldSiteRoot` into `StrategicManagementRuntime.Commands`, then refreshed the dashboard with a command-result notice.
- 2026-06-14: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
