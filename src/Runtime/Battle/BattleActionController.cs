using System;
using System.Linq;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed class BattleActionController
{
    private readonly BattleRuntimeActor _actor;

    internal BattleActionController(BattleRuntimeActor actor)
    {
        _actor = actor;
    }

    internal static bool HasActiveBasicAttackAction(BattleRuntimeActor actor) =>
        actor?.Phase is BattleRuntimeActorPhase.AttackWindup or BattleRuntimeActorPhase.AttackRecovery;

    internal static void AdvanceAttackRecoveryBoundaries(
        System.Collections.Generic.IEnumerable<BattleRuntimeActor> actors,
        double currentTimeSeconds)
    {
        foreach (BattleRuntimeActor actor in actors ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            if (actor?.Kind == BattleRuntimeActorKind.Corps &&
                actor.HitPoints > 0 &&
                actor.Phase == BattleRuntimeActorPhase.AttackRecovery &&
                actor.ActionReadyAtSeconds <= currentTimeSeconds + 0.0001)
            {
                // Recovery has no impact payload left, so it can reopen the actor's
                // decision boundary before this tick chooses new actions.
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(actor);
            }
        }
    }

    internal void ProposeBasicAttack(
        BattleRuntimeTickContext context,
        BattleCommitBuffer commitBuffer,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double currentTimeSeconds)
    {
        if (context?.Request == null ||
            context.Request.Kind != BattleRuntimeAiActionKind.AttackTarget ||
            context.Result != null)
        {
            return;
        }

        if (!ReferenceEquals(_actor, context.ActorFact.Actor))
        {
            throw new System.InvalidOperationException($"basic attack context actor mismatch: actor={_actor?.ActorId ?? ""} contextActor={context.ActorFact.Actor?.ActorId ?? ""}");
        }

        if (context.TargetFact == null)
        {
            BattleRuntimeActorStateMachine.MarkHolding(_actor, currentTimeSeconds);
            context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "invalid_target");
            return;
        }

        if (BattleActorFootprint.GetOrthogonalGap(
                _actor,
                context.ActorFact.Anchor,
                context.TargetFact.Value.Actor,
                context.TargetFact.Value.Anchor) > System.Math.Max(1, _actor.AttackRange))
        {
            BattleRuntimeActorStateMachine.MarkHolding(_actor, currentTimeSeconds);
            context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "target_out_of_range");
            return;
        }

        if (context.ActorFact.AttackCharge < 1.0)
        {
            BattleRuntimeActorStateMachine.MarkWaitingForCharge(_actor, currentTimeSeconds);
            context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "attack_charge_empty");
            return;
        }

        StartBasicAttackAction(context, commitBuffer, stream, battleId, runtimeTick, currentTimeSeconds);
    }

    internal void AdvanceBasicAttackAction(
        BattleRuntimeState state,
        BattleCommitBuffer commitBuffer,
        double currentTimeSeconds)
    {
        if (_actor == null || _actor.HitPoints <= 0)
        {
            return;
        }

        if (_actor.Phase == BattleRuntimeActorPhase.AttackWindup)
        {
            AdvanceBasicAttackWindup(state, commitBuffer, currentTimeSeconds);
            return;
        }

        if (_actor.Phase == BattleRuntimeActorPhase.AttackRecovery &&
            _actor.ActionReadyAtSeconds <= currentTimeSeconds + 0.0001)
        {
            BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
        }
    }

    private void StartBasicAttackAction(
        BattleRuntimeTickContext context,
        BattleCommitBuffer commitBuffer,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double currentTimeSeconds)
    {
        BattleRuntimeActor target = context.TargetFact!.Value.Actor;
        BattleRuntimeActorStateMachine.MarkAttackWindup(
            _actor,
            target.ActorId,
            context.ActorFact.Anchor,
            context.TargetFact.Value.Anchor,
            NormalizeBasicAttackDamage(_actor.AttackDamage),
            currentTimeSeconds);
        BattleAdvanceFailureStateBoundary.ResetAdvanceFailureState(_actor);
        BattlePlanStateEmitter.SetPlanState(
            stream,
            battleId,
            runtimeTick,
            currentTimeSeconds,
            _actor,
            BattleGroupPlanRuntimeState.Attacking,
            "attacking");
        context.Result = BattleRuntimeAiActionResult.Succeeded(context.Request, "attack_started");
        if (_actor.CurrentBasicAttackImpactAtSeconds <= currentTimeSeconds + 0.0001)
        {
            ApplyBasicAttackImpact(commitBuffer, target, currentTimeSeconds);
        }
    }

    private void AdvanceBasicAttackWindup(
        BattleRuntimeState state,
        BattleCommitBuffer commitBuffer,
        double currentTimeSeconds)
    {
        if (_actor.CurrentBasicAttackImpactApplied ||
            _actor.CurrentBasicAttackImpactAtSeconds > currentTimeSeconds + 0.0001)
        {
            return;
        }

        BattleRuntimeActor target = ResolveActorById(state, _actor.CurrentBasicAttackTargetActorId);
        ApplyBasicAttackImpact(commitBuffer, target, currentTimeSeconds);
    }

    private void ApplyBasicAttackImpact(
        BattleCommitBuffer commitBuffer,
        BattleRuntimeActor target,
        double currentTimeSeconds)
    {
        if (_actor.CurrentBasicAttackImpactApplied)
        {
            return;
        }

        if (target != null && target.HitPoints > 0)
        {
            commitBuffer.RequestBasicAttack(
                _actor,
                target,
                new BattleGridCoord(
                    _actor.CurrentBasicAttackActorGridX,
                    _actor.CurrentBasicAttackActorGridY,
                    _actor.CurrentBasicAttackActorGridHeight),
                new BattleGridCoord(
                    _actor.CurrentBasicAttackTargetGridX,
                    _actor.CurrentBasicAttackTargetGridY,
                    _actor.CurrentBasicAttackTargetGridHeight),
                _actor.CurrentBasicAttackDamage);
        }

        double actionEndsAtSeconds = _actor.CurrentBasicAttackEndsAtSeconds;
        _actor.CurrentBasicAttackImpactApplied = true;
        BattleRuntimeActorStateMachine.MarkAttackRecovery(_actor, currentTimeSeconds, actionEndsAtSeconds);
    }

    private static BattleRuntimeActor ResolveActorById(BattleRuntimeState state, string actorId)
    {
        if (state?.Actors == null || string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        return state.Actors.FirstOrDefault(actor =>
            string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));
    }

    private static int NormalizeBasicAttackDamage(int attackDamage)
    {
        return attackDamage > 0 ? attackDamage : 1;
    }
}
