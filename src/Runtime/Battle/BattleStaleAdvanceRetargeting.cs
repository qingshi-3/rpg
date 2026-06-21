using System;
using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleStaleAdvanceRetargeting
{
    internal static TryRetargetStaleAdvanceContextCallback CreateCallback(IBattleRuntimeAiExecutor aiExecutor)
    {
        ArgumentNullException.ThrowIfNull(aiExecutor);

        return (context,
            tickStartFacts,
            occupancy,
            navigationGraph,
            battleId,
            tick,
            currentTimeSeconds,
            navigationFailureDiagnostics,
            performanceCounters,
            tacticalStateStore,
            groupActionZones,
            combatZones) => TryRetarget(
                context,
                tickStartFacts,
                occupancy,
                navigationGraph,
                battleId,
                tick,
                currentTimeSeconds,
                navigationFailureDiagnostics,
                performanceCounters,
                tacticalStateStore,
                groupActionZones,
                combatZones,
                aiExecutor);
    }

    internal static bool TryRetarget(
        BattleRuntimeTickContext context,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleDynamicOccupancy occupancy,
        BattleNavigationGraph navigationGraph,
        string battleId,
        int tick,
        double currentTimeSeconds,
        HashSet<string> navigationFailureDiagnostics,
        BattlePerformanceCounters performanceCounters,
        BattleGroupTacticalStateStore tacticalStateStore,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> groupActionZones,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        IBattleRuntimeAiExecutor aiExecutor)
    {
        if (context == null ||
            !IsRetargetableStaleAdvance(context.Request.Kind) ||
            context.ActorFact.Actor.HitPoints <= 0)
        {
            return false;
        }

        BattleRuntimeTickContext refreshed = BattleRuntimeDecisionContextBuilder.Build(
            context.ActorFact.Actor,
            tickStartFacts,
            navigationGraph,
            occupancy,
            performanceCounters,
            battleId,
            currentTimeSeconds,
            tick,
            navigationFailureDiagnostics,
            tacticalStateStore,
            groupActionZones,
            combatZones,
            aiExecutor);
        if (refreshed.TargetFact == null ||
            refreshed.TargetFact.Value.Actor.HitPoints <= 0 ||
            !IsRetargetableStaleAdvance(refreshed.Request.Kind) ||
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
        BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState(context.ActorFact.Actor);
        return true;
    }

    private static bool IsRetargetableStaleAdvance(BattleRuntimeAiActionKind kind)
    {
        return kind is BattleRuntimeAiActionKind.AdvanceTowardTarget or
            BattleRuntimeAiActionKind.JoinLocalCombat or
            BattleRuntimeAiActionKind.HoldSupport;
    }
}
