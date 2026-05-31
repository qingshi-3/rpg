using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupTacticalRegionMutationResult
{
    public bool Accepted { get; init; }
    public string ReasonCode { get; init; } = "";
    public BattleEvent Event { get; init; } = new();

    public BattleGroupTacticalRegionMutationResult Clone()
    {
        return new BattleGroupTacticalRegionMutationResult
        {
            Accepted = Accepted,
            ReasonCode = ReasonCode ?? "",
            Event = CloneEvent(Event)
        };
    }

    private static BattleEvent CloneEvent(BattleEvent source)
    {
        if (source == null)
        {
            return new BattleEvent();
        }

        return new BattleEvent
        {
            EventId = source.EventId ?? "",
            BattleId = source.BattleId ?? "",
            Kind = source.Kind,
            ActorId = source.ActorId ?? "",
            BattleGroupId = source.BattleGroupId ?? "",
            SourceCommandId = source.SourceCommandId ?? "",
            TargetId = source.TargetId ?? "",
            ReasonCode = source.ReasonCode ?? "",
            RuntimeTick = source.RuntimeTick,
            RuntimeTimeSeconds = source.RuntimeTimeSeconds,
            ActionDurationSeconds = source.ActionDurationSeconds,
            ActionImpactDelaySeconds = source.ActionImpactDelaySeconds,
            CorpsStrengthDelta = source.CorpsStrengthDelta,
            HasActorCells = source.HasActorCells,
            ActorGridX = source.ActorGridX,
            ActorGridY = source.ActorGridY,
            ActorGridHeight = source.ActorGridHeight,
            HasTargetCells = source.HasTargetCells,
            TargetGridX = source.TargetGridX,
            TargetGridY = source.TargetGridY,
            TargetGridHeight = source.TargetGridHeight,
            TacticalRegionId = source.TacticalRegionId ?? "",
            TacticalRegionKind = source.TacticalRegionKind ?? "",
            TacticalRegionVersion = source.TacticalRegionVersion,
            HasMovementCells = source.HasMovementCells,
            FromGridX = source.FromGridX,
            FromGridY = source.FromGridY,
            FromGridHeight = source.FromGridHeight,
            ToGridX = source.ToGridX,
            ToGridY = source.ToGridY,
            ToGridHeight = source.ToGridHeight
        };
    }
}
