using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Results;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

public sealed partial class BattleRuntimeSession
{
    // This cap is a runaway guard, not a combat-length budget. Multi-unit live battles can
    // legitimately need hundreds of actor-local decision slices before one side is defeated.
    internal const int MaxAutonomousCombatTicks = 2048;
    private const string NavigationTopologyMissingReason = "navigation_topology_missing";
    private const string GroupSnapshotInvalidReason = "battle_group_snapshot_invalid";
    private const string StartFootprintInvalidReason = "battle_start_footprint_invalid";
    private const string SkillSnapshotInvalidReason = "battle_skill_snapshot_invalid";
    private const int EngagementRange = 1;
    private readonly BattleRuntimeTickResolver _tickResolver;
    private readonly BattlePerformanceCounters _performanceCounters;

    public BattleRuntimeSession(IBattleRuntimeAiExecutor aiExecutor = null, BattlePerformanceCounters performanceCounters = null)
    {
        IBattleRuntimeAiExecutor executor = aiExecutor ?? new DefaultBattleRuntimeAiExecutor();
        _tickResolver = new BattleRuntimeTickResolver(executor);
        _performanceCounters = performanceCounters;
    }

    public BattleRuntimeSessionResult RunMinimal(BattleStartSnapshot snapshot)
    {
        BattleRuntimeSessionController controller = Begin(snapshot);
        return controller.AdvanceToCompletion();
    }

