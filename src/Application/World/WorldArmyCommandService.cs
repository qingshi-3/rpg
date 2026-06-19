using System.Collections.Generic;
using Godot;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldArmyCommandResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; } = "";
    public List<string> CommandedArmyIds { get; } = new();
    public List<GameEvent> Events { get; } = new();

    public static WorldArmyCommandResult Fail(string failureReason)
    {
        return new WorldArmyCommandResult
        {
            Success = false,
            FailureReason = failureReason ?? ""
        };
    }
}

// Application owns durable army command writes; Presentation may resolve input targets,
// but it must submit the resulting intent here instead of mutating WorldArmyState itself.
public sealed class WorldArmyCommandService
{
    public WorldArmyCommandResult ApplyMoveToPosition(
        IReadOnlyList<WorldArmyState> armies,
        Vector2 destination,
        IReadOnlyDictionary<string, StrategicNavigationPath> paths,
        int navigationSurfaceVersion,
        string requiredOwnerFactionId = StrategicWorldIds.FactionPlayer)
    {
        if (!TryValidateCommandableArmies(armies, requiredOwnerFactionId, out WorldArmyCommandResult invalidResult))
        {
            return invalidResult;
        }

        WorldArmyCommandResult result = new() { Success = true };
        foreach (WorldArmyState army in armies)
        {
            if (army == null)
            {
                continue;
            }

            army.TargetSiteId = "";
            army.Destination = destination;
            army.Intent = WorldArmyIntent.MoveToPosition;
            army.Status = WorldArmyStatus.Moving;
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
            ApplyNavigationPath(army, paths, destination, navigationSurfaceVersion);
            result.CommandedArmyIds.Add(army.ArmyId);
        }

        result.Events.Add(BuildCommandEvent("WorldArmyMoveCommanded", result.CommandedArmyIds, destination, "", WorldArmyIntent.MoveToPosition));
        GameLog.Info(nameof(WorldArmyCommandService), $"WorldArmyCommandMove count={result.CommandedArmyIds.Count} destination={destination}");
        return result;
    }

    public WorldArmyCommandResult ApplySiteCommand(
        IReadOnlyList<WorldArmyState> armies,
        string targetSiteId,
        Vector2 destination,
        Vector2 arrivalApproachOffset,
        WorldSiteAttackDirection approachDirection,
        WorldArmyIntent intent,
        IReadOnlyDictionary<string, StrategicNavigationPath> paths,
        int navigationSurfaceVersion,
        string requiredOwnerFactionId = StrategicWorldIds.FactionPlayer)
    {
        if (string.IsNullOrWhiteSpace(targetSiteId))
        {
            return WorldArmyCommandResult.Fail("missing_target_site");
        }

        if (intent is not (WorldArmyIntent.ReinforceSite or WorldArmyIntent.AssaultSite))
        {
            return WorldArmyCommandResult.Fail($"unsupported_site_command_intent:{intent}");
        }

        if (!TryValidateCommandableArmies(armies, requiredOwnerFactionId, out WorldArmyCommandResult invalidResult))
        {
            return invalidResult;
        }

        WorldArmyCommandResult result = new() { Success = true };
        foreach (WorldArmyState army in armies)
        {
            if (army == null)
            {
                continue;
            }

            army.TargetSiteId = targetSiteId;
            army.Destination = destination;
            army.Intent = intent;
            army.Status = WorldArmyStatus.Moving;
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            army.SetTargetApproachDirection(approachDirection);
            ApplyNavigationPath(army, paths, destination, navigationSurfaceVersion);
            result.CommandedArmyIds.Add(army.ArmyId);
        }

        result.Events.Add(BuildCommandEvent("WorldArmySiteCommanded", result.CommandedArmyIds, destination, targetSiteId, intent));
        GameLog.Info(
            nameof(WorldArmyCommandService),
            $"WorldArmyCommandSite count={result.CommandedArmyIds.Count} target={targetSiteId} intent={intent} approachDirection={approachDirection}");
        return result;
    }

