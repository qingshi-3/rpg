using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleFlowFieldCache
{
    private readonly Dictionary<TargetFlowFieldCacheKey, BattleFlowField> _targetFields = new();
    private readonly Dictionary<ObjectiveFlowFieldCacheKey, BattleFlowField> _objectiveFields = new();
    private readonly Dictionary<OpenAttackFlowFieldCacheKey, BattleFlowField> _openAttackFields = new();
    private readonly Dictionary<GoalFlowFieldCacheKey, BattleFlowField> _goalFields = new();
    private readonly BattlePerformanceCounters _performanceCounters;

    public BattleFlowFieldCache(BattlePerformanceCounters performanceCounters = null)
    {
        _performanceCounters = performanceCounters;
    }

    public BattleFlowField GetOrBuild(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        bool preferSupportSlots,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        TargetFlowFieldCacheKey key = TargetFlowFieldCacheKey.Create(actor, target, preferSupportSlots, localCombatRegion);
        if (_targetFields.TryGetValue(key, out BattleFlowField field))
        {
            _performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        _performanceCounters?.RecordFlowFieldCacheMiss();
        field = BattleFlowFieldBuilder.Build(actor, target, graph, preferSupportSlots, _performanceCounters, localCombatRegion);
        _targetFields[key] = field;
        return field;
    }

    public BattleFlowField PreferOpenAttackSlots(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleFlowField field,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || graph == null || occupancy == null || field?.GoalSlots == null)
        {
            return field;
        }

        List<BattleCombatSlot> attackGoals = new();
        foreach (BattleCombatSlot goal in field.GoalSlots)
        {
            if (goal.Kind == BattleCombatSlotKind.Attack)
            {
                attackGoals.Add(goal);
            }
        }

        if (attackGoals.Count == 0)
        {
            return field;
        }

        List<BattleCombatSlot> openAttackGoals = new();
        foreach (BattleCombatSlot goal in attackGoals)
        {
            if (occupancy.CountOtherOccupiedCells(actor, goal.Anchor) == 0)
            {
                openAttackGoals.Add(goal);
            }
        }

        if (openAttackGoals.Count == attackGoals.Count)
        {
            return field;
        }

        _performanceCounters?.RecordOpenAttackFlowFieldRequest();
        OpenAttackFlowFieldCacheKey key = new(actor, openAttackGoals, localCombatRegion);
        if (_openAttackFields.TryGetValue(key, out BattleFlowField openField))
        {
            _performanceCounters?.RecordOpenAttackFlowFieldCacheHit();
            return openField;
        }

        _performanceCounters?.RecordOpenAttackFlowFieldBuild();
        // This cache is owned by one Runtime advance; reusing identical open-slot
        // facts avoids duplicate integration fields without relaxing tick-start
        // occupancy semantics.
        openField = BattleFlowFieldBuilder.BuildFromGoalSlots(
            actor,
            graph,
            openAttackGoals,
            _performanceCounters,
            BattleFlowFieldSearchScope.FromRegion(localCombatRegion));
        _openAttackFields[key] = openField;
        return openField;
    }

    public BattleFlowField GetOrBuildGoalField(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        IReadOnlyList<BattleCombatSlot> goals,
        BattleCombatSlotKind goalKind,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        GoalFlowFieldCacheKey key = new(actor, goals, goalKind, localCombatRegion);
        if (_goalFields.TryGetValue(key, out BattleFlowField field))
        {
            _performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        _performanceCounters?.RecordFlowFieldCacheMiss();
        // Direct slot-intent fields share the same battlefield scope as target
        // fields; actors sample the field, but actor id/current cell do not own it.
        field = BattleFlowFieldBuilder.BuildFromGoalSlots(
            actor,
            graph,
            goals,
            _performanceCounters,
            BattleFlowFieldSearchScope.FromRegion(localCombatRegion));
        _goalFields[key] = field;
        return field;
    }

    public BattleFlowField GetOrBuildObjective(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleGridCoord objectiveAnchor,
        int objectiveWidth,
        int objectiveHeight)
    {
        ObjectiveFlowFieldCacheKey key = ObjectiveFlowFieldCacheKey.Create(actor, objectiveAnchor, objectiveWidth, objectiveHeight);
        if (_objectiveFields.TryGetValue(key, out BattleFlowField field))
        {
            _performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        _performanceCounters?.RecordFlowFieldCacheMiss();
        BattleCombatSlot[] goals = BuildObjectiveGoals(actor, graph, objectiveAnchor, objectiveWidth, objectiveHeight);
        field = BattleFlowFieldBuilder.BuildFromGoalSlots(actor, graph, goals, _performanceCounters);
        _objectiveFields[key] = field;
        return field;
    }

    private static BattleCombatSlot[] BuildObjectiveGoals(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleGridCoord objectiveAnchor,
        int objectiveWidth,
        int objectiveHeight)
    {
        if (actor == null || graph == null)
        {
            return Array.Empty<BattleCombatSlot>();
        }

        int width = Math.Max(1, objectiveWidth);
        int height = Math.Max(1, objectiveHeight);
        var goals = new List<BattleCombatSlot>();
        for (int y = objectiveAnchor.Y; y < objectiveAnchor.Y + height; y++)
        {
            for (int x = objectiveAnchor.X; x < objectiveAnchor.X + width; x++)
            {
                BattleGridCoord anchor = new(x, y, objectiveAnchor.Height);
                if (!graph.CanPlaceFootprint(actor, anchor))
                {
                    continue;
                }

                goals.Add(new BattleCombatSlot(anchor, BattleCombatSlotKind.Support, 0, goals.Count));
            }
        }

        return goals.ToArray();
    }

    private readonly record struct TargetFlowFieldCacheKey(
        bool PreferSupportSlots,
        string TargetActorId,
        int TargetX,
        int TargetY,
        int TargetHeight,
        int ActorFootprintWidth,
        int ActorFootprintHeight,
        int ActorAttackRange,
        string LocalRegionId,
        int LocalRegionVersion,
        int LocalRegionCenterX,
        int LocalRegionCenterY,
        int LocalRegionCenterHeight,
        int LocalRegionWidth,
        int LocalRegionHeight)
    {
        public static TargetFlowFieldCacheKey Create(
            BattleRuntimeActor actor,
            BattleRuntimeActor target,
            bool preferSupportSlots,
            BattleTacticalRegionSnapshot localCombatRegion)
        {
            return new TargetFlowFieldCacheKey(
                preferSupportSlots,
                target?.ActorId ?? "",
                target?.GridX ?? 0,
                target?.GridY ?? 0,
                target?.GridHeight ?? 0,
                actor?.FootprintWidth ?? 1,
                actor?.FootprintHeight ?? 1,
                actor?.AttackRange ?? 1,
                localCombatRegion?.RegionId ?? "",
                localCombatRegion?.Version ?? 0,
                localCombatRegion?.CenterCellX ?? 0,
                localCombatRegion?.CenterCellY ?? 0,
                localCombatRegion?.CenterCellHeight ?? 0,
                localCombatRegion?.Width ?? 0,
                localCombatRegion?.Height ?? 0);
        }
    }

    private readonly record struct ObjectiveFlowFieldCacheKey(
        int ObjectiveX,
        int ObjectiveY,
        int ObjectiveHeight,
        int ObjectiveWidth,
        int ObjectiveRegionHeight,
        int ActorFootprintWidth,
        int ActorFootprintHeight)
    {
        public static ObjectiveFlowFieldCacheKey Create(
            BattleRuntimeActor actor,
            BattleGridCoord objectiveAnchor,
            int objectiveWidth,
            int objectiveHeight)
        {
            return new ObjectiveFlowFieldCacheKey(
                objectiveAnchor.X,
                objectiveAnchor.Y,
                objectiveAnchor.Height,
                Math.Max(1, objectiveWidth),
                Math.Max(1, objectiveHeight),
                actor?.FootprintWidth ?? 1,
                actor?.FootprintHeight ?? 1);
        }
    }

    private readonly struct OpenAttackFlowFieldCacheKey : IEquatable<OpenAttackFlowFieldCacheKey>
    {
        private readonly BattleGridCoord[] _goalAnchors;

        public OpenAttackFlowFieldCacheKey(
            BattleRuntimeActor actor,
            IReadOnlyList<BattleCombatSlot> openAttackGoals,
            BattleTacticalRegionSnapshot localCombatRegion)
        {
            ActorFootprintWidth = actor?.FootprintWidth ?? 1;
            ActorFootprintHeight = actor?.FootprintHeight ?? 1;
            ActorAttackRange = actor?.AttackRange ?? 1;
            LocalRegionId = localCombatRegion?.RegionId ?? "";
            LocalRegionVersion = localCombatRegion?.Version ?? 0;
            LocalRegionCenterX = localCombatRegion?.CenterCellX ?? 0;
            LocalRegionCenterY = localCombatRegion?.CenterCellY ?? 0;
            LocalRegionCenterHeight = localCombatRegion?.CenterCellHeight ?? 0;
            LocalRegionWidth = localCombatRegion?.Width ?? 0;
            LocalRegionHeight = localCombatRegion?.Height ?? 0;
            _goalAnchors = new BattleGridCoord[openAttackGoals?.Count ?? 0];
            for (int i = 0; i < _goalAnchors.Length; i++)
            {
                _goalAnchors[i] = openAttackGoals[i].Anchor;
            }
        }

        private int ActorFootprintWidth { get; }
        private int ActorFootprintHeight { get; }
        private int ActorAttackRange { get; }
        private string LocalRegionId { get; }
        private int LocalRegionVersion { get; }
        private int LocalRegionCenterX { get; }
        private int LocalRegionCenterY { get; }
        private int LocalRegionCenterHeight { get; }
        private int LocalRegionWidth { get; }
        private int LocalRegionHeight { get; }

        public bool Equals(OpenAttackFlowFieldCacheKey other)
        {
            if (ActorFootprintWidth != other.ActorFootprintWidth ||
                ActorFootprintHeight != other.ActorFootprintHeight ||
                ActorAttackRange != other.ActorAttackRange ||
                !string.Equals(LocalRegionId, other.LocalRegionId, StringComparison.Ordinal) ||
                LocalRegionVersion != other.LocalRegionVersion ||
                LocalRegionCenterX != other.LocalRegionCenterX ||
                LocalRegionCenterY != other.LocalRegionCenterY ||
                LocalRegionCenterHeight != other.LocalRegionCenterHeight ||
                LocalRegionWidth != other.LocalRegionWidth ||
                LocalRegionHeight != other.LocalRegionHeight ||
                _goalAnchors.Length != other._goalAnchors.Length)
            {
                return false;
            }

            for (int i = 0; i < _goalAnchors.Length; i++)
            {
                if (_goalAnchors[i] != other._goalAnchors[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is OpenAttackFlowFieldCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(ActorFootprintWidth);
            hash.Add(ActorFootprintHeight);
            hash.Add(ActorAttackRange);
            hash.Add(LocalRegionId, StringComparer.Ordinal);
            hash.Add(LocalRegionVersion);
            hash.Add(LocalRegionCenterX);
            hash.Add(LocalRegionCenterY);
            hash.Add(LocalRegionCenterHeight);
            hash.Add(LocalRegionWidth);
            hash.Add(LocalRegionHeight);
            for (int i = 0; i < _goalAnchors.Length; i++)
            {
                hash.Add(_goalAnchors[i]);
            }

            return hash.ToHashCode();
        }
    }

    private readonly struct GoalFlowFieldCacheKey : IEquatable<GoalFlowFieldCacheKey>
    {
        private readonly BattleGridCoord[] _goalAnchors;

        public GoalFlowFieldCacheKey(
            BattleRuntimeActor actor,
            IReadOnlyList<BattleCombatSlot> goals,
            BattleCombatSlotKind goalKind,
            BattleTacticalRegionSnapshot localCombatRegion)
        {
            GoalKind = goalKind;
            ActorFootprintWidth = actor?.FootprintWidth ?? 1;
            ActorFootprintHeight = actor?.FootprintHeight ?? 1;
            ActorAttackRange = actor?.AttackRange ?? 1;
            LocalRegionId = localCombatRegion?.RegionId ?? "";
            LocalRegionVersion = localCombatRegion?.Version ?? 0;
            LocalRegionCenterX = localCombatRegion?.CenterCellX ?? 0;
            LocalRegionCenterY = localCombatRegion?.CenterCellY ?? 0;
            LocalRegionCenterHeight = localCombatRegion?.CenterCellHeight ?? 0;
            LocalRegionWidth = localCombatRegion?.Width ?? 0;
            LocalRegionHeight = localCombatRegion?.Height ?? 0;
            _goalAnchors = new BattleGridCoord[goals?.Count ?? 0];
            for (int i = 0; i < _goalAnchors.Length; i++)
            {
                _goalAnchors[i] = goals[i].Anchor;
            }
        }

        private BattleCombatSlotKind GoalKind { get; }
        private int ActorFootprintWidth { get; }
        private int ActorFootprintHeight { get; }
        private int ActorAttackRange { get; }
        private string LocalRegionId { get; }
        private int LocalRegionVersion { get; }
        private int LocalRegionCenterX { get; }
        private int LocalRegionCenterY { get; }
        private int LocalRegionCenterHeight { get; }
        private int LocalRegionWidth { get; }
        private int LocalRegionHeight { get; }

        public bool Equals(GoalFlowFieldCacheKey other)
        {
            if (GoalKind != other.GoalKind ||
                ActorFootprintWidth != other.ActorFootprintWidth ||
                ActorFootprintHeight != other.ActorFootprintHeight ||
                ActorAttackRange != other.ActorAttackRange ||
                !string.Equals(LocalRegionId, other.LocalRegionId, StringComparison.Ordinal) ||
                LocalRegionVersion != other.LocalRegionVersion ||
                LocalRegionCenterX != other.LocalRegionCenterX ||
                LocalRegionCenterY != other.LocalRegionCenterY ||
                LocalRegionCenterHeight != other.LocalRegionCenterHeight ||
                LocalRegionWidth != other.LocalRegionWidth ||
                LocalRegionHeight != other.LocalRegionHeight ||
                _goalAnchors.Length != other._goalAnchors.Length)
            {
                return false;
            }

            for (int i = 0; i < _goalAnchors.Length; i++)
            {
                if (_goalAnchors[i] != other._goalAnchors[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GoalFlowFieldCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(GoalKind);
            hash.Add(ActorFootprintWidth);
            hash.Add(ActorFootprintHeight);
            hash.Add(ActorAttackRange);
            hash.Add(LocalRegionId, StringComparer.Ordinal);
            hash.Add(LocalRegionVersion);
            hash.Add(LocalRegionCenterX);
            hash.Add(LocalRegionCenterY);
            hash.Add(LocalRegionCenterHeight);
            hash.Add(LocalRegionWidth);
            hash.Add(LocalRegionHeight);
            for (int i = 0; i < _goalAnchors.Length; i++)
            {
                hash.Add(_goalAnchors[i]);
            }

            return hash.ToHashCode();
        }
    }
}
