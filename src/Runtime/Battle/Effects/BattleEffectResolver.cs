using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

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
            BattleSkillEffectKind.TeleportToThunderMark => ApplyTeleportToThunderMark(context, payload),
            BattleSkillEffectKind.StartChanneledAreaDamage => ApplyStartChanneledAreaDamage(context, payload),
            _ => Array.Empty<BattleEvent>()
        };
    }

    internal static IReadOnlyList<BattleEvent> AdvanceActiveChannels(
        BattleRuntimeState state,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
    {
        if (state?.ActiveChannels == null || state.ActiveChannels.Count == 0)
        {
            return Array.Empty<BattleEvent>();
        }

        List<BattleEvent> events = new();
        List<BattleRuntimeActiveChannel> remove = new();
        foreach (BattleRuntimeActiveChannel channel in state.ActiveChannels)
        {
            BattleRuntimeActor actor = FindActor(state, channel.ActorId);
            if (actor == null || actor.HitPoints <= 0 || runtimeTimeSeconds >= channel.EndsAtSeconds + 0.0001)
            {
                remove.Add(channel);
                continue;
            }

            if (channel.NextTickAtSeconds <= runtimeTimeSeconds + 0.0001)
            {
                events.AddRange(ApplyChannelDamageTick(
                    state,
                    battleId ?? "",
                    runtimeTick,
                    runtimeTimeSeconds,
                    actor,
                    channel));
                channel.NextTickAtSeconds += Math.Max(0.001, channel.TickIntervalSeconds);
            }
        }

        foreach (BattleRuntimeActiveChannel channel in remove)
        {
            state.ActiveChannels.Remove(channel);
        }

        return events;
    }

    private static IReadOnlyList<BattleEvent> ApplyDamage(
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        BattleRuntimeActor actor = context.Actor ?? new BattleRuntimeActor();
        BattleRuntimeActor target = context.Target ?? new BattleRuntimeActor();
        int appliedDamage = Math.Max(0, payload.Amount);
        if (string.IsNullOrWhiteSpace(actor.ActorId) || string.IsNullOrWhiteSpace(target.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

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

    private static IReadOnlyList<BattleEvent> ApplyCreateThunderMark(BattleEffectExecutionContext context)
    {
        BattleRuntimeState state = context.State;
        BattleRuntimeActor actor = context.Actor ?? new BattleRuntimeActor();
        BattleRuntimeActor target = context.Target ?? new BattleRuntimeActor();
        if (state == null || string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        if (string.IsNullOrWhiteSpace(target.ActorId) && context.HasTargetGrid)
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

        if (string.IsNullOrWhiteSpace(target.ActorId))
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
        BattleRuntimeActor actor = context.Actor ?? new BattleRuntimeActor();
        if (state == null || string.IsNullOrWhiteSpace(actor.ActorId))
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
        state.ActiveChannels.Add(channel);

        return ApplyChannelDamageTick(
            state,
            context.BattleId,
            context.RuntimeTick,
            context.RuntimeTimeSeconds,
            actor,
            channel);
    }

    private static IReadOnlyList<BattleEvent> ApplyChannelDamageTick(
        BattleRuntimeState state,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor actor,
        BattleRuntimeActiveChannel channel)
    {
        if (state?.Actors == null || actor == null || channel == null || channel.DamageAmount <= 0)
        {
            return Array.Empty<BattleEvent>();
        }

        List<BattleEvent> events = new();
        BattleGridCoord channelCenter = ResolveChannelCenter(actor, channel);
        int radius = Math.Max(0, channel.Radius);
        foreach (BattleRuntimeActor target in state.Actors)
        {
            if (target == null ||
                target.Kind != BattleRuntimeActorKind.Corps ||
                target.HitPoints <= 0 ||
                BattleRuntimeTickResolver.SameFaction(actor, target))
            {
                continue;
            }

            if (!IsTargetOverlappingChannelArea(target, channelCenter, radius))
            {
                continue;
            }

            BattleEffectExecutionContext damageContext = new()
            {
                BattleId = battleId ?? "",
                RuntimeTick = runtimeTick,
                RuntimeTimeSeconds = runtimeTimeSeconds,
                SourceCommandId = channel.SourceCommandId ?? "",
                SourceActionId = channel.SourceActionId ?? "",
                SourceDefinitionId = channel.SourceDefinitionId ?? "",
                State = state,
                Actor = actor,
                Target = target
            };
            events.AddRange(ApplyDamage(
                damageContext,
                new BattleEffectPayload
                {
                    EffectKind = BattleSkillEffectKind.Damage,
                    Amount = channel.DamageAmount
                }));
            if (target.HitPoints <= 0)
            {
                BattleRuntimeActorStateMachine.MarkDefeated(target);
            }
        }

        return events;
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

    private static IReadOnlyList<BattleEvent> ApplyTeleportToThunderMark(
        BattleEffectExecutionContext context,
        BattleEffectPayload payload)
    {
        BattleRuntimeState state = context.State;
        BattleRuntimeActor actor = context.Actor ?? new BattleRuntimeActor();
        if (state == null || string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        if (!context.HasTargetGrid)
        {
            return new[] { CreateCommandFailedEvent(context, actor, "", "skill_target_cell_required") };
        }

        if (string.IsNullOrWhiteSpace(context.SelectedSpatialMarkId))
        {
            return new[] { CreateCommandFailedEvent(context, actor, "", "thunder_mark_selection_required") };
        }

        if (!BattleRuntimeThunderMarkQueries.TryResolveLiveMarkAnchorById(state, context.SelectedSpatialMarkId, actor.BattleGroupId, context.RuntimeTimeSeconds, out BattleRuntimeSpatialMark mark, out BattleGridCoord markAnchor))
        {
            return new[] { CreateCommandFailedEvent(context, actor, "", "thunder_mark_missing") };
        }

        BattleGridCoord destination = new(context.TargetGridX, context.TargetGridY, context.TargetGridHeight);
        int radius = Math.Max(1, payload.Amount);
        if (destination.Height != markAnchor.Height ||
            Math.Max(Math.Abs(destination.X - markAnchor.X), Math.Abs(destination.Y - markAnchor.Y)) > radius)
        {
            return new[] { CreateCommandFailedEvent(context, actor, mark.AttachedActorId, "thunder_mark_destination_not_near_mark") };
        }

        if (context.NavigationGraph?.CanPlaceFootprint(actor, destination) != true)
        {
            return new[] { CreateCommandFailedEvent(context, actor, mark.AttachedActorId, "thunder_mark_destination_invalid") };
        }

        BattleDynamicOccupancy occupancy = BattleDynamicOccupancy.FromActors(state.Actors);
        if (!occupancy.CanPlaceFootprint(actor, destination))
        {
            return new[] { CreateCommandFailedEvent(context, actor, mark.AttachedActorId, "thunder_mark_destination_occupied") };
        }

        BattleGridCoord from = new(actor.GridX, actor.GridY, actor.GridHeight);
        string beforeDisplacementState = DescribeActorDisplacementState(actor);
        BattleRuntimeActorStateMachine.CommitDisplacement(actor, destination, context.RuntimeTimeSeconds);
        string afterDisplacementState = DescribeActorDisplacementState(actor);
        GameLog.Info(
            nameof(BattleEffectResolver),
            $"BattleRuntimeThunderFoldDisplacementCommitted battle={context.BattleId ?? ""} tick={context.RuntimeTick} actor={actor.ActorId} mark={context.SelectedSpatialMarkId ?? ""} from={FormatGridCoord(from)} markAnchor={FormatGridCoord(markAnchor)} to={FormatGridCoord(destination)} before=[{beforeDisplacementState}] after=[{afterDisplacementState}]");

        return new[]
        {
            new BattleEvent
            {
                EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:thunder_fold:{destination.X},{destination.Y},{destination.Height}",
                BattleId = context.BattleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                TargetId = mark.AttachedActorId ?? "",
                SourceCommandId = context.SourceCommandId ?? "",
                SourceActionId = context.SourceActionId ?? "",
                SourceDefinitionId = context.SourceDefinitionId ?? "",
                EffectKind = BattleSkillEffectKind.TeleportToThunderMark.ToString(),
                Kind = BattleEventKind.ThunderMarkTeleported,
                ReasonCode = "thunder_mark_fold",
                RuntimeTick = context.RuntimeTick,
                RuntimeTimeSeconds = context.RuntimeTimeSeconds,
                HasActorCells = true,
                ActorGridX = actor.GridX,
                ActorGridY = actor.GridY,
                ActorGridHeight = actor.GridHeight,
                HasTargetCells = true,
                TargetGridX = markAnchor.X,
                TargetGridY = markAnchor.Y,
                TargetGridHeight = markAnchor.Height,
                HasMovementCells = true,
                FromGridX = from.X,
                FromGridY = from.Y,
                FromGridHeight = from.Height,
                ToGridX = destination.X,
                ToGridY = destination.Y,
                ToGridHeight = destination.Height
            }
        };
    }

    private static string DescribeActorDisplacementState(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return "missing";
        }

        return
            $"phase={actor.Phase} motion={actor.MotionState} target={actor.TargetActorId ?? ""} " +
            $"reserved={(actor.HasReservedGridCell ? FormatGridCoord(actor.ReservedGridX, actor.ReservedGridY, actor.ReservedGridHeight) : "none")} " +
            $"movement={(actor.HasMovementTarget ? $"{FormatGridCoord(actor.MovementFromGridX, actor.MovementFromGridY, actor.MovementFromGridHeight)}->{FormatGridCoord(actor.MovementToGridX, actor.MovementToGridY, actor.MovementToGridHeight)}" : "none")} " +
            $"intent={(actor.HasMovementIntentSnapshot ? $"{actor.MovementIntentKind}:{actor.MovementIntentTargetActorId ?? ""}:{actor.MovementIntentObjectiveZoneId ?? ""}:{actor.MovementIntentReasonCode ?? ""}" : "none")} " +
            $"steering={actor.MovementSteeringMode}:{actor.MovementSteeringIntentKey ?? ""}:{actor.MovementSteeringBudgetRemaining} " +
            $"backtrack={(actor.HasMovementBacktrackGuardCell ? FormatGridCoord(actor.MovementBacktrackGuardGridX, actor.MovementBacktrackGuardGridY, actor.MovementBacktrackGuardGridHeight) : "none")}/{(actor.HasSecondaryMovementBacktrackGuardCell ? FormatGridCoord(actor.SecondaryMovementBacktrackGuardGridX, actor.SecondaryMovementBacktrackGuardGridY, actor.SecondaryMovementBacktrackGuardGridHeight) : "none")}";
    }

    private static string FormatGridCoord(BattleGridCoord coord)
    {
        return FormatGridCoord(coord.X, coord.Y, coord.Height);
    }

    private static string FormatGridCoord(int x, int y, int height)
    {
        return $"{x},{y},{height}";
    }

    private static BattleRuntimeActor FindActor(BattleRuntimeState state, string actorId)
    {
        if (state?.Actors == null || string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        return state.Actors.Find(item => string.Equals(item.ActorId, actorId, StringComparison.Ordinal));
    }

    private static BattleEvent CreateCommandFailedEvent(
        BattleEffectExecutionContext context,
        BattleRuntimeActor actor,
        string targetId,
        string reasonCode)
    {
        return new BattleEvent
        {
            EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{context.SourceCommandId}:thunder_skill_failed:{reasonCode}",
            BattleId = context.BattleId,
            BattleGroupId = actor.BattleGroupId ?? "",
            ActorId = actor.ActorId ?? "",
            TargetId = targetId ?? "",
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            Kind = BattleEventKind.CommandFailed,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = context.RuntimeTick,
            RuntimeTimeSeconds = context.RuntimeTimeSeconds
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