    public WorldArmyCommandResult ApplyCreatedExpeditionCommandState(
        WorldArmyState army,
        WorldArmyIntent intent,
        StrategicNavigationPath path,
        int navigationSurfaceVersion,
        Vector2 arrivalApproachOffset,
        WorldSiteAttackDirection approachDirection,
        string requiredOwnerFactionId = StrategicWorldIds.FactionPlayer)
    {
        // ExpeditionService owns army creation and garrison transfer; this boundary owns
        // the command metadata that Presentation previously patched onto the created army.
        if (!TryValidateCommandableArmy(army, requiredOwnerFactionId, out string failureReason))
        {
            return WorldArmyCommandResult.Fail(failureReason);
        }

        if (intent is not (WorldArmyIntent.MoveToPosition or WorldArmyIntent.ReinforceSite or WorldArmyIntent.AssaultSite))
        {
            return WorldArmyCommandResult.Fail($"unsupported_expedition_command_intent:{intent}");
        }

        ApplyNavigationPath(army, path, army.Destination, navigationSurfaceVersion);
        if (intent == WorldArmyIntent.MoveToPosition)
        {
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
        }
        else
        {
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            army.SetTargetApproachDirection(approachDirection);
        }

        WorldArmyCommandResult result = new() { Success = true };
        result.CommandedArmyIds.Add(army.ArmyId);
        result.Events.Add(BuildCommandEvent(
            "WorldArmyExpeditionCommandStateApplied",
            result.CommandedArmyIds,
            army.Destination,
            army.TargetSiteId,
            intent));
        GameLog.Info(
            nameof(WorldArmyCommandService),
            $"WorldArmyExpeditionCommandStateApplied army={army.ArmyId} intent={intent} target={army.TargetSiteId} approachDirection={army.TargetApproachDirection}");
        return result;
    }

    public WorldArmyCommandResult ResetUnsupportedAssault(WorldArmyState army)
    {
        if (army == null)
        {
            return WorldArmyCommandResult.Fail("missing_army");
        }

        string targetSiteId = army.TargetSiteId ?? "";
        army.Status = WorldArmyStatus.Idle;
        army.Intent = WorldArmyIntent.None;
        army.ClearNavigationPath();

        WorldArmyCommandResult result = new() { Success = true };
        result.CommandedArmyIds.Add(army.ArmyId);
        result.Events.Add(BuildCommandEvent(
            "WorldArmyUnsupportedAssaultReset",
            result.CommandedArmyIds,
            army.Destination,
            targetSiteId,
            WorldArmyIntent.None));
        GameLog.Warn(nameof(WorldArmyCommandService), $"WorldArmyUnsupportedAssaultReset army={army.ArmyId} target={targetSiteId}");
        return result;
    }

    public WorldArmyCommandResult ApplyDeferredAssaultStandbyMovement(
        WorldArmyState army,
        Vector2 destination,
        IReadOnlyDictionary<string, StrategicNavigationPath> paths,
        int navigationSurfaceVersion,
        string requiredOwnerFactionId = StrategicWorldIds.FactionPlayer)
    {
        if (!TryValidateDeferredAssaultArmy(army, requiredOwnerFactionId, out string failureReason))
        {
            return WorldArmyCommandResult.Fail(failureReason);
        }

        string targetSiteId = army.TargetSiteId ?? "";
        army.TargetSiteId = "";
        army.Destination = destination;
        army.Intent = WorldArmyIntent.MoveToPosition;
        army.Status = WorldArmyStatus.Moving;
        army.ClearArrivalApproachOffset();
        army.ClearTargetApproachDirection();
        ApplyNavigationPath(army, paths, destination, navigationSurfaceVersion);

        WorldArmyCommandResult result = new() { Success = true };
        result.CommandedArmyIds.Add(army.ArmyId);
        result.Events.Add(BuildCommandEvent(
            "WorldArmyArrivedAssaultDeferred",
            result.CommandedArmyIds,
            destination,
            targetSiteId,
            WorldArmyIntent.MoveToPosition));
        GameLog.Info(nameof(WorldArmyCommandService), $"WorldArmyArrivedAssaultDeferred army={army.ArmyId} previousTarget={targetSiteId} destination={destination}");
        return result;
    }

