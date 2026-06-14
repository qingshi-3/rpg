# Battle Combat Pressure Advance Implementation Proposal

Status: Implemented; manual QA pending

## Originating Design Proposal

- Existing accepted authority already covers local combat degradation; no new design proposal is required.

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

## Scope

- When a unit is in a local-combat decision and no attack/support slot is currently available, keep pressure by moving toward the combat pressure point when a legal neighboring pressure step exists.
- Use the local-combat situation center as the pressure point; do not turn ordinary target pursuit failures into combat-pressure movement.
- Route the fallback through region-directed local movement so it reuses objective/region local steering and occupancy validation.
- Preserve explicit queue/hold behavior when even pressure movement has no legal neighboring step.

## Non-Goals

- No Runtime flow fields.
- No per-unit whole-map A*.
- No changes to combat-zone merge policy.
- No changes to deployment, commands, abilities, settlement, or presentation interpolation.

## Touched Systems

- Battle Runtime target/slot fallback.
- Battle Runtime movement continuation fallback.
- Local movement diagnostics and reason codes.
- Target battle architecture regression tests.

## Tests

- Add a regression where a unit near an active fight has no immediately available attack/support slot toward the retained target, but still advances toward the combat pressure point instead of reporting `path_not_found`.
- Keep local-neighbor, local-steering, combat-zone, and performance regressions green.

## Diagnostics And QA

- Movement events should use a named pressure reason instead of generic target pursuit.
- Path failures should remain explicit only when pressure movement also cannot find a legal local step.
- Manual QA: Bonefield multi-company battle should show lower-lane units continuing to close on the fight when direct combat entry is full.

## Acceptance Evidence

- 2026-06-10 `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: pass. Covers no-slot local-combat pressure advance, top-corridor non-detour, Bonefield joiner pacing, local-neighbor navigation, no flow-field hot-path regressions.
- 2026-06-10 `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`: pass.
- 2026-06-10 `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: pass, 0 warnings, 0 errors.
- Manual QA pending: Bonefield multi-company battle should show no-slot rear/lane units continuing to pressure toward the active fight when a legal neighboring pressure step exists, without top-corridor detours or large-unit pacing loops.
