using Rpg.Runtime.Battle.Events;

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
        string reasonCode)
    {
        if (stream == null || actor == null || actor.PlanState == state)
        {
            return;
        }

        actor.PlanState = state;
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
}
