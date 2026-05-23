using Rpg.Application.Battle;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeActorStateMachine
{
    private const int MovementActionLockTicks = 0;
    private const int AttackRecoveryLockTicks = 1;
    private const double TimeEpsilon = 0.0001;

    internal static bool AdvanceTimeBoundary(
        BattleRuntimeActor actor,
        double currentTimeSeconds,
        out BattleGridCoord movementFrom,
        out BattleGridCoord movementTo)
    {
        movementFrom = default;
        movementTo = default;
        if (actor == null)
        {
            return false;
        }

        if (actor.HitPoints <= 0)
        {
            MarkDefeated(actor);
            return false;
        }

        if (actor.Phase == BattleRuntimeActorPhase.Moving)
        {
            return AdvanceMovementBoundary(actor, currentTimeSeconds, out movementFrom, out movementTo);
        }

        if (actor.ActionReadyAtSeconds > currentTimeSeconds + TimeEpsilon)
        {
            return false;
        }

        actor.AttackCharge = 1.0;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        return false;
    }

    internal static void MarkMovementCommitted(BattleRuntimeActor actor, BattleGridCoord to, double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        double moveSeconds = ResolveMoveStepSeconds(actor);
        // Movement starts from the current authoritative anchor and only commits
        // the target cell when fixed runtime time reaches the movement boundary.
        actor.MotionState = BattleRuntimeActorMotionState.Moving;
        actor.Phase = BattleRuntimeActorPhase.Moving;
        actor.ActionLockTicksRemaining = MovementActionLockTicks;
        actor.ActionLockReason = "movement";
        actor.ActionReadyAtSeconds = currentTimeSeconds + moveSeconds;
        actor.HasMovementTarget = true;
        actor.MovementFromGridX = actor.GridX;
        actor.MovementFromGridY = actor.GridY;
        actor.MovementFromGridHeight = actor.GridHeight;
        actor.MovementToGridX = to.X;
        actor.MovementToGridY = to.Y;
        actor.MovementToGridHeight = to.Height;
        actor.MovementStartedAtSeconds = currentTimeSeconds;
        actor.MovementDurationSeconds = moveSeconds;
        actor.MovementProgress = 0;
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
        actor.HasMovementTarget = false;
        actor.MovementProgress = 0;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        actor.ActionReadyAtSeconds = 0;
    }

    private static bool AdvanceMovementBoundary(
        BattleRuntimeActor actor,
        double currentTimeSeconds,
        out BattleGridCoord movementFrom,
        out BattleGridCoord movementTo)
    {
        movementFrom = new BattleGridCoord(
            actor.MovementFromGridX,
            actor.MovementFromGridY,
            actor.MovementFromGridHeight);
        movementTo = new BattleGridCoord(
            actor.MovementToGridX,
            actor.MovementToGridY,
            actor.MovementToGridHeight);
        double duration = System.Math.Max(TimeEpsilon, actor.MovementDurationSeconds);
        actor.MovementProgress = System.Math.Clamp(
            (currentTimeSeconds - actor.MovementStartedAtSeconds) / duration,
            0,
            1);
        if (actor.ActionReadyAtSeconds > currentTimeSeconds + TimeEpsilon)
        {
            return false;
        }

        if (actor.HasMovementTarget)
        {
            actor.GridX = actor.MovementToGridX;
            actor.GridY = actor.MovementToGridY;
            actor.GridHeight = actor.MovementToGridHeight;
            actor.Position = actor.GridX;
        }

        actor.AttackCharge = 1.0;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        actor.HasReservedGridCell = false;
        actor.HasMovementTarget = false;
        actor.MovementProgress = 1;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        return true;
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
