# Battle Combat Zones And Group Action Zones Implementation Proposal

Status: In Progress

## Requirement Id

BCZ-GAZ-2026-06-03

## Originating Design Proposal

`design-proposals/archived/2026-06-03-battle-combat-zones-group-action-zones/`

## Accepted Authority

- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`

## Goal

Implement the first runtime slice of global combat zones and commander-owned group action zones so contact no longer leaves rear members in ordinary objective advance, and so diagnostics print complete area/unit distribution snapshots at zone rebuild times.

## Scope

- Add runtime global combat-zone snapshots computed from all living corps actors.
- Add commander-group action-zone snapshots for objective movement, combat join, support/hold, and selected combat-zone participation.
- Let engaged groups consume a selected global combat zone when building target/local-combat facts.
- Keep normal movement and combat-entry movement using the same movement validation while preserving distinct intent reasons.
- Add stable combat-slot intent for local-combat movement so a unit commits to an attack/support anchor and continues toward it until target, slot legality, occupancy, command, or local-combat scope invalidates it.
- Tighten local-combat position continuation so an actor that cannot stably progress toward an attack-capable position reselects a non-attack combat position or stops at an explicit blocked state instead of oscillating between adjacent cells.
- Replace per-candidate local-combat path/flow-field scoring with shared multi-goal combat-position fields keyed by combat-zone scope and actor capability group. Attack-position and non-attack join-position movement may still validate occupancy and reservations per actor, but selecting among many positions must not build one navigation field per candidate anchor.
- Treat target anchor changes, defeats, combat-zone membership changes, and command target changes as dirty-field events. Rebuild fields lazily at actor decision or movement-continuation boundaries; do not synchronously rebuild all opposing actors' attack fields during the same contact tick.
- Add low-noise area snapshot logs when combat zones are built and when group action zones are rebuilt.
- Cover deployment-zone bounds, combat-zone bounds, group action-zone bounds, unit positions, footprints, group ids, factions, plan states, and zone membership in the same diagnostic cluster.

## Non-Goals

- Do not add full LimboAI behavior trees.
- Do not redesign command UI.
- Do not change campaign persistence.
- Do not make global combat zones command units directly.
- Do not remove existing runtime validation, occupancy, reservation, or footprint rules.
- Do not redesign ranged attack-position generation in this slice. Local-combat position intent must not make the existing adjacent-slot assumption deeper, but attack-range-specific position generation remains a separate follow-up.

## Touched Systems

- Runtime tactical observation and state.
- Runtime target/local-combat context building.
- Runtime movement intent commit and continuation.
- Runtime navigation slot selection and current-occupancy pathfinding.
- Tactical region/action-zone snapshots.
- Battle runtime diagnostics.
- Target battle architecture regression tests.

## Tests

- Global combat zone regression: two front units in contact build one combat zone with expected bounds and no owner group.
- Group action-zone regression: each commander group gets an owned action zone and the log prints combat zones, deployment zones, group action zones, and unit positions together.
- Rear join regression: when a commander group has entered a combat zone, rear members stop ordinary objective advance and receive combat-zone movement/support/queue intent instead of continuing to the enemy deployment zone.
- Blocking regression: if a rear member is inside the selected combat zone but every executable entry is blocked, the failure remains named as `reject_no_reachable_slot` and is logged with the combat-zone id.
- Dynamic ingress regression: if the nearest attack-slot entrance is blocked by live footprints but another currently executable path exists inside the combat zone, the rear member must follow that dynamic path instead of reporting `reject_no_reachable_slot`.
- Stable combat-slot regression: with the bonefield 2x2 rear-unit formation around a retained 2x1 target, a rear unit must not alternate between two adjacent anchors; it must keep moving toward its assigned attack/support slot.
- Blocked attack-position regression: with four player units and three enemy units already fighting and one living enemy unable to stably enter an attack-capable position, the extra enemy must not oscillate between adjacent cells before any unit is defeated; it must either progress toward a stable non-attack combat position or stop without fake movement.
- Hot-area padding regression: under 2x2 rear-unit pressure, one global combat zone keeps all clustered participants, preserves full member footprints, and expands the fact bounds with configured join-space padding instead of clipping them to the local search cap.
- Local-combat performance regression: selecting an executable combat position from a large expanded combat zone builds at most a small fixed number of shared multi-goal fields for the actor capability group, instead of one field per candidate anchor.
- Dirty-field regression: moving a target actor invalidates the relevant opposing attack-position field version, but current movement execution still validates attack legality and occupancy from live Runtime facts.

## Diagnostics

- `BattleAreaSnapshot` records the rebuild reason and counts.
- `BattleCombatZoneSnapshot` records `zone`, `bounds`, `center`, `units`, `groups`, and reason.
- `BattleDeploymentZoneSnapshot` records authored deployment/objective zone bounds.
- `BattleGroupActionZoneSnapshot` records owner group, kind, bounds, target combat zone or target region, and reason.
- `BattleUnitPositionSnapshot` records actor id, group id, faction, anchor, footprint, combat-zone membership, group action-zone membership, and plan state.
- `BattleRuntimeCombatSlotIntent` records actor, retained target, local-combat situation id, slot kind, assigned slot anchor, first selected step, and reason only when the slot is newly assigned or reassigned.
- Local-combat position invalidation diagnostics should record when a stored combat position is rejected because it cannot be placed, is outside the selected combat zone, has no reachable path, or cannot produce a progress-making next step.

## Manual QA

- Start the bonefield battle with the V0 hero/corps setup.
- At first contact, confirm the log prints a full area snapshot around the engagement tick.
- Confirm the enemy 2x2 formation creates one combat zone covering front and rear join space.
- Confirm rear enemy units and the upper player unit stop ordinary objective advance and either join, support, queue, regroup, or emit a named blocked-entry diagnostic.
- Confirm rear units that cannot immediately enter an attack slot move toward a stable support/queue slot instead of alternating between two cells.
- Confirm an extra living unit at the edge of a full local fight either moves toward a stable non-attack combat position or stops; it must not keep playing movement between two adjacent cells while no one has died.
- Confirm deployment footprint overlap does not regress.

## Acceptance Evidence

- 2026-06-03 automated regression: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed. This covers the new global combat-zone and group action-zone regression, rear engaged-member intent regression, existing local-combat region scope regressions, movement/golden order regressions, and battle architecture guards.
- 2026-06-03 hot-area padding regression: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` first failed on `runtime combat zone keeps member footprints and hot-area padding under cap pressure` with `minX=32`, then passed after combat-zone bounds stopped clipping member footprints and hot-area padding to `DefaultLocalCombatMaxCells`.
- 2026-06-03 build: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-03 patch check: `git diff --check` passed with no whitespace errors. Git reported only line-ending normalization warnings for pre-existing edited system-design markdown files.
- 2026-06-04 dynamic ingress regression: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed after the blocked-ingress fixture was corrected to use live footprint blockers. This locks both outcomes: truly blocked local-combat ingress keeps `reject_no_reachable_slot`, while an available current dynamic detour starts movement instead of idling.
- 2026-06-04 build: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-04 stable slot regression: `runtime local combat movement keeps stable slot intent instead of oscillating` first failed with `bonefield:f6_draugarlord:3` alternating `(39,21)->(39,22)` and `(39,22)->(39,21)` against retained target `expedition:player_camp:1:army:f1_azuritelion:2`, then passed after local-combat movement stored and reused an assigned support slot anchor.
- 2026-06-04 stable slot verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed, then `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-04 patch check: `git diff --check` passed with no whitespace errors. Git reported only line-ending normalization warnings for pre-existing edited system-design markdown files.
- 2026-06-04 full local fight regression: `runtime full local fight does not oscillate extra living unit` first failed with `bonefield:f6_draugarlord:1` alternating `(35,18)->(36,19)` and `(36,19)->(35,18)` while every actor was still alive, then passed after local-combat movement required executable combat-position intent and rejected immediate reverse steps that undo the previous combat-position segment.
- 2026-06-04 full local fight verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed, then `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-04 live-frontline reroute regression: `runtime rear local combat unit routes before frontline defeat` first failed with `bonefield:f6_draugarlord:3` idling on `reject_no_reachable_slot` at `(38,17)` while all actors were alive, then passed after executable local-combat intent selection kept target-adjacent combat positions first and used combat-zone join positions only as a later movement fallback.
- 2026-06-04 live-frontline reroute verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed, then `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` and `git diff --check` passed.
- 2026-06-04 shared multi-goal field regression: `runtime local combat position selection uses shared multi-goal fields` first failed because `BattleCombatSlotIntentResolver` still had per-candidate scoring helpers (`TryScoreSlot` / `BuildScoredCandidates`), then passed after local-combat candidate groups began building one shared multi-goal field and sampling it through `FindNextStepCandidatesTowardCombatField`.
- 2026-06-04 shared multi-goal verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed, then `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors, and `git diff --check` passed with only a pre-existing CRLF normalization warning on `system-design/battle-runtime-architecture.md`.
- Manual QA still needs a presentation-backed bonefield battle run to confirm the area snapshot logs and visible rear-member join/support/queue behavior match the user-reported formation case.
