# Zone-First Unit Decision Flow

Status: Archived - aligned with current zone-first unit decision implementation; manual QA not retained as active work per user cleanup request on 2026-06-07

## Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

No gameplay authority change is intended. This implements the accepted Runtime/AI boundary that a unit first consumes its selected combat zone or action zone, then decides whether to route toward the zone or join combat inside it.

## Scope

- Keep unit behavior common for player and enemy units; differences come from command, engagement rule, group state, and selected action zone.
- For a decision-ready unit outside its selected combat zone/action zone, preserve simple region movement toward the zone.
- For a decision-ready unit inside its selected combat zone, prefer executable combat-zone join opportunities over stale retained-target priority.
- Keep the current combat-zone generation boundary unchanged. The proposed `<=4` small-contact boundary is explicitly out of scope for this slice.

## Non-Goals

- Do not redesign combat-zone clustering or add the small-contact no-zone threshold.
- Do not split overlapping combat zones into a new selection model in this slice.
- Do not change skill release rules, effect execution, damage, or attack cadence.
- Do not add player-only or enemy-only behavior branches.

## Tests

- Add regression coverage showing an in-zone unit chooses an executable combat-zone join target before a retained but blocked target, without requiring a previous failure marker.
- Keep existing coverage for combat-zone outsiders routing to the zone before slot search.
- Keep existing retained-target stickiness coverage outside combat-zone scoped joining.

## Diagnostics

- Preserve low-noise logs. Existing area snapshots and movement failure diagnostics remain the manual QA entry point.
- Future diagnostics should read as: selected zone, inside/outside zone, chosen join target/slot, or named failure reason.

## Manual QA

- Re-run Bonefield and confirm an already-in-zone enemy unit joins the local fight instead of waiting for a retained target failure cycle.
- Confirm units outside the selected battle area still path toward the action zone before slot search.

## Acceptance Evidence

- RED verified: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed on `runtime combat-zone join ignores stale retained target priority` when the test required first-boundary selection of an executable combat-zone target without a prior retained-target failure marker.
- GREEN verified: the same command passed after implementing same-zone alternate join selection before emitting the retained-target blocked failure marker.
- Existing guards stayed green for combat-zone outsider routing, retained-target stickiness outside combat-zone scoped joining, blocked local-combat support diagnostics, stable slot intent, and `BattleRuntimeTickResolver` decomposition.
