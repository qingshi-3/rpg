# TD-003 Plan State Emitter Slice

Status: Accepted

## Requirement

Extract the `SetPlanState` helper from `BattleRuntimeTickResolver` into `BattlePlanStateEmitter` as the first minimal TD-003 runtime extraction slice.

## Authority

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `gameplay-alignment/tech-debt-register.md` row `TD-003`

## Scope

- Create `src/Runtime/Battle/BattlePlanStateEmitter.cs`.
- Move the existing `SetPlanState` body without behavior changes.
- Update the five existing resolver call sites to call `BattlePlanStateEmitter.SetPlanState`.
- Preserve plan-state idempotence, mutation order, event construction, event id format, and reason-code behavior byte-for-byte.

## Non-Goals

- Do not change attack, movement, targeting, retargeting, diagnostics, or plan-state policy.
- Do not change regression tests to accommodate the refactor.
- Do not extract other TD-003 services in this slice.

## Touched Systems

- Runtime/Battle tick resolution.
- Runtime/Battle semantic event emission.
- Runtime/Battle plan-state mutation.

## Tests

- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`
- `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`

## Diagnostics

No new runtime diagnostics are expected. Existing semantic events must remain identical for the same inputs.

## Manual QA

No manual QA is required for this pure position-move slice unless the requested automated regressions fail or reveal runtime behavior drift.

## Acceptance Evidence

- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: passed, 0 warnings, 0 errors.
- `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`: passed, 148 PASS, 0 FAIL, 1 build warning, 0 errors on the counted run. An earlier cold run also passed and emitted 10 build warnings.
- `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`: passed, 75 PASS, 0 FAIL, 2 build warnings, 0 errors.

## Reviewer Verification

Per `.codex/collaboration.md`, acceptance is gated by the reviewer (Claude), not self-declared by the implementer (Codex). Independent verification on 2026-05-31:

- Static equivalence review (isolated sub-agent): `SetPlanState` body moved byte-for-byte into `BattlePlanStateEmitter`; idempotence short-circuit, mutation order, event id format, and all event fields preserved; old definition cleanly removed from `Plan.cs`; all five resolver call sites only gained the `BattlePlanStateEmitter.` prefix with unchanged arguments; exactly one definition remains; no smuggled logic changes.
- Reviewer-run verification: `dotnet build rpg.csproj` 0 warnings / 0 errors; `TargetBattleArchitectureRegression` 148 PASS / 0 FAIL; `BattleHitFeedbackRegression` 75 PASS / 0 FAIL.
- Status `Accepted` confirmed by reviewer after the above; this slice is the first completed TD-003 increment. Remaining TD-003 scope (attack/movement/event-factory extraction) stays Open in `tech-debt-register.md`.