    public WorldArmyCommandResult ApplyResolvedSiteNavigationPoints(
        WorldArmyState army,
        Vector2? resolvedWorldPosition,
        Vector2? resolvedDestination,
        Vector2 arrivalApproachOffset,
        WorldSiteAttackDirection approachDirection)
    {
        if (army == null)
        {
            return WorldArmyCommandResult.Fail("missing_army");
        }

        bool changed = false;
        if (resolvedWorldPosition is { } worldPosition &&
            army.WorldPosition.DistanceSquaredTo(worldPosition) > 0.001f)
        {
            army.WorldPosition = worldPosition;
            changed = true;
        }

        if (resolvedDestination is { } destination &&
            army.Destination.DistanceSquaredTo(destination) > 0.001f)
        {
            army.Destination = destination;
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            changed = true;
        }

        if (army.TargetApproachDirection != approachDirection)
        {
            army.SetTargetApproachDirection(approachDirection);
            changed = true;
        }

        WorldArmyCommandResult result = new() { Success = true };
        result.CommandedArmyIds.Add(army.ArmyId);
        if (!changed)
        {
            return result;
        }

        // Re-resolved site navigation points invalidate any path cached against the old source,
        // destination, or approach metadata; movement service will rebuild through navigation authority.
        army.ClearNavigationPath();
        result.Events.Add(BuildCommandEvent(
            "WorldArmySiteNavigationPointsResolved",
            result.CommandedArmyIds,
            army.Destination,
            army.TargetSiteId,
            army.Intent));
        GameLog.Info(
            nameof(WorldArmyCommandService),
            $"WorldArmySiteNavigationPointsResolved army={army.ArmyId} source={army.SourceSiteId} target={army.TargetSiteId} position={army.WorldPosition} destination={army.Destination}");
        return result;
    }

    public WorldArmyCommandResult RemoveResolvedStrategicExpeditionCarrier(
        IDictionary<string, WorldArmyState> armies,
        string armyId,
        string expeditionId,
        string reason = "")
    {
        if (armies == null || string.IsNullOrWhiteSpace(armyId))
        {
            return WorldArmyCommandResult.Fail("missing_army");
        }

        if (!armies.TryGetValue(armyId, out WorldArmyState army) || army == null)
        {
            return new WorldArmyCommandResult { Success = true };
        }

        if (string.IsNullOrWhiteSpace(army.StrategicExpeditionId))
        {
            return WorldArmyCommandResult.Fail($"army_not_strategic_expedition_carrier:{DescribeArmyId(army)}");
        }

        if (!string.IsNullOrWhiteSpace(expeditionId) &&
            !string.Equals(army.StrategicExpeditionId, expeditionId, System.StringComparison.Ordinal))
        {
            return WorldArmyCommandResult.Fail($"army_expedition_mismatch:{DescribeArmyId(army)}");
        }

        string previousTargetSiteId = army.TargetSiteId ?? "";
        WorldArmyStatus previousStatus = army.Status;
        WorldArmyIntent previousIntent = army.Intent;
        string resolvedExpeditionId = army.StrategicExpeditionId ?? "";
        armies.Remove(armyId);

        WorldArmyCommandResult result = new() { Success = true };
        result.CommandedArmyIds.Add(armyId);
        result.Events.Add(new GameEvent
        {
            Kind = "WorldArmyStrategicExpeditionCarrierRemoved",
            SourceSystem = nameof(WorldArmyCommandService),
            TargetIds = { armyId, resolvedExpeditionId, previousTargetSiteId },
            Payload =
            {
                ["expedition"] = resolvedExpeditionId,
                ["targetSite"] = previousTargetSiteId,
                ["previousStatus"] = previousStatus.ToString(),
                ["previousIntent"] = previousIntent.ToString(),
                ["reason"] = reason ?? ""
            }
        });
        GameLog.Info(
            nameof(WorldArmyCommandService),
            $"WorldArmyStrategicExpeditionCarrierRemoved army={armyId} expedition={resolvedExpeditionId} previousStatus={previousStatus} previousIntent={previousIntent} target={previousTargetSiteId} reason={reason ?? ""}");
        return result;
    }

