using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Effects;

internal static class BattleChannelDamageResolver
{
    internal static IReadOnlyList<BattleEvent> ApplyDamageTick(
        BattleRuntimeState state,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor actor,
        BattleRuntimeActiveChannel channel,
        BattleCommitBuffer commitBuffer = null,
        bool deferEffectDamageCommit = false)
    {
        if (state?.Actors == null || actor == null || channel == null || channel.DamageAmount <= 0)
        {
            return Array.Empty<BattleEvent>();
        }

        BattleCommitBuffer deliveryBuffer = commitBuffer ?? new BattleCommitBuffer();
        BattleGridCoord channelCenter = ResolveChannelCenter(actor, channel);
        int radius = Math.Max(0, channel.Radius);
        foreach (BattleRuntimeActor target in state.Actors)
        {
            if (target == null ||
                target.Kind != BattleRuntimeActorKind.Corps ||
                target.HitPoints <= 0 ||
                BattleRuntimeIdentityRules.SameFaction(actor, target))
            {
                continue;
            }

            if (!IsTargetOverlappingChannelArea(target, channelCenter, radius))
            {
                continue;
            }

            deliveryBuffer.RequestEffectDelivery(
                new BattleEffectExecutionContext
                {
                    BattleId = battleId ?? "",
                    RuntimeTick = runtimeTick,
                    RuntimeTimeSeconds = runtimeTimeSeconds,
                    SourceCommandId = channel.SourceCommandId ?? "",
                    SourceActionId = channel.SourceActionId ?? "",
                    SourceDefinitionId = channel.SourceDefinitionId ?? "",
                    CommitBuffer = commitBuffer,
                    DeferEffectDamageCommit = deferEffectDamageCommit,
                    State = state,
                    Actor = actor,
                    Target = target
                },
                target,
                BattleSkillEffectKind.Damage,
                channel.DamageAmount);
        }

        return deferEffectDamageCommit && commitBuffer != null
            ? Array.Empty<BattleEvent>()
            : deliveryBuffer.CommitEffectDeliveries();
    }

    private static BattleGridCoord ResolveChannelCenter(
        BattleRuntimeActor actor,
        BattleRuntimeActiveChannel channel)
    {
        return channel?.HasTargetOffset == true
            ? new BattleGridCoord(
                actor.GridX + channel.TargetOffsetX,
                actor.GridY + channel.TargetOffsetY,
                actor.GridHeight + channel.TargetOffsetHeight)
            : new BattleGridCoord(actor?.GridX ?? 0, actor?.GridY ?? 0, actor?.GridHeight ?? 0);
    }

    private static bool IsTargetOverlappingChannelArea(
        BattleRuntimeActor target,
        BattleGridCoord center,
        int radius)
    {
        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(
                     target,
                     new BattleGridCoord(target.GridX, target.GridY, target.GridHeight)))
        {
            if (cell.Height == center.Height &&
                Math.Max(Math.Abs(cell.X - center.X), Math.Abs(cell.Y - center.Y)) <= radius)
            {
                return true;
            }
        }

        return false;
    }
}
