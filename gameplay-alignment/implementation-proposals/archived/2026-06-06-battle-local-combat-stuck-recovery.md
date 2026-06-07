# Battle Local Combat Stuck Recovery

Status: Archived - aligned with current local-combat stuck recovery implementation; manual QA not retained as active work per user cleanup request on 2026-06-07

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`

No gameplay authority change is intended. This proposal implements the accepted rule that dynamically blocked local-combat entry must degrade through named support or queue roles, and that combat-zone diagnostics must explain battlefield distribution and local-combat decision failures.

## Scope

- Keep an engaged group in local combat when a short perception gap occurs while its members are still executing or pursuing combat actions.
- When attack anchors are statically reachable but no attack-slot first step is executable this tick, try an explicit local-combat support or queue move before reporting `reject_no_reachable_slot`.
- When a retained local-combat target has already produced an ingress failure, demote the retained-target preference inside the same combat zone and score candidate targets by executable attack-slot travel cost.
- Improve region-directed movement failure diagnostics so logs show the active region movement goal instead of misleading empty actor objective fields.

## Non-Goals

- Redesign combat-zone clustering, global enemy policy, or map topology compilation.
- Change skill release rules, effect execution, or attack cadence.
- Add per-target global path optimizers or per-frame tactical recomputation.

## Touched Systems

- Battle tactical observation and group engagement transitions.
- Local-combat movement intent selection and crowd movement fallback.
- Runtime navigation and region movement diagnostics.
- Target battle architecture regression tests.

## Tests

- Add regression coverage for no-perception engagement retention when combat actors are still in active local-combat/action states.
- Add regression coverage for dynamically blocked attack entry degrading to a support/queue move when static attack anchors exist.
- Add regression coverage for the Bonefield large joiner case where a saturated retained target must not strand the unit when another combat-zone target has an executable local-combat entry.
- Add regression coverage or focused assertion for region failure diagnostics using the region goal context.

## Diagnostics

- Keep logs low-noise: emit important state transitions and one movement failure reason per actor/path state, not per frame.
- Local-combat blocked entry logs should distinguish static attack-slot reachability from dynamic first-step blockage and named fallback selection.
- Region movement failures should identify the active `BattleRegionMovementGoal` context.

## Manual QA

- Re-run Bonefield from the current user scenario.
- Confirm the enemy unit that previously paced or froze either joins through a support/queue move, holds with a named reason, or exits combat only after combat facts actually clear.
- Confirm hero/unit behavior resumes after skill release instead of leaving the actor permanently idle.

## Acceptance Evidence

- 2026-06-06 RED: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed on `runtime bonefield blocked retained target does not strand large joiner` with `lastFailure=local_region_degrade_no_reachable_slot`.
- 2026-06-06 Green: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed, including the Bonefield retained-target regression and the existing retained-target stickiness regression.
- 2026-06-06 Green: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- Pending: Bonefield manual QA after automated verification.
