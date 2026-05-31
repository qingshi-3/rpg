using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleFlowFieldCache
{
    private readonly Dictionary<string, BattleFlowField> _fields = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, BattleFlowField> _openAttackFields = new(System.StringComparer.Ordinal);
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
        string key = BuildKey(actor, target, preferSupportSlots, localCombatRegion);
        if (_fields.TryGetValue(key, out BattleFlowField field))
        {
            _performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        _performanceCounters?.RecordFlowFieldCacheMiss();
        field = BattleFlowFieldBuilder.Build(actor, target, graph, preferSupportSlots, _performanceCounters, localCombatRegion);
        _fields[key] = field;
        return field;
    }

    public BattleFlowField PreferOpenAttackSlots(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleFlowField field)
    {
        if (actor == null || graph == null || occupancy == null || field?.GoalSlots == null)
        {
            return field;
        }

        BattleCombatSlot[] attackGoals = field.GoalSlots
            .Where(item => item.Kind == BattleCombatSlotKind.Attack)
            .ToArray();
        if (attackGoals.Length == 0)
        {
            return field;
        }

        BattleCombatSlot[] openAttackGoals = attackGoals
            .Where(item => occupancy.CountOtherOccupiedCells(actor, item.Anchor) == 0)
            .ToArray();
        if (openAttackGoals.Length == attackGoals.Length)
        {
            return field;
        }

        _performanceCounters?.RecordOpenAttackFlowFieldRequest();
        string key = BuildOpenAttackKey(actor, openAttackGoals);
        if (_openAttackFields.TryGetValue(key, out BattleFlowField openField))
        {
            _performanceCounters?.RecordOpenAttackFlowFieldCacheHit();
            return openField;
        }

        _performanceCounters?.RecordOpenAttackFlowFieldBuild();
        // This cache is owned by one Runtime advance; reusing identical open-slot facts avoids
        // duplicate integration fields without relaxing tick-start occupancy semantics.
        openField = BattleFlowFieldBuilder.BuildFromGoalSlots(actor, graph, openAttackGoals, _performanceCounters);
        _openAttackFields[key] = openField;
        return openField;
    }

    public BattleFlowField GetOrBuildObjective(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleGridCoord objectiveAnchor,
        int objectiveWidth,
        int objectiveHeight)
    {
        string key = BuildObjectiveKey(actor, objectiveAnchor, objectiveWidth, objectiveHeight);
        if (_fields.TryGetValue(key, out BattleFlowField field))
        {
            _performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        _performanceCounters?.RecordFlowFieldCacheMiss();
        BattleCombatSlot[] goals = BuildObjectiveGoals(actor, graph, objectiveAnchor, objectiveWidth, objectiveHeight);
        field = BattleFlowFieldBuilder.BuildFromGoalSlots(actor, graph, goals, _performanceCounters);
        _fields[key] = field;
        return field;
    }

    private static string BuildKey(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        bool preferSupportSlots,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        return string.Join("|",
            preferSupportSlots ? "support" : "attack",
            target?.ActorId ?? "",
            target?.GridX.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            target?.GridY.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            target?.GridHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            actor?.FootprintWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.FootprintHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.AttackRange.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            localCombatRegion?.RegionId ?? "",
            localCombatRegion?.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            localCombatRegion?.CenterCellX.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            localCombatRegion?.CenterCellY.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            localCombatRegion?.CenterCellHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            localCombatRegion?.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            localCombatRegion?.Height.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0");
    }

    private static string BuildOpenAttackKey(BattleRuntimeActor actor, IReadOnlyList<BattleCombatSlot> openAttackGoals)
    {
        return string.Join("|",
            "open_attack",
            actor?.FootprintWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.FootprintHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.AttackRange.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            string.Join(
                ";",
                (openAttackGoals ?? System.Array.Empty<BattleCombatSlot>())
                    .Select(item => string.Join(
                        ",",
                        item.Anchor.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.Anchor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.Anchor.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
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
            return System.Array.Empty<BattleCombatSlot>();
        }

        int width = System.Math.Max(1, objectiveWidth);
        int height = System.Math.Max(1, objectiveHeight);
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

    private static string BuildObjectiveKey(
        BattleRuntimeActor actor,
        BattleGridCoord objectiveAnchor,
        int objectiveWidth,
        int objectiveHeight)
    {
        return string.Join("|",
            "objective",
            objectiveAnchor.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
            objectiveAnchor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
            objectiveAnchor.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Math.Max(1, objectiveWidth).ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Math.Max(1, objectiveHeight).ToString(System.Globalization.CultureInfo.InvariantCulture),
            actor?.FootprintWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.FootprintHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1");
    }
}
