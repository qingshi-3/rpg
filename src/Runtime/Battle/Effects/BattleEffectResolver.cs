using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal static class BattleEffectResolver
{
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
            _ => Array.Empty<BattleEvent>()
        };
    }

    private static IReadOnlyList<BattleEvent> ApplyDamage(
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        BattleRuntimeActor actor = context.Actor ?? new BattleRuntimeActor();
        BattleRuntimeActor target = context.Target ?? new BattleRuntimeActor();
        int appliedDamage = Math.Max(0, payload.Amount);

        if (appliedDamage > 0)
        {
            target.HitPoints = Math.Max(0, target.HitPoints - appliedDamage);
            if (target.HitPoints == 0)
            {
                target.Phase = BattleRuntimeActorPhase.Defeated;
            }
        }

        string effectKind = payload.EffectKind.ToString();
        int delta = -appliedDamage;
        bool defeated = target.HitPoints == 0;

        return new[]
        {
            CreateEvent(
                context,
                actor,
                target,
                BattleEventKind.EffectApplied,
                $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:effect:{target.ActorId}",
                effectKind,
                "effect_applied",
                delta),
            CreateEvent(
                context,
                actor,
                target,
                BattleEventKind.DamageApplied,
                $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:effect_damage:{target.ActorId}",
                effectKind,
                defeated ? "effect_damage_target_defeated" : "effect_damage",
                delta)
        };
    }

    private static BattleEvent CreateEvent(
        BattleEffectExecutionContext context,
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleEventKind kind,
        string eventId,
        string effectKind,
        string reasonCode,
        int corpsStrengthDelta)
    {
        return new BattleEvent
        {
            EventId = eventId,
            BattleId = context.BattleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = target.ActorId,
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            EffectKind = effectKind,
            Kind = kind,
            ReasonCode = reasonCode,
            RuntimeTick = context.RuntimeTick,
            RuntimeTimeSeconds = context.RuntimeTimeSeconds,
            CorpsStrengthDelta = corpsStrengthDelta,
            HasActorCells = true,
            ActorGridX = actor.GridX,
            ActorGridY = actor.GridY,
            ActorGridHeight = actor.GridHeight,
            HasTargetCells = true,
            TargetGridX = target.GridX,
            TargetGridY = target.GridY,
            TargetGridHeight = target.GridHeight
        };
    }
}
