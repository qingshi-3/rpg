# Battle Navigation Soft-Zone Cache Optimization

Status: Archived - aligned with current battlefield-scoped navigation cache implementation; Bonefield manual QA not retained as active work per user cleanup request on 2026-06-07

## Relationship Metadata

Requirement Id: `REQ-BATTLE-NAVIGATION-SOFT-ZONE-CACHE-OPTIMIZATION`

Originating Design Proposal: None. This is an implementation slice against already accepted battle Runtime, AI, tactical-region, and navigation architecture.

Parent Implementation Proposal: `gameplay-alignment/implementation-proposals/archived/2026-06-06-battle-local-combat-stuck-recovery.md`

Related Implementation Proposals:

- `gameplay-alignment/implementation-proposals/archived/2026-06-06-zone-first-unit-decision-flow.md`
- `gameplay-alignment/implementation-proposals/archived/2026-06-06-battle-local-combat-stuck-recovery.md`

Supersedes: None

Superseded By: None

Amends: None

Amended By: None

Blocking Issues: Bonefield manual QA shows first-contact runtime spikes caused by full-topology flow-field rebuilds.

Verification Records: 2026-06-06 automated regression and solution build passed for the earlier soft-zone/cache slice. 2026-06-07 automated regression and solution build passed for the battlefield-scoped field update. Bonefield manual QA remains required before acceptance.

