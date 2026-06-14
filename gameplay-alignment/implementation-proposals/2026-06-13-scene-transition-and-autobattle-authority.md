# Scene Transition And AutoBattle Authority Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review batch: architecture review cleanup batch 1.
- Authority documents:
  - `system-design/scene-transition-router-architecture.md`
  - `system-design/hero-led-light-rts-system-architecture.md`
  - `system-design/battle-runtime-architecture.md`

The accepted architecture already says `SceneTransitionRouter` owns player-facing root scene replacement and waits for `SceneTree.SceneChanged` before a transition is considered entered. It also says live battle truth belongs to Runtime; old auto-battle may remain only as isolated legacy simulation, not as a second consumer of formal battle handoff.

No design proposal is required for this batch because the target authority is already accepted.

## Scope

- Make `SceneTransitionRouter` treat `ChangeSceneToFile` success as "transition started", not "destination entered".
- Keep router busy until the gateway reports the destination scene has entered.
- Invoke battle transition success callbacks only after the scene-entered boundary.
- Preserve existing failure rollback for immediate scene-change errors.
- Add gateway support for a one-shot scene-entered callback.
- Prevent legacy AutoBattle runner/controller/world adapter from consuming active `BattleSessionHandoff`.
- Keep pure AutoBattle simulation/report tests as legacy-isolated coverage; do not delete the simulation in this batch.

## Non-Goals

- Do not add the loading overlay or preload cache.
- Do not replace static handoff stores with a unified scene transition context.
- Do not delete all AutoBattle classes while tests and historical report helpers still cover them.
- Do not refactor `WorldSiteRoot`, `StrategicWorldRoot`, battle command channels, world content definitions, or navigation topology fallback in this batch.
- Do not change accepted gameplay rules.

## Touched Systems

- Infrastructure scene transition gateway and router.
- World-site deployment cache regression tests for router behavior.
- Legacy AutoBattle runner/controller/world adapter behavior.
- AutoBattle runtime regression tests.
- Implementation proposal index.

## Tests

- `tests/WorldSiteDeploymentCacheRegression`
  - Router starts transition, remains busy, rejects overlap, and only clears busy after scene-entered callback.
  - Battle `OnSuccess` callback runs after scene-entered callback, not immediately after `ChangeSceneToFile`.
  - Immediate scene-change failure still clears site visit or cancels battle handoff and runs rollback.
- `tests/AutoBattleRuntimeRegression`
  - AutoBattle session runner rejects formal active handoff and leaves it active.
  - AutoBattle runtime controller reports disabled formal-handoff start without consuming the handoff.
  - World-site AutoBattle adapter rejects formal handoff and does not produce battle result/report.
  - Pure AutoBattle simulation/report coverage remains intact.

## Diagnostics

- Router logs transition started, entered, and immediate failure using low-noise messages.
- AutoBattle formal-handoff entry points report `auto_battle_handoff_disabled_runtime_authority`.

## Manual QA

No Godot editor launch is required for this batch unless automated checks expose a runtime-only Godot signal issue. If manual QA is later requested, use strategic world -> site/battle entry and confirm the destination scene opens through the router.

## Acceptance Evidence

- 2026-06-13: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet run --project tests\AutoBattleRuntimeRegression\AutoBattleRuntimeRegression.csproj -v:minimal` passed.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
