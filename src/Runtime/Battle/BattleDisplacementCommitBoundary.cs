using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Effects;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleDisplacementCommitBoundary
{
    internal static bool ValidateMarkTeleportDestination(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        string selectedSpatialMarkId,
        BattleGridCoord destination,
        int radius,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        out BattleRuntimeSpatialMark mark,
        out BattleGridCoord markAnchor,
        out string reasonCode)
    {
        mark = null;
        markAnchor = default;
        reasonCode = "";
        if (!BattleRuntimeThunderMarkQueries.TryResolveLiveMarkAnchorById(
                state,
                selectedSpatialMarkId,
                actor?.BattleGroupId ?? "",
                runtimeTimeSeconds,
                out mark,
                out markAnchor))
        {
            reasonCode = "thunder_mark_missing";
            return false;
        }

        int normalizedRadius = Math.Max(1, radius);
        if (destination.Height != markAnchor.Height ||
            Math.Max(Math.Abs(destination.X - markAnchor.X), Math.Abs(destination.Y - markAnchor.Y)) > normalizedRadius)
        {
            reasonCode = "thunder_mark_destination_not_near_mark";
            return false;
        }

        if (navigationGraph?.CanPlaceFootprint(actor, destination) != true)
        {
            reasonCode = "thunder_mark_destination_invalid";
            return false;
        }

        BattleDynamicOccupancy occupancy = BattleDynamicOccupancy.FromActors(state?.Actors);
        if (!occupancy.CanPlaceFootprint(actor, destination))
        {
            reasonCode = "thunder_mark_destination_occupied";
            return false;
        }

        return true;
    }

    internal static IReadOnlyList<BattleEvent> CommitMarkTeleport(
        BattleEffectExecutionContext context,
        TeleportToMarkSkillEffectSnapshot payload)
    {
        BattleRuntimeState state = context?.State;
        BattleRuntimeActor actor = context?.Actor;
        if (state == null || string.IsNullOrWhiteSpace(actor?.ActorId))
        {
            return Array.Empty<BattleEvent>();
        }

        if (context?.HasTargetGrid != true)
        {
            return new[] { CreateCommandFailedEvent(context, actor, "", "skill_target_cell_required") };
        }

        if (string.IsNullOrWhiteSpace(context.SelectedSpatialMarkId))
        {
            return new[] { CreateCommandFailedEvent(context, actor, "", "thunder_mark_selection_required") };
        }

        BattleGridCoord destination = new(context.TargetGridX, context.TargetGridY, context.TargetGridHeight);
        int radius = Math.Max(1, payload?.LandingRadius ?? 0);
        if (!ValidateMarkTeleportDestination(
                state,
                actor,
                context.SelectedSpatialMarkId,
                destination,
                radius,
                context.RuntimeTimeSeconds,
                context.NavigationGraph,
                out BattleRuntimeSpatialMark mark,
                out BattleGridCoord markAnchor,
                out string reasonCode))
        {
            return new[] { CreateCommandFailedEvent(context, actor, mark?.AttachedActorId ?? "", reasonCode) };
        }

        BattleGridCoord from = new(actor.GridX, actor.GridY, actor.GridHeight);
        string beforeDisplacementState = DescribeActorDisplacementState(actor);
        BattleRuntimeActorStateMachine.CommitDisplacement(actor, destination, context.RuntimeTimeSeconds);
        string afterDisplacementState = DescribeActorDisplacementState(actor);
        GameLog.Info(
            nameof(BattleDisplacementCommitBoundary),
            $"BattleRuntimeMarkTeleportDisplacementCommitted battle={context.BattleId ?? ""} tick={context.RuntimeTick} actor={actor.ActorId} mark={context.SelectedSpatialMarkId ?? ""} from={FormatGridCoord(from)} markAnchor={FormatGridCoord(markAnchor)} to={FormatGridCoord(destination)} before=[{beforeDisplacementState}] after=[{afterDisplacementState}]");

        BattleEvent teleportEvent = new()
        {
            EventId = $"{context.BattleId}:tick_{context.RuntimeTick}:{actor.ActorId}:mark_teleport:{destination.X},{destination.Y},{destination.Height}",
            BattleId = context.BattleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = mark.AttachedActorId ?? "",
            SourceCommandId = context.SourceCommandId ?? "",
            SourceActionId = context.SourceActionId ?? "",
            SourceDefinitionId = context.SourceDefinitionId ?? "",
            EffectKind = BattleEffectKindLabels.TeleportToMark,
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
        };
        BattleEventPresentationFields.CopyFromContext(teleportEvent, context);
        return new[] { teleportEvent };
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

    private static BattleEvent CreateCommandFailedEvent(
        BattleEffectExecutionContext context,
        BattleRuntimeActor actor,
        string targetId,
        string reasonCode)
    {
        BattleEvent failedEvent = new()
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
        BattleEventPresentationFields.CopyFromContext(failedEvent, context);
        return failedEvent;
    }
}
