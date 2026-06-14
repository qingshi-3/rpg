# Unit Animation Timing Policy Extraction Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review follow-up: remaining battle-facing Presentation optimization after hit feedback and grid-highlight geometry extraction.
- Priority: P1 battle Presentation cleanup; the user explicitly excluded strategic-management site-map / facility-slot presentation refactors from this pass.
- Authority documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/hero-led-light-rts-system-architecture.md`
  - `system-design/battle-runtime-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`

No design proposal is required because this is a behavior-preserving Presentation refactor. If implementation changes animation timing, attack cadence, hit impact timing, defeated timing, runtime action seconds, or authored animation resource contracts, stop and use the design proposal flow first.

## Scope

- Extract cue timing and loop/one-shot policy from `UnitAnimationComponent` into `UnitAnimationTimingPolicy`.
- Keep `UnitAnimationComponent` owning Godot node binding, animation playback, procedural fallback tweens, pause/resume, defeated fade, and callbacks.
- Preserve existing behavior for:
  - idle/move/attack/skill/hit/defeated target seconds;
  - attack-speed scaling;
  - animation-player speed scaling;
  - animated-sprite loop mode;
  - one-shot return-to-idle policy.

## Non-Goals

- Do not change strategic-management site-map, facility-slot, city/stronghold/resource-site, garrison, or large-map management presentation.
- Do not change authored animation resources, fallback procedural motion, skill-cast FX, hit/defeated presentation timing, pause behavior, or Runtime attack/action cadence.
- Do not introduce new animation states or migrate animation scene resources.

## Touched Systems

- `src/Presentation/Battle/Entities/UnitAnimationComponent.cs`
- New `src/Presentation/Battle/Entities/UnitAnimationTimingPolicy.cs`
- `tests/BattleHitFeedbackRegression` source-architecture guards
- This implementation proposal index

## Tests

- Add RED source-architecture guard requiring `UnitAnimationTimingPolicy`.
- Run `tests/BattleHitFeedbackRegression`.
- Run `tests/TargetBattleArchitectureRegression` because oversized file guards and high-frequency presentation log guards are adjacent.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.

## Diagnostics

- Preserve current logging. The timing policy must stay pure Presentation policy and must not read Runtime state, mutate Godot nodes, create tweens, or emit gameplay events.

## Manual QA

No Godot editor launch is required for this behavior-preserving source decomposition unless automated checks expose a runtime-only issue. Optional manual QA: enter battle and observe idle, move, attack, skill cast, hit, and defeated cues for pacing regressions.

## Acceptance Evidence

- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - Passed after an initial RED failure confirmed the missing `UnitAnimationTimingPolicy.cs` guard.
  - Existing Godot source-generator warning remains in the test host.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - Passed.
  - Existing test-host nullable/source-generator warnings remain.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - Passed.
  - Existing test-host nullable/source-generator warnings remain.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed with 0 warnings and 0 errors.
