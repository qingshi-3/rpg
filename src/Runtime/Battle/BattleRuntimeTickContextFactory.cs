using System.Collections.Generic;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeTickContextFactory
{
    internal static BattleRuntimeTickContext Create(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        bool hasMoveTo,
        BattleGridCoord moveTo,
        string failureReason,
        IReadOnlyList<BattleGridCoord> moveOptions = null,
        LocalCombatSituation localCombatSituation = null,
        string movementReasonCode = "",
        BattleRegionMovementGoal regionMovementGoal = null,
        BattleCombatSlotIntent? combatSlotIntent = null)
    {
        return new BattleRuntimeTickContext
        {
            Request = request,
            ActorFact = actorFact,
            TargetFact = targetFact,
            LocalCombatSituation = localCombatSituation,
            Proposal = new BattleRuntimeActionProposal
            {
                Request = request,
                Actor = actorFact.Actor,
                Target = targetFact?.Actor,
                ActorStart = actorFact.Anchor,
                TargetStart = targetFact?.Anchor ?? default,
                HasMoveTo = hasMoveTo,
                MoveTo = moveTo,
                MoveOptions = moveOptions ?? new List<BattleGridCoord>(),
                HasCombatSlotIntent = combatSlotIntent.HasValue,
                CombatSlotAnchor = combatSlotIntent?.Anchor ?? default,
                CombatSlotKind = combatSlotIntent?.Kind ?? BattleCombatSlotKind.Support,
                FailureReason = failureReason ?? "",
                MovementReasonCode = movementReasonCode ?? "",
                LocalCombatSituationId = localCombatSituation?.SituationId ?? ""
            }
        };
    }
}
