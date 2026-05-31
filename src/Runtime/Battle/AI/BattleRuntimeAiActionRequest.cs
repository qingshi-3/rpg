using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle.AI;

public sealed class BattleRuntimeAiActionRequest
{
    private BattleRuntimeAiActionRequest(
        BattleRuntimeAiActionKind kind,
        string actorId,
        string targetActorId,
        string failureReason,
        string reasonCode = "",
        string localCombatSituationId = "",
        BattleRegionMovementGoal regionMovementGoal = null)
    {
        Kind = kind;
        ActorId = actorId ?? "";
        TargetActorId = targetActorId ?? "";
        FailureReason = failureReason ?? "";
        ReasonCode = reasonCode ?? "";
        LocalCombatSituationId = localCombatSituationId ?? "";
        RegionMovementGoal = regionMovementGoal;
    }

    public BattleRuntimeAiActionKind Kind { get; }
    public string ActorId { get; }
    public string TargetActorId { get; }
    public string FailureReason { get; }
    public string ReasonCode { get; }
    public string LocalCombatSituationId { get; }
    public BattleRegionMovementGoal RegionMovementGoal { get; }

    public static BattleRuntimeAiActionRequest Hold(string actorId, string reason)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.Hold, actorId, "", reason);
    }

    public static BattleRuntimeAiActionRequest AdvanceTowardTarget(string actorId, string targetActorId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.AdvanceTowardTarget, actorId, targetActorId, "");
    }

    public static BattleRuntimeAiActionRequest AdvanceTowardObjective(string actorId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.AdvanceTowardObjective, actorId, "", "");
    }

    public static BattleRuntimeAiActionRequest AdvanceTowardRegion(string actorId, BattleRegionMovementGoal goal)
    {
        return new BattleRuntimeAiActionRequest(
            BattleRuntimeAiActionKind.AdvanceTowardRegion,
            actorId,
            "",
            "",
            goal?.ReasonCode ?? "",
            regionMovementGoal: goal);
    }

    public static BattleRuntimeAiActionRequest WaitForAttackCharge(string actorId, string targetActorId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.WaitForAttackCharge, actorId, targetActorId, "");
    }

    public static BattleRuntimeAiActionRequest AttackTarget(string actorId, string targetActorId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.AttackTarget, actorId, targetActorId, "");
    }

    public static BattleRuntimeAiActionRequest JoinLocalCombat(string actorId, string targetActorId, string reasonCode, string situationId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.JoinLocalCombat, actorId, targetActorId, "", reasonCode, situationId);
    }

    public static BattleRuntimeAiActionRequest HoldSupport(string actorId, string targetActorId, string reasonCode, string situationId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.HoldSupport, actorId, targetActorId, "", reasonCode, situationId);
    }

    public static BattleRuntimeAiActionRequest ReturnToObjective(string actorId, string reasonCode, string situationId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.ReturnToObjective, actorId, "", "", reasonCode, situationId);
    }
}
