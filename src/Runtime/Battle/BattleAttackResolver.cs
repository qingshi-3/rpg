using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle;

internal static class BattleAttackResolver
{
    private readonly record struct PendingAttack(BattleRuntimeTickContext Context, int DeclaredDamage);
    private readonly record struct AttackApplication(BattleRuntimeTickContext Context, int AppliedDamage, bool IsFinishingHit);

    internal static void Resolve(
        List<BattleRuntimeTickContext> contexts,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        List<PendingAttack> pendingAttacks = new();
        foreach (BattleRuntimeTickContext context in contexts
                     .Where(item =>
                         item.Request.Kind == BattleRuntimeAiActionKind.AttackTarget &&
                         item.Result == null))
        {
            if (context.TargetFact == null)
            {
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
                continue;
            }

            if (BattleRuntimeTickResolver.GetOrthogonalAttackGap(
                    context.ActorFact.Actor,
                    context.ActorFact.Anchor,
                    context.TargetFact.Value.Actor,
                    context.TargetFact.Value.Anchor) > System.Math.Max(1, context.ActorFact.Actor.AttackRange))
            {
                BattleRuntimeActorStateMachine.MarkHolding(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "target_out_of_range");
                continue;
            }

            if (context.ActorFact.AttackCharge < 1.0)
            {
                BattleRuntimeActorStateMachine.MarkWaitingForCharge(context.ActorFact.Actor, currentTimeSeconds);
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "attack_charge_empty");
                continue;
            }

            pendingAttacks.Add(new PendingAttack(
                context,
                BattleRuntimeTickResolver.ResolveAttackDamage(context.ActorFact.Actor.AttackDamage)));
        }

        Dictionary<string, int> postAttackHitPoints = tickStartFacts.Values.ToDictionary(
            item => item.Actor.ActorId,
            item => System.Math.Max(0, item.HitPoints),
            System.StringComparer.Ordinal);

        List<AttackApplication> applications = new();
        foreach (IGrouping<string, PendingAttack> targetGroup in pendingAttacks
                     .GroupBy(item => item.Context.TargetFact!.Value.Actor.ActorId, System.StringComparer.Ordinal))
        {
            if (!postAttackHitPoints.TryGetValue(targetGroup.Key, out int remaining))
            {
                throw new System.InvalidOperationException($"battle attack target missing tick-start fact: targetActorId={targetGroup.Key}");
            }

            foreach (PendingAttack pending in targetGroup.OrderBy(
                         item => item.Context.ActorFact.Actor.ActorId,
                         System.StringComparer.Ordinal))
            {
                int applied = System.Math.Min(pending.DeclaredDamage, remaining);
                remaining = System.Math.Max(0, remaining - applied);
                applications.Add(new AttackApplication(
                    pending.Context,
                    applied,
                    IsFinishingHit: applied > 0 && remaining == 0));
            }

            postAttackHitPoints[targetGroup.Key] = remaining;
        }

        foreach (AttackApplication application in applications.OrderBy(
                     item => item.Context.ActorFact.Actor.ActorId,
                     System.StringComparer.Ordinal))
        {
            // The factory preserves RuntimeTimeSeconds = currentTimeSeconds; the resolver owns this stream position.
            stream.Add(BattleRuntimeEventFactory.CreateDamageApplied(
                battleId,
                tick,
                currentTimeSeconds,
                application.Context.ActorFact.Actor,
                application.Context.TargetFact!.Value.Actor,
                application.Context.ActorFact.Anchor,
                application.Context.TargetFact!.Value.Anchor,
                application.AppliedDamage,
                application.IsFinishingHit));
        }

        foreach (PendingAttack pending in pendingAttacks)
        {
            pending.Context.ActorFact.Actor.AttackCharge = System.Math.Max(0, pending.Context.ActorFact.Actor.AttackCharge - 1.0);
            BattleRuntimeTickResolver.ResetAdvanceFailureState(pending.Context.ActorFact.Actor);
            BattlePlanStateEmitter.SetPlanState(
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                pending.Context.ActorFact.Actor,
                BattleGroupPlanRuntimeState.Attacking,
                "attacking");
            BattleRuntimeActorStateMachine.MarkAttackRecovery(pending.Context.ActorFact.Actor, currentTimeSeconds);
            pending.Context.Result = BattleRuntimeAiActionResult.Succeeded(pending.Context.Request, "attacked");
        }

        foreach (KeyValuePair<string, int> pair in postAttackHitPoints)
        {
            if (tickStartFacts.TryGetValue(pair.Key, out BattleRuntimeTickStartActorFact targetFact))
            {
                targetFact.Actor.HitPoints = System.Math.Max(0, pair.Value);
                if (targetFact.Actor.HitPoints <= 0)
                {
                    BattlePlanStateEmitter.SetPlanState(
                        stream,
                        battleId,
                        tick,
                        currentTimeSeconds,
                        targetFact.Actor,
                        BattleGroupPlanRuntimeState.Defeated,
                        "defeated");
                    BattleRuntimeActorStateMachine.MarkDefeated(targetFact.Actor);
                }
            }
        }
    }
}
