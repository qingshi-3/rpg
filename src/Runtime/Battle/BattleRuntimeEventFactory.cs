using Rpg.Application.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeEventFactory
{
    // Event construction stays pure here; stream insertion order and actor state mutation remain with the tick resolver.
    internal static BattleEvent CreateDamageApplied(
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor actor,
        BattleRuntimeActor targetActor,
        BattleGridCoord actorAnchor,
        BattleGridCoord targetAnchor,
        int appliedDamage,
        bool isFinishingHit)
    {
        return new BattleEvent
        {
            EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:attack:{targetActor.ActorId}",
            BattleId = battleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = targetActor.ActorId,
            Kind = BattleEventKind.DamageApplied,
            ReasonCode = isFinishingHit
                ? "auto_attack_target_defeated"
                : "auto_attack",
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds,
            ActionDurationSeconds = actor.AttackActionSeconds,
            ActionImpactDelaySeconds = actor.AttackImpactDelaySeconds,
            CorpsStrengthDelta = -appliedDamage,
            HasActorCells = true,
            ActorGridX = actorAnchor.X,
            ActorGridY = actorAnchor.Y,
            ActorGridHeight = actorAnchor.Height,
            HasTargetCells = true,
            TargetGridX = targetAnchor.X,
            TargetGridY = targetAnchor.Y,
            TargetGridHeight = targetAnchor.Height
        };
    }

    internal static BattleEvent CreateMovementEvent(
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
            ActionDurationSeconds = ResolveMovementEventDuration(actor),
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

    private static double ResolveMovementEventDuration(BattleRuntimeActor actor)
    {
        if (actor?.MovementDurationSeconds > 0)
        {
            return actor.MovementDurationSeconds;
        }

        return BattleActionTimingPolicy.NormalizeMoveStepSeconds(
            actor?.MoveStepSeconds ?? BattleActionTimingPolicy.DefaultMoveStepSeconds,
            BattleActionTimingPolicy.DefaultMoveStepSeconds);
    }
}
