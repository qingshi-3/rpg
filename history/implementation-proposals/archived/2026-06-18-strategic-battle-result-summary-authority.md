# Strategic Battle Result Summary Authority Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: High

## Relationship Metadata

- Origin: 2026-06-18 modular code/document consistency audit high-risk finding.
- Requirement slice: Strategic Management-backed battle results must be summarized from Bridge Active Context plus Runtime/settlement/report facts, not from legacy request/result authority.
- Originating design proposal: Not required; current accepted authority already defines the contract.
- Amendment proposals: None.
- Blocking issues: Legacy request/result summary construction remains usable by strategic tests and can still act as a second result source.
- Verification records: Pending implementation verification.

## Authority

- Implements `gameplay-design/content-systems-long-term-design.md`.
- Implements first playable result and continuation cases in `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-12, VS-13, VS-14, VS-15, and VS-16.
- Implements `system-design/strategic-management-system-architecture.md`.
- Implements `system-design/strategic-battle-bridge-architecture.md`, especially Result Summary and Legacy Retirement.
- Follows `system-design/battle-result-settlement-architecture.md` for Runtime result, settlement, report, and writeback truth.

## Current Inconsistency

The accepted contract says the bridge consumes complete Runtime outcome, event stream, settlement plan, and battle report facts, then creates `StrategicBattleResultSummary`. For Strategic Management-backed battles, that summary must be built from Bridge Active Context plus Runtime/settlement/report facts. It must not be reconstructed from legacy `BattleStartRequest` and `BattleResult` as source authority.

The current implementation still exposes a legacy strategic summary API:

- `StrategicBattleBridgeService.BuildResultSummary(StrategicBattleActiveContext context)` exists.
- `StrategicBattleBridgeService.BuildResultSummary(BattleStartRequest request, BattleResult result)` also exists and can produce a strategic summary.
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleResults.cs` still verifies strategic consequences through the legacy request/result path.

This leaves the strategic result pipeline vulnerable to applying consequences from a stale request/result pair that bypasses the active bridge session.

## Goal

Make Bridge Active Context plus Runtime/settlement/report facts the only authority for Strategic Management-backed battle result summaries.

After this optimization, the Strategic Management result flow should follow this invariant:

```text
Runtime outcome/event stream/settlement/report facts
-> Bridge Active Context result consumption
-> StrategicBattleResultSummary
-> Strategic Management apply-result command
```

The legacy request/result summary path may remain only as an explicitly named non-strategic compatibility adapter until retired.

## Scope

- Route Strategic Management-backed result summary construction through an active-context API.
- Restrict, rename, or reduce visibility of `BuildResultSummary(BattleStartRequest, BattleResult)` so it cannot be mistaken for strategic authority.
- Ensure active-context result summary validates session ID, snapshot ID, target location, expedition, and participant mappings before producing a normal summary.
- Update Strategic Management regressions that currently use legacy request/result-only summary generation to use Active Context-backed facts.
- Ensure legacy request/result-only data cannot apply Strategic Management consequences for the new path.
- Preserve non-strategic legacy compatibility only where explicitly scoped and named as compatibility.

## Non-Goals

- Do not redesign full battle report content or add a new report UI in this slice.
- Do not change battle Runtime simulation or settlement math beyond the fields required for authoritative summary construction.
- Do not remove old tests that still verify non-strategic compatibility behavior; rename or constrain them instead.
- Do not add persistence for active battle result context.

## Touched Systems

- `src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs`
- Strategic battle result DTOs and failure reasons under `src/Application/StrategicBattleBridge/` or adjacent Application namespaces
- Strategic Management apply-result command tests
- `tests/StrategicManagementRegression/StrategicManagementRegressionCases.BattleResults.cs`
- Battle result settlement/report test fixtures only where they supply active-context facts

## Implementation Guidance

1. Treat `BuildResultSummary(StrategicBattleActiveContext context)` or an equivalent active-context API as the strategic path.
2. Move any missing Runtime outcome, event stream, settlement plan, or report fields into the active context before summary construction.
3. Make mismatch checks active-context-first: context ID, session ID, snapshot ID, expedition ID, target location ID, force IDs, hero IDs, and corps instance IDs.
4. Rename or restrict the legacy API so call sites read as compatibility, not strategic writeback authority.
5. Update strategic regression fixtures to build an active context and result facts instead of directly pairing `BattleStartRequest` with `BattleResult`.
6. Add a rejection test for request/result-only strategic application.

## Tests

- Add a regression proving Strategic Management-backed result summary uses Active Context and preserves context/session/snapshot IDs.
- Add a regression proving missing or mismatched active context rejects summary construction without state mutation.
- Add a regression proving request/result-only legacy summary construction cannot apply Strategic Management consequences for the new strategic path.
- Update existing Strategic Management battle-result tests to use active-context summary creation for strategic consequences.
- Keep duplicate-result, reward, defeat, recovery, hero feedback, and equipment-sample regressions green after the source authority change.
- Re-run:
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

## Diagnostics

- Log active-context result summary accepted with context ID, session ID, snapshot ID, expedition ID, target location ID, outcome, and participant count.
- Log rejected result summaries with stable reasons for missing context, snapshot mismatch, session mismatch, participant mismatch, missing Runtime outcome, missing settlement, or missing report facts.
- Avoid logging full event streams unless a diagnostic flag or test fixture explicitly requests it.

## Manual QA

- Complete a Bonefield battle through the Strategic Management path.
- Confirm victory changes the strategic world and records rewards/hero feedback through Strategic Management commands.
- Confirm defeat or heavy loss produces recovery/loss feedback without applying duplicate or stale consequences.
- Confirm the result screen/report aligns with the participating hero companies from the launched active context.

## Acceptance Evidence

- 2026-06-18: Strategic result summary generation now rejects strategic `BattleStartRequest` / `BattleResult` pairs as legacy source authority, while Strategic Management regression fixtures use Bridge Active Context plus Runtime outcome facts for strategic writeback. Added coverage proving legacy request/result-only summaries cannot produce strategic participant consequences or mutate expedition/corps state. Verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`, `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`, `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`, `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`, and `git diff --check -- src tests gameplay-alignment/implementation-proposals`. Regression project runs still report existing Godot source-generator / nullable warnings; project build reports 0 warnings and 0 errors.
