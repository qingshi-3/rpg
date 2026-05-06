using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldArmyMovementService
{
    private const float FieldInterceptThreshold = 24.0f;
    private const float WaypointReachDistance = 4.0f;
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public WorldArmyMovementResult AdvanceArmies(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        double delta,
        StrategicNavigationContext navigationContext = null)
    {
        WorldArmyMovementResult result = new();
        if (state == null || delta <= 0.0)
        {
            return result;
        }

        foreach (WorldArmyState army in state.ArmyStates.Values)
        {
            if (army.Status != WorldArmyStatus.Moving)
            {
                army.ClearNavigationPath();
                continue;
            }

            Vector2 current = army.WorldPosition;
            Vector2 destination = army.Destination;
            float arriveDistance = Mathf.Max(1.0f, army.Radius);
            if (!EnsureNavigationPath(state, army, current, destination, navigationContext, result))
            {
                continue;
            }

            if (current.DistanceTo(destination) <= arriveDistance)
            {
                army.WorldPosition = destination;
                MarkArrived(state, definition, army, result);
                continue;
            }

            Vector2 nextPoint = GetNextPathPoint(army, current, arriveDistance);
            float distance = current.DistanceTo(nextPoint);
            if (distance <= 0.001f)
            {
                continue;
            }

            float step = Mathf.Max(0.0f, army.MoveSpeed) * (float)delta;
            if (step >= distance)
            {
                army.WorldPosition = nextPoint;
                AdvancePathIndexIfReached(army, nextPoint, arriveDistance);
                if (nextPoint.DistanceTo(destination) <= arriveDistance)
                {
                    army.WorldPosition = destination;
                    MarkArrived(state, definition, army, result);
                }

                continue;
            }

            army.WorldPosition = current + (nextPoint - current).Normalized() * step;
        }

        if (result.BattleReadyArmyIds.Count == 0 && result.AttackingThreatIds.Count == 0)
        {
            DetectFieldIntercepts(state, result);
        }

        return result;
    }

    private static bool EnsureNavigationPath(
        StrategicWorldState state,
        WorldArmyState army,
        Vector2 current,
        Vector2 destination,
        StrategicNavigationContext navigationContext,
        WorldArmyMovementResult result)
    {
        if (navigationContext == null)
        {
            FailNavigationPath(state, army, result, "strategic_navigation_context_missing");
            return false;
        }

        if (army.HasValidNavigationPath(destination, navigationContext.Version))
        {
            return true;
        }

        if (!navigationContext.TryBuildPath(current, destination, out StrategicNavigationPath path, out string failureReason))
        {
            FailNavigationPath(state, army, result, failureReason);
            return false;
        }

        army.SetNavigationPath(path.Points, destination, navigationContext.Version);
        GameLog.Info(
            nameof(WorldArmyMovementService),
            $"WorldArmyPathBuilt army={army.ArmyId} provider={path.ProviderId} points={army.NavigationPathPoints.Count} version={navigationContext.Version} destination={destination}");
        return true;
    }

    private static void FailNavigationPath(
        StrategicWorldState state,
        WorldArmyState army,
        WorldArmyMovementResult result,
        string failureReason)
    {
        army.ClearNavigationPath();
        army.Status = WorldArmyStatus.Idle;
        result.PathFailedArmyIds.Add(army.ArmyId);
        result.Messages.Add($"部队 {army.ArmyId} 无法找到行军路径。");
        result.Events.Add(new GameEvent
        {
            Kind = "WorldArmyPathFailed",
            Tick = state.WorldTick,
            TargetIds = { army.ArmyId, army.TargetSiteId },
            Payload = { ["reason"] = failureReason }
        });
        GameLog.Error(nameof(WorldArmyMovementService), $"WorldArmyPathFailed army={army.ArmyId} reason={failureReason}");
    }

    private static Vector2 GetNextPathPoint(WorldArmyState army, Vector2 current, float arriveDistance)
    {
        if (!army.HasNavigationPath || army.NavigationPathPoints.Count == 0)
        {
            return army.Destination;
        }

        float waypointReachDistance = Mathf.Max(WaypointReachDistance, arriveDistance * 0.25f);
        while (army.NavigationPathPointIndex < army.NavigationPathPoints.Count - 1 &&
               current.DistanceTo(army.NavigationPathPoints[army.NavigationPathPointIndex]) <= waypointReachDistance)
        {
            army.NavigationPathPointIndex++;
        }

        return army.NavigationPathPointIndex < army.NavigationPathPoints.Count
            ? army.NavigationPathPoints[army.NavigationPathPointIndex]
            : army.Destination;
    }

    private static void AdvancePathIndexIfReached(WorldArmyState army, Vector2 current, float arriveDistance)
    {
        if (!army.HasNavigationPath || army.NavigationPathPoints.Count == 0)
        {
            return;
        }

        float waypointReachDistance = Mathf.Max(WaypointReachDistance, arriveDistance * 0.25f);
        if (army.NavigationPathPointIndex < army.NavigationPathPoints.Count - 1 &&
            current.DistanceTo(army.NavigationPathPoints[army.NavigationPathPointIndex]) <= waypointReachDistance)
        {
            army.NavigationPathPointIndex++;
        }
    }

    private void MarkArrived(StrategicWorldState state, StrategicWorldDefinition definition, WorldArmyState army, WorldArmyMovementResult result)
    {
        army.ClearNavigationPath();
        if (army.Intent == WorldArmyIntent.ReinforceSite &&
            !_deploymentService.CanAcceptArmyGarrison(state, definition, army, out string failureReason))
        {
            army.Status = WorldArmyStatus.Idle;
            result.GarrisonRejectedArmyIds.Add(army.ArmyId);
            result.Messages.Add("驻军区已满，无法进驻。");
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
            WorldArmyIntent.Raid or WorldArmyIntent.AssaultSite => WorldArmyStatus.Attacking,
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

        if (army.Intent == WorldArmyIntent.ReinforceSite)
        {
            TransferArrivedGarrison(state, definition, army, result);
        }
        else if (army.Intent == WorldArmyIntent.AssaultSite &&
                 army.OwnerFactionId == state.PlayerFactionId)
        {
            result.BattleReadyArmyIds.Add(army.ArmyId);
            result.Messages.Add("玩家进攻部队已抵达，准备进入攻占战。");
        }

        if (!string.IsNullOrWhiteSpace(army.RelatedThreatId) &&
            state.ThreatPlans.TryGetValue(army.RelatedThreatId, out EnemyThreatPlan threat) &&
            threat.Stage != ThreatStage.Resolved)
        {
            threat.Stage = ThreatStage.Attacking;
            threat.CountdownTicks = 0;
            result.AttackingThreatIds.Add(threat.Id);
            result.Messages.Add("敌方部队已抵达，必须处理。");
            result.Events.Add(new GameEvent
            {
                Kind = "ThreatStageChanged",
                Tick = state.WorldTick,
                TargetIds = { threat.Id, army.ArmyId },
                Payload = { ["stage"] = nameof(ThreatStage.Attacking), ["reason"] = "army_arrived" }
            });
        }

        GameLog.Info(nameof(WorldArmyMovementService), $"WorldArmyArrived army={army.ArmyId} target={army.TargetSiteId} intent={army.Intent}");
    }

    private void TransferArrivedGarrison(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldArmyState army,
        WorldArmyMovementResult result)
    {
        if (!state.SiteStates.TryGetValue(army.TargetSiteId, out WorldSiteState site))
        {
            result.Messages.Add($"玩家部队已抵达目标，但找不到驻守场域：{army.TargetSiteId}。");
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

            AddGarrison(site, unit.UnitTypeId, unit.Count);
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
            result.Messages.Add($"玩家部队已抵达 {site.SiteId}，{transferred} 队单位加入驻军。");
            GameLog.Info(nameof(WorldArmyMovementService), $"WorldArmyGarrisoned army={army.ArmyId} target={site.SiteId} units={transferred}");
        }
    }

    private static void AddGarrison(WorldSiteState site, string unitTypeId, int count)
    {
        GarrisonState garrison = site.Garrison.Find(item => item.UnitTypeId == unitTypeId);
        if (garrison == null)
        {
            site.Garrison.Add(new GarrisonState { UnitTypeId = unitTypeId, Count = count });
            return;
        }

        garrison.Count += count;
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
                result.FieldIntercepts.Add(new WorldArmyInterceptResult
                {
                    PlayerArmyId = playerArmy.ArmyId,
                    EnemyArmyId = enemyArmy.ArmyId
                });
                result.Messages.Add("玩家部队与敌军接触，准备进入野外遭遇战。");
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
               army.Intent is WorldArmyIntent.Raid or WorldArmyIntent.AssaultSite or WorldArmyIntent.ReinforceSite or WorldArmyIntent.Intercept or WorldArmyIntent.MoveToPosition;
    }
}
