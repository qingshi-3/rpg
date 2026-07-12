# Enemy Region-Directed Combat AI Implementation Proposal

Status: Archived - accepted; manual QA waived by user request on 2026-05-31
Created: 2026-05-29

Originating Design Proposal:
- `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai`

Requirement Id:
- `battle-ai-enemy-region-directed-combat-v1`

Parent / Superseded Records:
- Amends historical design records: `design-proposals/archived/2026-05-23-battle-plan-state-machine`, `design-proposals/archived/2026-05-27-local-combat-situation-ai`.
- Supersedes enemy-behavior acceptance from `gameplay-alignment/implementation-proposals/archived/2026-05-23-battle-plan-state-machine.md` and `gameplay-alignment/implementation-proposals/archived/2026-05-28-local-combat-situation-ai.md`.
- Does not supersede player battle-plan authority or reusable local-combat facts from those historical records.

Authority Documents:
- `gameplay-design/content-systems-long-term-design.md`
- `gameplay-design/details/combat-command/README.md`
- `system-design/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/world-battle-entry-architecture.md`
- `system-design/semantic-map-marker-architecture.md`

## Goal

Implement battle-group-owned tactical regions and enemy region-directed combat AI so enemy non-engaged movement targets fixed or temporary regions, enemy engaged combat uses bounded group-local combat regions, hold defenders activate as a whole group, and player command/plan authority remains untouched by enemy-only policy.

## Architecture Judgment

This is Battle Runtime, AI, Navigation, and battle-entry work.

- Runtime owns live battle-group tactical state, engagement transitions, current target regions, temporary target regions, local combat regions, movement validation, reservations, damage, events, and outcome.
- Application / battle entry seeds initial enemy tactical mode and fixed-region facts from the current battle request, authored deployment zones, objective zones, battle kind, side, and placement facts.
- Navigation owns reachability, region route scoring, attack-slot legality, support-slot legality, occupancy, and reservations.
- AI consumes typed group tactical facts and returns typed intent; it does not mutate Runtime truth.
- Presentation and LimboAI may expose the same decision facts, debug overlays, and behavior-tree branches through the facade, but they must not create tactical region truth, movement truth, damage truth, or player command truth.

The implementation must extend the current battle-plan, local perception, local-combat, and attack-slot Runtime path. It must not introduce a parallel global tactical director or revive per-unit global target scoring as ordinary movement.

## Current Implementation Facts

These facts should be rechecked at implementation start because code may move, but they define the expected starting point for this proposal:

- `BattleStartRequest` already carries `ObjectiveZones`, `PlayerBattleGroupPlans`, and `EnemyBattleGroupPlans` in `src/Application/Battle/BattleStartRequest.cs`.
- `BattleGroupPlanSnapshot` currently carries one objective anchor and engagement rule in `src/Application/Battle/Snapshots/BattleGroupPlanSnapshot.cs`.
- `BattleObjectiveZoneSnapshot` currently carries authored region geometry, role, deployment side, faction id, and priority in `src/Application/Battle/Snapshots/BattleObjectiveZoneSnapshot.cs`.
- Runtime actors already carry `BattleGroupId`, objective anchor fields, engagement rule, retained target state, and plan state in `src/Runtime/Battle/BattleRuntimeActor.cs`.
- Runtime target acquisition currently has actor-level plan-scoped sensing in `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs` and AI action selection in `src/Runtime/Battle/BattleRuntimeTickResolver.Ai.cs`.
- Local combat facts already exist under `src/Runtime/Battle/Tactics/LocalCombatSituation.cs` and `src/Runtime/Battle/Tactics/LocalCombatSituationBuilder.cs`, but they need explicit battle-group ownership, bounded local-region extent, perception-coverage weighting, and group-level engagement integration.

## Scope

### Phase 1: Data Contracts And Tunables

Add explicit battle-only tactical-region contracts without campaign persistence.

