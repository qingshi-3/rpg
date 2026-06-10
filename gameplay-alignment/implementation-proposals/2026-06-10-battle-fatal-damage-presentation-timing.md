# Battle Fatal Damage Presentation Timing

Status: Active

## Requirement

Reduce the visible delay between Runtime-confirmed defeat and Presentation health/death state for live battles.

## Authority Documents

- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`

## Scope

- Keep Runtime as the only combat truth for damage, defeat, and battle outcome.
- Keep Presentation responsible only for visual interpolation, animation, health UI, and feedback timing.
- Split live damage presentation so health/death state applies through a target-ordered semantic damage queue instead of waiting for the attacker's full visual action tail.
- Preserve target movement dependency and Runtime impact delay before applying Presentation health damage.
- Keep attack feedback animation on the actor visual tail.
- Keep low-noise diagnostics for fatal damage queue timing until manual QA confirms the delay is acceptable.

## Non-Goals

- Do not change Runtime attack cadence, damage values, targeting, or outcome resolution.
- Do not make Runtime wait for death animation.
- Do not predict defeat before receiving a Runtime `DamageApplied` event.
- Do not refactor battle animation resources or authored unit scenes.

## Touched Systems

- Combat Presentation live runtime observation.
- Battle health/death presentation timing.
- Battle hit feedback regression tests.

## Tests

- Add regression coverage that live damage semantic application is ordered per target and is not blocked by the attacker's visual action backlog.
- Keep existing coverage that damage waits for target movement but not target attack backlog.
- Run `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj`.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.

## Diagnostics

- Use `BattlePresentationFatalDamage...` log lines to verify:
  - target movement wait is reported separately from actor visual backlog;
  - fatal health application no longer reports actor action tails as semantic blockers;
  - target damage queue preserves damage application order without serializing each event's impact delay;
  - Runtime defeat to `MarkDefeatedRequested` delay is close to movement wait plus impact delay plus any remaining prior target damage application time.

## Manual QA

- Run the same `bonefield` battle path.
- Confirm the last enemy's HP bar disappears and death animation starts shortly after Runtime defeat.
- Confirm down/mid lane troops no longer visibly idle for a long interval while the final enemy still appears alive.

## Acceptance Evidence

- `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj` passed.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- Pending: user manual battle log review.