## Authority

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`

No gameplay authority change is intended. The accepted architecture already says non-engaged movement is region-directed, engaged combat uses bounded local combat, pathfinding remains Runtime authority, and dirty navigation data rebuilds lazily at actor decision or movement-continuation boundaries.

## Architectural Judgment

This is a Runtime Navigation and Tactical AI boundary cleanup, not a new combat-design rule.

Runtime Navigation owns topology, footprint legality, occupancy, reservation, flow fields, first-step ranking, and movement failure diagnostics. Tactical AI owns target or slot intent inside the current command and battle-group state. Local combat regions are decision context and preference data; they must not become a hard topology substitute that can make an otherwise legal route disappear.

The current code already creates one `BattleFlowFieldCache` per runtime advance in `BattleRuntimeTickResolver`, and `BattleFlowFieldCache.BuildKey` does not include `actorId`. Therefore the earlier diagnosis that every actor independently builds a target flow field is not the main current issue. The useful optimization is narrower: preserve lazy shared cache behavior, remove string-key allocation, expose better cache diagnostics, and stop local combat regions from hard-clipping search.

The 2026-06-07 first-contact spike diagnosis refines this further: field construction is still bounded by the full battle topology, so a combat-zone decision can run a Dijkstra-style expansion across all 2,133 Bonefield topology nodes. Combat zones and local combat regions must become the Runtime Navigation search and cache boundary for engaged movement. A flow field is battlefield-owned, not actor-owned: units select the field for their current battlefield and movement profile, while dynamic occupancy and same-tick reservations remain first-step validation facts outside the field key.

## Accepted And Rejected Parts Of The Reviewed Optimization Plan

| Reviewed Item | Decision | Reason |
|---|---|---|
| Shared flow fields | Keep current lazy shared-cache direction, but do not prebuild all fields | Runtime already shares a per-advance cache. Tick-start prebuild of every enemy x range can build unused fields and amplify work when targets move. |
| Soft zone constraints | Accept | Hard local-region filters in pathfinder, slot allocation, and slot-intent validation can produce false `path_not_found` or stale-slot invalidation when the topology route exists. Zone should rank choices, not delete them. |
| String cache keys -> struct keys | Accept | This keeps semantics while reducing hot-path allocation and making cache kind diagnostics easier. |
| LINQ sort removal in hot path | Accept | Sorting small movement/slot lists is frequent enough that `OrderBy().ThenBy().ToArray()` should be replaced with deterministic list/array sort helpers. |
| Dynamic occupancy tuple replacement | Defer | Replacing `Dictionary<BattleGridCoord, HashSet<string>>` with fixed tuples risks correctness around footprints and reserved cells. First remove `Any/Where/ToArray` allocations; consider a small-set structure only after profiling. |
| Short path cache stored as raw arrays on `BattleRuntimeActor` | Reject for this slice | Advisory path data must not become actor combat truth. A later slice can add a navigation-owned path cache if diagnostics prove it is needed. |
| Target movement triggers rebuild | Reject | Target movement may make a cached target/goal field stale, but it must not synchronously rebuild fields for every pursuer. Rebuild may happen only when a decision-ready actor consumes navigation and the relevant attackable-cell goal key has changed. |
| Full reservation window / WHCA* | Reject for this slice | Current authority is one-step reservation plus immediate edge-swap prevention. Longer reservation windows are a separate movement-system design. |

## Goal

Make local-combat navigation more stable and cheaper without changing player-facing battle rules: a unit still moves toward attackable cells for its retained target or selected slot, but local combat regions become a strong preference instead of a hard pathfinding wall, and flow-field/cache hot paths stop allocating avoidable strings and LINQ sort arrays.

The updated performance goal is to make first-contact flow-field work scale with the selected battlefield bounds plus padding, not the whole map. Units outside a combat zone move toward that combat zone as an ingress region. Units inside the zone use that battlefield's cached attack/support fields. Field keys must be stable and independent: they include topology/version, battlefield region identity and padded bounds, movement profile, goal kind, and goal anchors; they must not include actor id, actor current cell, dynamic occupancy, reservations, or presentation state.

## Scope

- Add focused diagnostics before behavioral changes so logs and counters can explain whether navigation failure came from topology, occupancy, reservation, local-region soft fallback, or cache behavior.
- Remove `localCombatRegion` hard rejects from:
  - `BattlePathfinder` neighbor and goal filtering;
  - `BattleCombatSlotAllocator` target-local slot scan;
  - `BattleCombatSlotIntentResolver` stored-slot validation and candidate filtering.
- Replace local-region hard filtering with deterministic soft penalties:
  - in-region attack/support slots remain preferred;
  - candidate first steps inside the selected local region remain preferred;
  - out-of-region slots or steps are allowed only when in-region executable choices are unavailable or worse by the configured penalty.
- Keep topology, occupancy, reservation, footprint, command scope, target validity, and attack-range validation as hard rules.
- Replace `BattleFlowFieldCache` string keys with typed value keys and split target, objective, and open-attack field caches by key type.
- Keep flow-field construction lazy. No tick-start prebuild of all target fields.
- Reuse the current runtime-advance flow-field cache across stale-target retargeting when the retarget path is still inside the same advance.
- Build local-combat and combat-join flow fields inside battlefield-scoped search bounds:
  - selected combat zone or local combat region bounds plus deterministic padding;
  - no actor-id or actor-current-cell binding in the field key;
  - units outside the action zone keep using region ingress movement toward the battlefield instead of target-slot field construction;
  - scoped fields may fall back to full-topology search only when the bounded field cannot reach a legal goal, and that fallback must be diagnostic-visible.
- Maintain a bounded dirty-build policy for future expansion:
  - target battlefield search size should stay around 300-600 nodes when possible;
  - a scoped battlefield should not exceed roughly 900 nodes without a named fallback or budget warning;
  - normal runtime should not rebuild more than one or two dirty battlefield fields in the same tick.
- Remove hot-path LINQ sorting/allocation in movement candidate, combat-slot, flow-field goal, and movement-commit ordering where the comparator is already deterministic.
- Reduce low-risk occupancy allocations by replacing `Any`, `Where`, and `ToArray` in `BattleDynamicOccupancy` query methods with explicit loops and reusable empty arrays.

## Non-Goals

- Do not redesign combat-zone clustering, enemy target-region policy, or battle-group engagement rules.
- Do not add the `<=4` small-contact no-zone rule in this slice.
- Do not prebuild every target/range flow field at tick start.
- Do not rebuild a path or flow field just because a target actor moved, another render frame elapsed, or a pursuer changed anchor. Rebuild is allowed only at Runtime decision or movement-continuation boundaries when the consumed goal key is stale.
- Do not add long-horizon path reservations, WHCA*, velocity-obstacle steering, or continuous physics avoidance.
- Do not bake dynamic occupancy into flow fields. Occupancy and same-tick reservations remain first-step Runtime validation.
- Do not store raw path arrays on `BattleRuntimeActor` as authoritative runtime state.
- Do not bind field results to a unit. Unit id and actor anchor are not field-key dimensions.
- Do not use full-map target-slot fields for engaged local-combat movement unless a bounded battlefield field explicitly fails and logs the fallback.
- Do not change skill release, effect execution, damage, attack cadence, cooldown, or settlement behavior.

## Touched Systems And Files

Navigation behavior:

- Modify `src/Runtime/Battle/Navigation/BattlePathfinder.cs`
- Modify `src/Runtime/Battle/Navigation/BattleCombatSlotAllocator.cs`
- Modify `src/Runtime/Battle/Navigation/BattleCombatSlotIntentResolver.cs`
- Modify `src/Runtime/Battle/Navigation/BattleCrowdMovementPlanner.cs`
- Create or modify a focused region-preference helper under `src/Runtime/Battle/Navigation/`

Flow-field cache and performance counters:

- Modify `src/Runtime/Battle/Navigation/BattleFlowFieldCache.cs`
- Modify `src/Runtime/Battle/Navigation/BattleFlowFieldBuilder.cs`
- Modify `src/Infrastructure/Diagnostics/BattlePerformanceCounters.cs`
- Modify `src/Runtime/Battle/BattleRuntimeSpikeDiagnostics.cs`
- Modify `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- Modify `src/Runtime/Battle/BattleStaleAdvanceRetargeting.cs`