Files to create:
- `src/Application/Battle/Snapshots/BattleGroupTacticalMode.cs`
- `src/Application/Battle/Snapshots/BattleTacticalRegionKind.cs`
- `src/Application/Battle/Snapshots/BattleTacticalRegionSnapshot.cs`
- `src/Runtime/Battle/Tactics/BattleGroupEngagementState.cs`
- `src/Runtime/Battle/Tactics/BattleGroupTacticalState.cs`
- `src/Runtime/Battle/Tactics/BattleGroupTacticalPolicySettings.cs`
- `src/Runtime/Battle/Tactics/BattleGroupTacticalReasonCode.cs`

Files to modify:
- `src/Application/Battle/Snapshots/BattleGroupPlanSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleGroupSnapshot.cs`
- `src/Runtime/Battle/BattleRuntimeState.cs`
- `src/Runtime/Battle/BattleRuntimeActor.cs`

Actor authority boundary:
- `BattleRuntimeActor` may keep actor-local execution facts such as anchor, current phase, retained target id, command id, objective anchor copy, and action locks.
- `BattleRuntimeActor` must not own authoritative target region, temporary target region, local combat region, or group engagement truth. Those facts are owned by `BattleGroupTacticalStateStore` after Phase 3.
- If actor fields mirror group tactical facts for diagnostics or event emission, they must be derived snapshots and must not be mutation sources.

Required contract shape:

```csharp
public enum BattleGroupTacticalMode
{
    PlayerCommanded = 0,
    EnemyOffense = 1,
    EnemyActiveDefense = 2,
    EnemyHoldDefense = 3
}

public enum BattleTacticalRegionKind
{
    FixedTarget = 0,
    TemporaryTarget = 1,
    LocalCombat = 2,
    Hold = 3,
    Retreat = 4,
    Protect = 5
}

public sealed class BattleTacticalRegionSnapshot
{
    public string RegionId { get; set; } = "";
    public string OwnerBattleGroupId { get; set; } = "";
    public BattleTacticalRegionKind Kind { get; set; }
    public string SourceRegionId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public int CenterCellX { get; set; }
    public int CenterCellY { get; set; }
    public int CenterCellHeight { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public int Version { get; set; }
}
```

First-slice tunables:

- `BattleGroupTacticalPolicySettings.DefaultTemporaryRegionRefreshTicks = 5`.
- `BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange = BattlePerceptionPolicy.DefaultLocalPerceptionRange`.
- `BattleGroupTacticalPolicySettings.DefaultLocalCombatMaxCells = 64`.
- `BattleGroupTacticalPolicySettings.DefaultDisengageGraceTicks = 1`.
- `BattleGroupTacticalPolicySettings.MinimumRegionWidth = 1` and `MinimumRegionHeight = 1`.

Acceptance for this phase:
- A battle group cannot own a target, temporary, or local combat region unless `OwnerBattleGroupId` is non-empty and matches the group.
- Tactical modes are battle-only snapshot/runtime facts and are not written into campaign persistence.
- Existing player plan snapshots still deserialize and initialize without requiring enemy tactical fields.

### Phase 2: Battle Entry Seeding

Seed enemy tactical mode and initial fixed target-region facts before Runtime starts.

Files to modify:
- `src/Application/Battle/BattleStartRequest.cs`
- `src/Application/Battle/BattleGroupSessionProbeService.cs`
- `src/Application/Battle/Adapters/LegacyBattleStartSnapshotAdapter.cs`
- `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`
- `src/Application/Battle/Snapshots/BattleGroupSnapshot.cs`
- `src/Presentation/World/Sites/WorldSiteBattleDeploymentPreparer.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattlePreparation*.cs` where deployment rows, player plan drafts, or semantic marker facts are submitted to Application-owned request builders.

Presentation boundary:
- Presentation may display, edit, and submit deployment rows, player plan drafts, and semantic marker facts through existing Application request paths.
- Presentation must not choose enemy tactical mode, select enemy fixed target regions, generate temporary target regions, or mutate Runtime tactical state.
- Enemy tactical mode and fixed-region seed facts are produced by Application battle-entry/request-building code from pure request, marker, placement, and side facts.

