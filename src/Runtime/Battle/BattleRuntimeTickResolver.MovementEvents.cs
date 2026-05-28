using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private static BattleEvent BuildMovementEvent(
        BattleEventKind kind,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor actor,
        string targetId,
        BattleGridCoord from,
        BattleGridCoord to,
        string reasonCode)
    {
        string suffix = kind == BattleEventKind.MovementStarted ? "move_start" : "move_complete";
        BattleEvent movementEvent = new()
        {
            EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:{suffix}",
            BattleId = battleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = targetId ?? "",
            Kind = kind,
            ReasonCode = reasonCode,
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds,
            ActionDurationSeconds = actor.MoveStepSeconds,
            HasMovementCells = true,
            FromGridX = from.X,
            FromGridY = from.Y,
            FromGridHeight = from.Height,
            ToGridX = to.X,
            ToGridY = to.Y,
            ToGridHeight = to.Height
        };

        return movementEvent;
    }
}
