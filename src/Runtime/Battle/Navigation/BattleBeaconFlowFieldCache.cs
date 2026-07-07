using System.Collections.Generic;
using System.Diagnostics;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleBeaconFlowFieldCache
{
    private readonly Dictionary<BattleBeaconFlowFieldKey, BattleBeaconFlowField> _fields = new();

    internal BattleBeaconFlowField GetOrBuild(
        BattleRuntimeDestinationBeacon beacon,
        BattleRuntimeActor actorProfile,
        BattleNavigationGraph graph,
        BattlePerformanceCounters performanceCounters)
    {
        if (beacon == null || actorProfile == null || graph == null)
        {
            return null;
        }

        BattleBeaconFlowFieldKey key = new(
            beacon.BeaconId ?? "",
            beacon.Revision,
            graph.TopologyIdentity,
            beacon.Anchor.X,
            beacon.Anchor.Y,
            beacon.Anchor.Height,
            BattleActorFootprint.NormalizeSize(actorProfile.FootprintWidth),
            BattleActorFootprint.NormalizeSize(actorProfile.FootprintHeight));
        if (_fields.TryGetValue(key, out BattleBeaconFlowField field))
        {
            performanceCounters?.RecordFlowFieldCacheHit();
            return field;
        }

        performanceCounters?.RecordFlowFieldCacheMiss();
        long startedAt = Stopwatch.GetTimestamp();
        field = Build(key, actorProfile, beacon.Anchor, graph);
        performanceCounters?.RecordFlowFieldBuild();
        performanceCounters?.RecordFlowFieldBuildElapsedTicks(Stopwatch.GetTimestamp() - startedAt);
        performanceCounters?.RecordFlowFieldSearchNodes(field?.SearchedNodeCount ?? 0, scoped: true);
        _fields[key] = field;
        return field;
    }

    private static BattleBeaconFlowField Build(
        BattleBeaconFlowFieldKey key,
        BattleRuntimeActor actorProfile,
        BattleGridCoord destination,
        BattleNavigationGraph graph)
    {
        Dictionary<BattleGridCoord, int> distances = new();
        if (!graph.CanPlaceFootprint(actorProfile, destination))
        {
            return new BattleBeaconFlowField(key, distances, searchedNodeCount: 0);
        }

        PriorityQueue<BattleGridCoord, int> frontier = new();
        distances[destination] = 0;
        frontier.Enqueue(destination, 0);
        while (frontier.TryDequeue(out BattleGridCoord current, out int currentDistance))
        {
            if (distances.TryGetValue(current, out int knownDistance) &&
                currentDistance > knownDistance)
            {
                continue;
            }

            foreach (BattleGridCoord previous in graph.GetIncomingNeighbors(current))
            {
                if (!BattlePathStepRules.CanUseStaticStep(actorProfile, previous, current, graph))
                {
                    continue;
                }

                int nextDistance = currentDistance + graph.GetStepCost(previous, current, BattlePathCostPolicy.StepCost);
                if (distances.TryGetValue(previous, out int knownPreviousDistance) &&
                    knownPreviousDistance <= nextDistance)
                {
                    continue;
                }

                distances[previous] = nextDistance;
                frontier.Enqueue(previous, nextDistance);
            }
        }

        return new BattleBeaconFlowField(key, distances, distances.Count);
    }
}