Seeding rules:
- Player battle groups default to `PlayerCommanded` and keep their accepted battle plan or live command as the only source of target-region intent.
- Enemy attacker groups default to `EnemyOffense`.
- Enemy resident defender groups with authored hold posture default to `EnemyHoldDefense` and use their current deployed region as hold region.
- Enemy defender groups marked active or without hold posture default to `EnemyActiveDefense`.
- Enemy offense fixed target candidates are player defensive deployment regions first.
- Enemy active defense fixed target candidates are player offensive deployment regions first.
- If a map lacks the required deployment-region marker, seed no fixed region, emit a diagnostic reason, and let Runtime create a temporary region only at a valid replan boundary.

Initial fixed-region scoring:

```text
score = opposingAliveActorsInsideRegion * 1000
      + authoredRegionPriority * 10
      - approximateDistanceFromGroupAnchorToRegionCenter
```

Tie-break order:

1. higher score;
2. lower approximate distance;
3. lexicographically smaller `RegionId`.

Acceptance for this phase:
- Normal assault with two player deployment regions chooses the region containing more player units.
- If both fixed regions contain equal player count, priority and then distance choose deterministically.
- Player-side plan snapshots remain unchanged when enemy tactical seeding runs.

### Phase 3: Runtime Group Tactical State Store

Add one Runtime-owned tactical state per battle group and a read-only snapshot cache keyed by group id.

Files to create:
- `src/Runtime/Battle/Tactics/BattleGroupTacticalStateStore.cs`
- `src/Runtime/Battle/Tactics/BattleGroupTacticalSnapshotCache.cs`
- `src/Runtime/Battle/Tactics/BattleGroupPerceptionSummary.cs`
- `src/Runtime/Battle/Tactics/BattleGroupPerceptionSummaryBuilder.cs`

Files to modify:
- `src/Runtime/Battle/BattleRuntimeSession.cs`
- `src/Runtime/Battle/BattleRuntimeSessionController.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Diagnostics.cs`
- `src/Runtime/Battle/Events/BattleEventKind.cs`
- `src/Runtime/Battle/Events/BattleEvent.cs`

Runtime ownership rules:
- `BattleGroupTacticalStateStore` is the only Runtime owner of `BattleGroupTacticalState` mutation.
- `BattleGroupTacticalSnapshotCache` stores immutable or versioned snapshots indexed by `BattleGroupId`; it is query/diagnostic-only and cannot mutate group intent.
- A missing owner id, mismatched owner id, unreachable region, or player-policy overwrite attempt rejects the proposed region and emits a low-noise diagnostic.
- Player-policy overwrite rejection is part of the first store mutation API, not a late Phase 8 addition. `PlayerCommanded` groups reject enemy-policy region mutation from the moment the store exists.
- Unit-level action logic reads group state and actor facts; it does not independently enter or exit group engagement.

Perception summary rules:
- Build one perception summary per battle group from alive group members.
- A hostile actor is perceived by the group if at least one alive group member can perceive it using `BattlePerceptionPolicy.DefaultLocalPerceptionRange` and current grid/height gap rules.
- The summary records perceived hostile ids, per-member coverage anchors, group anchor bounds, and last built Runtime tick.

Acceptance for this phase:
- Runtime initializes one `BattleGroupTacticalState` per battle group from battle-only snapshot facts.
- Owner validation rejects missing or mismatched region owners before any policy can use the region.
- Group perception summaries are built from alive members and expose perceived hostile ids without changing engagement state yet.
- `PlayerCommanded` groups reject enemy-policy target-region mutation through the store API.
- A globally cached tactical-region snapshot cannot be used to mutate player or enemy group intent.

### Phase 4: Enemy Fixed Region Policy And Non-Engaged Movement

Replace enemy ordinary non-engaged actor target chasing with group region movement.

Files to create:
- `src/Runtime/Battle/Tactics/EnemyBattleGroupRegionPolicy.cs`
- `src/Runtime/Battle/Tactics/BattleFixedTargetRegionSelector.cs`
- `src/Runtime/Battle/Tactics/BattleRegionMovementGoal.cs`

