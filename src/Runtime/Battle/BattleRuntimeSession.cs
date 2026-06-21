using System.Collections.Generic;
using System.Diagnostics;
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

public sealed class BattleRuntimeSession
{
    // This cap is a runaway guard, not a combat-length budget. Multi-unit live battles can
    // legitimately need hundreds of actor-local decision slices before one side is defeated.
    internal const int MaxAutonomousCombatTicks = 2048;
    private const int LegacyCorpsAttackDamage = 20;
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

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:started",
            BattleId = battleId,
            Kind = BattleEventKind.BattleStarted
        });

        BattleRuntimeState state = BuildRuntimeState(snapshot);
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

        BattleNavigationGraph navigationGraph = BattleNavigationGraph.Create(snapshot.LocationContext, state.Actors);
        LogNavigationGraphSummary(battleId, navigationGraph);
        LogNavigationRouteTopologySummary(battleId, navigationGraph);
        LogNavigationActorStarts(battleId, navigationGraph, state.Actors);
        EmitInitialCommandEvents(stream, battleId, state);
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
            state.Actors.Add(new BattleRuntimeActor
            {
                ActorId = $"{group.BattleGroupId}:hero",
                BattleGroupId = commanderGroupId,
                FactionId = group.FactionId ?? "",
                UnitDefinitionId = heroBattleUnitId,
                SourceForceId = sourceForceId,
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
                AttackDamage = ResolveAttackDamage(group.AttackDamage),
                AttackSpeed = BattleAttackSpeedPolicy.DefaultAttackSpeed,
                MoveStepSeconds = moveStepSeconds,
                AttackActionSeconds = attackActionSeconds,
                AttackImpactDelaySeconds = attackImpactDelaySeconds
            });
            state.Actors.Add(new BattleRuntimeActor
            {
                ActorId = $"{sourceForceId}:{sourceForceIndex + 1}",
                BattleGroupId = commanderGroupId,
                FactionId = group.FactionId ?? "",
                UnitDefinitionId = corpsBattleUnitId,
                SourceForceId = sourceForceId,
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
                AttackDamage = ResolveAttackDamage(group.AttackDamage),
                AttackSpeed = BattleAttackSpeedPolicy.Normalize(group.AttackSpeed),
                MoveStepSeconds = moveStepSeconds,
                AttackActionSeconds = attackActionSeconds,
                AttackImpactDelaySeconds = attackImpactDelaySeconds,
                CommandId = BattleRuntimeIdentityRules.NormalizeCorpsCommandId(group.InitialCorpsCommandId),
                EngagementRule = NormalizeEngagementRule(plan.EngagementRule, group.InitialCorpsCommandId),
                HasObjectiveAnchor = plan.HasObjectiveAnchor,
                ObjectiveZoneId = plan.ObjectiveZoneId ?? "",
                ObjectiveGridX = plan.ObjectiveCellX,
                ObjectiveGridY = plan.ObjectiveCellY,
                ObjectiveGridHeight = plan.ObjectiveCellHeight,
                ObjectiveWidth = System.Math.Max(1, plan.ObjectiveWidth),
                ObjectiveHeight = System.Math.Max(1, plan.ObjectiveHeight),
                PlanState = plan.HasObjectiveAnchor
                    ? BattleGroupPlanRuntimeState.AdvancingToObjective
                    : BattleGroupPlanRuntimeState.SensingContact
            });
        }

        return state;
    }

    internal BattleTerminationReason ResolveAutonomousCombat(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        BattleNavigationGraph navigationGraph)
    {
        if (state?.Actors == null)
        {
            return BattleTerminationReason.RuntimeException;
        }

        var navigationFailureDiagnostics = new HashSet<string>(System.StringComparer.Ordinal);
        BattleRuntimeClock runtimeClock = new();
        for (int tick = 0; tick < MaxAutonomousCombatTicks; tick++)
        {
            BattleTerminationReason resolved = ResolveTermination(state);
            if (resolved != BattleTerminationReason.None)
            {
                return resolved;
            }

            double? nextReady = state.Actors
                .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
                .Select(item => (double?)System.Math.Max(runtimeClock.CurrentTimeSeconds, item.ActionReadyAtSeconds))
                .DefaultIfEmpty(runtimeClock.CurrentTimeSeconds)
                .Min();
            if (nextReady.HasValue)
            {
                runtimeClock.AdvanceTo(nextReady.Value);
            }

            long resolveStartedAt = Stopwatch.GetTimestamp();
            _tickResolver.ResolveTick(
                state,
                stream,
                battleId,
                tick,
                runtimeClock.CurrentTimeSeconds,
                navigationGraph,
                navigationFailureDiagnostics,
                _performanceCounters);
            long resolveElapsedTicks = Stopwatch.GetTimestamp() - resolveStartedAt;
            bool isNewMaximum = _performanceCounters?.RecordRuntimeAdvanceElapsedTicks(resolveElapsedTicks, tick) == true;
            BattleRuntimeSpikeDiagnostics.LogIfNeeded(
                battleId,
                tick,
                runtimeClock.CurrentTimeSeconds,
                resolveElapsedTicks,
                isNewMaximum,
                _performanceCounters);
        }

        return BattleTerminationReason.RuntimeException;
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

    internal static BattleTerminationReason ResolveTermination(BattleRuntimeState state)
    {
        BattleRuntimeActor[] corps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps)
            .ToArray();
        if (corps.Length == 0)
        {
            return BattleTerminationReason.RuntimeException;
        }

        bool hasPlayer = corps.Any(item => item.HitPoints > 0 && BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId));
        bool hasEnemy = corps.Any(item => item.HitPoints > 0 && !BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId));
        if (hasPlayer && !hasEnemy)
        {
            return BattleTerminationReason.NormalVictory;
        }

        if (!hasPlayer && hasEnemy)
        {
            return BattleTerminationReason.NormalDefeat;
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

    private static int ResolveAttackDamage(int attackDamage)
    {
        return attackDamage > 0 ? attackDamage : LegacyCorpsAttackDamage;
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

    private static BattleGroupPlanSnapshot ResolveBattleGroupPlan(
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
            ObjectiveHeight = source.HasObjectiveAnchor ? source.ObjectiveHeight : zone?.Height ?? 1
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

    private static List<BattleSkillSnapshot> CloneSkillDefinitions(
        IEnumerable<BattleSkillSnapshot> skillDefinitions)
    {
        return (skillDefinitions ?? Enumerable.Empty<BattleSkillSnapshot>())
            .Select(skill =>
            {
                BattleSkillSnapshot clone = new()
                {
                    SkillId = skill?.SkillId ?? "",
                    DisplayName = skill?.DisplayName ?? "",
                    TargetingMode = skill?.TargetingMode ?? BattleSkillTargetingMode.TargetedActor,
                    Range = System.Math.Max(0, skill?.Range ?? 0),
                    CasterUnitIds = (skill?.CasterUnitIds ?? new List<string>())
                        .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
                        .Select(unitId => unitId.Trim())
                        .Distinct(System.StringComparer.Ordinal)
                        .ToList(),
                    CastSeconds = System.Math.Max(0, skill?.CastSeconds ?? 0),
                    ImpactDelaySeconds = System.Math.Max(0, skill?.ImpactDelaySeconds ?? 0),
                    RecoverySeconds = System.Math.Max(0, skill?.RecoverySeconds ?? 0),
                    CanInterruptBasicAttackWindup = skill?.CanInterruptBasicAttackWindup ?? true,
                    CanCancelBasicAttackRecovery = skill?.CanCancelBasicAttackRecovery ?? false,
                    ReleasesWithoutOccupyingCaster = skill?.ReleasesWithoutOccupyingCaster ?? false
                };
                foreach (BattleSkillEffectSnapshot effect in skill?.Effects ?? Enumerable.Empty<BattleSkillEffectSnapshot>())
                {
                    clone.Effects.Add(new BattleSkillEffectSnapshot
                    {
                        Kind = effect?.Kind ?? BattleSkillEffectKind.Damage,
                        Amount = effect?.Amount ?? 0,
                        DurationSeconds = System.Math.Max(0, effect?.DurationSeconds ?? 0),
                        TickIntervalSeconds = System.Math.Max(0, effect?.TickIntervalSeconds ?? 0),
                        Radius = System.Math.Max(0, effect?.Radius ?? 0)
                    });
                }

                return clone;
            })
            .Where(skill => !string.IsNullOrWhiteSpace(skill.SkillId))
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
        foreach (BattleRuntimeActor actor in state?.Actors?.Where(item =>
                     item.Kind == BattleRuntimeActorKind.Corps &&
                     BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId) &&
                     !string.IsNullOrWhiteSpace(item.CommandId)) ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{actor.ActorId}:initial_command",
                BattleId = battleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                Kind = BattleEventKind.CommandAccepted,
                ReasonCode = actor.CommandId
            });
        }
    }

    internal static void EmitInitialPlanEvents(
        BattleEventStream stream,
        string battleId,
        BattleRuntimeState state)
    {
        foreach (BattleRuntimeActor actor in state?.Actors?.Where(item =>
                     item.Kind == BattleRuntimeActorKind.Corps &&
                     BattleRuntimeIdentityRules.IsPlayerFaction(item.FactionId)) ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{actor.ActorId}:initial_plan",
                BattleId = battleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                TargetId = actor.ObjectiveZoneId ?? "",
                Kind = BattleEventKind.BattleGroupPlanAccepted,
                ReasonCode = actor.EngagementRule.ToString()
            });
        }
    }

}
