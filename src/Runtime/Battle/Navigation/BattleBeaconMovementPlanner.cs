using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Navigation;

internal static partial class BattleCrowdMovementPlanner
{
    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardBeacon(
        BattleRuntimeActor actor,
        BattleRuntimeDestinationBeacon beacon,
        BattleBeaconFlowField flowField,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null ||
            beacon == null ||
            flowField == null ||
            graph == null ||
            occupancy == null ||
            reservations == null)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!flowField.TryGetDistance(start, out int startDistance) ||
            start == beacon.Anchor)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                IsRecentBacktrackStep(actor, start, neighbor) ||
                !flowField.TryGetDistance(neighbor, out int candidateDistance) ||
                candidateDistance >= startDistance)
            {
                continue;
            }

            // The flow field is only an intent gradient. Runtime still commits
            // one reserved neighbor at a time through the normal movement boundary.
            int score = candidateDistance * FlowCostWeight +
                        BattleLocalRegionPreference.GetStepPenalty(neighbor, localCombatRegion) +
                        GetStepCost(start, neighbor);
            options.Add(new MoveOption(neighbor, score, candidateDistance));
        }

        return SortMoveOptions(options);
    }
}
