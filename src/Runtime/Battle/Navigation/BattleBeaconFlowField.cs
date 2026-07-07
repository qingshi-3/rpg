using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleBeaconFlowField
{
    private readonly Dictionary<BattleGridCoord, int> _distanceByAnchor;

    internal BattleBeaconFlowField(
        BattleBeaconFlowFieldKey key,
        Dictionary<BattleGridCoord, int> distanceByAnchor,
        int searchedNodeCount)
    {
        Key = key;
        _distanceByAnchor = distanceByAnchor ?? new Dictionary<BattleGridCoord, int>();
        SearchedNodeCount = System.Math.Max(0, searchedNodeCount);
    }

    internal BattleBeaconFlowFieldKey Key { get; }
    internal int SearchedNodeCount { get; }

    internal bool TryGetDistance(BattleGridCoord anchor, out int distance)
    {
        return _distanceByAnchor.TryGetValue(anchor, out distance);
    }
}

internal readonly record struct BattleBeaconFlowFieldKey(
    string BeaconId,
    int BeaconRevision,
    int TopologyIdentity,
    int DestinationX,
    int DestinationY,
    int DestinationHeight,
    int FootprintWidth,
    int FootprintHeight);
