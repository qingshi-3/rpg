using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle;

internal static class BattleAttackResolver
{
    internal static void Resolve(
        List<BattleRuntimeTickContext> contexts,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeState state)
    {
        BattleCommitBuffer commitBuffer = new();
        foreach (BattleRuntimeActor actor in (state?.Actors ?? Enumerable.Empty<BattleRuntimeActor>())
                     .Where(item =>
                         item.HitPoints > 0 &&
                         BattleActionController.HasActiveBasicAttackAction(item))
                     .OrderBy(item => item.ActorId, System.StringComparer.Ordinal))
        {
            BattleActorRuntime actorRuntime = new(actor);
            actorRuntime.ActionController.AdvanceBasicAttackAction(state, commitBuffer, currentTimeSeconds);
        }

        foreach (BattleRuntimeTickContext context in contexts
                     .Where(item =>
                         item.Request.Kind == BattleRuntimeAiActionKind.AttackTarget &&
                         item.Result == null))
        {
            BattleActorRuntime actorRuntime = new(context.ActorFact.Actor);
            actorRuntime.ActionController.ProposeBasicAttack(context, commitBuffer, stream, battleId, tick, currentTimeSeconds);
        }

        commitBuffer.CommitBasicAttacks(tickStartFacts, stream, battleId, tick, currentTimeSeconds);
    }
}