Files to modify:
- `src/Runtime/Battle/BattleRuntimeTickResolver.Plan.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Ai.cs`
- `src/Runtime/Battle/AI/BattleRuntimeAiActionKind.cs`
- `src/Runtime/Battle/AI/BattleRuntimeAiActionRequest.cs`
- `src/Runtime/Battle/AI/BattleRuntimeAiDecisionFacts.cs`
- `src/Runtime/Battle/Navigation/BattleCrowdMovementPlanner.cs`
- `src/Runtime/Battle/Navigation/BattleFlowFieldCache.cs`

Movement rules:
- Non-engaged enemy groups request movement to their current fixed or temporary target region.
- Non-engaged enemy movement must not set a moving actor as the movement objective.
- Region movement may reuse the existing objective-zone routing path after it is generalized to `BattleRegionMovementGoal`.
- Existing player objective movement remains valid and should call the same region movement helper with player-owned objective plan facts.
- Retained actor targets are cleared when the group exits engaged state and returns to region movement.

Required refactor:
- Rename or wrap `BuildObjectiveAdvanceContext` into a region-goal path that accepts `BattleRegionMovementGoal` while preserving existing objective behavior.
- Add `BattleRuntimeAiActionKind.AdvanceTowardRegion` or extend the existing advance action with a region goal. Do not overload actor target ids as region ids.
- Keep attack-slot flow fields target-specific and build them only when the group is engaged and a concrete target/slot approach is valid.

Acceptance for this phase:
- A non-engaged enemy group advances toward a fixed region even if a player unit moves elsewhere outside perception.
- Replanning the fixed region happens only at group replan boundaries, not every render frame or every unit movement segment.
- Player objective movement still follows player command or accepted battle plan and is not rewritten by enemy policy.

### Phase 5: Engagement Triggers And Hold Defense Activation

Move engagement state transitions to the battle-group level.

Files to create:
- `src/Runtime/Battle/Tactics/BattleGroupEngagementStateMachine.cs`

Files to modify:
- `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Ai.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Diagnostics.cs`
- `src/Runtime/Battle/Events/BattleEventKind.cs`
- `src/Runtime/Battle/Events/BattleEvent.cs`

Entry triggers:
- Enemy offense and enemy active defense enter engaged state when group perception summary has at least one perceived player unit.
- Enemy hold defense remains in hold region until any alive member perceives a player unit, any member receives damage, or any member performs an attack.
- When hold defense activates, the whole group switches to active assault. V1 does not require return-to-defense after activation.

Exit triggers:
- A group exits engaged state only after the whole group has no perceived hostile for `DefaultDisengageGraceTicks` and no recent damage/attack trigger remains active.
- On exit, clear per-actor target locks for members of that group and request target-region replanning.
- If all fixed target regions contain no relevant opposing units, request temporary region generation at the configured refresh boundary.

Acceptance for this phase:
- Damaging one hold defender switches all members of that battle group to active assault.
- Perceiving one player unit switches the whole hold group to active assault.
- A single unit losing sight does not exit engagement while another member still perceives a hostile.
- Exit clears stale target locks and resumes region movement.

### Phase 6: Temporary Target Region Generation

Generate per-group temporary target regions only when fixed target regions contain no relevant opposing units.

Files to create:
- `src/Runtime/Battle/Tactics/BattleTemporaryTargetRegionBuilder.cs`
- `src/Runtime/Battle/Tactics/BattleOpposingClusterBuilder.cs`

Files to modify:
- `src/Runtime/Battle/Tactics/EnemyBattleGroupRegionPolicy.cs`
- `src/Runtime/Battle/Tactics/BattleGroupTacticalStateStore.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Diagnostics.cs`

Cluster rules:
- Input is alive opposing actors, current topology, and owning group id.
- Build deterministic clusters by square-grid distance. First-slice cluster merge range is `BattlePerceptionPolicy.DefaultLocalPerceptionRange * 2`.
- A temporary region center is the median occupied anchor of the selected cluster; width/height are the cluster bounds clamped to `DefaultLocalCombatMaxCells` where applicable.
- Score clusters by actor count, then total hit points, then distance from owning group anchor, then deterministic region id.
- Cache the result on the owning group state with `LastTemporaryRegionRefreshTick`.
- Refresh only when `currentTick - LastTemporaryRegionRefreshTick >= DefaultTemporaryRegionRefreshTicks` or no valid temporary region exists.

