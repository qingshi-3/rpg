using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

// The decision phase owns tick-start facts and actor decision context assembly.
// Later phases consume the returned contexts; they do not rebuild decision truth.
internal static class BattleRuntimeDecisionPhaseCoordinator
{
    internal static BattleRuntimeDecisionPhaseResult AdvanceDecisionPhase(
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
        IReadOnlySet<string> movementCompletedActorIds,
        IReadOnlySet<string> skillConsumedActorIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Actors);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(navigationGraph);
        ArgumentNullException.ThrowIfNull(aiExecutor);
        ArgumentNullException.ThrowIfNull(occupancy);
        ArgumentNullException.ThrowIfNull(movementCompletedActorIds);
        ArgumentNullException.ThrowIfNull(skillConsumedActorIds);

        BattleRuntimeActor[] livingCorps = BattleTacticalObservationUpdater.RefreshAtTickStart(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds);
        if (livingCorps.Length == 0)
        {
            return BattleRuntimeDecisionPhaseResult.Empty;
        }

        Dictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts = BattleTickStartProjectionBuilder.BuildFactMap(livingCorps);
        BattleRuntimeActor[] decisionReadyCorps = livingCorps
            .Where(item => item.Phase == BattleRuntimeActorPhase.AnchoredDecision &&
                           !movementCompletedActorIds.Contains(item.ActorId ?? "") &&
                           !skillConsumedActorIds.Contains(item.ActorId ?? ""))
            .ToArray();
        performanceCounters?.RecordDecisionReadyActors(decisionReadyCorps.Length);

        List<BattleRuntimeTickContext> contexts = decisionReadyCorps
            .Select(item => BattleRuntimeDecisionContextBuilder.Build(
                item,
                tickStartFacts,
                navigationGraph,
                occupancy,
                performanceCounters,
                battleId,
                currentTimeSeconds,
                tick,
                navigationFailureDiagnostics,
                state.TacticalStateStore,
                state.GroupActionZones,
                state.CombatZones,
                state.BeaconFlowFields,
                state.DestinationBeacons,
                aiExecutor))
            .ToList();
        BattleDecisionOutcomeApplier.Apply(
            contexts,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            BattleAdvanceFailureStateBoundary.RecordAdvanceFailure,
            BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState);

        HashSet<string> continuationEligibleMovementCompletedActorIds = movementCompletedActorIds
            .Where(actorId => !skillConsumedActorIds.Contains(actorId ?? ""))
            .ToHashSet(StringComparer.Ordinal);
        BattleMovementReservationMap continuationCandidateReservations = new();
        contexts.AddRange(BattleMovementController.BuildContinuationContexts(
            continuationEligibleMovementCompletedActorIds,
            tickStartFacts,
            navigationGraph,
            occupancy,
            continuationCandidateReservations,
            performanceCounters,
            battleId,
            currentTimeSeconds,
            tick,
            navigationFailureDiagnostics,
            state.TacticalStateStore,
            state.GroupActionZones,
            state.CombatZones,
            state.BeaconFlowFields,
            state.DestinationBeacons));

        return new BattleRuntimeDecisionPhaseResult(
            hasLivingActors: true,
            tickStartFacts,
            contexts,
            decisionReadyCorps.Length);
    }
}

internal sealed class BattleRuntimeDecisionPhaseResult
{
    internal static BattleRuntimeDecisionPhaseResult Empty { get; } = new(
        hasLivingActors: false,
        new Dictionary<string, BattleRuntimeTickStartActorFact>(StringComparer.Ordinal),
        new List<BattleRuntimeTickContext>(),
        decisionReadyActorCount: 0);

    internal BattleRuntimeDecisionPhaseResult(
        bool hasLivingActors,
        Dictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        List<BattleRuntimeTickContext> contexts,
        int decisionReadyActorCount)
    {
        ArgumentNullException.ThrowIfNull(tickStartFacts);
        ArgumentNullException.ThrowIfNull(contexts);
        if (decisionReadyActorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decisionReadyActorCount));
        }

        HasLivingActors = hasLivingActors;
        TickStartFacts = tickStartFacts;
        Contexts = contexts;
        DecisionReadyActorCount = decisionReadyActorCount;
    }

    internal bool HasLivingActors { get; }

    internal Dictionary<string, BattleRuntimeTickStartActorFact> TickStartFacts { get; }

    internal List<BattleRuntimeTickContext> Contexts { get; }

    internal int DecisionReadyActorCount { get; }
}
