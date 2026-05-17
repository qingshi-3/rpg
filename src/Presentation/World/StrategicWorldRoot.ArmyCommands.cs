using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private bool TryCommandSelectedArmiesToSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        WorldArmyState[] selectedArmies = GetSelectedCommandableArmies();
        if (selectedArmies.Length == 0)
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition siteDefinition = queries.GetSite(siteId);
        if (siteDefinition == null)
        {
            return false;
        }

        if (site.OwnerFactionId == State.PlayerFactionId)
        {
            int incomingSlots = selectedArmies.Sum(army => _deploymentService.GetArmyGarrisonSlotUsage(army));
            if (!_deploymentService.CanAcceptGarrison(site, siteDefinition, incomingSlots, out string failureReason))
            {
                StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
                GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandReinforceRejected site={siteId} reason={failureReason} incoming={incomingSlots}");
                RefreshAll();
                return true;
            }

            Vector2 approachFrom = GetAverageArmyPosition(selectedArmies);
            if (!TryResolveSiteArmyNavigationPoint(siteDefinition.Id, approachFrom, out Vector2 siteArmyPosition, out Vector2 siteArrivalOffset, out WorldSiteAttackDirection siteApproachDirection, out string siteNavigationFailureReason))
            {
                ReportWorldArmyCommandNavigationRejected("reinforce_site", siteNavigationFailureReason);
                return true;
            }

            if (!TryBuildCommandPaths(
                    selectedArmies,
                    siteArmyPosition,
                    out Dictionary<string, StrategicNavigationPath> commandPaths,
                    out bool navigationDeferred,
                    out string navigationFailureReason))
            {
                ReportWorldArmyCommandNavigationRejected("reinforce_site", navigationFailureReason);
                return true;
            }

            CommandArmiesToSite(selectedArmies, siteDefinition, siteArmyPosition, siteArrivalOffset, siteApproachDirection, WorldArmyIntent.ReinforceSite, commandPaths);
            StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队进驻 {siteDefinition.DisplayName}。";
            RefreshAll();
            return true;
        }

        if (!CanBuildAssaultBattleForSite(siteDefinition.Id))
        {
            StrategicWorldRuntime.LastNotice = BuildUnsupportedAssaultNotice(siteDefinition);
            GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandAssaultRejected site={siteDefinition.Id} reason=missing_assault_battle_config");
            RefreshAll();
            return true;
        }

        Vector2 assaultApproachFrom = GetAverageArmyPosition(selectedArmies);
        if (!TryResolveSiteArmyNavigationPoint(siteDefinition.Id, assaultApproachFrom, out Vector2 assaultSiteArmyPosition, out Vector2 assaultArrivalOffset, out WorldSiteAttackDirection assaultApproachDirection, out string assaultSiteNavigationFailureReason))
        {
            ReportWorldArmyCommandNavigationRejected("assault_site", assaultSiteNavigationFailureReason);
            return true;
        }

        if (!TryBuildCommandPaths(
                selectedArmies,
                assaultSiteArmyPosition,
                out Dictionary<string, StrategicNavigationPath> commandPathsToSite,
                out bool assaultNavigationDeferred,
                out string navigationFailureReasonToSite))
        {
            ReportWorldArmyCommandNavigationRejected("assault_site", navigationFailureReasonToSite);
            return true;
        }

        CommandArmiesToSite(selectedArmies, siteDefinition, assaultSiteArmyPosition, assaultArrivalOffset, assaultApproachDirection, WorldArmyIntent.AssaultSite, commandPathsToSite);
        StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队进攻 {siteDefinition.DisplayName}。";
        RefreshAll();
        return true;
    }

    private void CommandArmiesToSite(
        WorldArmyState[] armies,
        WorldSiteDefinition siteDefinition,
        Vector2 siteArmyPosition,
        Vector2 arrivalApproachOffset,
        WorldSiteAttackDirection approachDirection,
        WorldArmyIntent intent,
        IReadOnlyDictionary<string, StrategicNavigationPath> commandPaths)
    {
        foreach (WorldArmyState army in armies)
        {
            army.TargetSiteId = siteDefinition.Id;
            army.Destination = siteArmyPosition;
            army.Intent = intent;
            army.Status = WorldArmyStatus.Moving;
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            army.SetTargetApproachDirection(approachDirection);
            ApplyCommandNavigationPath(army, commandPaths, siteArmyPosition);
        }

        GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandSite count={armies.Length} target={siteDefinition.Id} intent={intent} approachDirection={approachDirection}");
    }

    private bool TryBuildCommandPaths(
        IReadOnlyList<WorldArmyState> armies,
        Vector2 destination,
        out Dictionary<string, StrategicNavigationPath> commandPaths,
        out bool navigationDeferred,
        out string failureReason)
    {
        commandPaths = new Dictionary<string, StrategicNavigationPath>();
        navigationDeferred = false;
        if (!StrategicCommandNavigationService.TryBuildOrDeferPaths(
                armies,
                destination,
                _strategicNavigationContext,
                out StrategicCommandNavigationResult result,
                out failureReason))
        {
            return false;
        }

        commandPaths = result.CommandPaths;
        navigationDeferred = result.HasDeferredPaths;
        return true;
    }

    private void ApplyCommandNavigationPath(
        WorldArmyState army,
        IReadOnlyDictionary<string, StrategicNavigationPath> commandPaths,
        Vector2 destination)
    {
        if (army == null)
        {
            return;
        }

        if (commandPaths != null &&
            commandPaths.TryGetValue(army.ArmyId, out StrategicNavigationPath path) &&
            path?.Points?.Count > 0)
        {
            army.SetNavigationPath(path.Points, destination, _strategicNavigationContext.Version);
            return;
        }

        army.ClearNavigationPath();
    }

    private static Vector2 GetAverageArmyPosition(IReadOnlyList<WorldArmyState> armies)
    {
        if (armies == null || armies.Count == 0)
        {
            return Vector2.Zero;
        }

        Vector2 sum = Vector2.Zero;
        int count = 0;
        foreach (WorldArmyState army in armies)
        {
            if (army == null)
            {
                continue;
            }

            sum += army.WorldPosition;
            count++;
        }

        return count == 0 ? Vector2.Zero : sum / count;
    }

    private void ReportWorldArmyCommandNavigationRejected(string commandKind, string failureReason)
    {
        StrategicWorldRuntime.LastNotice = failureReason?.Contains("start_", System.StringComparison.Ordinal) == true
            ? "部队当前位置不在可行军区域，无法行军。"
            : "目标地点不在可行军区域，无法行军。";
        GameLog.Warn(nameof(StrategicWorldRoot), $"WorldArmyCommandNavigationRejected kind={commandKind} reason={failureReason}");
        RefreshAll();
    }

    private bool TryResolveSiteArmyNavigationPoint(string siteId, out Vector2 mapPosition, out string failureReason)
    {
        return TryResolveSiteArmyNavigationPoint(siteId, null, out mapPosition, out _, out _, out failureReason);
    }

    private bool TryResolveSiteArmyNavigationPoint(
        string siteId,
        Vector2? approachFrom,
        out Vector2 mapPosition,
        out Vector2 arrivalApproachOffset,
        out WorldSiteAttackDirection approachDirection,
        out string failureReason)
    {
        mapPosition = default;
        arrivalApproachOffset = default;
        approachDirection = WorldSiteAttackDirection.Any;
        failureReason = "";
        if (Definition == null || _worldMapRoot == null)
        {
            failureReason = "strategic_world_not_ready";
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition siteDefinition = queries.GetSite(siteId);
        if (siteDefinition == null)
        {
            failureReason = $"missing_site_definition site={siteId}";
            return false;
        }

        if (_armySpawnPointRoot?.GetNodeOrNull<Node2D>(siteId) is { } spawnPoint)
        {
            mapPosition = _worldMapRoot.ToLocal(spawnPoint.GlobalPosition);
            if (_strategicNavigationContext.IsPointNavigable(mapPosition, out failureReason))
            {
                return true;
            }

            failureReason = $"site_army_spawn_point_not_navigable site={siteId} {failureReason}";
            return false;
        }

        if (approachFrom is { } sourcePosition &&
            TryResolveSiteFootprintApproachPoint(siteDefinition, sourcePosition, out mapPosition, out arrivalApproachOffset, out approachDirection, out _))
        {
            ReportSiteNavigationPointResolved(siteId, GetSiteMapPosition(siteDefinition), mapPosition);
            return true;
        }

        Vector2 siteCenter = GetSiteMapPosition(siteDefinition);
        if (!_strategicNavigationContext.TryGetNearestNavigablePoint(
                siteCenter,
                SiteNavigationPointSearchCellRadius,
                out mapPosition,
                out failureReason))
        {
            failureReason = $"site_navigation_point_missing site={siteId} {failureReason}";
            return false;
        }

        if (siteCenter.DistanceSquaredTo(mapPosition) > 0.001f)
        {
            ReportSiteNavigationPointResolved(siteId, siteCenter, mapPosition);
        }

        return true;
    }

    private bool TryResolveSiteExitArmyNavigationPoint(
        string siteId,
        Vector2 towardPosition,
        out Vector2 mapPosition,
        out string failureReason)
    {
        return TryResolveSiteArmyNavigationPoint(siteId, towardPosition, out mapPosition, out _, out _, out failureReason);
    }

    private bool TryResolveSiteFootprintApproachPoint(
        WorldSiteDefinition siteDefinition,
        Vector2 approachFrom,
        out Vector2 mapPosition,
        out Vector2 arrivalApproachOffset,
        out WorldSiteAttackDirection approachDirection,
        out string failureReason)
    {
        mapPosition = default;
        arrivalApproachOffset = default;
        approachDirection = WorldSiteAttackDirection.Any;
        failureReason = "";
        if (siteDefinition == null ||
            _siteVisualLayer == null ||
            !_siteVisualFootprints.TryGetValue(siteDefinition.Id, out SiteVisualFootprint footprint))
        {
            failureReason = "site_visual_footprint_missing";
            return false;
        }

        Vector2 siteCenter = GetSiteMapPosition(siteDefinition);
        Vector2 direction = siteCenter - approachFrom;
        if (!IsFinite(approachFrom) || direction.LengthSquared() <= 0.001f)
        {
            failureReason = "site_approach_direction_invalid";
            return false;
        }

        direction = direction.Normalized();
        if (!TryFindSiteFootprintEdgePoint(footprint, approachFrom, siteCenter, out Vector2 edgePoint))
        {
            failureReason = "site_footprint_edge_missing";
            return false;
        }

        approachDirection = ResolveFootprintApproachDirection(footprint, edgePoint);

        Vector2 searchPoint = edgePoint - direction * SiteApproachEdgeNudge;
        if (!_strategicNavigationContext.TryGetNearestReachableNavigablePoint(
                approachFrom,
                searchPoint,
                SiteNavigationPointSearchCellRadius,
                out mapPosition,
                out _,
                out failureReason))
        {
            failureReason = $"site_approach_navigation_missing site={siteDefinition.Id} {failureReason}";
            return false;
        }

        arrivalApproachOffset = direction * SiteApproachVisualOffset;
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteApproachNavigationPointResolved site={siteDefinition.Id} from={approachFrom} edge={edgePoint} navigation={mapPosition} arrivalOffset={arrivalApproachOffset} approachDirection={approachDirection}");
        return true;
    }

    private bool TryFindSiteFootprintEdgePoint(
        SiteVisualFootprint footprint,
        Vector2 approachFrom,
        Vector2 siteCenter,
        out Vector2 edgePoint)
    {
        edgePoint = default;
        if (footprint == null || _siteVisualLayer == null)
        {
            return false;
        }

        bool hasIntersection = false;
        float bestSegmentRatio = float.PositiveInfinity;
        foreach (Vector2I cell in footprint.Cells)
        {
            Vector2[] polygon = BuildTileCellMapPolygon(_siteVisualLayer, cell);
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 edgeStart = polygon[index];
                Vector2 edgeEnd = polygon[(index + 1) % polygon.Length];
                if (!TryIntersectSegments(
                        approachFrom,
                        siteCenter,
                        edgeStart,
                        edgeEnd,
                        out float segmentRatio,
                        out Vector2 intersection) ||
                    segmentRatio >= bestSegmentRatio)
                {
                    continue;
                }

                bestSegmentRatio = segmentRatio;
                edgePoint = intersection;
                hasIntersection = true;
            }
        }

        return hasIntersection;
    }

    private static WorldSiteAttackDirection ResolveFootprintApproachDirection(
        SiteVisualFootprint footprint,
        Vector2 edgePoint)
    {
        if (footprint == null || footprint.MapBounds.Size == Vector2.Zero)
        {
            return WorldSiteAttackDirection.Any;
        }

        Rect2 bounds = footprint.MapBounds;
        float leftDistance = Mathf.Abs(edgePoint.X - bounds.Position.X);
        float rightDistance = Mathf.Abs(edgePoint.X - bounds.End.X);
        float topDistance = Mathf.Abs(edgePoint.Y - bounds.Position.Y);
        float bottomDistance = Mathf.Abs(edgePoint.Y - bounds.End.Y);
        float best = Mathf.Min(Mathf.Min(leftDistance, rightDistance), Mathf.Min(topDistance, bottomDistance));
        if (Mathf.Abs(best - leftDistance) <= 0.001f)
        {
            return WorldSiteAttackDirection.West;
        }

        if (Mathf.Abs(best - rightDistance) <= 0.001f)
        {
            return WorldSiteAttackDirection.East;
        }

        if (Mathf.Abs(best - topDistance) <= 0.001f)
        {
            return WorldSiteAttackDirection.North;
        }

        return WorldSiteAttackDirection.South;
    }

    private void ResolveMovingArmySiteNavigationPoints()
    {
        if (State == null || Definition == null || _strategicNavigationContext == null)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        foreach (WorldArmyState army in State.ArmyStates.Values)
        {
            if (army.Status != WorldArmyStatus.Moving || army.HasNavigationPath || army.IsCompletingArrivalApproach)
            {
                continue;
            }

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(army.SourceSiteId) &&
                queries.GetSite(army.SourceSiteId) is { } sourceSite &&
                army.WorldPosition.DistanceSquaredTo(GetSiteMapPosition(sourceSite)) <= SiteNavigationPointSnapDistance * SiteNavigationPointSnapDistance &&
                TryResolveSiteExitArmyNavigationPoint(army.SourceSiteId, army.Destination, out Vector2 sourcePosition, out _))
            {
                if (army.WorldPosition.DistanceSquaredTo(sourcePosition) > 0.001f)
                {
                    army.WorldPosition = sourcePosition;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(army.TargetSiteId) &&
                TryResolveSiteArmyNavigationPoint(army.TargetSiteId, army.WorldPosition, out Vector2 destinationPosition, out Vector2 arrivalApproachOffset, out WorldSiteAttackDirection approachDirection, out _))
            {
                if (army.Destination.DistanceSquaredTo(destinationPosition) > 0.001f)
                {
                    army.Destination = destinationPosition;
                    army.SetArrivalApproachOffset(arrivalApproachOffset);
                    changed = true;
                }

                if (army.TargetApproachDirection != approachDirection)
                {
                    army.SetTargetApproachDirection(approachDirection);
                    changed = true;
                }
            }

            if (!changed)
            {
                continue;
            }

            army.ClearNavigationPath();
            GameLog.Info(
                nameof(StrategicWorldRoot),
                $"WorldArmySiteNavigationPointsResolved army={army.ArmyId} source={army.SourceSiteId} target={army.TargetSiteId} position={army.WorldPosition} destination={army.Destination}");
        }
    }

    private void ReportSiteNavigationPointResolved(string siteId, Vector2 siteCenter, Vector2 navigationPoint)
    {
        if (!_reportedSiteNavigationPointResolutions.Add(siteId))
        {
            return;
        }

        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteNavigationPointResolved site={siteId} center={siteCenter} point={navigationPoint} source=StrategicNavigationTileLayer");
    }

    private void OnSiteButtonGuiInput(string siteId, InputEvent @event)
    {
        if (TryHandleWorldCameraPointerInput(@event))
        {
            _worldMapOverlay?.AcceptEvent();
            return;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            return;
        }

        if (_isExpeditionTargeting)
        {
            TryIssueExpeditionToSite(siteId);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_selectedArmyIds.Count == 0)
        {
            return;
        }

        TryCommandSelectedArmiesToSite(siteId);
        GetViewport().SetInputAsHandled();
    }
}
