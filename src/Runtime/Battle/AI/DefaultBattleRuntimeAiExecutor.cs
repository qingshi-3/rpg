namespace Rpg.Runtime.Battle.AI;

public sealed class DefaultBattleRuntimeAiExecutor : IBattleRuntimeAiExecutor
{
    public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
    {
        if (string.IsNullOrWhiteSpace(facts?.ActorId))
        {
            return BattleRuntimeAiActionRequest.Hold("", "missing_actor");
        }

        if (facts.HasTarget == false || string.IsNullOrWhiteSpace(facts.TargetActorId))
        {
            return BattleRuntimeAiActionRequest.Hold(facts.ActorId, "no_target");
        }

        if (facts.DistanceToTarget > System.Math.Max(1, facts.AttackRange))
        {
            return BattleRuntimeAiActionRequest.AdvanceTowardTarget(facts.ActorId, facts.TargetActorId);
        }

        return facts.CanAttackNow
            ? BattleRuntimeAiActionRequest.AttackTarget(facts.ActorId, facts.TargetActorId)
            : BattleRuntimeAiActionRequest.WaitForAttackCharge(facts.ActorId, facts.TargetActorId);
    }
}
