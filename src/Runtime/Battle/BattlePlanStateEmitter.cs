using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattlePlanStateEmitter
{
    public static void SetPlanState(
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor actor,
        BattleGroupPlanRuntimeState state,
        string reasonCode,
        bool logWhenUnchanged = false,
        string actionCode = "",
        BattleGridCoord? from = null,
        BattleGridCoord? to = null)
    {
        if (stream == null || actor == null)
        {
            return;
        }

        BattleGroupPlanRuntimeState previousState = actor.PlanState;
        bool stateChanged = previousState != state;
        if (!stateChanged && !logWhenUnchanged)
        {
            return;
        }

        if (stateChanged)
        {
            actor.PlanState = state;
        }

        LogStateTransition(
            battleId,
            tick,
            currentTimeSeconds,
            actor,
            state,
            previousState,
            reasonCode,
            actionCode,
            from,
            to,
            stateChanged);
        if (!stateChanged)
        {
            return;
        }

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:plan:{state}",
            BattleId = battleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = actor.ObjectiveZoneId ?? "",
            Kind = BattleEventKind.BattleGroupPlanStateChanged,
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? state.ToString() : reasonCode,
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds
        });
    }

    private static void LogStateTransition(
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor actor,
        BattleGroupPlanRuntimeState state,
        BattleGroupPlanRuntimeState previousState,
        string reasonCode,
        string actionCode,
        BattleGridCoord? from,
        BattleGridCoord? to,
        bool stateChanged)
    {
        string reason = string.IsNullOrWhiteSpace(reasonCode) ? state.ToString() : reasonCode;
        string target = !string.IsNullOrWhiteSpace(actor.TargetActorId)
            ? actor.TargetActorId
            : actor.ObjectiveZoneId ?? "";
        string action = string.IsNullOrWhiteSpace(actionCode)
            ? ""
            : $" action={actionCode}";
        string movement = from.HasValue && to.HasValue
            ? $" from={from.Value.X},{from.Value.Y},{from.Value.Height} to={to.Value.X},{to.Value.Y},{to.Value.Height}"
            : "";

        // Plan-state events are emitted only for real state changes; this log
        // also records action-boundary transitions such as starting a new
        // objective movement segment while remaining AdvancingToObjective.
        GameLog.Info(
            "BattleRuntimeStateTransition",
            $"BattleRuntimeStateTransition battle={battleId ?? ""} tick={tick} time={currentTimeSeconds:0.00} actor={actor.ActorId ?? ""} group={actor.BattleGroupId ?? ""} state={state} previous={previousState} changed={stateChanged} reason={reason} target={target} cell={actor.GridX},{actor.GridY},{actor.GridHeight}{action}{movement}");
    }
}