    public BattleRuntimeSessionController Begin(BattleStartSnapshot snapshot)
    {
        BattleEventStream stream = new();
        string battleId = snapshot?.BattleId ?? "";
        string snapshotId = snapshot?.SnapshotId ?? "";
        if (string.IsNullOrWhiteSpace(snapshotId) ||
            string.IsNullOrWhiteSpace(battleId) ||
            snapshot?.BattleGroups == null ||
            snapshot.BattleGroups.Count == 0 ||
            snapshot.BattleGroups.Any(item => !HasRequiredGroupIdentity(item)))
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:snapshot_invalid",
                BattleId = battleId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = "battle_snapshot_invalid"
            });

            return BattleRuntimeSessionController.CompletedInvalid(
                snapshotId,
                battleId,
                stream,
                new BattleRuntimeState
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId
                },
                BattleTerminationReason.RuntimeException);
        }

        BattleGroupSnapshotValidationFailure groupValidationFailure = ValidateGroupSnapshots(snapshot.BattleGroups);
        if (groupValidationFailure.HasFailure)
        {
            GameLog.Warn(
                nameof(BattleRuntimeSession),
                $"BattleRuntimeLaunchRejected battle={battleId} snapshot={snapshotId} group={groupValidationFailure.BattleGroupId} reason={groupValidationFailure.ReasonCode}");
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{groupValidationFailure.BattleGroupId}:{GroupSnapshotInvalidReason}",
                BattleId = battleId,
                BattleGroupId = groupValidationFailure.BattleGroupId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = groupValidationFailure.ReasonCode
            });

            return BattleRuntimeSessionController.CompletedInvalid(
                snapshotId,
                battleId,
                stream,
                new BattleRuntimeState
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId
                },
                BattleTerminationReason.RuntimeException);
        }

        BattleSkillSnapshotValidationFailure skillValidationFailure = ValidateSkillSnapshots(snapshot.SkillDefinitions);
        if (skillValidationFailure.HasFailure)
        {
            GameLog.Warn(
                nameof(BattleRuntimeSession),
                $"BattleRuntimeLaunchRejected battle={battleId} snapshot={snapshotId} skill={skillValidationFailure.SkillDefinitionId} reason={skillValidationFailure.ReasonCode}");
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{skillValidationFailure.SkillDefinitionId}:skill_snapshot_invalid",
                BattleId = battleId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = skillValidationFailure.ReasonCode,
                SourceDefinitionId = skillValidationFailure.SkillDefinitionId
            });

            return BattleRuntimeSessionController.CompletedInvalid(
                snapshotId,
                battleId,
                stream,
                new BattleRuntimeState
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId
                },
                BattleTerminationReason.RuntimeException);
        }

        if (snapshot.LocationContext?.NavigationTopology?.HasNodes != true)
        {
            GameLog.Warn(
                nameof(BattleRuntimeSession),
                $"BattleRuntimeLaunchRejected battle={battleId} snapshot={snapshotId} reason={NavigationTopologyMissingReason}");
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:navigation_topology_missing",
                BattleId = battleId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = NavigationTopologyMissingReason
            });

            return BattleRuntimeSessionController.CompletedInvalid(
                snapshotId,
                battleId,
                stream,
                new BattleRuntimeState
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId
                },
                BattleTerminationReason.RuntimeException);
        }

        BattleRuntimeState state = BuildRuntimeState(snapshot);
        BattleNavigationGraph navigationGraph = BattleNavigationGraph.Create(snapshot.LocationContext, state.Actors);
        BattleRuntimeActor invalidStartActor = FindInvalidStartFootprintActor(state, navigationGraph);
        if (invalidStartActor != null)
        {
            BattleGridCoord anchor = new(invalidStartActor.GridX, invalidStartActor.GridY, invalidStartActor.GridHeight);
            GameLog.Warn(
                nameof(BattleRuntimeSession),
                $"BattleRuntimeLaunchRejected battle={battleId} snapshot={snapshotId} actor={invalidStartActor.ActorId} anchor={anchor} reason={StartFootprintInvalidReason}");
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{invalidStartActor.ActorId}:start_footprint_invalid",
                BattleId = battleId,
                BattleGroupId = invalidStartActor.BattleGroupId,
                ActorId = invalidStartActor.ActorId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = StartFootprintInvalidReason
            });

            return BattleRuntimeSessionController.CompletedInvalid(
                snapshotId,
                battleId,
                stream,
                state,
                BattleTerminationReason.RuntimeException);
        }

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:started",
            BattleId = battleId,
            Kind = BattleEventKind.BattleStarted
        });

        foreach (BattleRuntimeActor actor in state.Actors)
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{actor.ActorId}:spawned",
                BattleId = battleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                Kind = BattleEventKind.RuntimeActorSpawned,
                ReasonCode = "runtime_actor_spawned"
            });
        }

        LogNavigationGraphSummary(battleId, navigationGraph);
        LogNavigationRouteTopologySummary(battleId, navigationGraph);
        LogNavigationActorStarts(battleId, navigationGraph, state.Actors);
        EmitInitialCommandEvents(stream, battleId, state);
        EmitInitialDestinationBeaconEvents(stream, battleId, state);
        EmitInitialPlanEvents(stream, battleId, state);

        return new BattleRuntimeSessionController(
            _tickResolver,
            state,
            stream,
            battleId,
            snapshotId,
            navigationGraph,
            MaxAutonomousCombatTicks,
            _performanceCounters);
    }

    internal static BattleRuntimeState BuildRuntimeState(BattleStartSnapshot snapshot)
    {
        BattleRuntimeState state = new()
        {
            SnapshotId = snapshot?.SnapshotId ?? "",
            BattleId = snapshot?.BattleId ?? "",
            ObjectiveZones = CloneObjectiveZones(snapshot?.ObjectiveZones),
            SkillDefinitions = CloneSkillDefinitions(snapshot?.SkillDefinitions),
            // Runtime owns group tactical truth; snapshots only seed battle-local intent at session start.
            TacticalStateStore = BattleGroupTacticalStateStore
                .FromBattleGroups(
                    snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>(),
                    snapshot?.BattleId ?? "",
                    snapshot?.ObjectiveZones)
        };
        state.SkillAvailability.Initialize(state.SkillDefinitions);

        var sourceForceIndexes = new Dictionary<string, int>();
        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            string commanderGroupId = BattleCommanderGroupIdentity.Resolve(group);
            string sourceForceId = string.IsNullOrWhiteSpace(group.SourceForceId)
                ? group.BattleGroupId
                : group.SourceForceId;
            sourceForceIndexes.TryGetValue(sourceForceId, out int sourceForceIndex);
            sourceForceIndexes[sourceForceId] = sourceForceIndex + 1;
            double moveStepSeconds = ResolveMoveStepSeconds(group);
            double attackActionSeconds = ResolveAttackActionSeconds(group);
            double attackImpactDelaySeconds = ResolveAttackImpactDelaySeconds(group, attackActionSeconds);
            int corpsHitPoints = ResolveCombatHitPoints(group);
            int corpsFootprintWidth = BattleActorFootprint.NormalizeSize(group.FootprintWidth);
            int corpsFootprintHeight = BattleActorFootprint.NormalizeSize(group.FootprintHeight);
            string heroBattleUnitId = ResolveHeroBattleUnitId(group);
            string corpsBattleUnitId = ResolveCorpsBattleUnitId(group);
            BattleGroupPlanSnapshot plan = ResolveBattleGroupPlan(group, snapshot?.ObjectiveZones);
            BattleRuntimeActor heroActor = new()
            {
                ActorId = $"{group.BattleGroupId}:hero",
                BattleGroupId = commanderGroupId,
                FactionId = group.FactionId ?? "",
                UnitDefinitionId = heroBattleUnitId,
                SourceForceId = sourceForceId,
                SourceHeroId = group.HeroId ?? "",
                SourceStateId = group.HeroId,
                Kind = BattleRuntimeActorKind.Hero,
                HitPoints = 1,
                Position = group.CellX,
                GridX = group.CellX,
                GridY = group.CellY,
                GridHeight = group.CellHeight,
                MotionState = BattleRuntimeActorMotionState.Anchored,
                Phase = BattleRuntimeActorPhase.AnchoredDecision,
                AttackRange = EngagementRange,
                AttackDamage = group.AttackDamage,
                AttackSpeed = BattleAttackSpeedPolicy.DefaultAttackSpeed,
                MoveStepSeconds = moveStepSeconds,
                AttackActionSeconds = attackActionSeconds,
                AttackImpactDelaySeconds = attackImpactDelaySeconds
            };
            state.Actors.Add(heroActor);
            state.TacticalStateStore.SynchronizeActorExecutionCache(heroActor);
            BattleRuntimeActor corpsActor = new()
            {
                ActorId = $"{sourceForceId}:{sourceForceIndex + 1}",
                BattleGroupId = commanderGroupId,
                FactionId = group.FactionId ?? "",
                UnitDefinitionId = corpsBattleUnitId,
                SourceForceId = sourceForceId,
                SourceHeroId = group.HeroId ?? "",
                SourceStateId = group.CorpsId,
                Kind = BattleRuntimeActorKind.Corps,
                HitPoints = corpsHitPoints,
                Position = group.CellX,
                GridX = group.CellX,
                GridY = group.CellY,
                GridHeight = group.CellHeight,
                FootprintWidth = corpsFootprintWidth,
                FootprintHeight = corpsFootprintHeight,
                MotionState = BattleRuntimeActorMotionState.Anchored,
                Phase = BattleRuntimeActorPhase.AnchoredDecision,
                AttackRange = ResolveAttackRange(group.AttackRange),
                AttackDamage = group.AttackDamage,
                AttackSpeed = BattleAttackSpeedPolicy.Normalize(group.AttackSpeed),
                MoveStepSeconds = moveStepSeconds,
                AttackActionSeconds = attackActionSeconds,
                AttackImpactDelaySeconds = attackImpactDelaySeconds
            };
            state.Actors.Add(corpsActor);
            state.TacticalStateStore.SynchronizeActorExecutionCache(corpsActor);
            if (BattleRuntimeIdentityRules.IsPlayerFaction(group.FactionId))
            {
                SeedInitialDestinationBeacon(state, commanderGroupId, plan);
            }
        }

        return state;
    }

    private static void SeedInitialDestinationBeacon(
        BattleRuntimeState state,
        string battleGroupId,
        BattleGroupPlanSnapshot plan)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(battleGroupId) ||
            plan?.HasInitialDestinationBeacon != true)
        {
            return;
        }

        BattleGroupTacticalState commanderState = state.TacticalStateStore.GetRequiredSnapshot(battleGroupId);
        if (!string.IsNullOrWhiteSpace(commanderState.ActiveDestinationBeaconId))
        {
            return;
        }

        string commandId = $"{state.BattleId}:initial_destination_beacon:{battleGroupId}";
        BattleRuntimeDestinationBeacon beacon = new()
        {
            BeaconId = $"{commandId}:destination",
            CommandId = commandId,
            Anchor = new BattleGridCoord(
                plan.InitialDestinationCellX,
                plan.InitialDestinationCellY,
                plan.InitialDestinationCellHeight),
            Revision = 1,
            IsValid = true
        };
        beacon.OwnerBattleGroupIds.Add(battleGroupId);
        state.DestinationBeacons.Add(beacon);

        state.TacticalStateStore.TryApplyDestinationCommand(
            battleGroupId,
            commandId,
            beacon.BeaconId,
            beacon.Revision,
            beacon.Anchor.X,
            beacon.Anchor.Y,
            beacon.Anchor.Height,
            out _);

        foreach (BattleRuntimeActor actor in state.Actors.Where(actor =>
                     string.Equals(actor.BattleGroupId ?? "", battleGroupId, System.StringComparison.Ordinal)))
        {
            state.TacticalStateStore.SynchronizeActorExecutionCache(actor);
            // The seeded beacon is the first player movement intent. Actors still
            // wait for normal Runtime decision boundaries before committing cells.
            BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot(actor);
        }
    }

    internal static void LogNavigationGraphSummary(string battleId, BattleNavigationGraph navigationGraph)
    {
        GameLog.Info(
            nameof(BattleRuntimeSession),
            $"BattleRuntimeNavigationGraph battle={battleId ?? ""} {navigationGraph?.DescribeTopology() ?? "graph=missing"}");
    }

    internal static void LogNavigationRouteTopologySummary(string battleId, BattleNavigationGraph navigationGraph)
    {
        GameLog.Info(
            nameof(BattleRuntimeSession),
            $"BattleRuntimeRouteTopology battle={battleId ?? ""} {navigationGraph?.DescribeRouteTopology() ?? "routeTopology=missing"}");
    }

    internal static void LogNavigationActorStarts(
        string battleId,
        BattleNavigationGraph navigationGraph,
        IEnumerable<BattleRuntimeActor> actors)
    {
        string starts = string.Join(";",
            (actors ?? Enumerable.Empty<BattleRuntimeActor>())
                .Where(actor => actor?.Kind == BattleRuntimeActorKind.Corps)
                .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
                .Select(actor =>
                {
                    BattleGridCoord anchor = new(actor.GridX, actor.GridY, actor.GridHeight);
                    bool inGraph = navigationGraph?.Contains(anchor) == true;
                    bool footprintLegal = navigationGraph?.CanPlaceFootprint(actor, anchor) == true;
                    return $"{actor.ActorId}@{anchor}:inGraph={inGraph}:footprint={actor.FootprintWidth}x{actor.FootprintHeight}:footprintLegal={footprintLegal}";
                }));

        GameLog.Info(
            nameof(BattleRuntimeSession),
            $"BattleRuntimeNavigationActorStarts battle={battleId ?? ""} {starts}");
    }

    private static BattleRuntimeActor FindInvalidStartFootprintActor(
        BattleRuntimeState state,
        BattleNavigationGraph navigationGraph)
    {
        return (state?.Actors ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(actor => actor?.Kind == BattleRuntimeActorKind.Corps)
            .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
            .FirstOrDefault(actor =>
            {
                BattleGridCoord anchor = new(actor.GridX, actor.GridY, actor.GridHeight);
                return navigationGraph?.CanPlaceFootprint(actor, anchor) != true;
            });
    }

    internal static BattleTerminationReason ResolveTermination(BattleRuntimeState state)
    {
        BattleRuntimeActor[] corps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps)
            .ToArray();
        if (corps.Length == 0)
        {
            return BattleTerminationReason.RuntimeException;
        }

        bool hasPlayer = corps.Any(item => item.HitPoints > 0 && !item.HasRetreated && BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId));
        bool hasEnemy = corps.Any(item => item.HitPoints > 0 && !BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId));
        bool hasRetreatedPlayer = corps.Any(item => item.HitPoints > 0 && item.HasRetreated && BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId));
        if (hasPlayer && !hasEnemy)
        {
            return BattleTerminationReason.NormalVictory;
        }

        if (!hasPlayer && hasEnemy)
        {
            return hasRetreatedPlayer
                ? BattleTerminationReason.PlayerRetreat
                : BattleTerminationReason.NormalDefeat;
        }

        if (!hasPlayer && !hasEnemy)
        {
            return BattleTerminationReason.NormalDefeat;
        }

        return BattleTerminationReason.None;
    }

    internal static BattleOutcomeResult BuildCompletedOutcome(
        string snapshotId,
        string battleId,
        BattleRuntimeState state,
        BattleTerminationReason terminationReason)
    {
        BattleOutcomeResult outcome = BattleOutcomeResult.Completed(
            snapshotId,
            battleId,
            terminationReason);
        foreach (BattleRuntimeActor actor in state?.Actors ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            outcome.ActorOutcomes.Add(new BattleActorOutcome
            {
                ActorId = actor.ActorId,
                BattleGroupId = actor.BattleGroupId,
                FactionId = actor.FactionId,
                SourceForceId = actor.SourceForceId,
                SourceStateId = actor.SourceStateId,
                Kind = actor.Kind,
                Survived = actor.HitPoints > 0,
                RemainingHitPoints = System.Math.Max(0, actor.HitPoints)
            });
        }

        return outcome;
    }

    private static int ResolveCombatHitPoints(BattleGroupSnapshot group)
    {
        if (group?.MaxHitPoints > 0)
        {
            return group.MaxHitPoints;
        }

        return System.Math.Max(1, group?.CorpsStrength ?? 0);
    }

    private static string ResolveHeroBattleUnitId(BattleGroupSnapshot group)
    {
        if (!string.IsNullOrWhiteSpace(group?.HeroBattleUnitId))
        {
            return group.HeroBattleUnitId;
        }

        if (!string.IsNullOrWhiteSpace(group?.HeroDefinitionId))
        {
            return group.HeroDefinitionId;
        }

        return ResolveCorpsBattleUnitId(group);
    }

    private static string ResolveCorpsBattleUnitId(BattleGroupSnapshot group)
    {
        if (!string.IsNullOrWhiteSpace(group?.CorpsBattleUnitId))
        {
            return group.CorpsBattleUnitId;
        }

        return group?.CorpsDefinitionId ?? "";
    }

    private static int ResolveAttackRange(int attackRange)
    {
        return System.Math.Max(1, attackRange);
    }

    private static double ResolveMoveStepSeconds(BattleGroupSnapshot group)
    {
        return BattleActionTimingPolicy.NormalizeMoveStepSeconds(
            group?.MoveStepSeconds ?? BattleActionTimingPolicy.DefaultMoveStepSeconds,
            BattleActionTimingPolicy.DefaultMoveStepSeconds);
    }

    private static double ResolveAttackActionSeconds(BattleGroupSnapshot group)
    {
        if (group?.AttackActionSeconds > 0)
        {
            return BattleActionTimingPolicy.NormalizeActionSeconds(
                group.AttackActionSeconds,
                BattleActionTimingPolicy.DefaultAttackActionSeconds);
        }

        return BattleActionTimingPolicy.ResolveAttackActionSeconds(
            BattleActionTimingPolicy.DefaultAttackActionSeconds,
            group?.AttackSpeed ?? BattleAttackSpeedPolicy.DefaultAttackSpeed);
    }

    private static double ResolveAttackImpactDelaySeconds(BattleGroupSnapshot group, double attackActionSeconds)
    {
        return group != null && group.AttackImpactDelaySeconds >= 0
            ? BattleActionTimingPolicy.NormalizeAttackImpactDelaySeconds(group.AttackImpactDelaySeconds, attackActionSeconds)
            : BattleActionTimingPolicy.ResolveAttackImpactDelaySeconds(
                attackActionSeconds,
                BattleActionTimingPolicy.DefaultAttackImpactNormalizedTime);
    }

    private static bool HasRequiredGroupIdentity(BattleGroupSnapshot group)
    {
        return group != null &&
            !string.IsNullOrWhiteSpace(group.BattleGroupId) &&
            !string.IsNullOrWhiteSpace(group.HeroId) &&
            !string.IsNullOrWhiteSpace(group.CorpsId) &&
            !string.IsNullOrWhiteSpace(group.HeroDefinitionId) &&
            !string.IsNullOrWhiteSpace(group.CorpsDefinitionId) &&
            !string.IsNullOrWhiteSpace(group.SourceLocationId);
    }

    private static BattleGroupSnapshotValidationFailure ValidateGroupSnapshots(
        IReadOnlyList<BattleGroupSnapshot> battleGroups)
    {
        foreach (BattleGroupSnapshot group in battleGroups ?? System.Array.Empty<BattleGroupSnapshot>())
        {
            string groupId = group?.BattleGroupId ?? "";
            if (group == null)
            {
                return BattleGroupSnapshotValidationFailure.Create("", "battle_group_missing");
            }

            // Runtime launch must consume authored bridge facts. These guards stop
            // legacy normalization from converting malformed production content into combat truth.
            if (string.IsNullOrWhiteSpace(group.FactionId))
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_faction_missing");
            }

            if (group.MaxHitPoints <= 0)
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_hit_points_invalid");
            }

            if (group.AttackDamage <= 0)
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_attack_damage_invalid");
            }

            if (group.AttackRange <= 0)
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_attack_range_invalid");
            }

            if (!IsPositiveFinite(group.AttackSpeed))
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_attack_speed_invalid");
            }

            if (!IsPositiveFinite(group.MoveStepSeconds))
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_move_timing_invalid");
            }

            if (!IsNonNegativeFinite(group.AttackActionSeconds))
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_attack_timing_invalid");
            }

            if (!double.IsNaN(group.AttackImpactDelaySeconds) &&
                !IsNonNegativeFinite(group.AttackImpactDelaySeconds))
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_attack_impact_timing_invalid");
            }

            if (!IsValidFootprintSize(group.FootprintWidth) ||
                !IsValidFootprintSize(group.FootprintHeight))
            {
                return BattleGroupSnapshotValidationFailure.Create(groupId, "battle_group_footprint_invalid");
            }
        }

        return BattleGroupSnapshotValidationFailure.None;
    }

    private static bool IsValidFootprintSize(int value)
    {
        return value >= 1 && value <= BattleActorFootprint.MaxSupportedFootprintSize;
    }

    internal static BattleGroupPlanSnapshot ResolveBattleGroupPlan(
        BattleGroupSnapshot group,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        BattleGroupPlanSnapshot source = group?.Plan ?? new BattleGroupPlanSnapshot();
        BattleObjectiveZoneSnapshot zone = FindObjectiveZone(source.ObjectiveZoneId, objectiveZones);
        BattleGroupPlanSnapshot resolved = new()
        {
            BattleGroupId = string.IsNullOrWhiteSpace(source.BattleGroupId)
                ? BattleCommanderGroupIdentity.Resolve(group)
                : source.BattleGroupId,
            ObjectiveZoneId = source.ObjectiveZoneId ?? "",
            EngagementRule = NormalizeEngagementRule(source.EngagementRule, group?.InitialCorpsCommandId),
            InitialFormationId = source.InitialFormationId ?? "",
            HasObjectiveAnchor = source.HasObjectiveAnchor || zone != null,
            ObjectiveCellX = source.HasObjectiveAnchor ? source.ObjectiveCellX : zone?.CellX ?? 0,
            ObjectiveCellY = source.HasObjectiveAnchor ? source.ObjectiveCellY : zone?.CellY ?? 0,
            ObjectiveCellHeight = source.HasObjectiveAnchor ? source.ObjectiveCellHeight : zone?.CellHeight ?? 0,
            ObjectiveWidth = source.HasObjectiveAnchor ? source.ObjectiveWidth : zone?.Width ?? 1,
            ObjectiveHeight = source.HasObjectiveAnchor ? source.ObjectiveHeight : zone?.Height ?? 1,
            HasInitialDestinationBeacon = source.HasInitialDestinationBeacon,
            InitialDestinationCellX = source.InitialDestinationCellX,
            InitialDestinationCellY = source.InitialDestinationCellY,
            InitialDestinationCellHeight = source.InitialDestinationCellHeight
        };

        if (string.IsNullOrWhiteSpace(resolved.ObjectiveZoneId) && zone != null)
        {
            resolved.ObjectiveZoneId = zone.ObjectiveZoneId ?? "";
        }

        return resolved;
    }

    private static BattleObjectiveZoneSnapshot FindObjectiveZone(
        string objectiveZoneId,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        if (string.IsNullOrWhiteSpace(objectiveZoneId))
        {
            return null;
        }

        return (objectiveZones ?? System.Array.Empty<BattleObjectiveZoneSnapshot>())
            .FirstOrDefault(item => string.Equals(
                item?.ObjectiveZoneId,
                objectiveZoneId,
                System.StringComparison.Ordinal));
    }

    private static List<BattleObjectiveZoneSnapshot> CloneObjectiveZones(
        IEnumerable<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        return (objectiveZones ?? Enumerable.Empty<BattleObjectiveZoneSnapshot>())
            .Select(zone => new BattleObjectiveZoneSnapshot
            {
                ObjectiveZoneId = zone?.ObjectiveZoneId ?? "",
                DisplayName = zone?.DisplayName ?? "",
                ObjectiveRole = zone?.ObjectiveRole ?? "",
                DeploymentSide = zone?.DeploymentSide ?? "",
                FactionId = zone?.FactionId ?? "",
                Priority = zone?.Priority ?? 0,
                CellX = zone?.CellX ?? 0,
                CellY = zone?.CellY ?? 0,
                CellHeight = zone?.CellHeight ?? 0,
                Width = System.Math.Max(1, zone?.Width ?? 1),
                Height = System.Math.Max(1, zone?.Height ?? 1)
            })
            .ToList();
    }

    private static BattleEngagementRule NormalizeEngagementRule(
        BattleEngagementRule rule,
        string initialCorpsCommandId)
    {
        // Legacy snapshots do not yet carry authored objective plans. Keep their
        // attack-first behavior stable while the new battle-preparation UI is phased in.
        if (BattleRuntimeIdentityRules.IsHoldLineCommand(initialCorpsCommandId))
        {
            return BattleEngagementRule.Hold;
        }

        return System.Enum.IsDefined(typeof(BattleEngagementRule), rule)
            ? rule
            : BattleEngagementRule.AttackFirst;
    }

    internal static void EmitInitialCommandEvents(
        BattleEventStream stream,
        string battleId,
        BattleRuntimeState state)
    {
        foreach (BattleGroupTacticalState commanderState in state?.TacticalStates.Values.Where(item =>
                     BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId) &&
                     !string.IsNullOrWhiteSpace(item.ActiveCommandId)) ?? Enumerable.Empty<BattleGroupTacticalState>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{commanderState.BattleGroupId}:initial_command",
                BattleId = battleId,
                BattleGroupId = commanderState.BattleGroupId,
                SourceCommandId = commanderState.ActiveCommandId,
                Kind = BattleEventKind.CommandAccepted,
                ReasonCode = commanderState.ActiveCommandId
            });
        }
    }

    internal static void EmitInitialPlanEvents(
        BattleEventStream stream,
        string battleId,
        BattleRuntimeState state)
    {
        foreach (BattleGroupTacticalState commanderState in state?.TacticalStates.Values.Where(item =>
                     BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId)) ?? Enumerable.Empty<BattleGroupTacticalState>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{commanderState.BattleGroupId}:initial_plan",
                BattleId = battleId,
                BattleGroupId = commanderState.BattleGroupId,
                TargetId = commanderState.ObjectiveZoneId ?? "",
                Kind = BattleEventKind.BattleGroupPlanAccepted,
                ReasonCode = commanderState.EngagementRule.ToString()
            });
        }
    }

    internal static void EmitInitialDestinationBeaconEvents(
        BattleEventStream stream,
        string battleId,
        BattleRuntimeState state)
    {
        foreach (BattleRuntimeDestinationBeacon beacon in state?.DestinationBeacons?.Where(item =>
                     item != null &&
                     item.IsValid &&
                     item.OwnerBattleGroupIds.Count > 0 &&
                     (item.CommandId ?? "").Contains(":initial_destination_beacon:", System.StringComparison.Ordinal)) ??
                 Enumerable.Empty<BattleRuntimeDestinationBeacon>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{beacon.BeaconId}:initial_destination_beacon_seeded",
                BattleId = battleId,
                BattleGroupId = string.Join(",", beacon.OwnerBattleGroupIds),
                SourceCommandId = beacon.CommandId ?? "",
                TargetId = beacon.BeaconId ?? "",
                Kind = BattleEventKind.CommandAccepted,
                ReasonCode = "initial_destination_beacon_seeded",
                HasTargetCells = true,
                TargetGridX = beacon.Anchor.X,
                TargetGridY = beacon.Anchor.Y,
                TargetGridHeight = beacon.Anchor.Height
            });
        }
    }

    private readonly record struct BattleSkillSnapshotValidationFailure(string SkillDefinitionId, string ReasonCode)
    {
        internal static BattleSkillSnapshotValidationFailure None { get; } = new("", "");
        internal bool HasFailure => !string.IsNullOrWhiteSpace(ReasonCode);
        internal static BattleSkillSnapshotValidationFailure Create(string skillDefinitionId, string reasonCode) =>
            new(skillDefinitionId ?? "", string.IsNullOrWhiteSpace(reasonCode) ? SkillSnapshotInvalidReason : reasonCode);
    }

    private readonly record struct BattleGroupSnapshotValidationFailure(string BattleGroupId, string ReasonCode)
    {
        internal static BattleGroupSnapshotValidationFailure None { get; } = new("", "");
        internal bool HasFailure => !string.IsNullOrWhiteSpace(ReasonCode);
        internal static BattleGroupSnapshotValidationFailure Create(string battleGroupId, string reasonCode) =>
            new(battleGroupId ?? "", string.IsNullOrWhiteSpace(reasonCode) ? GroupSnapshotInvalidReason : reasonCode);
    }

}
