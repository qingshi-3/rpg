using System.Collections.Generic;
using System.Linq;
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
        bool preferSupportSlots)
    {
        string key = BuildKey(actor, target, preferSupportSlots);
        if (_fields.TryGetValue(key, out BattleFlowField field))
        {
            _performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        _performanceCounters?.RecordFlowFieldCacheMiss();
        field = BattleFlowFieldBuilder.Build(actor, target, graph, preferSupportSlots, _performanceCounters);
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

    private static string BuildKey(BattleRuntimeActor actor, BattleRuntimeActor target, bool preferSupportSlots)
    {
        return string.Join("|",
            preferSupportSlots ? "support" : "attack",
            target?.ActorId ?? "",
            target?.GridX.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            target?.GridY.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            target?.GridHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            actor?.FootprintWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.FootprintHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1",
            actor?.AttackRange.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1");
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
}
