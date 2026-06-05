using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private bool TryRetargetStaleAdvanceContext(
        BattleRuntimeTickContext context,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleDynamicOccupancy occupancy,
        BattleNavigationGraph navigationGraph,
        string battleId,
        int tick,
        double currentTimeSeconds,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters)
    {
        if (context == null ||
            context.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget ||
            context.ActorFact.Actor.HitPoints <= 0)
        {
            return false;
        }

        BattleFlowFieldCache flowFields = new(performanceCounters);
        BattleRuntimeTickContext refreshed = BuildTickContext(
            context.ActorFact.Actor,
            tickStartFacts,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            battleId,
            currentTimeSeconds,
            tick,
            navigationFailureDiagnostics,
            null,
            null,
            null);
        if (refreshed.TargetFact == null ||
            refreshed.TargetFact.Value.Actor.HitPoints <= 0 ||
            refreshed.Request.Kind != BattleRuntimeAiActionKind.AdvanceTowardTarget ||
            !refreshed.Proposal.HasMoveTo ||
            !string.IsNullOrWhiteSpace(refreshed.Proposal.FailureReason))
        {
            return false;
        }

        // Movement intents are built before same-tick damage is applied. If a
        // different actor kills that target first, this actor keeps its action
        // boundary and immediately spends it on the next live assault target.
        context.Request = refreshed.Request;
        context.TargetFact = refreshed.TargetFact;
        context.Proposal = refreshed.Proposal;
        context.ActorFact.Actor.TargetActorId = refreshed.TargetFact.Value.Actor.ActorId;
        ResetAdvanceFailureState(context.ActorFact.Actor);
        return true;
    }
}
