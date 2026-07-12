using System.Collections.Generic;
using System.Diagnostics;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// The resolution phase owns post-decision world resolution entry points.
// TickResolver keeps phase order; this coordinator keeps attack/move commits adjacent.
internal static class BattleRuntimeResolutionPhaseCoordinator
{
    internal static BattleRuntimeResolutionPhaseResult AdvanceResolutionPhase(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters,
        IBattleRuntimeAiExecutor aiExecutor,
        BattleDynamicOccupancy occupancy,
        IReadOnlyCollection<string> movementCompletedActorIds,
        BattleRuntimeDecisionPhaseResult decisionPhase)
    {
        System.ArgumentNullException.ThrowIfNull(state);
        System.ArgumentNullException.ThrowIfNull(state.Actors);
        System.ArgumentNullException.ThrowIfNull(stream);
        System.ArgumentNullException.ThrowIfNull(navigationGraph);
        System.ArgumentNullException.ThrowIfNull(navigationFailureDiagnostics);
        System.ArgumentNullException.ThrowIfNull(aiExecutor);
        System.ArgumentNullException.ThrowIfNull(occupancy);
        System.ArgumentNullException.ThrowIfNull(movementCompletedActorIds);
        System.ArgumentNullException.ThrowIfNull(decisionPhase);

        Dictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts = decisionPhase.TickStartFacts;
        List<BattleRuntimeTickContext> contexts = decisionPhase.Contexts;

        // Gather all due basic-attack impacts before one deterministic commit,
        // including mixed delayed and instant impacts in the same tick.
        BattleAttackEngagementCoordinator.Resolve(contexts, tickStartFacts, stream, battleId, tick, currentTimeSeconds, state);
        long movementResolveStartedAt = Stopwatch.GetTimestamp();
        int movementEvents = BattleMovementCommitResolver.Resolve(
            contexts,
            tickStartFacts,
            occupancy,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph,
            navigationFailureDiagnostics,
            performanceCounters,
            state.TacticalStateStore,
            state.GroupActionZones,
            state.CombatZones,
            BattleStaleAdvanceRetargeting.CreateCallback(aiExecutor));
        performanceCounters?.RecordMovementResolveElapsedTicks(Stopwatch.GetTimestamp() - movementResolveStartedAt);
        performanceCounters?.RecordActorsReadyNoMoveLastAdvance(decisionPhase.DecisionReadyActorCount - movementEvents);
        BattleMovementController.ClearEndedMovementChains(state.Actors, movementCompletedActorIds);
        BattleGroupCommanderTransitionCoordinator.Apply(
            state,
            contexts,
            stream,
            battleId,
            tick,
            currentTimeSeconds);

        return new BattleRuntimeResolutionPhaseResult(contexts, movementEvents);
    }
}

internal sealed class BattleRuntimeResolutionPhaseResult
{
    internal static BattleRuntimeResolutionPhaseResult Empty { get; } = new(
        new List<BattleRuntimeTickContext>(),
        movementEventCount: 0);

    internal BattleRuntimeResolutionPhaseResult(
        List<BattleRuntimeTickContext> contexts,
        int movementEventCount)
    {
        System.ArgumentNullException.ThrowIfNull(contexts);
        Contexts = contexts;
        MovementEventCount = System.Math.Max(0, movementEventCount);
    }

    internal List<BattleRuntimeTickContext> Contexts { get; }

    internal int MovementEventCount { get; }
}
