namespace Rpg.Runtime.Battle;

internal static class BattleTickStartProjectionBuilder
{
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
