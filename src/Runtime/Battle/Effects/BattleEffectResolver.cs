using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal static class BattleEffectResolver
{
    private const double DefaultThunderMarkDurationSeconds = 8.0;
    private const double DefaultChannelDurationSeconds = 0.8;
    private const double DefaultChannelTickIntervalSeconds = 0.2;

    // Runtime effects are source-agnostic: skills, attacks, equipment, and terrain
    // should provide payload/source context without making this resolver own intent.
    internal static IReadOnlyList<BattleEvent> Apply(
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        return payload.EffectKind switch
        {
            BattleSkillEffectKind.Damage => ApplyDamage(context, payload),
            BattleSkillEffectKind.CreateThunderMark => ApplyCreateThunderMark(context),
            BattleSkillEffectKind.TeleportToThunderMark => BattleDisplacementCommitBoundary.CommitThunderMarkTeleport(context, payload),
            BattleSkillEffectKind.StartChanneledAreaDamage => ApplyStartChanneledAreaDamage(context, payload),
            _ => throw new InvalidOperationException($"Unsupported battle effect kind {payload.EffectKind}")
        };
    }

    private static IReadOnlyList<BattleEvent> ApplyDamage(
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        BattleRuntimeActor actor = context.Actor;
        BattleRuntimeActor target = context.Target;
        if (string.IsNullOrWhiteSpace(actor?.ActorId))
        {
            return new[] { CreateEffectFailedEvent(context, null, "", "battle_effect_source_actor_missing") };
        }

        if (string.IsNullOrWhiteSpace(target?.ActorId))
        {
            return new[] { CreateEffectFailedEvent(context, actor, "", "battle_effect_target_actor_missing") };
        }

        BattleCommitBuffer commitBuffer = context.CommitBuffer ?? new BattleCommitBuffer();
        BattleActorRuntime targetRuntime = new(target);
        targetRuntime.EffectReceiver.ReceiveDamage(commitBuffer, context, payload);
        return context.DeferEffectDamageCommit
            ? Array.Empty<BattleEvent>()
            : commitBuffer.CommitEffectDamage();
    }

    private static IReadOnlyList<BattleEvent> ApplyCreateThunderMark(BattleEffectExecutionContext context)
    {
        BattleRuntimeState state = context.State;
        BattleRuntimeActor actor = context.Actor;
        BattleRuntimeActor target = context.Target;
        if (state == null || string.IsNullOrWhiteSpace(actor?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        if (string.IsNullOrWhiteSpace(target?.ActorId) && context.HasTargetGrid)
        {
            BattleRuntimeSpatialMark groundMark = new()
            {
                MarkId = $"{context.SourceCommandId}:mark:ground:{context.TargetGridX},{context.TargetGridY},{context.TargetGridHeight}",
                OwnerBattleGroupId = actor.BattleGroupId ?? "",
                SourceActorId = actor.ActorId ?? "",
                SourceCommandId = context.SourceCommandId ?? "",
                SourceDefinitionId = context.SourceDefinitionId ?? "",
                AttachedActorId = "",
                HasGroundAnchor = true,
                GridX = context.TargetGridX,
                GridY = context.TargetGridY,
                GridHeight = context.TargetGridHeight,
                CreatedAtSeconds = context.RuntimeTimeSeconds,
                ExpiresAtSeconds = context.RuntimeTimeSeconds + DefaultThunderMarkDurationSeconds
            };
            state.SpatialMarks.Add(groundMark);

            return new[]
            {
                new BattleEvent
                {
                    EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:thunder_mark:ground:{context.TargetGridX},{context.TargetGridY},{context.TargetGridHeight}",
                    BattleId = context.BattleId,
                    BattleGroupId = actor.BattleGroupId,
                    ActorId = actor.ActorId,
                    TargetId = "",
                    SourceCommandId = context.SourceCommandId ?? "",
                    SourceActionId = context.SourceActionId ?? "",
                    SourceDefinitionId = context.SourceDefinitionId ?? "",
                    EffectKind = BattleSkillEffectKind.CreateThunderMark.ToString(),
                    Kind = BattleEventKind.ThunderMarkCreated,
                    ReasonCode = "thunder_mark_ground",
                    RuntimeTick = context.RuntimeTick,
                    RuntimeTimeSeconds = context.RuntimeTimeSeconds,
                    HasActorCells = true,
                    ActorGridX = actor.GridX,
                    ActorGridY = actor.GridY,
                    ActorGridHeight = actor.GridHeight,
                    HasTargetCells = true,
                    TargetGridX = context.TargetGridX,
                    TargetGridY = context.TargetGridY,
                    TargetGridHeight = context.TargetGridHeight
                }
            };
        }

        if (string.IsNullOrWhiteSpace(target?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        // Thunder marks are Runtime coordinates. Attached marks cache the target's
        // current anchor for reports, while later resolution follows the live actor.
        BattleRuntimeSpatialMark mark = new()
        {
            MarkId = $"{context.SourceCommandId}:mark:{target.ActorId}",
            OwnerBattleGroupId = actor.BattleGroupId ?? "",
            SourceActorId = actor.ActorId ?? "",
            SourceCommandId = context.SourceCommandId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            AttachedActorId = target.ActorId ?? "",
            HasGroundAnchor = false,
            GridX = target.GridX,
            GridY = target.GridY,
            GridHeight = target.GridHeight,
            CreatedAtSeconds = context.RuntimeTimeSeconds,
            ExpiresAtSeconds = context.RuntimeTimeSeconds + DefaultThunderMarkDurationSeconds
        };
        state.SpatialMarks.Add(mark);

        return new[]
        {
            new BattleEvent
            {
                EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:thunder_mark:{target.ActorId}",
                BattleId = context.BattleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                TargetId = target.ActorId,
                SourceCommandId = context.SourceCommandId ?? "",
                SourceActionId = context.SourceActionId ?? "",
                SourceDefinitionId = context.SourceDefinitionId ?? "",
                EffectKind = BattleSkillEffectKind.CreateThunderMark.ToString(),
                Kind = BattleEventKind.ThunderMarkCreated,
                ReasonCode = "thunder_mark_attached",
                RuntimeTick = context.RuntimeTick,
                RuntimeTimeSeconds = context.RuntimeTimeSeconds,
                HasActorCells = true,
                ActorGridX = actor.GridX,
                ActorGridY = actor.GridY,
                ActorGridHeight = actor.GridHeight,
                HasTargetCells = true,
                TargetGridX = target.GridX,
                TargetGridY = target.GridY,
                TargetGridHeight = target.GridHeight
            }
        };
    }

    private static IReadOnlyList<BattleEvent> ApplyStartChanneledAreaDamage(
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        BattleRuntimeState state = context.State;
        BattleRuntimeActor actor = context.Actor;
        if (state == null || string.IsNullOrWhiteSpace(actor?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        double durationSeconds = payload.DurationSeconds > 0
            ? payload.DurationSeconds
            : DefaultChannelDurationSeconds;
        double tickIntervalSeconds = payload.TickIntervalSeconds > 0
            ? payload.TickIntervalSeconds
            : DefaultChannelTickIntervalSeconds;
        BattleRuntimeActiveChannel channel = new()
        {
            ChannelId = $"{context.SourceCommandId}:channel:{context.SourceDefinitionId}",
            ActorId = actor.ActorId ?? "",
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            StartedAtSeconds = context.RuntimeTimeSeconds,
            EndsAtSeconds = context.RuntimeTimeSeconds + durationSeconds,
            NextTickAtSeconds = context.RuntimeTimeSeconds + tickIntervalSeconds,
            TickIntervalSeconds = tickIntervalSeconds,
            DamageAmount = Math.Max(0, payload.Amount),
            Radius = Math.Max(0, payload.Radius),
            HasTargetOffset = context.HasTargetGrid,
            TargetOffsetX = context.HasTargetGrid ? context.TargetGridX - actor.GridX : 0,
            TargetOffsetY = context.HasTargetGrid ? context.TargetGridY - actor.GridY : 0,
            TargetOffsetHeight = context.HasTargetGrid ? context.TargetGridHeight - actor.GridHeight : 0
        };
        actor.ActiveChannels.Add(channel);

        return BattleChannelDamageResolver.ApplyDamageTick(
            state,
            context.BattleId,
            context.RuntimeTick,
            context.RuntimeTimeSeconds,
            actor,
            channel,
            context.CommitBuffer,
            context.DeferEffectDamageCommit);
    }

    private static BattleEvent CreateEffectFailedEvent(
        BattleEffectExecutionContext context,
        BattleRuntimeActor actor,
        string targetId,
        string reasonCode)
    {
        return new BattleEvent
        {
            EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{context.SourceCommandId}:effect_failed:{reasonCode}",
            BattleId = context.BattleId,
            BattleGroupId = actor?.BattleGroupId ?? "",
            ActorId = actor?.ActorId ?? "",
            TargetId = targetId ?? "",
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            EffectKind = context.SourceDefinitionId ?? "",
            Kind = BattleEventKind.CommandFailed,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = context.RuntimeTick,
            RuntimeTimeSeconds = context.RuntimeTimeSeconds
        };
    }

}
