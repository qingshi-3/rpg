using System.Collections.Generic;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleTickStartProjectionBuilder
{
    internal static Dictionary<string, BattleRuntimeTickStartActorFact> BuildFactMap(IEnumerable<BattleRuntimeActor> livingCorps)
    {
        Dictionary<string, BattleRuntimeTickStartActorFact> facts = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeActor actor in livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
        {
            string actorId = actor?.ActorId ?? "";
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new System.InvalidOperationException("missing runtime actor id in tick-start facts");
            }

            BattleRuntimeTickStartActorFact fact = new(
                actor,
                new BattleGridCoord(actor.GridX, actor.GridY, actor.GridHeight),
                actor.HitPoints,
                actor.AttackCharge,
                actor.TargetActorId ?? "",
                actor.CommandId ?? "");
            if (!facts.TryAdd(actorId, fact))
            {
                throw new System.InvalidOperationException($"duplicate runtime actor id in tick-start facts: actorId={actorId}");
            }
        }

        return facts;
    }

    internal static BattleRuntimeActor Build(BattleRuntimeTickStartActorFact fact)
    {
        return new BattleRuntimeActor
        {
            ActorId = fact.Actor.ActorId,
            FactionId = fact.Actor.FactionId,
            Kind = fact.Actor.Kind,
            FootprintWidth = fact.Actor.FootprintWidth,
            FootprintHeight = fact.Actor.FootprintHeight,
            GridX = fact.Anchor.X,
            GridY = fact.Anchor.Y,
            GridHeight = fact.Anchor.Height,
            AttackRange = fact.Actor.AttackRange,
            AttackDamage = fact.Actor.AttackDamage,
            HitPoints = fact.HitPoints,
            EngagementRule = fact.Actor.EngagementRule,
            PlanState = fact.Actor.PlanState,
            MovementFromGridX = fact.Actor.MovementFromGridX,
            MovementFromGridY = fact.Actor.MovementFromGridY,
            MovementFromGridHeight = fact.Actor.MovementFromGridHeight,
            MovementToGridX = fact.Actor.MovementToGridX,
            MovementToGridY = fact.Actor.MovementToGridY,
            MovementToGridHeight = fact.Actor.MovementToGridHeight,
            HasMovementBacktrackGuardCell = fact.Actor.HasMovementBacktrackGuardCell,
            MovementBacktrackGuardGridX = fact.Actor.MovementBacktrackGuardGridX,
            MovementBacktrackGuardGridY = fact.Actor.MovementBacktrackGuardGridY,
            MovementBacktrackGuardGridHeight = fact.Actor.MovementBacktrackGuardGridHeight,
            HasSecondaryMovementBacktrackGuardCell = fact.Actor.HasSecondaryMovementBacktrackGuardCell,
            SecondaryMovementBacktrackGuardGridX = fact.Actor.SecondaryMovementBacktrackGuardGridX,
            SecondaryMovementBacktrackGuardGridY = fact.Actor.SecondaryMovementBacktrackGuardGridY,
            SecondaryMovementBacktrackGuardGridHeight = fact.Actor.SecondaryMovementBacktrackGuardGridHeight,
            HasObjectiveAnchor = fact.Actor.HasObjectiveAnchor,
            ObjectiveZoneId = fact.Actor.ObjectiveZoneId,
            ObjectiveGridX = fact.Actor.ObjectiveGridX,
            ObjectiveGridY = fact.Actor.ObjectiveGridY,
            ObjectiveGridHeight = fact.Actor.ObjectiveGridHeight,
            ObjectiveWidth = fact.Actor.ObjectiveWidth,
            ObjectiveHeight = fact.Actor.ObjectiveHeight
        };
    }
}