Acceptance for this phase:
- If all player units leave fixed deployment/objective regions, enemy groups choose a temporary region from player clusters.
- Temporary region is owner-scoped and does not become a global map fact.
- With refresh interval `5`, ticks `1-4` reuse the cached temporary region, and tick `5` may rebuild it.
- Dispersed player units produce deterministic cluster choice and deterministic tie-breaks.

### Phase 7A: Bounded Local Combat Region Build And Storage

Upgrade local combat from actor/target observation to a battle-group-owned bounded local combat region before AI, Navigation, or facade consumers depend on it.

Files to modify:
- `src/Runtime/Battle/Tactics/LocalCombatSituation.cs`
- `src/Runtime/Battle/Tactics/LocalCombatSituationBuilder.cs`
- `src/Runtime/Battle/Tactics/LocalCombatDecisionReason.cs`
- `src/Runtime/Battle/Tactics/BattleGroupTacticalStateStore.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Diagnostics.cs`

Local region build algorithm:

1. For the owning battle group, enumerate alive members and each member's perception cells.
2. Add `+1` weight for each member covering a cell; overlapping perception cells therefore score higher.
3. Candidate centers are group member anchors, perceived hostile anchors, and the top weighted cells.
4. Evaluate bounded candidate rectangles whose area is `<= DefaultLocalCombatMaxCells`.
5. Score each rectangle by weighted covered cells, perceived hostile count, route-blocking status, and existing region stability.
6. Choose the highest score; tie-break by lower distance to group centroid, then deterministic center coordinates.
7. Store the selected local combat region on the owning group's `BattleGroupTacticalState` and expose it through `LocalCombatSituation.OwnerBattleGroupId`, `RegionId`, `Center`, `Width`, `Height`, `Version`, and reason code.

Acceptance for this phase:
- Local combat region records owner group id and bounded extent.
- Two overlapping member perception ranges increase the selected region score compared with either member alone.
- The selected local combat region never exceeds `DefaultLocalCombatMaxCells`.
- Rebuild diagnostics include `local_region_built_perception_overlap` or a rejection/degradation reason.

### Phase 7B: Local Target, Attack-Slot, And Support-Slot Solver Consumption

Use the stored local combat region for engaged target choice, attack-slot choice, support-slot choice, and fallback behavior.

Files to modify:
- `src/Runtime/Battle/AI/BattleRuntimeAiDecisionFacts.cs`
- `src/Runtime/Battle/AI/DefaultBattleRuntimeAiExecutor.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Ai.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- `src/Runtime/Battle/Navigation/BattleCombatSlotAllocator.cs`
- `src/Runtime/Battle/Navigation/BattleCrowdMovementPlanner.cs`

Local-optimal solver rules:
- Inside engaged state, target choice, attack-slot choice, support-slot choice, and fallback consider only actors and slots inside the owning group's local combat region plus retained target validation.
- Open attack slots are preferred over support slots.
- Support slots use existing roles `MeleeQueue`, `LineHold`, and `RangedHold` and remain deterministic.
- If no reachable target, attack slot, or support slot exists inside the local region, the group degrades to hold/regroup inside the local region until exit conditions or replan conditions apply.

Acceptance for this phase:
- A far hostile outside the local region is ignored while the group is engaged unless it becomes part of a later valid local region rebuild.
- Attack/support slot reservations still prevent multiple actors from claiming the same destination.
- Solver diagnostics include the local combat region id, owner group id, and selected target/slot or degradation reason.

### Phase 7C: Presentation And LimboAI Read-Only Facade Exposure

Expose local combat region facts to Presentation and LimboAI only as read-only observations after Runtime owns and validates them.

Files to modify:
- `src/Presentation/Battle/AI/BattleAiDecisionFacts.cs`
- `src/Presentation/Battle/AI/BattleAiDecisionResult.cs`
- `src/Presentation/Battle/AI/BattleAiFacade.cs`
- `src/Presentation/Battle/AI/BattleAiFacadeCore.cs`

Facade rules:
- Presentation and LimboAI receive local combat region id, owner battle-group id, selected target id, selected slot facts, and reason codes as observations.
- Presentation and LimboAI must not create, resize, refresh, or mutate target regions, temporary regions, local combat regions, or engagement state.
- Facade results remain typed intent requests; Runtime remains the final validator.

Acceptance for this phase:
- Facade facts match Runtime-owned local combat region snapshots.
- No Presentation or LimboAI path can mutate group tactical state.
- Debug/overlay output can explain local-region and slot decisions without creating tactical truth.
### Phase 8: Player Policy Separation

Finalize player-policy separation after the store and enemy policy already reject player-overwrite attempts in earlier phases.

Files to modify:
- `src/Runtime/Battle/Tactics/BattleGroupTacticalStateStore.cs`
- `src/Runtime/Battle/Tactics/EnemyBattleGroupRegionPolicy.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Ai.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- `src/Runtime/Battle/AI/BattleRuntimeAiDecisionFacts.cs`
- `src/Presentation/Battle/AI/BattleAiFacadeCore.cs`

