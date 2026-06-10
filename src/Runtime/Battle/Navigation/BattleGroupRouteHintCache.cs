using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleGroupRouteHintCache
{
    private readonly BattleRouteTopology _topology;
    private readonly Dictionary<RouteHintCacheKey, BattleRouteHint> _cache = new();

    public BattleGroupRouteHintCache(BattleRouteTopology topology)
    {
        _topology = topology;
    }

    public int QueryBuildCount { get; private set; }

    public bool TryResolve(BattleRouteQuery query, out BattleRouteHint hint)
    {
        return TryResolve(query, out hint, out _);
    }

    public bool TryResolve(BattleRouteQuery query, out BattleRouteHint hint, out BattleRouteHintResolution resolution)
    {
        hint = default;
        resolution = default;
        if (_topology == null)
        {
            resolution = BattleRouteHintResolution.Create("topology_missing", "", "", false, false, QueryBuildCount);
            return false;
        }

        BattleRouteRegionId sourceRegion = _topology.GetRegionId(query.SourceAnchor);
        BattleRouteRegionId targetRegion = _topology.GetRegionId(query.TargetAnchor);
        string sourceRegionId = sourceRegion.ToString();
        string targetRegionId = targetRegion.ToString();
        RouteHintCacheKey key = new(
            query.BattleGroupId ?? "",
            query.IntentId ?? "",
            query.Profile,
            sourceRegionId,
            targetRegionId,
            _topology.Version);
        if (_cache.TryGetValue(key, out hint))
        {
            resolution = BattleRouteHintResolution.Create("cache_hit", sourceRegionId, targetRegionId, true, false, QueryBuildCount);
            return true;
        }

        QueryBuildCount++;
        if (!_topology.TryFindRoute(query, out hint))
        {
            resolution = BattleRouteHintResolution.Create("not_found", sourceRegionId, targetRegionId, false, true, QueryBuildCount);
            return false;
        }

        _cache[key] = hint;
        resolution = BattleRouteHintResolution.Create("built", sourceRegionId, targetRegionId, true, true, QueryBuildCount);
        return true;
    }

    private readonly record struct RouteHintCacheKey(
        string BattleGroupId,
        string IntentId,
        BattleRouteProfile Profile,
        string SourceRegionId,
        string TargetRegionId,
        int TopologyVersion);
}

internal readonly record struct BattleRouteHintResolution(
    string Result,
    string SourceRegionId,
    string TargetRegionId,
    bool Success,
    bool BuiltThisQuery,
    int QueryBuildCount)
{
    public static BattleRouteHintResolution Create(
        string result,
        string sourceRegionId,
        string targetRegionId,
        bool success,
        bool builtThisQuery,
        int queryBuildCount)
    {
        return new BattleRouteHintResolution(
            result ?? "",
            sourceRegionId ?? "",
            targetRegionId ?? "",
            success,
            builtThisQuery,
            queryBuildCount);
    }
}
