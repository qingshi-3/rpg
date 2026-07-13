using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.StrategicManagement;
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
            !TryBuildStrategicWorldMapSitePresentation(siteId, out StrategicWorldMapSitePresentation target))
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

        if (target.CanReinforce)
        {
            bool hasStrategicExpeditionCarrier = HasSelectedStrategicExpeditionCarrier(selectedArmies);
            if (hasStrategicExpeditionCarrier &&
                !TrySyncStrategicExpeditionCommand(selectedArmies, siteDefinition.Id, WorldArmyIntent.ReinforceSite))
            {
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

            WorldArmyCommandResult commandResult = _armyCommandService.ApplySiteCommand(
                selectedArmies,
                siteDefinition.Id,
                siteArmyPosition,
                siteArrivalOffset,
                siteApproachDirection,
                WorldArmyIntent.ReinforceSite,
                commandPaths,
                _strategicNavigationContext.Version,
                State?.PlayerFactionId);
            if (!commandResult.Success)
            {
                StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(commandResult.FailureReason);
                GameLog.Warn(nameof(StrategicWorldRoot), $"WorldArmyCommandReinforceRejected site={siteId} reason={commandResult.FailureReason}");
                RefreshAll();
                return true;
            }

            StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队进驻 {siteDefinition.DisplayName}。";
            RefreshAll();
            return true;
        }

        if (!target.CanAttack)
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(target.CommandDisabledReason);
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

        if (!TrySyncStrategicExpeditionCommand(selectedArmies, siteDefinition.Id, WorldArmyIntent.AssaultSite))
        {
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

        WorldArmyCommandResult assaultCommandResult = _armyCommandService.ApplySiteCommand(
            selectedArmies,
            siteDefinition.Id,
            assaultSiteArmyPosition,
            assaultArrivalOffset,
            assaultApproachDirection,
            WorldArmyIntent.AssaultSite,
            commandPathsToSite,
            _strategicNavigationContext.Version,
            State?.PlayerFactionId);
        if (!assaultCommandResult.Success)
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(assaultCommandResult.FailureReason);
            GameLog.Warn(nameof(StrategicWorldRoot), $"WorldArmyCommandAssaultRejected site={siteDefinition.Id} reason={assaultCommandResult.FailureReason}");
            RefreshAll();
            return true;
        }

        StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队进攻 {siteDefinition.DisplayName}。";
        RefreshAll();
        return true;
    }

    private static bool HasSelectedStrategicExpeditionCarrier(IEnumerable<WorldArmyState> armies)
    {
        return armies != null && armies.Any(army => !string.IsNullOrWhiteSpace(army?.StrategicExpeditionId));
    }

    private bool TrySyncStrategicExpeditionCommand(
        IReadOnlyList<WorldArmyState> armies,
        string targetSiteId,
        WorldArmyIntent worldIntent)
    {
        WorldArmyState[] strategicArmies = (armies ?? System.Array.Empty<WorldArmyState>())
            .Where(army => !string.IsNullOrWhiteSpace(army?.StrategicExpeditionId))
            .ToArray();
        if (strategicArmies.Length == 0)
        {
            return true;
        }

        StrategicExpeditionIntent strategicIntent = ToStrategicExpeditionIntent(worldIntent);
        if (strategicIntent == StrategicExpeditionIntent.Unknown)
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(StrategicFailureReasons.UnsupportedExpeditionIntent);
            GameLog.Warn(nameof(StrategicWorldRoot), $"StrategicExpeditionCommandSyncRejected intent={worldIntent} reason={StrategicFailureReasons.UnsupportedExpeditionIntent}");
            return false;
        }

        StrategicManagementRuntime.EnsureInitialized();
        string targetLocationId = "";
        if (strategicIntent != StrategicExpeditionIntent.MoveToPosition &&
            !TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveLocationId(targetSiteId, out targetLocationId))
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(StrategicFailureReasons.MissingLocation);
            GameLog.Warn(nameof(StrategicWorldRoot), $"StrategicExpeditionCommandSyncRejected targetSite={targetSiteId} intent={worldIntent} reason={StrategicFailureReasons.MissingLocation}");
            return false;
        }

        foreach (WorldArmyState army in strategicArmies)
        {
            string failureReason = StrategicManagementRuntime.Rules.GetExpeditionRetargetFailureReason(
                StrategicManagementRuntime.State,
                army.StrategicExpeditionId,
                targetLocationId,
                strategicIntent);
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                continue;
            }

            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(failureReason);
            GameLog.Warn(
                nameof(StrategicWorldRoot),
                $"StrategicExpeditionCommandSyncRejected army={army.ArmyId} expedition={army.StrategicExpeditionId} targetSite={targetSiteId} targetLocation={targetLocationId} intent={strategicIntent} reason={failureReason}");
            return false;
        }

        foreach (WorldArmyState army in strategicArmies)
        {
            StrategicCommandResult result = StrategicManagementRuntime.Commands.RetargetExpedition(
                StrategicManagementRuntime.State,
                army.StrategicExpeditionId,
                targetLocationId,
                strategicIntent);
            if (result.Success)
            {
                continue;
            }

            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(result.FailureReason);
            GameLog.Warn(
                nameof(StrategicWorldRoot),
                $"StrategicExpeditionCommandSyncFailed army={army.ArmyId} expedition={army.StrategicExpeditionId} targetSite={targetSiteId} targetLocation={targetLocationId} intent={strategicIntent} reason={result.FailureReason}");
            return false;
        }

        return true;
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

            Vector2? resolvedWorldPosition = null;
            Vector2? resolvedDestination = null;
            Vector2 arrivalApproachOffset = default;
            WorldSiteAttackDirection approachDirection = army.TargetApproachDirection;
            Vector2 effectiveWorldPosition = army.WorldPosition;
            if (!string.IsNullOrWhiteSpace(army.SourceSiteId) &&
                queries.GetSite(army.SourceSiteId) is { } sourceSite &&
                army.WorldPosition.DistanceSquaredTo(GetSiteMapPosition(sourceSite)) <= SiteNavigationPointSnapDistance * SiteNavigationPointSnapDistance &&
                TryResolveSiteExitArmyNavigationPoint(army.SourceSiteId, army.Destination, out Vector2 sourcePosition, out _))
            {
                resolvedWorldPosition = sourcePosition;
                effectiveWorldPosition = sourcePosition;
            }

            if (!string.IsNullOrWhiteSpace(army.TargetSiteId) &&
                TryResolveSiteArmyNavigationPoint(army.TargetSiteId, effectiveWorldPosition, out Vector2 destinationPosition, out arrivalApproachOffset, out approachDirection, out _))
            {
                resolvedDestination = destinationPosition;
            }

            _armyCommandService.ApplyResolvedSiteNavigationPoints(
                army,
                resolvedWorldPosition,
                resolvedDestination,
                arrivalApproachOffset,
                approachDirection);
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
            $"SiteNavigationPointResolved site={siteId} center={siteCenter} point={navigationPoint} source={StrategicNavigationLayerName}");
    }
}