    private static bool TryValidateCommandableArmies(
        IReadOnlyList<WorldArmyState> armies,
        string requiredOwnerFactionId,
        out WorldArmyCommandResult result)
    {
        result = null;
        if (armies == null || armies.Count == 0)
        {
            result = WorldArmyCommandResult.Fail("no_commandable_army");
            return false;
        }

        bool hasArmy = false;
        foreach (WorldArmyState army in armies)
        {
            if (army != null)
            {
                hasArmy = true;
                if (!TryValidateCommandableArmy(army, requiredOwnerFactionId, out string failureReason))
                {
                    result = WorldArmyCommandResult.Fail(failureReason);
                    return false;
                }
            }
        }

        if (!hasArmy)
        {
            result = WorldArmyCommandResult.Fail("no_commandable_army");
            return false;
        }

        return true;
    }

    private static bool TryValidateDeferredAssaultArmy(
        WorldArmyState army,
        string requiredOwnerFactionId,
        out string failureReason)
    {
        if (!TryValidateOwnedArmy(army, requiredOwnerFactionId, out failureReason))
        {
            return false;
        }

        if (army.Status != WorldArmyStatus.Attacking || army.Intent != WorldArmyIntent.AssaultSite)
        {
            failureReason = $"army_not_deferable_assault:{DescribeArmyId(army)}:{army.Status}:{army.Intent}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(army.TargetSiteId))
        {
            failureReason = $"army_missing_assault_target:{DescribeArmyId(army)}";
            return false;
        }

        return true;
    }

    private static bool TryValidateCommandableArmy(
        WorldArmyState army,
        string requiredOwnerFactionId,
        out string failureReason)
    {
        if (!TryValidateOwnedArmy(army, requiredOwnerFactionId, out failureReason))
        {
            return false;
        }

        if (army.Status is WorldArmyStatus.Defeated or WorldArmyStatus.Garrisoned or WorldArmyStatus.Attacking)
        {
            failureReason = $"army_not_commandable:{DescribeArmyId(army)}:{army.Status}";
            return false;
        }

        return true;
    }

    private static bool TryValidateOwnedArmy(
        WorldArmyState army,
        string requiredOwnerFactionId,
        out string failureReason)
    {
        failureReason = "";
        if (army == null)
        {
            failureReason = "missing_army";
            return false;
        }

        string normalizedOwner = string.IsNullOrWhiteSpace(requiredOwnerFactionId)
            ? StrategicWorldIds.FactionPlayer
            : requiredOwnerFactionId;
        if (!string.Equals(army.OwnerFactionId, normalizedOwner, System.StringComparison.Ordinal))
        {
            failureReason = $"army_not_owned:{DescribeArmyId(army)}";
            return false;
        }

        return true;
    }

    private static void ApplyNavigationPath(
        WorldArmyState army,
        IReadOnlyDictionary<string, StrategicNavigationPath> paths,
        Vector2 destination,
        int navigationSurfaceVersion)
    {
        if (army == null)
        {
            return;
        }

        if (paths != null &&
            paths.TryGetValue(army.ArmyId, out StrategicNavigationPath path) &&
            path?.Points?.Count > 0)
        {
            army.SetNavigationPath(path.Points, destination, navigationSurfaceVersion);
            return;
        }

        army.ClearNavigationPath();
    }

    private static void ApplyNavigationPath(
        WorldArmyState army,
        StrategicNavigationPath path,
        Vector2 destination,
        int navigationSurfaceVersion)
    {
        if (army == null)
        {
            return;
        }

        if (path?.Points?.Count > 0)
        {
            army.SetNavigationPath(path.Points, destination, navigationSurfaceVersion);
            return;
        }

        army.ClearNavigationPath();
    }

    private static string DescribeArmyId(WorldArmyState army)
    {
        return string.IsNullOrWhiteSpace(army?.ArmyId) ? "unknown" : army.ArmyId;
    }

    private static GameEvent BuildCommandEvent(
        string kind,
        IReadOnlyList<string> armyIds,
        Vector2 destination,
        string targetSiteId,
        WorldArmyIntent intent)
    {
        GameEvent gameEvent = new()
        {
            Kind = kind,
            SourceSystem = nameof(WorldArmyCommandService),
            Payload =
            {
                ["destinationX"] = destination.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["destinationY"] = destination.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["targetSite"] = targetSiteId ?? "",
                ["intent"] = intent.ToString()
            }
        };

        if (armyIds != null)
        {
            gameEvent.TargetIds.AddRange(armyIds);
        }

        return gameEvent;
    }
}
