# Strategic Expedition Carrier Mirror Retirement

- Status: Completed
- Executor: Codex (single context)
- Verifier: Codex primary context (independent from executor)
- Created: 2026-07-13
- Updated: 2026-07-13

## Objective

Make Strategic Management expedition `Participants` the only runtime roster authority and constrain `WorldArmyState` to a map-movement carrier identified by `StrategicExpeditionId`, removing duplicated hero/corps/participant roster mirrors and silent runtime fallbacks.

## Confirmed Discussion Result

The user authorized continuous remediation and requested a leaner execution/verification process. P2-03 is completed at `main` HEAD `c9a12fe5`. This batch removes `StrategicExpeditionState.HeroId` / `CorpsInstanceId` lead aliases as runtime authority and removes strategic carrier `StrategicHeroId`, `StrategicCorpsInstanceId`, and strategic `GarrisonUnits` roster population. Legacy save shapes may be converted only at the versioned migration boundary; current runtime code must require canonical `Participants`.

## Authority Impact

Impact: Medium. No gameplay rule changes. `system-design/strategic-management-system-architecture.md` already makes Strategic Management the expedition authority and permits WorldArmy only as an explicit presentation/movement carrier. `system-design/strategic-battle-bridge-architecture.md` already makes Bridge participant references derive from the strategic expedition. No authority edit is required unless implementation finds a contradiction.

## Scope

- Add behavior RED coverage proving canonical expedition participants are required and strategic WorldArmy carries no duplicated roster facts.
- Remove expedition lead hero/corps aliases and runtime fallback enumeration.
- Preserve compatibility only through explicit versioned save migration into `Participants`; invalid or ambiguous legacy data fails explicitly.
- Remove strategic hero/corps mirror fields from WorldArmy and stop creating strategic garrison-unit mirrors.
- Keep `StrategicExpeditionId`, map position/destination, movement status, intent, faction projection, and other map-carrier facts required by the current world-map path.
- Update presentation/application consumers to resolve strategic roster facts from Strategic Management or Bridge views by expedition ID, never from carrier garrison rows.
- Preserve battle entry, rollback, arrival, carrier cleanup, save/load, P2-03 CAS, and legacy non-strategic armies.

## Non-Goals

- Do not remove the WorldArmy movement carrier itself.
- No map movement rewrite, new strategic travel architecture, UI redesign, result-summary expansion, regroup/retreat work, or general slimming.
- No scene/resource/preview changes.

## Constraints

- Work on `main`; do not branch, stage, or commit in the executor.
- Preserve the unrelated untracked coordinator `.uid`; do not read, modify, delete, stage, or include it.
- Ignore `scenes/world/preview/`; do not run a runner that enumerates it.
- Use one explicitly configured `gpt-5.6-sol` + `high` executor. The executor must not spawn another executor or subagent.
- Lean gate: one RED, one focused GREEN regression, one final affected build/solution build, one final review. Repeat only after an actual failure.
- Use `apply_patch`; no Godot process launch or inspection.
- Required skills: `csharp-godot`, `save-load`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. Current runtime expedition roster reads only `Participants`; lead hero/corps aliases and fallback enumeration are gone.
2. Strategic WorldArmy contains only carrier/movement facts plus `StrategicExpeditionId`; it does not copy strategic hero/corps IDs or strategic participant garrison rows.
3. Battle entry and Bridge compilation resolve every participant from Strategic Management/Bridge authority by expedition ID.
4. Versioned legacy save migration converts provable old alias data into canonical participants and rejects missing/ambiguous identity without runtime fallback.
5. Strategic arrival, rollback, scene failure, settlement cleanup, and carrier removal remain atomic and identity-matched.
6. Legacy non-strategic WorldArmy/garrison behavior remains passing.
7. Focused regressions, safe affected builds, and low-concurrency solution build pass; exact diff review has no unresolved Critical/Improvement.

## Current Progress

Completed:

- Confirmed duplicate mirrors in expedition aliases and strategic WorldArmy lead/roster fields.
- Confirmed current authority already requires Strategic Management/Bridge ownership.
- Clean tracked baseline is `main` HEAD `c9a12fe5`.
- Removed expedition lead aliases and all current-runtime participant fallback enumeration.
- Removed strategic hero/corps carrier fields and strategic garrison roster population; Bridge now compiles empty carrier seeds from canonical session participants.
- Advanced strategic saves to version 3 with explicit v1/v2 alias migration and rejection of incomplete, contradictory, or ambiguous identity.
- Preserved identity-matched arrival, rollback, cleanup, settlement/CAS, and non-strategic WorldArmy behavior under the focused regression.

Remaining:

- None.

## Pause And Resume

- Blocker: None.
- Resume entry: No execution remains; consult this record only for the completed carrier-mirror retirement boundary.
- Latest verification: independent primary-context verification passed 113/113 focused Strategic Management regressions, confirmed the canonical participant/carrier contracts, and found no unresolved Critical or Improvement in the exact diff.

## Execution Record

- 2026-07-13: Primary context established the lean execution contract and confirmed no authority conflict.
- 2026-07-13: Single-context executor started at `main` HEAD `c9a12fe5`; architecture review confirmed Strategic Management owns expedition participants while WorldArmy remains a subordinate movement/presentation carrier. Loaded `csharp-godot`, `save-load`, `godot-testing`, and `godot-code-review`.
- 2026-07-13: Behavior RED failed for the expected retained lead alias. Minimal GREEN removed runtime mirrors/fallbacks and moved legacy conversion to save v3 migration; the first GREEN run exposed and corrected only three collection return-type compile errors.
- 2026-07-13: `StrategicManagementRegression` passed, including migration, arrival, rollback, cleanup, settlement/CAS, and legacy non-strategic coverage. Final affected build and low-concurrency `rpg.sln` build completed with zero errors; unrelated pre-existing warnings were unchanged. Exact `godot-code-review` pass found no unresolved Critical or Improvement.
- 2026-07-13: Independent verification passed all 113 focused regressions without rebuilding, confirmed no whitespace errors beyond an existing line-ending notice, and accepted all seven criteria.

## Final Result

Verified complete. `Participants` is the only current expedition roster authority; strategic WorldArmy is an identity/movement carrier; provable legacy aliases migrate only at the versioned save boundary. Focused regression, affected build, solution build, and independent exact review passed.