Allocation cleanup:

- Modify `src/Runtime/Battle/BattleMovementCommitResolver.cs`
- Modify `src/Runtime/Battle/Navigation/BattleDynamicOccupancy.cs`
- Modify the same navigation files listed above where they currently use LINQ sorting in Runtime hot paths.

Regression tests:

- Modify `tests/TargetBattleArchitectureRegression/TargetBattleNavigationRegressionCases.cs`
- Modify `tests/TargetBattleArchitectureRegression/TargetBattleMultiUnitNavigationRegressionCases.cs`
- Modify `tests/TargetBattleArchitectureRegression/TargetBattlePerformanceRegressionCases.cs`
- Modify `tests/TargetBattleArchitectureRegression/Program.cs`

## Implementation Plan

1. Add RED regression coverage for the intended behavior before changing navigation.
   - A tight local-combat region should not produce `path_not_found` when a legal topological route exists just outside the region.
   - When both in-region and out-of-region executable choices exist, the in-region choice should win.
   - When all in-region choices are dynamically blocked, an out-of-region fallback may be selected and should log a soft-region fallback reason.
   - Source guards should fail while `BattleFlowFieldCache` still uses `Dictionary<string, BattleFlowField>` or `string.Join`.
   - Source guards should fail while listed Runtime hot paths still use `OrderBy().ThenBy().ToArray()` for movement or slot ordering.

2. Introduce a single local-region preference helper.
   - The helper should expose `Contains(anchor, region)` and a deterministic penalty such as `LocalRegionExitPenalty`.
   - The helper is preference-only. It must not validate topology, occupancy, reservation, command scope, or attack range.
   - A short comment must state why local regions are not hard pathfinding authority.

3. Convert local-region hard clips to soft penalties.
   - In `BattlePathfinder`, remove local-region neighbor and goal rejects. Add region penalties to cost or priority so in-region search remains preferred.
   - In `BattleCombatSlotAllocator`, keep target-local slot enumeration, but add region penalty to slot priority instead of skipping outside-region anchors.
   - In `BattleCombatSlotIntentResolver`, a stored slot must fail only for graph, occupancy, target/slot semantic invalidity, or explicit command/scope invalidity; it must not fail solely because the local region shifted.
   - In `BattleCrowdMovementPlanner`, add a first-step region penalty when ordering move options, while keeping reservation and footprint validation hard.

4. Replace flow-field string keys with typed keys.
   - Use separate dictionaries for target fields, objective fields, and open-attack fields.
   - Target-field keys should preserve the current semantic dimensions: target id, target anchor, actor footprint, attack range, support preference, and local-region identity/bounds while region affects scoring.
   - Objective-field keys should preserve objective anchor, size, and actor footprint.
   - Open-attack keys should compare ordered goal anchors without building joined strings.
   - Existing cache behavior must stay lazy and per-runtime-advance.

5. Expand cache and soft-zone diagnostics.
   - Counters should distinguish target, objective, and open-attack field hits/misses/builds, or at least expose field kind in the recording path and spike log.
   - Add counters or low-noise diagnostics for local-region soft fallback selection.
   - Spike diagnostics should include enough field-kind and fallback data to avoid manual log archaeology.

6. Add battlefield-scoped field construction.
   - Introduce a flow-field search scope derived from the selected combat zone or local combat region plus padding.
   - Thread the scope through target fields, open-attack fields, and direct multi-goal slot fields.
   - Add a cached goal-field entry so `BattleCombatSlotIntentResolver` and combat-slot movement do not bypass the cache.
   - Keep objective/ingress movement allowed to use objective-region fields; units outside the combat zone should reach the zone before local slot selection.
   - Record scoped search-node counts so spike diagnostics can show whether a build was full-topology or battlefield-scoped.

