using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// Actor results are folded once per commander boundary. Actor count can change
// execution work, but it cannot multiply commander transitions or semantic events.
internal static class BattleGroupCommanderTransitionCoordinator
{
    internal static void Apply(
        BattleRuntimeState state,
        IReadOnlyCollection<BattleRuntimeTickContext> contexts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        if (state?.TacticalStateStore == null || state.Actors == null || stream == null)
        {
            return;
        }

        IReadOnlyCollection<BattleRuntimeTickContext> availableContexts =
            contexts ?? System.Array.Empty<BattleRuntimeTickContext>();
        foreach (IGrouping<string, BattleRuntimeActor> memberGroup in state.Actors
                     .Where(actor => actor.Kind == BattleRuntimeActorKind.Corps)
                     .GroupBy(actor => actor.BattleGroupId ?? "", System.StringComparer.Ordinal)
                     .OrderBy(group => group.Key, System.StringComparer.Ordinal))
        {
            string battleGroupId = memberGroup.Key;
            BattleRuntimeActor[] members = memberGroup
                .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
                .ToArray();
            BattleRuntimeActor representative = members.FirstOrDefault();
            if (representative == null)
            {
                continue;
            }

            if (members.All(actor => actor.HitPoints <= 0))
            {
                BattlePlanStateEmitter.SetPlanState(
                    state.TacticalStateStore,
                    stream,
                    battleId,
                    tick,
                    currentTimeSeconds,
                    representative,
                    BattleGroupPlanRuntimeState.Defeated,
                    "defeated");
                continue;
            }

            BattleGroupTacticalState commanderState = state.TacticalStateStore.GetRequiredSnapshot(battleGroupId);
            if (commanderState.HasActiveTacticalCommand)
            {
                // Regroup/retreat own the commander plan until their explicit
                // completion or failure boundary; actor movement cannot demote
                // them into ordinary objective or combat transitions.
                continue;
            }

            CommanderTransition transition = availableContexts
                .Where(context => string.Equals(
                    context?.ActorFact.Actor.BattleGroupId ?? "",
                    battleGroupId,
                    System.StringComparison.Ordinal))
                .Select(ResolveTransition)
                .Where(candidate => candidate != null)
                .OrderByDescending(candidate => candidate!.Priority)
                .ThenBy(candidate => candidate!.State)
                .FirstOrDefault();
            if (transition == null)
            {
                continue;
            }

            BattlePlanStateEmitter.SetPlanState(
                state.TacticalStateStore,
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                representative,
                transition.State,
                transition.ReasonCode);
        }

        state.TacticalStateStore.SynchronizeActorExecutionCaches(state.Actors);
    }

    private static CommanderTransition ResolveTransition(BattleRuntimeTickContext context)
    {
        if (context?.Request == null)
        {
            return null;
        }

        if (context.Result?.Success == true)
        {
            return context.Request.Kind switch
            {
                BattleRuntimeAiActionKind.AttackTarget =>
                    new CommanderTransition(BattleGroupPlanRuntimeState.Attacking, "attacking", 100),
                BattleRuntimeAiActionKind.JoinLocalCombat or
                BattleRuntimeAiActionKind.AdvanceTowardTarget or
                BattleRuntimeAiActionKind.HoldSupport =>
                    new CommanderTransition(BattleGroupPlanRuntimeState.MovingToAttackSlot, "moving_to_attack_slot", 80),
                BattleRuntimeAiActionKind.AdvanceTowardBeacon =>
                    new CommanderTransition(BattleGroupPlanRuntimeState.AdvancingToBeacon, "destination_beacon_advance", 70),
                BattleRuntimeAiActionKind.ReturnToObjective =>
                    new CommanderTransition(BattleGroupPlanRuntimeState.AdvancingToObjective, LocalCombatDecisionReason.ReturnObjectiveThreatClear, 60),
                BattleRuntimeAiActionKind.AdvanceTowardRegion =>
                    new CommanderTransition(BattleGroupPlanRuntimeState.AdvancingToObjective, context.Request.ReasonCode, 60),
                BattleRuntimeAiActionKind.AdvanceTowardObjective =>
                    new CommanderTransition(BattleGroupPlanRuntimeState.AdvancingToObjective, "objective_advance", 60),
                _ => null
            };
        }

        return context.TargetFact != null
            ? new CommanderTransition(BattleGroupPlanRuntimeState.TargetLocked, "target_locked", 40)
            : null;
    }

    private sealed record CommanderTransition(
        BattleGroupPlanRuntimeState State,
        string ReasonCode,
        int Priority);
}
