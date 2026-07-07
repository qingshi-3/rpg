using System.Collections.Generic;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleBeaconAdvancePlanner
{
    internal static bool HasActiveBeacon(BattleRuntimeActor actor)
    {
        return actor != null &&
               !string.IsNullOrWhiteSpace(actor.ActiveDestinationBeaconId);
    }

    internal static bool IsBeaconReached(BattleRuntimeTickStartActorFact actorFact)
    {
        BattleRuntimeActor actor = actorFact.Actor;
        if (!HasActiveBeacon(actor))
        {
            return false;
        }

        BattleGridCoord beaconAnchor = new(
            actor.ActiveDestinationBeaconGridX,
            actor.ActiveDestinationBeaconGridY,
            actor.ActiveDestinationBeaconGridHeight);
        return actorFact.Anchor == beaconAnchor;
    }

    internal static BattleRuntimeTickContext BuildBeaconAdvanceContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleBeaconFlowFieldCache flowFieldCache,
        IReadOnlyList<BattleRuntimeDestinationBeacon> beacons,
        BattlePerformanceCounters performanceCounters)
    {
        BattleRuntimeActor actor = actorFact.Actor;
        BattleRuntimeDestinationBeacon beacon = ResolveActiveBeacon(actor, beacons);
        if (beacon == null ||
            !beacon.IsValid ||
            IsBeaconReached(actorFact))
        {
            return BattleRuntimeTickContextFactory.Create(
                request,
                actorFact,
                null,
                hasMoveTo: false,
                moveTo: default,
                failureReason: beacon == null ? "destination_beacon_missing" : "destination_beacon_reached");
        }

        BattleBeaconFlowField field = flowFieldCache?.GetOrBuild(beacon, actor, navigationGraph, performanceCounters);
        IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardBeacon(
            actor,
            beacon,
            field,
            navigationGraph,
            occupancy,
            reservations);
        if (moveOptions.Count == 0)
        {
            return BattleRuntimeTickContextFactory.Create(
                request,
                actorFact,
                null,
                hasMoveTo: false,
                moveTo: default,
                failureReason: "destination_beacon_no_step");
        }

        return BattleRuntimeTickContextFactory.Create(
            request,
            actorFact,
            null,
            hasMoveTo: true,
            moveTo: moveOptions[0],
            failureReason: "",
            moveOptions: moveOptions,
            movementReasonCode: "destination_beacon_advance");
    }

    private static BattleRuntimeDestinationBeacon ResolveActiveBeacon(
        BattleRuntimeActor actor,
        IReadOnlyList<BattleRuntimeDestinationBeacon> beacons)
    {
        if (!HasActiveBeacon(actor))
        {
            return null;
        }

        return (beacons ?? System.Array.Empty<BattleRuntimeDestinationBeacon>())
            .FirstOrDefault(item =>
                item != null &&
                string.Equals(item.BeaconId ?? "", actor.ActiveDestinationBeaconId ?? "", System.StringComparison.Ordinal) &&
                item.Revision == actor.ActiveDestinationBeaconRevision);
    }
}
