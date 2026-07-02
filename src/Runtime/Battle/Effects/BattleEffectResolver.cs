using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal static class BattleEffectResolver
{
    // The resolver owns reusable effect primitives only. Typed executors decide
    // which primitive to call, so new effect families do not expand a shared
    // generic payload switch.
    internal static IReadOnlyList<BattleEvent> ApplyDamage(
        BattleEffectExecutionContext context,
        DamageSkillEffectSnapshot payload)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

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
        targetRuntime.EffectReceiver.ReceiveDamage(
            commitBuffer,
            context,
            payload.BaseDamage,
            BattleEffectKindLabels.Damage);
        return context.DeferEffectDamageCommit
            ? Array.Empty<BattleEvent>()
            : commitBuffer.CommitEffectDamage();
    }

    internal static IReadOnlyList<BattleEvent> ApplyCreateMark(
        BattleEffectExecutionContext context,
        CreateMarkSkillEffectSnapshot payload)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        BattleRuntimeState state = context.State;
        BattleRuntimeActor actor = context.Actor;
        BattleRuntimeActor target = context.Target;
        if (state == null || string.IsNullOrWhiteSpace(actor?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        double lifetimeSeconds = payload.LifetimeSeconds;
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
                ExpiresAtSeconds = context.RuntimeTimeSeconds + lifetimeSeconds
            };
            state.SpatialMarks.Add(groundMark);

            BattleEvent markEvent = new()
            {
                EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:mark:ground:{context.TargetGridX},{context.TargetGridY},{context.TargetGridHeight}",
                BattleId = context.BattleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                TargetId = "",
                SourceCommandId = context.SourceCommandId ?? "",
                SourceActionId = context.SourceActionId ?? "",
                SourceDefinitionId = context.SourceDefinitionId ?? "",
                EffectKind = BattleEffectKindLabels.CreateMark,
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
            };
            BattleEventPresentationFields.CopyFromContext(markEvent, context);
            return new[] { markEvent };
        }

        if (string.IsNullOrWhiteSpace(target?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        // Attached marks store the current target anchor for reports; future
        // resolution follows the live actor through the mark query boundary.
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
            ExpiresAtSeconds = context.RuntimeTimeSeconds + lifetimeSeconds
        };
        state.SpatialMarks.Add(mark);

        BattleEvent attachedMarkEvent = new()
        {
            EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:mark:{target.ActorId}",
            BattleId = context.BattleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = target.ActorId,
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            EffectKind = BattleEffectKindLabels.CreateMark,
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
        };
        BattleEventPresentationFields.CopyFromContext(attachedMarkEvent, context);
        return new[] { attachedMarkEvent };
    }

    internal static IReadOnlyList<BattleEvent> BeginChanneledArea(
        BattleEffectExecutionContext context,
        ChanneledAreaDamageSkillEffectSnapshot payload)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        BattleRuntimeState state = context.State;
        BattleRuntimeActor actor = context.Actor;
        if (state == null || string.IsNullOrWhiteSpace(actor?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        BattleRuntimeActiveChannel channel = new()
        {
            ChannelId = $"{context.SourceCommandId}:channel:{context.SourceDefinitionId}",
            ActorId = actor.ActorId ?? "",
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            PresentationProfileId = context.PresentationProfileId ?? "",
            CastFxProfileId = context.CastFxProfileId ?? "",
            ImpactFxProfileId = context.ImpactFxProfileId ?? "",
            MarkFxProfileId = context.MarkFxProfileId ?? "",
            AreaFxProfileId = context.AreaFxProfileId ?? "",
            SuppressActorCastFx = context.SuppressActorCastFx,
            HoldCastAnimationDuringAction = context.HoldCastAnimationDuringAction,
            StartedAtSeconds = context.RuntimeTimeSeconds,
            EndsAtSeconds = context.RuntimeTimeSeconds + payload.DurationSeconds,
            NextTickAtSeconds = context.RuntimeTimeSeconds + payload.TickIntervalSeconds,
            TickIntervalSeconds = payload.TickIntervalSeconds,
            DamageAmount = payload.BaseDamage,
            Radius = payload.Radius,
            HasTargetOffset = payload.UsesTargetOffset && context.HasTargetGrid,
            TargetOffsetX = payload.UsesTargetOffset && context.HasTargetGrid ? context.TargetGridX - actor.GridX : 0,
            TargetOffsetY = payload.UsesTargetOffset && context.HasTargetGrid ? context.TargetGridY - actor.GridY : 0,
            TargetOffsetHeight = payload.UsesTargetOffset && context.HasTargetGrid ? context.TargetGridHeight - actor.GridHeight : 0
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
        BattleEvent failedEvent = new()
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
        BattleEventPresentationFields.CopyFromContext(failedEvent, context);
        return failedEvent;
    }
}
