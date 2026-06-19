# Strategic Battle ID Mapping Validation Implementation Proposal

Status: Implemented - Mapping Verification Passed

## Origin And Authority

- Related implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-bridge-identity-writeback.md`
  - `gameplay-alignment/implementation-proposals/2026-06-11-thunder-mark-demo-skill-family.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
- System authority:
  - `system-design/strategic-battle-bridge-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/battle-content-progression-architecture.md`

Latest Runtime logs show Strategic Management-backed hero skills can appear available in the HUD while Runtime rejects submission with `skill_caster_not_allowed`. The observed failure is an identity-contract gap: strategic participant, hero battle unit, corps battle unit, HUD command group, and Runtime actor identities are not all validated against the same mapping chain.

## Goal

Systematically validate Strategic Management to battle Runtime ID mappings so hero skill availability, command submission, and Runtime skill acceptance agree on the same hero-company identity.

## Scope

- Add regression coverage for Strategic Battle Bridge snapshot identity fields that must preserve both hero and corps battle unit ids.
- Add regression coverage for Runtime actor construction so hidden hero actors use the hero battle unit identity and visible corps actors use the corps battle unit identity.
- Add regression coverage that a Strategic Management-backed thunder hero can submit `first_slice_skill_thunder_tag_throw` even when the assigned corps uses a different battle unit id.
- Keep HUD group identity aligned to `StrategicParticipantId` and command source actor ids aligned to Runtime actor ids.
- Add or update low-noise diagnostics only where they expose an ID mapping failure reason.

## Non-Goals

- Do not change thunder skill targeting rules, damage, cooldowns, interrupt rules, mark behavior, or presentation effects.
- Do not introduce multi-corps hero support.
- Do not replace battle preparation UI or legacy compatibility adapters in this slice.
- Do not make `BattleStartRequest` a new long-term authority; it remains a compatibility carrier.

## Touched Systems

- `src/Application/Battle/Snapshots/*`
- `src/Application/StrategicBattleBridge/*`
- `src/Runtime/Battle/BattleRuntimeSession.cs`
- `src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver.cs` only if validation diagnostics require it.
- `tests/StrategicManagementRegression/*`
- `tests/WorldSiteDeploymentCacheRegression/*`
- `tests/TargetBattleArchitectureRegression/*`

## Tests

RED/GREEN tests must cover:

- Bridge snapshot preserves strategic participant id, hero id, corps id, hero battle unit id, and corps battle unit id.
- Runtime actor construction keeps hero and corps `UnitDefinitionId` distinct when their battle units differ.
- Strategic participant HUD/runtime command path accepts a hero skill bound to the hero battle unit while the visible corps actor belongs to a different corps battle unit.

Verification commands:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Runtime rejection logs should continue to include battle id, tick, command source actor id, resolved caster id, skill id, target id or target cell, and rejection reason. If a mapping mismatch is detected before command submission, the diagnostic should name the missing hero or corps battle unit id without logging per-frame noise.

## Manual QA

After automated verification, optional manual QA is to enter Bonefield with the Strategic Management starter expedition, select the beast-tamer/thunder hero company, and confirm `雷符飞投` can be submitted against either an enemy unit or an in-range empty cell.

## Acceptance Evidence

- 2026-06-17: Added explicit battle-unit identity mapping across Strategic Battle Bridge snapshots, legacy compatibility requests, probe snapshots, and Runtime actor construction. `BattleGroupSnapshot` now carries distinct `HeroBattleUnitId` and `CorpsBattleUnitId`; Strategic Battle Bridge resolves them from Strategic Management definitions; `BattleStartRequest` remains a compatibility carrier; Runtime hidden hero actors use the hero battle unit id while visible corps actors use the corps battle unit id.
- RED verification before implementation:
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because `BattleGroupSnapshot` did not expose `HeroBattleUnitId`.
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed because the Runtime identity mapping test could not set `HeroBattleUnitId`. The same run also reported an unrelated existing anti-rot budget failure.
- GREEN verification after implementation:
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed the new Runtime hero/corps battle-unit identity test and failed only on unrelated tracked oversized file budget: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs:1168>1162`.
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
