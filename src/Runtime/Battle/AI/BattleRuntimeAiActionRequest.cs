namespace Rpg.Runtime.Battle.AI;

public sealed class BattleRuntimeAiActionRequest
{
    private BattleRuntimeAiActionRequest(
        BattleRuntimeAiActionKind kind,
        string actorId,
        string targetActorId,
        string failureReason)
    {
        Kind = kind;
        ActorId = actorId ?? "";
        TargetActorId = targetActorId ?? "";
        FailureReason = failureReason ?? "";
    }

    public BattleRuntimeAiActionKind Kind { get; }
    public string ActorId { get; }
    public string TargetActorId { get; }
    public string FailureReason { get; }

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

    public static BattleRuntimeAiActionRequest WaitForAttackCharge(string actorId, string targetActorId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.WaitForAttackCharge, actorId, targetActorId, "");
    }

    public static BattleRuntimeAiActionRequest AttackTarget(string actorId, string targetActorId)
    {
        return new BattleRuntimeAiActionRequest(BattleRuntimeAiActionKind.AttackTarget, actorId, targetActorId, "");
    }
}