Rules:
- Reusable services may build perception summaries, local combat regions, target candidates, attack slots, support slots, and rejection reasons for any side.
- `EnemyBattleGroupRegionPolicy` may mutate region intent only for enemy tactical modes; this rule must already be enforced by the Phase 3 store API and Phase 4 enemy policy, and Phase 8 only hardens remaining integration surfaces.
- Player target region, posture, objective, hold, protect, or retreat intent changes only through accepted player commands or accepted battle plans.
- A player group may enter local combat response inside its command scope, but automatic enemy policy must not replace its current region.

Acceptance for this phase:
- A player group with an accepted objective keeps that objective after enemy region policy runs.
- A player hold/protect/retreat command is not converted into enemy-style active assault.
- The same local combat solver can provide facts to player AI/facade without owning player command state.

## Non-Goals

- No defender return-home behavior after hold activation in V1.
- No campaign persistence for tactical regions, local combat regions, engagement state, or temporary target regions.
- No global tactical director, global best-target scan, or global best-attack-position scan as ordinary movement.
- No per-frame behavior-tree execution or per-render-frame replan.
- No morale, multi-front strategy director, reinforcement director, advanced formation planner, or commander personality policy in this slice.
- No rewrite of settlement, damage, defeat, or battle outcome authority.
- No Presentation-owned movement, occupancy, reservation, damage, tactical-region, or command truth.
- No new player live-command feature beyond preserving and validating existing command/plan authority.

## Diagnostics

Add low-noise diagnostics for state transitions and failure paths. Each implementation phase must add its own reason codes and regression checks before later phases consume those facts. Prefer `BattleEventStream` for meaningful battle facts and `GameLog.Info` / custom monitor counters for diagnostics that should not become battle report truth.

Required reason codes:
- `region_fixed_selected_player_density`
- `region_fixed_selected_priority`
- `region_fixed_missing_candidates`
- `region_unreachable`
- `region_rejected_missing_owner`
- `region_rejected_owner_mismatch`
- `region_rejected_player_policy_overwrite`
- `temporary_region_created_cluster`
- `temporary_region_reused_refresh_interval`
- `temporary_region_refreshed_interval_elapsed`
- `engagement_enter_group_perception`
- `engagement_enter_member_damaged`
- `engagement_enter_member_attacked`
- `engagement_exit_no_group_perception`
- `local_region_built_perception_overlap`
- `local_region_rejected_over_cap`
- `local_region_degrade_no_reachable_slot`

Required events or diagnostics:
- `BattleGroupTacticalRegionSelected`
- `BattleGroupTemporaryRegionSelected`
- `BattleGroupEngagementStateChanged`
- `BattleGroupLocalCombatRegionChanged`
- `BattleGroupRegionMovementRejected`

