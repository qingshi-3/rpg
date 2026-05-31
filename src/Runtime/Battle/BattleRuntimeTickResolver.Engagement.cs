using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private void ResolveAttackProposalsAndEngagementTriggers(
        List<BattleRuntimeTickContext> contexts,
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeState state)
    {
        int firstAttackEventIndex = stream?.Events?.Count ?? 0;
        ResolveAttackProposals(contexts, tickStartFacts, stream, battleId, tick, currentTimeSeconds);
        BattleEvent[] attackEvents = (stream?.Events ?? System.Array.Empty<BattleEvent>())
            .Skip(firstAttackEventIndex)
            .Where(item => item.Kind == BattleEventKind.DamageApplied)
            .ToArray();
        if (attackEvents.Length == 0 || state?.Actors == null)
        {
            return;
        }

        Dictionary<string, string> actorGroupIds = state.Actors
            .Where(item => !string.IsNullOrWhiteSpace(item.ActorId) &&
                           !string.IsNullOrWhiteSpace(item.BattleGroupId))
            .ToDictionary(
                item => item.ActorId,
                item => item.BattleGroupId,
                System.StringComparer.Ordinal);
        IReadOnlyList<BattleEvent> engagementEvents = BattleGroupEngagementStateMachine.ApplyMemberActionTransitions(
            state.TacticalStateStore,
            attackEvents,
            actorGroupIds,
            battleId,
            tick,
            currentTimeSeconds);
        foreach (BattleEvent engagementEvent in engagementEvents)
        {
            stream.Add(engagementEvent);
        }
    }
}
