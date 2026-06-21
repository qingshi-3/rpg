using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private readonly IBattleRuntimeAiExecutor _aiExecutor;

    internal BattleRuntimeTickResolver(IBattleRuntimeAiExecutor aiExecutor)
    {
        System.ArgumentNullException.ThrowIfNull(aiExecutor);
        _aiExecutor = aiExecutor;
    }

    internal void ResolveTick(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleNavigationGraph navigationGraph,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters = null)
    {
        if (state?.Actors == null || stream == null || navigationGraph == null)
        {
            return;
        }

        performanceCounters?.BeginRuntimeAdvance();
        performanceCounters?.RecordRuntimeTick();
        BattleRuntimeActionPhaseResult actionPhase = BattleRuntimeActionPhaseCoordinator.AdvanceActionPhase(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph);
        BattleDynamicOccupancy occupancy = actionPhase.Occupancy;
        HashSet<string> movementCompletedActorIds = actionPhase.MovementCompletedActorIds;
        HashSet<string> skillConsumedActorIds = actionPhase.SkillConsumedActorIds;

        BattleRuntimeDecisionPhaseResult decisionPhase = BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph,
            navigationFailureDiagnostics,
            performanceCounters,
            _aiExecutor,
            occupancy,
            movementCompletedActorIds,
            skillConsumedActorIds);
        if (!decisionPhase.HasLivingActors)
        {
            return;
        }

        BattleRuntimeResolutionPhaseResult resolutionPhase = BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase(
            state,
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            navigationGraph,
            navigationFailureDiagnostics,
            performanceCounters,
            _aiExecutor,
            occupancy,
            movementCompletedActorIds,
            decisionPhase);
        BattleRuntimeActionDiagnostics.LogTickActionResults(
            resolutionPhase.Contexts,
            battleId,
            tick,
            currentTimeSeconds);
    }
}
