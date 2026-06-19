# Strategic Management Facility Config Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: Strategic Management first-slice facility definitions must come from the accepted config boundary instead of being hardcoded in C# content construction.
- Originating design proposal: Not required; current accepted authority already defines selective Strategic Management data-driven content.
- Amendment proposals: None.
- Blocking issues: `FirstStrategicManagementDefinitions` still embeds first-slice facility content as C# object literals.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements `gameplay-design/content-systems-long-term-design.md`.
- Implements `system-design/strategic-management-system-architecture.md`, especially Content Definitions and Data-Driven Boundary.
- Follows `system-design/battle-content-progression-architecture.md`, especially Configuration Index Boundary.
- Uses GodotPrompter skills: `resource-pattern`, `assets-pipeline`, `godot-code-review`, and `godot-testing`.

## Goal

Move first-slice Strategic Management facility content into a config file while keeping rules, commands, state, and UI consuming typed `StrategicFacilityDefinition` objects.

After this slice, the invariant is:

```text
config/strategic_management/first_slice_facilities.json
-> Application config loader validation
-> FirstStrategicManagementDefinitions
-> Strategic Management rules, commands, and view models
```

## Scope

- Add a first-slice facility config file for training ground and beast pen definitions.
- Add a config loader that validates required ids, display names, slot costs, tags, and build costs.
- Update `FirstStrategicManagementDefinitions` to load facilities from config instead of constructing those facility definitions inline.
- Add regression coverage proving the content entry lives under config and the definition builder uses the loader.
- Keep existing facility behavior, costs, provided tags, dashboard availability, and beast-muster unlock behavior unchanged.

## Non-Goals

- Do not resourceize all Strategic Management definitions in this slice.
- Do not change facility rules, build commands, city slot behavior, or beast unlock requirements.
- Do not invest in legacy `StrategicWorldV1DefinitionFactory` as a new content authority.
- Do not move battle skill definitions in this slice.
- Do not add generic scripting, condition expression trees, or editor tooling.

## Touched Systems

- Strategic Management content definition construction.
- Config loading under `Application.Config`.
- First-slice Strategic Management facility config.
- `tests/StrategicManagementRegression`.

## Tests

- Add a regression that `FirstStrategicManagementDefinitions` routes first-slice facilities through config.
- Keep existing facility and beast-muster regressions green.
- Re-run:
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- Config load failures should throw explicit messages naming the config path and invalid field.
- No runtime logs are required because this is startup content validation, not a runtime state transition.

## Manual QA

- Build the training ground from the Strategic Management dashboard and confirm resources/slots update.
- Occupy the beast source, build the beast pen, and confirm beast-corps muster availability changes.

## Acceptance Evidence

- 2026-06-18: First-slice Strategic Management facility definitions now live under `config/strategic_management/first_slice_facilities.json` and load through `StrategicManagementFacilityDefinitionConfigLoader` before entering `FirstStrategicManagementDefinitions`. Rules, commands, and view models still consume typed `StrategicFacilityDefinition` objects; Runtime and Presentation do not read the config directly. Verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` and `dotnet build rpg.sln -maxcpucount:2 -v:minimal`.
