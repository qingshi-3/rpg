using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldArmyMovementService
{
    private const float ArrivalReachDistance = 1.0f;
    private const float FieldInterceptThreshold = 24.0f;
    private const float WaypointReachDistance = 4.0f;
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();

    public WorldArmyMovementResult AdvanceArmies(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        double delta,
        IStrategicNavigationContext navigationContext = null,
        double navigationRetryDeltaSeconds = -1.0)
    {
        WorldArmyMovementResult result = new();
        if (state == null || delta <= 0.0)
        {
            return result;
        }

        double retryDeltaSeconds = navigationRetryDeltaSeconds >= 0.0
            ? navigationRetryDeltaSeconds
            : delta;
        foreach (WorldArmyState army in state.ArmyStates.Values)
        {
            if (army.Status != WorldArmyStatus.Moving)
            {
                army.ClearNavigationPath();
                army.ClearArrivalApproachOffset();
                continue;
            }

            Vector2 current = army.WorldPosition;
            Vector2 destination = army.Destination;
            float waypointReachDistance = Mathf.Max(WaypointReachDistance, army.Radius * 0.25f);
            float step = Mathf.Max(0.0f, army.MoveSpeed) * (float)delta;
            if (army.IsCompletingArrivalApproach)
            {
                AdvanceArrivalApproach(state, definition, army, result, step);
                continue;
            }

            if (!EnsureNavigationPath(state, army, current, destination, navigationContext, result, retryDeltaSeconds))
            {
                continue;
            }

            if (current.DistanceTo(destination) <= ArrivalReachDistance)
            {
                if (TryBeginArrivalApproach(army))
                {
                    AdvanceArrivalApproach(state, definition, army, result, step);
                }
                else
                {
                    MarkArrived(state, definition, army, result);
                }

                continue;
            }

            Vector2 nextPoint = GetNextPathPoint(army, current, waypointReachDistance);
            float distance = current.DistanceTo(nextPoint);
            if (distance <= 0.001f)
            {
                continue;
            }

            if (step >= distance)
            {
                army.WorldPosition = nextPoint;
                AdvancePathIndexIfReached(army, nextPoint, waypointReachDistance);
                if (army.WorldPosition.DistanceTo(destination) <= ArrivalReachDistance)
                {
                    if (TryBeginArrivalApproach(army))
                    {
                        AdvanceArrivalApproach(state, definition, army, result, Mathf.Max(0.0f, step - distance));
                    }
                    else
                    {
                        MarkArrived(state, definition, army, result);
                    }
                }

                continue;
            }

            army.WorldPosition = current + (nextPoint - current).Normalized() * step;
        }

        if (result.BattleReadyArmyIds.Count == 0)
        {
            DetectFieldIntercepts(state, result);
        }

        return result;
    }

    private void AdvanceArrivalApproach(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldArmyState army,
        WorldArmyMovementResult result,
        float step)
    {
        Vector2 finalDestination = ResolveFinalArrivalPosition(army);
        if (MoveArmyToward(army, finalDestination, step, ArrivalReachDistance))
        {
            MarkArrived(state, definition, army, result);
        }
    }

    private static bool MoveArmyToward(WorldArmyState army, Vector2 target, float step, float reachDistance)
    {
        Vector2 current = army.WorldPosition;
        float distance = current.DistanceTo(target);
        if (distance <= reachDistance)
        {
            return true;
        }

        if (distance <= 0.001f || step <= 0.0f)
        {
            return false;
        }

        if (step >= distance)
        {
            army.WorldPosition = target;
            return true;
        }

        army.WorldPosition = current + (target - current).Normalized() * step;
        return army.WorldPosition.DistanceTo(target) <= reachDistance;
    }

    private static bool TryBeginArrivalApproach(WorldArmyState army)
    {
        if (army?.HasArrivalApproachOffset != true)
        {
            return false;
        }

        army.BeginArrivalApproach();
        army.ClearNavigationPath();
        GameLog.Info(
            nameof(WorldArmyMovementService),
            $"WorldArmyArrivalApproachStarted army={army.ArmyId} navigation={army.Destination} final={ResolveFinalArrivalPosition(army)}");
        return army.IsCompletingArrivalApproach;
    }

    private static bool EnsureNavigationPath(
        StrategicWorldState state,
        WorldArmyState army,
        Vector2 current,
        Vector2 destination,
        IStrategicNavigationContext navigationContext,
        WorldArmyMovementResult result,
        double retryDeltaSeconds)
    {
        if (navigationContext == null)
        {
            BlockNavigationPath(state, army, result, "strategic_navigation_context_missing");
            return false;
        }

        if (army.HasValidNavigationPath(destination, navigationContext.Version))
        {
            return true;
        }

        if (!navigationContext.TryBuildPath(current, destination, out StrategicNavigationPath path, out string failureReason))
        {
            BlockNavigationPath(state, army, result, failureReason);
            return false;
        }

        army.SetNavigationPath(path.Points, destination, navigationContext.Version);
        GameLog.Info(
            nameof(WorldArmyMovementService),
            $"WorldArmyPathBuilt army={army.ArmyId} provider={path.ProviderId} points={army.NavigationPathPoints.Count} version={navigationContext.Version} destination={destination}");
        return true;
    }

    private static void BlockNavigationPath(
        StrategicWorldState state,
        WorldArmyState army,
        WorldArmyMovementResult result,
        string failureReason)
    {
        int transientAttempts = army.TransientNavigationPathFailureCount;
        float transientSeconds = army.TransientNavigationPathFailureSeconds;
        // NavigationBlocked is the authoritative failure state for broken strategic path contracts.
        // The world layer pauses and exposes the problem instead of using presentation fallbacks.
        army.ClearNavigationPath();
        army.ClearArrivalApproachOffset();
        army.ClearTargetApproachDirection();
        army.Status = WorldArmyStatus.NavigationBlocked;
        result.NavigationBlockedArmyIds.Add(army.ArmyId);
        result.Messages.Add($"\u90e8\u961f {army.ArmyId} \u6218\u7565\u5bfc\u822a\u5931\u8d25\uff0c\u4e16\u754c\u63a8\u8fdb\u5df2\u6682\u505c\u3002\u539f\u56e0\uff1a{failureReason}");
        result.Events.Add(new GameEvent
        {
            Kind = "WorldArmyNavigationBlocked",
            Tick = state.WorldTick,
            TargetIds = { army.ArmyId, army.TargetSiteId },
            Payload =
            {
                ["reason"] = failureReason,
                ["transient_attempts"] = transientAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["transient_seconds"] = transientSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
            }
        });
        GameLog.Error(nameof(WorldArmyMovementService), $"WorldArmyNavigationBlocked army={army.ArmyId} reason={failureReason} transientAttempts={transientAttempts} transientSeconds={transientSeconds:0.000}");
    }

    private static Vector2 GetNextPathPoint(WorldArmyState army, Vector2 current, float waypointReachDistance)
    {
        if (!army.HasNavigationPath || army.NavigationPathPoints.Count == 0)
        {
            return army.Destination;
        }

        while (army.NavigationPathPointIndex < army.NavigationPathPoints.Count - 1 &&
               current.DistanceTo(army.NavigationPathPoints[army.NavigationPathPointIndex]) <= waypointReachDistance)
        {
            army.NavigationPathPointIndex++;
        }

        if (army.NavigationPathPointIndex >= army.NavigationPathPoints.Count - 1 &&
            current.DistanceTo(army.NavigationPathPoints[army.NavigationPathPointIndex]) <= waypointReachDistance &&
            army.NavigationPathPoints[army.NavigationPathPointIndex].DistanceSquaredTo(army.Destination) > 0.001f)
        {
            return army.Destination;
        }

        return army.NavigationPathPointIndex < army.NavigationPathPoints.Count
            ? army.NavigationPathPoints[army.NavigationPathPointIndex]
            : army.Destination;
    }

    private static void AdvancePathIndexIfReached(WorldArmyState army, Vector2 current, float waypointReachDistance)
    {
        if (!army.HasNavigationPath || army.NavigationPathPoints.Count == 0)
        {
            return;
        }

        if (army.NavigationPathPointIndex < army.NavigationPathPoints.Count - 1 &&
            current.DistanceTo(army.NavigationPathPoints[army.NavigationPathPointIndex]) <= waypointReachDistance)
        {
            army.NavigationPathPointIndex++;
        }
    }

    private void MarkArrived(StrategicWorldState state, StrategicWorldDefinition definition, WorldArmyState army, WorldArmyMovementResult result)
    {
        army.ClearNavigationPath();
        army.ClearArrivalApproachOffset();
        bool isStrategicExpeditionCarrier = IsStrategicExpeditionCarrier(army);
        if (army.Intent == WorldArmyIntent.ReinforceSite &&
            !isStrategicExpeditionCarrier &&
            !_deploymentService.CanAcceptArmyGarrison(state, definition, army, out string failureReason))
        {
            army.Status = WorldArmyStatus.Idle;
            result.GarrisonRejectedArmyIds.Add(army.ArmyId);
            result.Messages.Add("\u9a7b\u519b\u533a\u5df2\u6ee1\uff0c\u65e0\u6cd5\u8fdb\u9a7b\u3002");
            result.Events.Add(new GameEvent
            {
                Kind = "WorldArmyGarrisonRejected",
                Tick = state.WorldTick,
                TargetIds = { army.ArmyId, army.TargetSiteId },
                Payload =
                {
                    ["reason"] = failureReason,
                    ["targetSite"] = army.TargetSiteId
                }
            });
            GameLog.Info(nameof(WorldArmyMovementService), $"WorldArmyGarrisonRejected army={army.ArmyId} target={army.TargetSiteId} reason={failureReason}");
            return;
        }

        army.Status = army.Intent switch
        {
            WorldArmyIntent.AssaultSite => WorldArmyStatus.Attacking,
            WorldArmyIntent.ReinforceSite => WorldArmyStatus.Garrisoned,
            _ => WorldArmyStatus.Idle
        };
        result.ArrivedArmyIds.Add(army.ArmyId);
        result.Events.Add(new GameEvent
        {
            Kind = "WorldArmyArrived",
            Tick = state.WorldTick,
            TargetIds = { army.ArmyId, army.TargetSiteId },
            Payload =
            {
                ["owner"] = army.OwnerFactionId,
                ["intent"] = army.Intent.ToString(),
                ["targetSite"] = army.TargetSiteId
            }
        });

        if (army.Intent == WorldArmyIntent.ReinforceSite && !isStrategicExpeditionCarrier)
        {
            TransferArrivedGarrison(state, definition, army, result);
        }
        else if (army.Intent == WorldArmyIntent.AssaultSite &&
                 army.OwnerFactionId == state.PlayerFactionId)
        {
            result.BattleReadyArmyIds.Add(army.ArmyId);
            result.Messages.Add("\u73a9\u5bb6\u8fdb\u653b\u90e8\u961f\u5df2\u62b5\u8fbe\uff0c\u51c6\u5907\u8fdb\u5165\u653b\u5360\u6218\u3002");
        }

        GameLog.Info(
            nameof(WorldArmyMovementService),
            $"WorldArmyArrived army={army.ArmyId} owner={army.OwnerFactionId} target={army.TargetSiteId} intent={army.Intent} status={army.Status} units={FormatUnits(army.GarrisonUnits)} battleReady={result.BattleReadyArmyIds.Contains(army.ArmyId)}");
    }

    private static bool IsStrategicExpeditionCarrier(WorldArmyState army)
    {
        return !string.IsNullOrWhiteSpace(army?.StrategicExpeditionId);
    }

    private static string FormatUnits(IEnumerable<GarrisonState> units)
    {
        return units == null
            ? "none"
            : string.Join(",", units.Where(unit => unit != null).Select(unit => $"{unit.UnitTypeId}:{unit.Count}"));
    }

    private void TransferArrivedGarrison(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldArmyState army,
        WorldArmyMovementResult result)
    {
        if (!state.SiteStates.TryGetValue(army.TargetSiteId, out WorldSiteState site))
        {
            result.Messages.Add($"\u73a9\u5bb6\u90e8\u961f\u5df2\u62b5\u8fbe\u76ee\u6807\uff0c\u4f46\u627e\u4e0d\u5230\u9a7b\u5b88\u573a\u57df\uff1a{army.TargetSiteId}\u3002");
            GameLog.Warn(nameof(WorldArmyMovementService), $"WorldArmyGarrisonTransferFailed army={army.ArmyId} target={army.TargetSiteId}");
            return;
        }

        int transferred = 0;
        foreach (GarrisonState unit in army.GarrisonUnits)
        {
            if (unit.Count <= 0 || string.IsNullOrWhiteSpace(unit.UnitTypeId))
            {
                continue;
            }

            _garrisonMutations.Add(site, unit.UnitTypeId, unit.Count);
            transferred += unit.Count;
            result.Events.Add(new GameEvent
            {
                Kind = "GarrisonChanged",
                Tick = state.WorldTick,
                TargetIds = { site.SiteId, army.ArmyId },
                Payload =
                {
                    ["unit"] = unit.UnitTypeId,
                    ["amount"] = unit.Count.ToString(),
                    ["reason"] = "army_arrived"
                }
            });
        }

        army.GarrisonUnits.Clear();
        if (transferred > 0)
        {
            WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(site.SiteId);
            _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);
            result.Messages.Add($"\u73a9\u5bb6\u90e8\u961f\u5df2\u62b5\u8fbe {site.SiteId}\uff0c{transferred} \u961f\u5355\u4f4d\u52a0\u5165\u9a7b\u519b\u3002");
            GameLog.Info(nameof(WorldArmyMovementService), $"WorldArmyGarrisoned army={army.ArmyId} target={site.SiteId} units={transferred}");
        }
    }

    private static void DetectFieldIntercepts(StrategicWorldState state, WorldArmyMovementResult result)
    {
        foreach (WorldArmyState playerArmy in state.ArmyStates.Values)
        {
            if (!CanFieldIntercept(playerArmy) || playerArmy.OwnerFactionId != state.PlayerFactionId)
            {
                continue;
            }

            foreach (WorldArmyState enemyArmy in state.ArmyStates.Values)
            {
                if (!CanFieldIntercept(enemyArmy) || enemyArmy.OwnerFactionId == state.PlayerFactionId)
                {
                    continue;
                }

                float triggerDistance = playerArmy.Radius + enemyArmy.Radius + FieldInterceptThreshold;
                if (playerArmy.WorldPosition.DistanceTo(enemyArmy.WorldPosition) > triggerDistance)
                {
                    continue;
                }

                playerArmy.Status = WorldArmyStatus.Attacking;
                enemyArmy.Status = WorldArmyStatus.Attacking;
                playerArmy.ClearNavigationPath();
                enemyArmy.ClearNavigationPath();
                playerArmy.ClearArrivalApproachOffset();
                playerArmy.ClearTargetApproachDirection();
                enemyArmy.ClearArrivalApproachOffset();
                enemyArmy.ClearTargetApproachDirection();
                result.FieldIntercepts.Add(new WorldArmyInterceptResult
                {
                    PlayerArmyId = playerArmy.ArmyId,
                    EnemyArmyId = enemyArmy.ArmyId
                });
                result.Messages.Add("\u73a9\u5bb6\u90e8\u961f\u4e0e\u654c\u519b\u63a5\u89e6\uff0c\u51c6\u5907\u8fdb\u5165\u91ce\u5916\u906d\u9047\u6218\u3002");
                result.Events.Add(new GameEvent
                {
                    Kind = "WorldArmyFieldInterceptTriggered",
                    Tick = state.WorldTick,
                    TargetIds = { playerArmy.ArmyId, enemyArmy.ArmyId },
                    Payload =
                    {
                        ["distance"] = playerArmy.WorldPosition.DistanceTo(enemyArmy.WorldPosition).ToString("0.0"),
                        ["threshold"] = triggerDistance.ToString("0.0")
                    }
                });
                GameLog.Info(nameof(WorldArmyMovementService), $"WorldArmyFieldInterceptTriggered player={playerArmy.ArmyId} enemy={enemyArmy.ArmyId}");
                return;
            }
        }
    }

    private static bool CanFieldIntercept(WorldArmyState army)
    {
        return army.Status == WorldArmyStatus.Moving &&
               army.Intent is WorldArmyIntent.AssaultSite or WorldArmyIntent.ReinforceSite or WorldArmyIntent.Intercept or WorldArmyIntent.MoveToPosition;
    }

    private static Vector2 ResolveFinalArrivalPosition(WorldArmyState army)
    {
        if (army?.HasArrivalApproachOffset == true)
        {
            return army.Destination + army.ArrivalApproachOffset;
        }

        return army?.Destination ?? default;
    }
}