7. Remove avoidable hot-path allocations.
   - Replace repeated `OrderBy().ThenBy().ToArray()` movement/slot ordering with `List<T>.Sort`, `Array.Sort`, or local static comparers.
   - Avoid `.Select(...).ToArray()` when the caller can consume a sorted list or a pre-sized array.
   - Replace `BattleDynamicOccupancy.IsOccupiedByOther` LINQ `Any` and `GetOtherOccupants` `Where().ToArray()` with explicit loops.
   - Do not replace occupancy storage with a tuple structure in this slice.

8. Keep moving-target chase semantics bounded.
   - Retained-target movement still moves toward attackable cells, not the target center.
   - A target-position change may make a consumed field key stale, but no passive target-moved event should rebuild fields.
   - Rebuild happens only when a decision-ready or movement-continuing actor asks for navigation and the current target attackable-cell goal key differs from the cached key.

## Tests

Target battle architecture regression must cover:

- tight local-combat region no longer causing `path_not_found` when a legal topology route exists outside the region;
- in-region slot/step preference beating out-of-region alternatives when both are executable;
- out-of-region fallback selected only when in-region attack/support entry is unavailable, with a named soft-region diagnostic;
- stored combat-slot intent not invalidated solely by local-region boundary movement;
- moving-target pursuit does not trigger path/field rebuild outside actor decision or movement-continuation boundaries;
- `BattleFlowFieldCache` no longer using string keys or `string.Join`;
- hot movement/slot ordering paths no longer using LINQ sort chains;
- existing shared multi-goal flow-field guard remains green;
- battlefield-scoped flow fields do not expand across a full large topology when a local combat region is provided;
- direct combat-slot candidate-group selection uses the shared flow-field cache instead of bypassing it;
- field cache keys do not include actor id or actor current cell;
- scoped field diagnostics expose search-node counts and scoped/full fallback counts;
- combat-zone outsider movement uses ingress region movement before local slot field construction;
- existing no-overlap, reservation rejection, local-combat support degradation, and objective-first movement regressions remain green.

Run after implementation:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Diagnostics

- Runtime logs remain low-noise. Do not log every search node, every frame, every hover update, or every movement progress tick.
- Existing runtime log location remains `C:\Users\qs\AppData\Roaming\Godot\app_userdata\rpg\logs\`.
- Navigation failure diagnostics should make these facts visible when relevant:
  - selected combat zone and local region bounds;
  - actor anchor, target anchor, actor footprint, target footprint, and attack range;
  - whether a hard failure came from topology, footprint legality, occupancy, reservation, target invalidity, or no reachable attack/support goal;
  - whether a legal out-of-region fallback was selected because in-region choices were blocked.
- Spike diagnostics should expose flow-field cache behavior by field kind and local-region soft fallback counts so performance regressions can be read from one summary line.
- Spike diagnostics should expose scoped search-node counts and scoped/full fallback counts so a first-contact stall can be attributed to either full-topology fallback or excessive dirty battlefield rebuilds.

## Manual QA

Bonefield runtime:

1. Start the current battle scenario and let both sides reach first contact.
2. Confirm the previously stuck enemy unit either joins combat, holds a named support/queue role, or selects an explicit soft-region fallback route instead of silently freezing.
3. Confirm normal nearby units still prefer in-region combat movement and do not scatter away from the active fight when in-region entries are available.
4. Confirm no visible overlap, same-tick cell swap, or fake movement into occupied cells occurs.
5. Confirm logs include area snapshots and, when relevant, soft-region fallback or hard navigation failure reasons.
6. Confirm runtime spike logs do not show increased flow-field builds from tick-start prebuild or passive target movement.

## Acceptance Evidence

2026-06-06 automated verification:

- `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`: passed. The run included the soft-zone, flow-field cache, hot-path allocation, Bonefield pacing, hero skill, and settlement regressions.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `dotnet build-server shutdown`: completed and shut down MSBuild plus VB/C# compiler servers.

2026-06-07 automated verification:

- `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`: passed. The run included the large-topology combat-join scoped field regression and shared goal-field cache guards.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: passed with 0 warnings and 0 errors.
- `dotnet build-server shutdown`: completed and shut down MSBuild plus VB/C# compiler servers.

Manual Bonefield QA remains pending, so this proposal is not yet accepted.
