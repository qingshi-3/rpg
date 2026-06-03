using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal readonly record struct BattleMovementIntentCommit(
    BattleRuntimeAiActionKind RequestKind,
    string TargetActorId,
    string ObjectiveZoneId,
    string RegionId,
    string CommandId,
    string ReasonCode,
    string LocalCombatSituationId,
    bool HasCombatSlotIntent,
    BattleGridCoord CombatSlotAnchor,
    BattleCombatSlotKind CombatSlotKind)
{
    internal static BattleMovementIntentCommit FromContext(BattleRuntimeTickContext context)
    {
        if (context == null)
        {
            return default;
        }

        BattleRuntimeActor actor = context.ActorFact.Actor;
        BattleRuntimeAiActionKind kind = context.Request?.Kind ?? BattleRuntimeAiActionKind.Hold;
        string targetActorId = IsTargetScoped(kind)
            ? context.TargetFact?.Actor.ActorId ?? context.Request?.TargetActorId ?? actor?.TargetActorId ?? ""
            : "";
        string objectiveZoneId = IsObjectiveScoped(kind)
            ? actor?.ObjectiveZoneId ?? ""
            : "";
        string regionId = kind == BattleRuntimeAiActionKind.AdvanceTowardRegion
            ? context.Request?.RegionMovementGoal?.RegionId ?? ""
            : "";
        string reasonCode = !string.IsNullOrWhiteSpace(context.Proposal?.MovementReasonCode)
            ? context.Proposal.MovementReasonCode
            : context.Request?.ReasonCode ?? "";
        string situationId = !string.IsNullOrWhiteSpace(context.Proposal?.LocalCombatSituationId)
            ? context.Proposal.LocalCombatSituationId
            : context.Request?.LocalCombatSituationId ?? "";

        return new BattleMovementIntentCommit(
            kind,
            targetActorId,
            objectiveZoneId,
            regionId,
            context.ActorFact.CommandId ?? actor?.CommandId ?? "",
            reasonCode,
            situationId,
            context.Proposal?.HasCombatSlotIntent == true,
            context.Proposal?.CombatSlotAnchor ?? default,
            context.Proposal?.CombatSlotKind ?? BattleCombatSlotKind.Support);
    }

    private static bool IsTargetScoped(BattleRuntimeAiActionKind kind)
    {
        return kind == BattleRuntimeAiActionKind.AdvanceTowardTarget ||
               kind == BattleRuntimeAiActionKind.JoinLocalCombat ||
               kind == BattleRuntimeAiActionKind.HoldSupport;
    }

    private static bool IsObjectiveScoped(BattleRuntimeAiActionKind kind)
    {
        return kind == BattleRuntimeAiActionKind.AdvanceTowardObjective ||
               kind == BattleRuntimeAiActionKind.ReturnToObjective;
    }
}
