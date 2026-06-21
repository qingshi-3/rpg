using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

// World-side movement boundary coordination owns the pre-boundary occupancy
// snapshot. Actor movement controllers only advance their own phase boundary.
internal static class BattleMovementBoundaryCoordinator
{
    internal static BattleMovementBoundaryAdvanceResult AdvanceBoundaries(
        IEnumerable<BattleRuntimeActor> actors,
        double currentTimeSeconds)
    {
        BattleRuntimeActor[] actorList = (actors ?? Enumerable.Empty<BattleRuntimeActor>()).ToArray();
        BattleDynamicOccupancy occupancy = BuildPreBoundaryOccupancy(actorList);
        List<BattleMovementBoundaryEvent> completed = new();
        HashSet<string> completedActorIds = new(System.StringComparer.Ordinal);

        foreach (BattleRuntimeActor actor in actorList.Where(item => item != null && item.Kind == BattleRuntimeActorKind.Corps))
        {
            BattleMovementController movementController = new(actor);
            if (!movementController.AdvanceMovementBoundary(
                    currentTimeSeconds,
                    out BattleGridCoord movementFrom,
                    out BattleGridCoord movementTo,
                    out string movementBoundaryReasonCode))
            {
                continue;
            }

            completed.Add(new BattleMovementBoundaryEvent(
                actor,
                movementFrom,
                movementTo,
                movementBoundaryReasonCode));
            completedActorIds.Add(actor.ActorId ?? "");
        }

        return new BattleMovementBoundaryAdvanceResult(occupancy, completed, completedActorIds);
    }

    private static BattleDynamicOccupancy BuildPreBoundaryOccupancy(IEnumerable<BattleRuntimeActor> actors)
    {
        BattleRuntimeActor[] preBoundaryLivingCorps = (actors ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(item => item != null && item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, System.StringComparer.Ordinal)
            .ToArray();
        return BattleDynamicOccupancy.FromActors(preBoundaryLivingCorps);
    }
}

internal sealed class BattleMovementBoundaryAdvanceResult
{
    internal BattleMovementBoundaryAdvanceResult(
        BattleDynamicOccupancy occupancy,
        IReadOnlyList<BattleMovementBoundaryEvent> completedBoundaries,
        HashSet<string> completedActorIds)
    {
        Occupancy = occupancy ?? BattleDynamicOccupancy.FromActors(System.Array.Empty<BattleRuntimeActor>());
        CompletedBoundaries = completedBoundaries ?? System.Array.Empty<BattleMovementBoundaryEvent>();
        CompletedActorIds = completedActorIds ?? new HashSet<string>(System.StringComparer.Ordinal);
    }

    internal BattleDynamicOccupancy Occupancy { get; }

    internal IReadOnlyList<BattleMovementBoundaryEvent> CompletedBoundaries { get; }

    internal HashSet<string> CompletedActorIds { get; }
}

internal readonly record struct BattleMovementBoundaryEvent(
    BattleRuntimeActor Actor,
    BattleGridCoord From,
    BattleGridCoord To,
    string ReasonCode);
