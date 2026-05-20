using Rpg.Application.Battle;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeActorStateMachine
{
    private const int MovementActionLockTicks = 0;
    private const int AttackRecoveryLockTicks = 1;
    private const double TimeEpsilon = 0.0001;

    internal static void AdvanceTimeBoundary(BattleRuntimeActor actor, double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        if (actor.HitPoints <= 0)
        {
            MarkDefeated(actor);
            return;
        }

        if (actor.ActionReadyAtSeconds > currentTimeSeconds + TimeEpsilon)
        {
            return;
        }

        actor.AttackCharge = 1.0;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
    }

    internal static void MarkMovementCommitted(BattleRuntimeActor actor, BattleGridCoord to, double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        // Movement consumes actor-local time on the central battle timeline.
        // Other ready actors can still act while this actor waits for its next decision boundary.
        actor.MotionState = BattleRuntimeActorMotionState.Moving;
        actor.Phase = BattleRuntimeActorPhase.Moving;
        actor.ActionLockTicksRemaining = MovementActionLockTicks;
        actor.ActionLockReason = "movement";
        actor.ActionReadyAtSeconds = currentTimeSeconds + ResolveMoveStepSeconds(actor);
        actor.GridX = to.X;
        actor.GridY = to.Y;
        actor.GridHeight = to.Height;
        actor.Position = actor.GridX;
        actor.HasReservedGridCell = false;
    }

    internal static void MarkAttackRecovery(BattleRuntimeActor actor, double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        actor.MotionState = BattleRuntimeActorMotionState.Attacking;
        actor.Phase = BattleRuntimeActorPhase.AttackRecovery;
        actor.ActionLockTicksRemaining = AttackRecoveryLockTicks;
        actor.ActionLockReason = "attack_recovery";
        actor.ActionReadyAtSeconds = currentTimeSeconds + ResolveAttackActionSeconds(actor);
    }

    internal static void MarkWaitingForCharge(BattleRuntimeActor actor, double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.WaitingForCharge;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        actor.ActionReadyAtSeconds = currentTimeSeconds + ResolveDecisionRetrySeconds(actor);
    }

    internal static void MarkHolding(BattleRuntimeActor actor, double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.Holding;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        actor.ActionReadyAtSeconds = currentTimeSeconds + ResolveDecisionRetrySeconds(actor);
    }

    internal static void MarkDefeated(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.HitPoints = System.Math.Max(0, actor.HitPoints);
        actor.MotionState = BattleRuntimeActorMotionState.Defeated;
        actor.Phase = BattleRuntimeActorPhase.Defeated;
        actor.HasReservedGridCell = false;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        actor.ActionReadyAtSeconds = 0;
    }

    private static double ResolveMoveStepSeconds(BattleRuntimeActor actor)
    {
        return BattleActionTimingPolicy.NormalizeActionSeconds(
            actor?.MoveStepSeconds ?? BattleActionTimingPolicy.DefaultMoveStepSeconds,
            BattleActionTimingPolicy.DefaultMoveStepSeconds);
    }

    private static double ResolveAttackActionSeconds(BattleRuntimeActor actor)
    {
        return BattleActionTimingPolicy.NormalizeActionSeconds(
            actor?.AttackActionSeconds ?? BattleActionTimingPolicy.DefaultAttackActionSeconds,
            BattleActionTimingPolicy.DefaultAttackActionSeconds);
    }

    private static double ResolveDecisionRetrySeconds(BattleRuntimeActor actor)
    {
        return ResolveMoveStepSeconds(actor);
    }
}
