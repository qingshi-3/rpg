# Battle Skill Action Lock Boundary Implementation Proposal

Status: Implemented - Automated Verification Passed

## Authority

- Follows `system-design/battle-runtime-architecture.md`.
- Follows `system-design/battle-command-architecture.md`.
- Follows `system-design/battle-content-progression-architecture.md`.

## Scope

- Fix active skill execution so a caster does not visually or semantically continue movement while releasing a skill.
- Keep accepted skill commands queued behind movement, active skill casting, skill recovery, and non-interruptible action locks.
- Ensure a skill release consumes the caster's current runtime slice before movement or attack decisions resume.
- Add focused regression coverage for movement-boundary and same-tick follow-up movement cases.

## Non-Goals

- No new skill effect primitive.
- No new interrupt trait for canceling movement mid-cell.
- No change to player skill targeting UI beyond consuming runtime `SkillUsed` events if needed for presentation ordering.
- No rebalance of first-slice skill damage, range, or cooldown.

## Touched Systems

- Runtime hero-skill command resolver.
- Runtime tick resolver action ordering.
- Battle live presentation event observation, if runtime event playback lacks a separate skill cast cue.
- Target battle architecture regression tests.

## Tests

- Runtime regression: a skill command queued while the caster is finishing a move must wait until the next runtime tick instead of releasing on the movement-completion tick.
- Runtime regression: a skill released at tick start must prevent that caster from submitting a movement or attack decision in the same tick.
- Existing target battle architecture regression suite must remain green.

## Diagnostics And QA

- Runtime should keep command accepted/failed/interrupted events as the source of low-noise skill diagnostics.
- Manual QA: issue a targeted hero skill while the selected unit is moving, and confirm the unit finishes/stops before the skill animation; after the cast/recovery, confirm it resumes normal movement or attack decisions.

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-08 after the new red-green skill action-lock regressions.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed on 2026-06-08.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed on 2026-06-08.
- The runs still report the existing Godot source generator warning that `GodotProjectDir` is null or empty in test projects; it did not block compilation or execution.
- Manual Godot UI QA was not run in this session.
