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
        BattleAttackResolver.Resolve(contexts, tickStartFacts, stream, battleId, tick, currentTimeSeconds);
        BattleEvent[] attackEvents = (stream?.Events ?? System.Array.Empty<BattleEvent>())
            .Skip(firstAttackEventIndex)
            .Where(item => item.Kind == BattleEventKind.DamageApplied)
            .ToArray();
        BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers(
            state,
            attackEvents,
            stream,
            battleId,
            tick,
            currentTimeSeconds);
    }
}