Required monitor counters:
- `Battle/TacticalRegionSelections`
- `Battle/TemporaryRegionBuilds`
- `Battle/TemporaryRegionCacheHits`
- `Battle/LocalCombatRegionBuilds`
- `Battle/EngagementStateTransitions`
- `Battle/RejectedTacticalRegionMutations`

## Tests

Add tests before implementation where practical. Each phase should land its relevant regression cases with the phase that introduces the state or policy, not defer all tests to the end. Keep tests deterministic and focused on Runtime/Application facts rather than presentation timing.

Target test project:
- `tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj`

Files to create:
- `tests/TargetBattleArchitectureRegression/TargetBattleGroupTacticalRegionRegressionCases.cs`

Files to modify:
- `tests/TargetBattleArchitectureRegression/Program.cs`
- `tests/TargetBattleArchitectureRegression/BattleNavigationTestTopology.cs` if new region-shape test helpers are needed.
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattlePerception.cs` only if battle-entry seeding requires site/deployment cache coverage.

Required regression cases:
1. `EnemyOffenseSelectsFixedRegionWithMostPlayerUnits`: two player-side defensive fixed regions exist; the enemy offense group selects the one containing more alive player actors.
2. `EnemyActiveDefenseSelectsPlayerOffensiveDeploymentRegion`: an enemy active-defense group selects a player offensive deployment region before temporary regions, emits `BattleGroupTacticalRegionSelected` with its active-defense tactical mode, and does not rewrite any player group region intent.
3. `EnemyRegionMovementIgnoresMovingUnitOutsidePerception`: while non-engaged, enemy movement target remains the selected region even if a player unit moves outside perception.
4. `EnemyGroupEntersEngagementWhenAnyMemberPerceivesPlayer`: one member perceives a player unit and the whole battle group enters engaged state.
5. `EnemyGroupStaysEngagedWhileAnyMemberPerceivesPlayer`: one member loses sight but another retains sight; group does not exit engagement.
6. `EnemyGroupExitsEngagementWhenWholeGroupLosesPerception`: no member perceives a hostile for the grace interval; target locks clear and region replan is requested.
7. `HoldDefenseActivatesWholeGroupOnDamage`: damaging one hold defender switches the whole group to active assault.
8. `HoldDefenseActivatesWholeGroupOnPerception`: one hold defender perceives a player unit and the whole group activates.
9. `TemporaryRegionBuildsFromPlayerClustersWhenFixedRegionsEmpty`: fixed regions contain no relevant player units; a temporary region is generated from player cluster facts.
10. `TemporaryRegionRefreshIntervalDefaultsToFiveTicks`: the temporary region is reused for four ticks and may refresh on the fifth Runtime tick.
11. `LocalCombatRegionUsesPerceptionOverlapAndCap`: overlapping group perception increases region score and the selected region does not exceed `DefaultLocalCombatMaxCells`.
12. `EngagedTargetingIgnoresFarHostileOutsideLocalRegion`: engaged local target/slot solving does not choose a hostile outside the local combat region.
13. `EnemyPolicyDoesNotRewritePlayerObjectiveRegion`: enemy policy runs, but a player group's accepted objective region remains unchanged.
14. `GlobalSnapshotCacheIsReadOnly`: query snapshots exist by group id, but mutation attempts must go through `BattleGroupTacticalStateStore` and reject owner mismatch.
15. `MissingOwnerRegionIsRejectedWithReason`: a region without an owner id is rejected and emits `region_rejected_missing_owner`.

Validation commands:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Expected result before acceptance:
- All listed test commands pass.
- Existing nullable or Godot source-generator warnings, if any, are identified as pre-existing and not expanded by this work.

## Manual QA

Run after headless tests pass.

Scenario A: Enemy offense against deployed player defense
- Start a battle where player units are split across at least two player-side deployment/defense regions.
- Expected: enemy non-engaged movement chooses the region with more player units and advances toward the region, not a moving unit.
- Expected diagnostics: `BattleGroupTacticalRegionSelected` with `region_fixed_selected_player_density`.

Scenario B: Enemy active defense against player assault
- Start a battle where an enemy active-defense group has at least one player offensive deployment region available.
- Expected: enemy active defense chooses the player offensive deployment region as its fixed target before using any temporary region.
- Expected: the player group keeps its command/plan region; active-defense selection does not rewrite player intent.
- Expected diagnostics: `BattleGroupTacticalRegionSelected` includes the active-defense tactical mode and a fixed-region selection reason.

Scenario C: Player leaves fixed regions
- Move or place player units outside all fixed player regions.
- Expected: enemy group uses a temporary cluster region and does not rebuild it every tick.
- Expected diagnostics: `temporary_region_created_cluster`, then `temporary_region_reused_refresh_interval` until the fifth Runtime tick.

Scenario D: Hold defense activation
- Place enemy defenders in hold defense.
- Damage one defender or move one player unit into group perception.
- Expected: all members of that battle group switch to active assault; V1 does not require return home.
- Expected diagnostics: `engagement_enter_member_damaged` or `engagement_enter_group_perception`.

Scenario E: Engaged local-optimal behavior
- Put two or more enemy group members near one fight and a far player unit elsewhere.
- Expected: local combat region covers the perceived fight, uses open attack slots first, support slots when attack slots are full, and ignores the far hostile until replan.
- Expected diagnostics: `local_region_built_perception_overlap` and slot/join reasons.

Scenario F: Player command separation
- Give a player group an objective/hold command while enemies replan.
- Expected: player region intent remains the command/plan region; enemy policy does not rewrite it.
- Expected diagnostics: no `region_rejected_player_policy_overwrite` in normal flow; if forced in a test hook, the rejection reason is emitted.
## Acceptance

This implementation proposal can be marked accepted only when:

- battle-group tactical state exists as Runtime-owned state keyed by battle-group id;
- fixed target regions, temporary target regions, local combat regions, and engagement state reject missing or mismatched owners;
- global tactical-region caches are query/diagnostic helpers only and cannot mutate intent;
- enemy offense and enemy active defense select fixed regions before falling back to temporary regions;
- enemy hold defense activates the whole group on damage, attack, or group perception;
- enemy non-engaged movement is region-directed and does not chase moving units;
- engaged enemy combat uses bounded local combat regions built from group perception coverage with overlap weighting and a hard cap;
- temporary regions are per-group, cached, and refreshed no more often than the configured default of 5 Runtime ticks;
- player groups can reuse tactical facts but enemy policy cannot rewrite player command or plan intent;
- diagnostics explain region selection, engagement transitions, local combat region builds, degradation, and rejection reasons;
- all required headless tests pass;
- manual QA scenarios pass or are explicitly waived by the user with a dated note in this proposal.

Acceptance note:

- 2026-05-31: User requested proposal archiving and git submission to `main`; this is recorded as the explicit manual QA waiver for this implementation proposal.

## Headless Verification Evidence

2026-05-30 current implemented phases through Phase 8:

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed, including `EngagedTargetingIgnoresFarHostileOutsideLocalRegion`, `EngagedAttackSlotsStayInsideLocalRegion`, `EngagedNoLocalSlotDegradesWithReason`, and `PlayerCommandedEngagementCannotBecomeEnemyActiveAssault`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed with the existing Godot source-generator `GodotProjectDir` warning.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed with the existing Godot source-generator `GodotProjectDir` warning, including `LimboAiBattleFacadeExposesLocalCombatObservationsReadOnly`.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- `dotnet build-server shutdown` completed after validation.

## Implementation Notes For Future Agent

- Work phase-by-phase. Do not implement local combat region scoring before the group tactical state store exists.
- Prefer small focused files under `src/Runtime/Battle/Tactics/` over growing `BattleRuntimeTickResolver` further.
- Keep comments near state transitions and authority boundaries. Comments should explain why group state owns engagement and why player policy cannot be mutated by enemy policy.
- If implementation reveals that authored deployment/objective markers do not provide enough fixed-region facts, stop and create a design amendment instead of inventing hidden global coordinates.
- If a test requires exposing a helper, expose a narrow internal helper in the relevant Runtime/Application namespace rather than adding Presentation hooks.
