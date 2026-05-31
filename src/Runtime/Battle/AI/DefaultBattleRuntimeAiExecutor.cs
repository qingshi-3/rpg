using Rpg.Runtime.Battle.Tactics;

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

        if (facts.HasLocalCombatSituation && !facts.LocalCombatInsideLeash)
        {
            return BattleRuntimeAiActionRequest.Hold(
                facts.ActorId,
                string.IsNullOrWhiteSpace(facts.LocalCombatRejectReasonCode)
                    ? "reject_local_combat"
                    : facts.LocalCombatRejectReasonCode);
        }

        if (facts.DistanceToTarget > facts.AttackRange &&
            facts.HasLocalCombatSituation &&
            facts.LocalCombatHasReachableAttackSlot &&
            !string.IsNullOrWhiteSpace(facts.LocalCombatTargetActorId))
        {
            return BattleRuntimeAiActionRequest.JoinLocalCombat(
                facts.ActorId,
                facts.LocalCombatTargetActorId,
                facts.LocalCombatJoinReasonCode,
                facts.LocalCombatSituationId);
        }

        if (facts.DistanceToTarget > facts.AttackRange &&
            facts.HasLocalCombatSituation &&
            facts.LocalCombatHasReachableSupportSlot &&
            !string.IsNullOrWhiteSpace(facts.LocalCombatTargetActorId))
        {
            return BattleRuntimeAiActionRequest.HoldSupport(
                facts.ActorId,
                facts.LocalCombatTargetActorId,
                facts.LocalCombatSupportReasonCode,
                facts.LocalCombatSituationId);
        }

        if (facts.DistanceToTarget > facts.AttackRange &&
            facts.HasLocalCombatSituation &&
            !facts.LocalCombatHasReachableAttackSlot &&
            !facts.LocalCombatHasReachableSupportSlot &&
            !string.IsNullOrWhiteSpace(facts.LocalCombatTargetActorId))
        {
            // A bounded local region with no reachable slot must hold/regroup
            // instead of falling back to ordinary target pursuit outside it.
            return BattleRuntimeAiActionRequest.Hold(
                facts.ActorId,
                BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot);
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
