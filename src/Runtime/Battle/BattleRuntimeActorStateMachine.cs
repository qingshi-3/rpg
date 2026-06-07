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
        out BattleGridCoord movementTo,
        out string boundaryReasonCode)
    {
        movementFrom = default;
        movementTo = default;
        boundaryReasonCode = "";
        if (actor == null)
        {
            return false;
        }

        if (actor.HitPoints <= 0)
        {
            if (actor.Phase == BattleRuntimeActorPhase.Moving && actor.HasMovementTarget)
            {
                movementFrom = new BattleGridCoord(
                    actor.MovementFromGridX,
                    actor.MovementFromGridY,
                    actor.MovementFromGridHeight);
                movementTo = new BattleGridCoord(actor.GridX, actor.GridY, actor.GridHeight);
                boundaryReasonCode = "movement_cancelled_defeated";
                // Presentation needs an explicit movement boundary even when defeat
                // cancels the segment before its target cell becomes authoritative.
                MarkDefeated(actor);
                return true;
            }

            MarkDefeated(actor);
            return false;
        }

        if (actor.Phase == BattleRuntimeActorPhase.Moving)
        {
            return AdvanceMovementBoundary(actor, currentTimeSeconds, out movementFrom, out movementTo, out boundaryReasonCode);
        }

        if (actor.Phase == BattleRuntimeActorPhase.SkillCasting ||
            actor.Phase == BattleRuntimeActorPhase.SkillRecovery)
        {
            return false;
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

    internal static void MarkMovementCommitted(
        BattleRuntimeActor actor,
        BattleGridCoord to,
        double currentTimeSeconds,
        BattleMovementIntentCommit intent)
    {
        if (actor == null)
        {
            return;
        }

        double moveSeconds = ResolveMoveStepSeconds(actor);
        BattleGridCoord previousFrom = new(
            actor.MovementFromGridX,
            actor.MovementFromGridY,
            actor.MovementFromGridHeight);
        BattleGridCoord previousTo = new(
            actor.MovementToGridX,
            actor.MovementToGridY,
            actor.MovementToGridHeight);
        BattleGridCoord currentAnchor = new(actor.GridX, actor.GridY, actor.GridHeight);
        bool hasContinuousPreviousSegment = previousFrom != previousTo && previousTo == currentAnchor;
        bool hadPrimaryGuard = actor.HasMovementBacktrackGuardCell;
        int primaryGuardX = actor.MovementBacktrackGuardGridX;
        int primaryGuardY = actor.MovementBacktrackGuardGridY;
        int primaryGuardHeight = actor.MovementBacktrackGuardGridHeight;
        actor.HasMovementBacktrackGuardCell = hasContinuousPreviousSegment;
        actor.MovementBacktrackGuardGridX = hasContinuousPreviousSegment ? previousFrom.X : 0;
        actor.MovementBacktrackGuardGridY = hasContinuousPreviousSegment ? previousFrom.Y : 0;
        actor.MovementBacktrackGuardGridHeight = hasContinuousPreviousSegment ? previousFrom.Height : 0;
        actor.HasSecondaryMovementBacktrackGuardCell = hasContinuousPreviousSegment && hadPrimaryGuard;
        actor.SecondaryMovementBacktrackGuardGridX = hasContinuousPreviousSegment && hadPrimaryGuard ? primaryGuardX : 0;
        actor.SecondaryMovementBacktrackGuardGridY = hasContinuousPreviousSegment && hadPrimaryGuard ? primaryGuardY : 0;
        actor.SecondaryMovementBacktrackGuardGridHeight = hasContinuousPreviousSegment && hadPrimaryGuard ? primaryGuardHeight : 0;
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
        actor.HasMovementIntentSnapshot = true;
        actor.MovementIntentKind = intent.RequestKind;
        actor.MovementIntentTargetActorId = intent.TargetActorId ?? "";
        actor.MovementIntentObjectiveZoneId = intent.ObjectiveZoneId ?? "";
        actor.MovementIntentRegionId = intent.RegionId ?? "";
        actor.MovementIntentCommandId = intent.CommandId ?? "";
        actor.MovementIntentReasonCode = intent.ReasonCode ?? "";
        actor.MovementIntentLocalCombatSituationId = intent.LocalCombatSituationId ?? "";
        actor.HasMovementIntentCombatSlot = intent.HasCombatSlotIntent;
        actor.MovementIntentCombatSlotX = intent.CombatSlotAnchor.X;
        actor.MovementIntentCombatSlotY = intent.CombatSlotAnchor.Y;
        actor.MovementIntentCombatSlotHeight = intent.CombatSlotAnchor.Height;
        actor.MovementIntentCombatSlotKind = intent.CombatSlotKind;
        actor.MovementIntentSegmentDurationSeconds = moveSeconds;
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
        ClearMovementIntentSnapshot(actor);
    }

    internal static void MarkSkillCasting(
        BattleRuntimeActor actor,
        string actionId,
        string skillId,
        string sourceCommandId,
        string targetActorId,
        double currentTimeSeconds,
        double impactDelaySeconds,
        double recoverySeconds)
    {
        if (actor == null)
        {
            return;
        }

        actor.MotionState = BattleRuntimeActorMotionState.Attacking;
        actor.Phase = BattleRuntimeActorPhase.SkillCasting;
        actor.ActionLockReason = "skill_casting";
        actor.ActionReadyAtSeconds = currentTimeSeconds + System.Math.Max(0, impactDelaySeconds) + System.Math.Max(0, recoverySeconds);
        actor.CurrentSkillActionId = actionId ?? "";
        actor.CurrentSkillId = skillId ?? "";
        actor.CurrentSkillSourceCommandId = sourceCommandId ?? "";
        actor.CurrentSkillTargetActorId = targetActorId ?? "";
        actor.CurrentSkillImpactAtSeconds = currentTimeSeconds + System.Math.Max(0, impactDelaySeconds);
        actor.CurrentSkillImpactApplied = false;
        ClearMovementIntentSnapshot(actor);
    }

    internal static void MarkSkillRecovery(BattleRuntimeActor actor, double currentTimeSeconds, double recoverySeconds)
    {
        if (actor == null)
        {
            return;
        }

        actor.MotionState = BattleRuntimeActorMotionState.Attacking;
        actor.Phase = BattleRuntimeActorPhase.SkillRecovery;
        actor.ActionLockReason = "skill_recovery";
        actor.ActionReadyAtSeconds = currentTimeSeconds + System.Math.Max(0, recoverySeconds);
        ClearMovementIntentSnapshot(actor);
    }

    internal static void MarkAnchoredDecision(BattleRuntimeActor actor)
    {
        if (actor == null || actor.HitPoints <= 0)
        {
            return;
        }

        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        actor.CurrentSkillActionId = "";
        actor.CurrentSkillId = "";
        actor.CurrentSkillSourceCommandId = "";
        actor.CurrentSkillTargetActorId = "";
        actor.CurrentSkillImpactAtSeconds = 0;
        actor.CurrentSkillImpactApplied = false;
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
        ClearMovementIntentSnapshot(actor);
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
        ClearMovementIntentSnapshot(actor);
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
        actor.HasMovementBacktrackGuardCell = false;
        actor.HasSecondaryMovementBacktrackGuardCell = false;
        actor.ActionReadyAtSeconds = 0;
        actor.CurrentSkillActionId = "";
        actor.CurrentSkillId = "";
        actor.CurrentSkillSourceCommandId = "";
        actor.CurrentSkillTargetActorId = "";
        actor.CurrentSkillImpactAtSeconds = 0;
        actor.CurrentSkillImpactApplied = false;
        ClearMovementIntentSnapshot(actor);
    }

    private static bool AdvanceMovementBoundary(
        BattleRuntimeActor actor,
        double currentTimeSeconds,
        out BattleGridCoord movementFrom,
        out BattleGridCoord movementTo,
        out string boundaryReasonCode)
    {
        boundaryReasonCode = "";
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
        boundaryReasonCode = "movement_committed";
        return true;
    }

    private static double ResolveMoveStepSeconds(BattleRuntimeActor actor)
    {
        return BattleActionTimingPolicy.NormalizeMoveStepSeconds(
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

    internal static void ClearMovementIntentSnapshot(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.HasMovementIntentSnapshot = false;
        actor.MovementIntentKind = Rpg.Runtime.Battle.AI.BattleRuntimeAiActionKind.Hold;
        actor.MovementIntentTargetActorId = "";
        actor.MovementIntentObjectiveZoneId = "";
        actor.MovementIntentRegionId = "";
        actor.MovementIntentCommandId = "";
        actor.MovementIntentReasonCode = "";
        actor.MovementIntentLocalCombatSituationId = "";
        actor.HasMovementIntentCombatSlot = false;
        actor.MovementIntentCombatSlotX = 0;
        actor.MovementIntentCombatSlotY = 0;
        actor.MovementIntentCombatSlotHeight = 0;
        actor.MovementIntentCombatSlotKind = Rpg.Runtime.Battle.Navigation.BattleCombatSlotKind.Support;
        actor.MovementIntentSegmentDurationSeconds = 0;
    }
}
