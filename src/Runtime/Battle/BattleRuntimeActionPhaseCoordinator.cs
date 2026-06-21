using System;
using System.Collections.Generic;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

// The action phase is the actor-time boundary before tactical observation:
// movement completions, recovery expiry, and ability ticks settle before new decisions.
internal static class BattleRuntimeActionPhaseCoordinator
{
    internal static BattleRuntimeActionPhaseResult AdvanceActionPhase(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph)
    {
        if (state?.Actors == null || stream == null)
        {
            return BattleRuntimeActionPhaseResult.Empty;
        }

        BattleMovementBoundaryAdvanceResult movementBoundary = BattleMovementBoundaryCoordinator.AdvanceBoundaries(
            state.Actors,
            currentTimeSeconds);
        foreach (BattleMovementBoundaryEvent completedBoundary in movementBoundary.CompletedBoundaries)
        {
            stream.Add(BattleRuntimeEventFactory.CreateMovementEvent(
                BattleEventKind.MovementCompleted,
                battleId,
                tick,
                currentTimeSeconds,
                completedBoundary.Actor,
                completedBoundary.Actor.TargetActorId ?? "",
                completedBoundary.From,
                completedBoundary.To,
                completedBoundary.ReasonCode));
        }

        BattleActionController.AdvanceAttackRecoveryBoundaries(state.Actors, currentTimeSeconds);

        // Pause-time skill commands only enter this phase when Runtime time advances,
        // so command acceptance never mutates combat facts while the battle is frozen.
        HashSet<string> skillWaitingAfterMovementActorIds = new(StringComparer.Ordinal);
        HashSet<string> skillActionActorIds = BattleAbilityTickCoordinator.ResolvePending(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph,
            movementBoundary.CompletedActorIds,
            skillWaitingAfterMovementActorIds);
        HashSet<string> skillConsumedActorIds = new(skillActionActorIds, StringComparer.Ordinal);
        skillConsumedActorIds.UnionWith(skillWaitingAfterMovementActorIds);

        return new BattleRuntimeActionPhaseResult(
            movementBoundary.Occupancy,
            movementBoundary.CompletedActorIds,
            skillConsumedActorIds);
    }
}

internal sealed class BattleRuntimeActionPhaseResult
{
    internal static BattleRuntimeActionPhaseResult Empty { get; } = new(
        BattleDynamicOccupancy.FromActors(Array.Empty<BattleRuntimeActor>()),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    internal BattleRuntimeActionPhaseResult(
        BattleDynamicOccupancy occupancy,
        HashSet<string> movementCompletedActorIds,
        HashSet<string> skillConsumedActorIds)
    {
        Occupancy = occupancy ?? BattleDynamicOccupancy.FromActors(Array.Empty<BattleRuntimeActor>());
        MovementCompletedActorIds = movementCompletedActorIds ?? new HashSet<string>(StringComparer.Ordinal);
        SkillConsumedActorIds = skillConsumedActorIds ?? new HashSet<string>(StringComparer.Ordinal);
    }

    internal BattleDynamicOccupancy Occupancy { get; }

    internal HashSet<string> MovementCompletedActorIds { get; }

    internal HashSet<string> SkillConsumedActorIds { get; }
}
