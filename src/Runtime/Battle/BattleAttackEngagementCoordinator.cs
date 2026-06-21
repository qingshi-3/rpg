using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle;

// Keeps attack-result engagement updates adjacent to the attack event slice
// without making the tick resolver inspect event-stream internals.
internal static class BattleAttackEngagementCoordinator
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
        System.ArgumentNullException.ThrowIfNull(contexts);
        System.ArgumentNullException.ThrowIfNull(tickStartFacts);
        System.ArgumentNullException.ThrowIfNull(stream);
        System.ArgumentNullException.ThrowIfNull(state);

        int firstAttackEventIndex = stream.Events.Count;
        BattleAttackResolver.Resolve(contexts, tickStartFacts, stream, battleId, tick, currentTimeSeconds, state);
        BattleEvent[] attackEvents = stream.Events
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
