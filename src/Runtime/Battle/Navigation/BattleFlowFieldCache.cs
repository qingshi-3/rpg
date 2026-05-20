using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleFlowFieldCache
{
    private readonly Dictionary<string, BattleFlowField> _fields = new(System.StringComparer.Ordinal);
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
}
