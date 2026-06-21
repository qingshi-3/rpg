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

        if (actor.Phase == BattleRuntimeActorPhase.AttackWindup ||
            actor.Phase == BattleRuntimeActorPhase.AttackRecovery)
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

    internal static void MarkAttackWindup(
        BattleRuntimeActor actor,
        string targetActorId,
        BattleGridCoord actorAnchor,
        BattleGridCoord targetAnchor,
        int declaredDamage,
        double currentTimeSeconds)
    {
        if (actor == null)
        {
            return;
        }

        double actionSeconds = ResolveAttackActionSeconds(actor);
        double impactDelaySeconds = ResolveAttackImpactDelaySeconds(actor, actionSeconds);
        actor.MotionState = BattleRuntimeActorMotionState.Attacking;
        actor.Phase = BattleRuntimeActorPhase.AttackWindup;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "attack_windup";
        actor.ActionReadyAtSeconds = currentTimeSeconds + impactDelaySeconds;
        actor.AttackCharge = System.Math.Max(0, actor.AttackCharge - 1.0);
        actor.CurrentBasicAttackTargetActorId = targetActorId ?? "";
        actor.CurrentBasicAttackDamage = System.Math.Max(1, declaredDamage);
        actor.CurrentBasicAttackImpactApplied = false;
        actor.CurrentBasicAttackStartedAtSeconds = currentTimeSeconds;
        actor.CurrentBasicAttackImpactAtSeconds = currentTimeSeconds + impactDelaySeconds;
        actor.CurrentBasicAttackEndsAtSeconds = currentTimeSeconds + actionSeconds;
        actor.CurrentBasicAttackActorGridX = actorAnchor.X;
        actor.CurrentBasicAttackActorGridY = actorAnchor.Y;
        actor.CurrentBasicAttackActorGridHeight = actorAnchor.Height;
        actor.CurrentBasicAttackTargetGridX = targetAnchor.X;
        actor.CurrentBasicAttackTargetGridY = targetAnchor.Y;
        actor.CurrentBasicAttackTargetGridHeight = targetAnchor.Height;
        ClearMovementIntentSnapshot(actor);
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

    internal static void MarkAttackRecovery(BattleRuntimeActor actor, double currentTimeSeconds, double actionEndsAtSeconds = double.NaN)
    {
        if (actor == null)
        {
            return;
        }

        double recoveryReadyAtSeconds = double.IsNaN(actionEndsAtSeconds) || double.IsInfinity(actionEndsAtSeconds)
            ? currentTimeSeconds + ResolveAttackActionSeconds(actor)
            : System.Math.Max(currentTimeSeconds, actionEndsAtSeconds);
        actor.MotionState = BattleRuntimeActorMotionState.Attacking;
        actor.Phase = BattleRuntimeActorPhase.AttackRecovery;
        actor.ActionLockTicksRemaining = AttackRecoveryLockTicks;
        actor.ActionLockReason = "attack_recovery";
        actor.ActionReadyAtSeconds = recoveryReadyAtSeconds;
        ClearBasicAttackAction(actor);
        ClearMovementIntentSnapshot(actor);
    }

    internal static void MarkSkillCasting(
        BattleRuntimeActor actor,
        string actionId,
        string skillId,
        string sourceCommandId,
        string targetActorId,
        bool hasTargetGrid,
        int targetGridX,
        int targetGridY,
        int targetGridHeight,
        string selectedSpatialMarkId,
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
        actor.CurrentSkillHasTargetGrid = hasTargetGrid;
        actor.CurrentSkillTargetGridX = targetGridX;
        actor.CurrentSkillTargetGridY = targetGridY;
        actor.CurrentSkillTargetGridHeight = targetGridHeight;
        actor.CurrentSkillSelectedSpatialMarkId = selectedSpatialMarkId ?? "";
        actor.CurrentSkillImpactAtSeconds = currentTimeSeconds + System.Math.Max(0, impactDelaySeconds);
        actor.CurrentSkillImpactApplied = false;
        ClearBasicAttackAction(actor);
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
        ClearBasicAttackAction(actor);
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
        actor.AttackCharge = 1.0;
        actor.ActionLockTicksRemaining = 0;
        actor.ActionLockReason = "";
        actor.CurrentSkillActionId = "";
        actor.CurrentSkillId = "";
        actor.CurrentSkillSourceCommandId = "";
        actor.CurrentSkillTargetActorId = "";
        actor.CurrentSkillHasTargetGrid = false;
        actor.CurrentSkillTargetGridX = 0;
        actor.CurrentSkillTargetGridY = 0;
        actor.CurrentSkillTargetGridHeight = 0;
        actor.CurrentSkillSelectedSpatialMarkId = "";
        actor.CurrentSkillImpactAtSeconds = 0;
        actor.CurrentSkillImpactApplied = false;
        ClearBasicAttackAction(actor);
    }

    internal static void CommitDisplacement(
        BattleRuntimeActor actor,
        BattleGridCoord destination,
        double currentTimeSeconds)
    {
        if (actor == null || actor.HitPoints <= 0)
        {
            return;
        }

        bool keepSkillLock = actor.Phase is BattleRuntimeActorPhase.SkillCasting or BattleRuntimeActorPhase.SkillRecovery;

        actor.GridX = destination.X;
        actor.GridY = destination.Y;
        actor.GridHeight = destination.Height;
        actor.Position = destination.X;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        actor.TargetActorId = "";
        actor.HasReservedGridCell = false;
        actor.ReservedGridX = 0;
        actor.ReservedGridY = 0;
        actor.ReservedGridHeight = 0;
        actor.HasMovementTarget = false;
        actor.MovementFromGridX = destination.X;
        actor.MovementFromGridY = destination.Y;
        actor.MovementFromGridHeight = destination.Height;
        actor.MovementToGridX = destination.X;
        actor.MovementToGridY = destination.Y;
        actor.MovementToGridHeight = destination.Height;
        actor.MovementStartedAtSeconds = currentTimeSeconds;
        actor.MovementDurationSeconds = 0;
        actor.MovementProgress = 1;
        actor.HasMovementBacktrackGuardCell = false;
        actor.MovementBacktrackGuardGridX = 0;
        actor.MovementBacktrackGuardGridY = 0;
        actor.MovementBacktrackGuardGridHeight = 0;
        actor.HasSecondaryMovementBacktrackGuardCell = false;
        actor.SecondaryMovementBacktrackGuardGridX = 0;
        actor.SecondaryMovementBacktrackGuardGridY = 0;
        actor.SecondaryMovementBacktrackGuardGridHeight = 0;
        // Displacement invalidates movement and target context derived from the
        // previous anchor; later perception and targeting rebuild from this cell.
        ClearMovementIntentSnapshot(actor);

        if (!keepSkillLock)
        {
            actor.Phase = BattleRuntimeActorPhase.AnchoredDecision;
            actor.ActionReadyAtSeconds = currentTimeSeconds;
            actor.ActionLockTicksRemaining = 0;
            actor.ActionLockReason = "";
            ClearBasicAttackAction(actor);
        }
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

    internal static void MarkHolding(BattleRuntimeActor actor, double currentTimeSeconds, bool preserveMovementSteering = false)
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
        ClearMovementIntentSnapshot(actor, clearSteering: !preserveMovementSteering);
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
        ClearMovementSteering(actor);
        actor.ActionReadyAtSeconds = 0;
        actor.CurrentSkillActionId = "";
        actor.CurrentSkillId = "";
        actor.CurrentSkillSourceCommandId = "";
        actor.CurrentSkillTargetActorId = "";
        actor.CurrentSkillHasTargetGrid = false;
        actor.CurrentSkillTargetGridX = 0;
        actor.CurrentSkillTargetGridY = 0;
        actor.CurrentSkillTargetGridHeight = 0;
        actor.CurrentSkillSelectedSpatialMarkId = "";
        actor.CurrentSkillImpactAtSeconds = 0;
        actor.CurrentSkillImpactApplied = false;
        ClearBasicAttackAction(actor);
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

    private static double ResolveAttackImpactDelaySeconds(BattleRuntimeActor actor, double actionSeconds)
    {
        return BattleActionTimingPolicy.NormalizeAttackImpactDelaySeconds(
            actor?.AttackImpactDelaySeconds ?? BattleActionTimingPolicy.ResolveAttackImpactDelaySeconds(
                actionSeconds,
                BattleActionTimingPolicy.DefaultAttackImpactNormalizedTime),
            actionSeconds);
    }

    private static double ResolveDecisionRetrySeconds(BattleRuntimeActor actor)
    {
        return ResolveMoveStepSeconds(actor);
    }

    internal static void ClearMovementIntentSnapshot(BattleRuntimeActor actor, bool clearSteering = true)
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
        if (clearSteering)
        {
            ClearMovementSteering(actor);
        }
    }

    internal static void ClearBasicAttackAction(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.CurrentBasicAttackTargetActorId = "";
        actor.CurrentBasicAttackDamage = 0;
        actor.CurrentBasicAttackImpactApplied = false;
        actor.CurrentBasicAttackStartedAtSeconds = 0;
        actor.CurrentBasicAttackImpactAtSeconds = 0;
        actor.CurrentBasicAttackEndsAtSeconds = 0;
        actor.CurrentBasicAttackActorGridX = 0;
        actor.CurrentBasicAttackActorGridY = 0;
        actor.CurrentBasicAttackActorGridHeight = 0;
        actor.CurrentBasicAttackTargetGridX = 0;
        actor.CurrentBasicAttackTargetGridY = 0;
        actor.CurrentBasicAttackTargetGridHeight = 0;
    }

    internal static void CopyMovementSteering(BattleRuntimeActor target, BattleRuntimeActor source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.MovementSteeringMode = source.MovementSteeringMode;
        target.MovementSteeringSide = source.MovementSteeringSide;
        target.MovementSteeringBestDistance = source.MovementSteeringBestDistance;
        target.MovementSteeringBudgetRemaining = source.MovementSteeringBudgetRemaining;
        target.MovementSteeringIntentKey = source.MovementSteeringIntentKey ?? "";
    }

    private static void ClearMovementSteering(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.MovementSteeringMode = Rpg.Runtime.Battle.Navigation.BattleLocalSteeringMode.SeekGoal;
        actor.MovementSteeringSide = 0;
        actor.MovementSteeringBestDistance = int.MaxValue;
        actor.MovementSteeringBudgetRemaining = 0;
        actor.MovementSteeringIntentKey = "";
    }
}
