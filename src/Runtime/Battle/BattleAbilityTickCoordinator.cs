using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleAbilityTickCoordinator
{
    internal static HashSet<string> ResolvePending(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        IReadOnlySet<string> actionBlockedActorIds = null,
        ISet<string> waitingActionActorIds = null)
    {
        var startedActionActorIds = new HashSet<string>(StringComparer.Ordinal);
        if (state?.Actors == null || stream == null)
        {
            return startedActionActorIds;
        }

        BattleCommitBuffer abilityTickCommitBuffer = new();
        AdvanceActiveAbilityControllers(state, stream, battleId, runtimeTick, runtimeTimeSeconds, navigationGraph, abilityTickCommitBuffer);
        AdvanceActiveChannelControllers(state, stream, battleId, runtimeTick, runtimeTimeSeconds, abilityTickCommitBuffer);

        return BattleAbilityController.ResolvePendingSkillOrders(
            state,
            stream,
            battleId,
            runtimeTick,
            runtimeTimeSeconds,
            navigationGraph,
            actionBlockedActorIds,
            waitingActionActorIds,
            abilityTickCommitBuffer);
    }

    private static void AdvanceActiveAbilityControllers(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        BattleCommitBuffer abilityTickCommitBuffer)
    {
        BattleCommitBuffer commitBuffer = abilityTickCommitBuffer ?? new BattleCommitBuffer();
        foreach (BattleRuntimeActor actor in state.Actors
                     .Where(item =>
                         item.HitPoints > 0 &&
                         BattleAbilityController.HasActiveSkillAction(item))
                     .OrderBy(item => item.ActorId, StringComparer.Ordinal))
        {
            new BattleActorRuntime(actor).AbilityController.AdvanceActiveSkillAction(
                state,
                stream,
                battleId,
                runtimeTick,
                runtimeTimeSeconds,
                navigationGraph,
                commitBuffer);
        }

        foreach (BattleEvent channelStartEvent in commitBuffer.CommitEffectDeliveries())
        {
            stream.Add(channelStartEvent);
        }
    }

    private static void AdvanceActiveChannelControllers(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleCommitBuffer abilityTickCommitBuffer)
    {
        // Channels are caster-owned, but one runtime tick shares a commit
        // buffer so opposing or overlapping channels apply as one batch.
        BattleCommitBuffer commitBuffer = abilityTickCommitBuffer ?? new BattleCommitBuffer();
        foreach (BattleRuntimeActor actor in state.Actors
                     .Where(item =>
                         item.HitPoints > 0 &&
                         BattleAbilityController.HasActiveChannels(item))
                     .OrderBy(item => item.ActorId, StringComparer.Ordinal))
        {
            foreach (BattleEvent channelEvent in new BattleActorRuntime(actor).AbilityController.AdvanceActiveChannels(
                         state,
                         battleId,
                         runtimeTick,
                         runtimeTimeSeconds,
                         commitBuffer))
            {
                stream.Add(channelEvent);
            }
        }

        foreach (BattleEvent channelEvent in commitBuffer.CommitEffectDeliveries())
        {
            stream.Add(channelEvent);
        }
    }
}
