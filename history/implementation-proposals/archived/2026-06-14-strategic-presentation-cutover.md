# Strategic Presentation Cutover Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Originating design proposals:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/proposal.md`
  - `design-proposals/archived/2026-06-13-strategic-battle-bridge-contract/proposal.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

The accepted Strategic Management architecture says Presentation consumes view models and submits commands. Presentation must not calculate strategic rules, mutate resources, create corps directly, or treat legacy world-site management as new strategic authority.

## Goal

Add the first Strategic Management presentation boundary: a read-only dashboard view model built from Strategic Management definitions, state, and rules. This gives future city panels, corps panels, hero assignment panels, and command buttons a single strategic read model without extending legacy `WorldSite` UI as a rule owner.

## Scope

- Add Strategic Management view model DTOs under `Rpg.Application.StrategicManagement`.
- Add a read-only view model service that summarizes:
  - faction resources;
  - selected city identity, facility slots, and built facilities;
  - buildable facilities with costs and disabled reasons;
  - available and unavailable muster templates with costs and disabled reasons;
  - persistent corps instances owned by the selected city;
  - strategic heroes, assigned corps, and derived aptitude grade.
- Keep view model construction deterministic and free of Godot node dependencies.
- Add regression tests for:
  - city dashboard summaries;
  - unavailable beast muster explanations;
  - command mutations reflected in the dashboard;
  - Strategic Management Application remaining independent from legacy `Rpg.Domain.World`.
- Update this proposal with RED/GREEN/build evidence after verification.

## Non-Goals

- Do not replace the Godot city panel scene in this slice.
- Do not wire command buttons or mutate Strategic Management state from UI in this slice.
- Do not implement save/load.
- Do not implement expeditions or the Strategic Battle Bridge session.
- Do not touch battle Runtime logic, deployment UI internals, scene-transition internals, or bridge DTO implementation.
- Do not delete the old large-map/site flow in this slice.
- Do not create a generic UI schema, expression tree, scripting language, or technology framework.

## Touched Systems

- Modify `tests/StrategicManagementRegression/Program.cs`.
- Create Strategic Management presentation DTO/service files under `src/Application/StrategicManagement/`.
- Update `gameplay-alignment/implementation-proposals/README.md`.

## Implementation Slices

### Slice 1: View Model Contract

- Add tests that describe the first dashboard contract before production code exists.
- The dashboard is keyed by faction and selected city so future UI can render one management surface without reading legacy world-site state.

### Slice 2: Read-Only Service

- Implement a deterministic service over `StrategicManagementDefinitionSet`, `StrategicManagementState`, and `StrategicManagementRules`.
- The service may call rules for availability and disabled reasons, but it must not mutate state or duplicate command validation as UI logic.

### Slice 3: Architecture Guard

- Extend the existing application-layer guard so the new view model service and DTOs cannot reference `Rpg.Domain.World`.

## Tests

Primary verification:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
```

Expected added coverage:

```text
PASS strategic management dashboard summarizes city resources facilities corps and heroes
PASS strategic management dashboard explains unavailable beast muster reasons
PASS strategic management dashboard reflects command mutations
PASS strategic management application has no legacy world state dependency
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

Expected result: build succeeds after the slice is implemented.

## Diagnostics

No new runtime diagnostics are required in this slice because the service is read-only and no user-facing action is executed. Command diagnostics remain owned by `StrategicManagementCommandService`.

## Manual QA

No Godot editor launch is required for this slice because it does not connect a scene or authored UI resource yet.

Manual QA begins when a later slice binds this dashboard view model into the large-map/city management presentation.

## Acceptance Evidence

- 2026-06-14: RED verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed before implementation because `StrategicManagementViewModelService`, `StrategicManagementDashboardViewModel`, and related dashboard DTOs did not exist.
- 2026-06-14: Implemented the first read-only Strategic Management dashboard boundary under `src/Application/StrategicManagement/`. The service builds faction resources, selected city summary, facility build options, muster templates, city corps instances, and hero assignment rows from definitions, state, and rules without Godot UI or legacy world-state dependencies.
- 2026-06-14: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed. The suite covers dashboard summary data, unavailable beast muster explanations, command mutations reflected in the dashboard, and the Strategic Management Application guard against legacy `Rpg.Domain.World` state references.
- 2026-06-14: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
