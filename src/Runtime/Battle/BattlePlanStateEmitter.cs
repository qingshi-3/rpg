using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattlePlanStateEmitter
{
    internal static bool SetPlanState(
        BattleGroupTacticalStateStore commanderStateStore,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor representativeActor,
        BattleGroupPlanRuntimeState state,
        string reasonCode)
    {
        if (commanderStateStore == null || stream == null || representativeActor == null)
        {
            return false;
        }

        string battleGroupId = representativeActor.BattleGroupId ?? "";
        if (!commanderStateStore.TryApplyPlanState(battleGroupId, state, out BattleGroupPlanRuntimeState previousState))
        {
            commanderStateStore.SynchronizeActorExecutionCache(representativeActor);
            return false;
        }

        BattleGroupTacticalState commanderState = commanderStateStore.GetRequiredSnapshot(battleGroupId);
        EmitAppliedTransition(
            stream,
            battleId,
            tick,
            currentTimeSeconds,
            commanderState,
            representativeActor,
            previousState,
            reasonCode);
        return true;
    }

    internal static void EmitAppliedTransition(
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleGroupTacticalState commanderState,
        BattleRuntimeActor representativeActor,
        BattleGroupPlanRuntimeState previousState,
        string reasonCode)
    {
        if (stream == null || commanderState == null || previousState == commanderState.PlanState)
        {
            return;
        }

        string reason = string.IsNullOrWhiteSpace(reasonCode)
            ? commanderState.PlanState.ToString()
            : reasonCode;
        string actorCell = representativeActor == null
            ? ""
            : $" actor={representativeActor.ActorId ?? ""} cell={representativeActor.GridX},{representativeActor.GridY},{representativeActor.GridHeight}";

        // One commander transition produces one semantic event regardless of how
        // many actor execution contexts contributed to the group decision.
        GameLog.Info(
            "BattleRuntimeStateTransition",
            $"BattleRuntimeStateTransition battle={battleId ?? ""} tick={tick} time={currentTimeSeconds:0.00} group={commanderState.BattleGroupId ?? ""}{actorCell} state={commanderState.PlanState} previous={previousState} changed=True reason={reason} target={commanderState.ObjectiveZoneId ?? ""}");
        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{tick}:{commanderState.BattleGroupId}:plan:{commanderState.PlanState}",
            BattleId = battleId ?? "",
            BattleGroupId = commanderState.BattleGroupId ?? "",
            TargetId = commanderState.ObjectiveZoneId ?? "",
            SourceCommandId = commanderState.ActiveCommandId ?? "",
            Kind = BattleEventKind.BattleGroupPlanStateChanged,
            ReasonCode = reason,
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds
        });
    }
}
