using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.Maps;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.Maps;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private IReadOnlyList<BattleObjectiveZoneSnapshot> BuildMarkerBackedBattlePreparationObjectiveZones(BattleStartRequest request)
    {
        List<SemanticMapMarkerData> objectiveMarkers = (_semanticMapMarkers?.Markers ?? new List<SemanticMapMarkerData>())
            .Where(marker => marker != null && marker.MarkerType == SemanticMapMarkerType.ObjectiveZone)
            .ToList();
        List<SemanticMapMarkerData> markers = objectiveMarkers.Count > 0
            ? objectiveMarkers
            : BuildBattlePreparationDeploymentMarkers(SemanticDeploymentSide.Enemy).ToList();

        return BuildBattlePreparationObjectiveZonesFromMarkers(markers, selectableTarget: true);
    }

    private IReadOnlyList<BattleObjectiveZoneSnapshot> BuildMarkerBackedBattlePreparationDeploymentZones(
        SemanticDeploymentSide deploymentSide,
        bool selectableTarget)
    {
        return BuildBattlePreparationObjectiveZonesFromMarkers(
            BuildBattlePreparationDeploymentMarkers(deploymentSide),
            selectableTarget);
    }

    private IEnumerable<SemanticMapMarkerData> BuildBattlePreparationDeploymentMarkers(SemanticDeploymentSide deploymentSide)
    {
        return (_semanticMapMarkers?.Markers ?? new List<SemanticMapMarkerData>())
            .Where(marker => marker != null &&
                             marker.MarkerType == SemanticMapMarkerType.DeploymentZone &&
                             marker.DeploymentSide == deploymentSide);
    }

    private IReadOnlyList<BattleObjectiveZoneSnapshot> BuildBattlePreparationObjectiveZonesFromMarkers(
        IEnumerable<SemanticMapMarkerData> markers,
        bool selectableTarget)
    {
        var usedIds = new HashSet<string>(System.StringComparer.Ordinal);
        List<BattleObjectiveZoneSnapshot> zones = new();
        int index = 1;
        foreach (SemanticMapMarkerData marker in (markers ?? Enumerable.Empty<SemanticMapMarkerData>())
                     .Where(marker => !string.IsNullOrWhiteSpace(marker.MarkerId))
                     .OrderBy(marker => marker.Priority)
                     .ThenBy(marker => marker.SourcePath, System.StringComparer.Ordinal)
                     .ThenBy(marker => marker.MarkerId, System.StringComparer.Ordinal))
        {
            BattleObjectiveZoneSnapshot zone = BuildBattlePreparationObjectiveZoneFromMarker(
                marker,
                index,
                usedIds,
                selectableTarget);
            if (zone == null)
            {
                continue;
            }

            zones.Add(zone);
            index++;
        }

        return zones;
    }

    private BattleObjectiveZoneSnapshot BuildBattlePreparationObjectiveZoneFromMarker(
        SemanticMapMarkerData marker,
        int index,
        HashSet<string> usedIds,
        bool selectableTarget)
    {
        GridSurfacePosition[] cells = ResolveBattlePreparationMarkerSurfaces(new[] { marker })
            .Distinct()
            .ToArray();
        if (cells.Length == 0)
        {
            return null;
        }

        int minX = cells.Min(cell => cell.X);
        int maxX = cells.Max(cell => cell.X);
        int minY = cells.Min(cell => cell.Y);
        int maxY = cells.Max(cell => cell.Y);
        string objectiveZoneId = BuildUniqueBattlePreparationMarkerId(marker, index, usedIds);

        return new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = objectiveZoneId,
            DisplayName = BuildBattlePreparationMarkerDisplayName(marker, index, selectableTarget),
            ObjectiveRole = string.IsNullOrWhiteSpace(marker.ObjectiveRole)
                ? ResolveBattlePreparationDefaultObjectiveRole(marker)
                : marker.ObjectiveRole.Trim(),
            DeploymentSide = marker.DeploymentSide.ToString(),
            FactionId = ResolveBattlePreparationMarkerFactionId(marker),
            Priority = marker.Priority == 0 ? index * 10 : marker.Priority,
            CellX = minX,
            CellY = minY,
            CellHeight = cells
                .OrderBy(cell => System.Math.Abs(cell.X - marker.AnchorCell.X) + System.Math.Abs(cell.Y - marker.AnchorCell.Y))
                .ThenBy(cell => cell.Height)
                .First()
                .Height,
            Width = System.Math.Max(1, maxX - minX + 1),
            Height = System.Math.Max(1, maxY - minY + 1)
        };
    }

    private static string ResolveBattlePreparationDefaultObjectiveRole(SemanticMapMarkerData marker)
    {
        if (marker?.MarkerType != SemanticMapMarkerType.DeploymentZone)
        {
            return "objective_marker";
        }

        return marker.DeploymentSide == SemanticDeploymentSide.Player
            ? "player_deployment"
            : "enemy_deployment";
    }

    private string ResolveBattlePreparationMarkerFactionId(SemanticMapMarkerData marker)
    {
        if (!string.IsNullOrWhiteSpace(marker?.FactionId))
        {
            return marker.FactionId.Trim();
        }

        return marker?.DeploymentSide switch
        {
            SemanticDeploymentSide.Player => ResolveBattlePreparationPlayerDeploymentFactionId(),
            SemanticDeploymentSide.Enemy => ResolveBattlePreparationEnemyDeploymentFactionId(),
            _ => ""
        };
    }

    private static string BuildUniqueBattlePreparationMarkerId(
        SemanticMapMarkerData marker,
        int index,
        HashSet<string> usedIds)
    {
        string baseId = string.IsNullOrWhiteSpace(marker?.MarkerId)
            ? $"marker_{index}"
            : marker.MarkerId.Trim();
        if (marker?.MarkerType == SemanticMapMarkerType.DeploymentZone)
        {
            baseId = $"{baseId}_{index}";
        }

        string candidate = baseId;
        int suffix = 2;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private IEnumerable<GridSurfacePosition> ResolveBattlePreparationMarkerSurfaces(
        IEnumerable<SemanticMapMarkerData> markers)
    {
        foreach (SemanticMapMarkerData marker in markers ?? Enumerable.Empty<SemanticMapMarkerData>())
        {
            foreach (Vector2I cell in marker.CoveredCells)
            {
                if (_activeGridMap != null &&
                    _activeGridMap.TryGetTopSurface(new GridPosition(cell.X, cell.Y), out GridCellSurface surface) &&
                    surface.HasFoundation)
                {
                    yield return surface.SurfacePosition;
                    continue;
                }

                yield return new GridSurfacePosition(cell.X, cell.Y, marker.CellHeight);
            }
        }
    }

    private static bool ContainsGeneratedBattlePreparationObjectiveZones(IEnumerable<BattleObjectiveZoneSnapshot> zones)
    {
        return (zones ?? Enumerable.Empty<BattleObjectiveZoneSnapshot>()).Any(zone =>
            zone != null &&
            (zone.ObjectiveZoneId ?? "").StartsWith("objective_", System.StringComparison.Ordinal) &&
            string.Equals(zone.DeploymentSide, "Enemy", System.StringComparison.Ordinal) &&
            zone.Width == 1 &&
            zone.Height == 1);
    }

    private static string BuildBattlePreparationMarkerDisplayName(
        SemanticMapMarkerData marker,
        int index,
        bool selectableTarget = true)
    {
        if (marker?.MarkerType == SemanticMapMarkerType.ObjectiveZone)
        {
            return $"目标区域 {index}";
        }

        if (marker?.DeploymentSide == SemanticDeploymentSide.Player)
        {
            return $"我方部署区 {index}";
        }

        return selectableTarget ? $"敌方部署区 {index}" : $"敌方部署区 {index}";
    }

    private bool TryResolveSelectedBattleObjectiveZone(
        BattleStartRequest request,
        out BattleObjectiveZoneSnapshot selectedZone)
    {
        selectedZone = null;
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            request,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        string objectiveZoneId = plan?.ObjectiveZoneId ?? "";
        if (string.IsNullOrWhiteSpace(objectiveZoneId))
        {
            return false;
        }

        selectedZone = request?.ObjectiveZones?
            .FirstOrDefault(zone => string.Equals(
                zone?.ObjectiveZoneId,
                objectiveZoneId,
                System.StringComparison.Ordinal));
        return selectedZone != null;
    }

    private static void ApplyBattlePreparationObjectiveZoneToPlan(
        BattleGroupPlanSnapshot plan,
        BattleObjectiveZoneSnapshot zone)
    {
        if (plan == null || zone == null)
        {
            return;
        }

        // The UI writes marker-backed objective facts into the draft plan once.
        // Runtime receives copied snapshot data and never queries Presentation nodes.
        plan.ObjectiveZoneId = zone.ObjectiveZoneId ?? "";
        plan.InitialFormationId = string.IsNullOrWhiteSpace(plan.InitialFormationId)
            ? BattlePreparationPlanUiModel.StandardFormationId
            : plan.InitialFormationId;
        plan.HasObjectiveAnchor = true;
        plan.ObjectiveCellX = zone.CellX;
        plan.ObjectiveCellY = zone.CellY;
        plan.ObjectiveCellHeight = zone.CellHeight;
        plan.ObjectiveWidth = System.Math.Max(1, zone.Width);
        plan.ObjectiveHeight = System.Math.Max(1, zone.Height);
    }

    private void ApplyBattlePreparationObjectiveZoneToPlan(
        BattleStartRequest request,
        BattleObjectiveZoneSnapshot zone)
    {
        if (request == null || zone == null)
        {
            return;
        }

        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            request,
            _selectedBattlePreparationPlanGroupKey,
            create: true);
        ApplyBattlePreparationObjectiveZoneToPlan(plan, zone);
        SyncSelectedBattlePreparationPlanFallback(request);
    }

    private IReadOnlyList<BattleRuntimeCommandGroupView> BuildBattlePreparationPlayerGroups()
    {
        List<BattleForceRequest> forces = (_battlePreparationRequest?.PlayerForces ?? new List<BattleForceRequest>())
            .Where(force => force != null && force.Count > 0)
            .ToList();
        return forces
            .GroupBy(ResolveBattleRuntimeGroupKey, System.StringComparer.Ordinal)
            .Select(group => BuildBattleRuntimeCommandGroup(group.Key, group.ToArray()))
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupKey))
            .ToArray();
    }

    private IReadOnlyList<BattleRuntimeCommandGroupView> BuildBattlePreparationEnemyGroups()
    {
        List<BattleForceRequest> forces = (_battlePreparationRequest?.EnemyForces ?? new List<BattleForceRequest>())
            .Where(force => force != null && force.Count > 0)
            .ToList();
        return forces
            .GroupBy(ResolveBattleRuntimeGroupKey, System.StringComparer.Ordinal)
            .Select(group => BuildBattleRuntimeCommandGroup(group.Key, group.ToArray()))
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupKey))
            .ToArray();
    }

    private void EnsureSelectedBattlePreparationPlanGroup(BattleStartRequest request)
    {
        IReadOnlyList<BattleRuntimeCommandGroupView> groups = BuildBattlePreparationPlayerGroups();
        if (groups.Any(group => string.Equals(group.GroupKey, _selectedBattlePreparationPlanGroupKey, System.StringComparison.Ordinal)))
        {
            return;
        }

        _selectedBattlePreparationPlanGroupKey = groups.FirstOrDefault()?.GroupKey ?? "";
    }

    private BattleGroupPlanSnapshot ResolveBattlePreparationGroupPlan(
        BattleStartRequest request,
        string groupKey,
        bool create)
    {
        if (request == null)
        {
            return null;
        }

        request.PlayerBattleGroupPlans ??= new Dictionary<string, BattleGroupPlanSnapshot>(System.StringComparer.Ordinal);
        string key = groupKey ?? "";
        if (!string.IsNullOrWhiteSpace(key) &&
            request.PlayerBattleGroupPlans.TryGetValue(key, out BattleGroupPlanSnapshot groupPlan))
        {
            return groupPlan;
        }

        if (!create)
        {
            return string.IsNullOrWhiteSpace(key) ? request.PlayerBattleGroupPlan : null;
        }

        bool canInheritSingleGroupFallback = request.PlayerBattleGroupPlans.Count == 0 &&
                                             BuildBattlePreparationPlayerGroups().Count <= 1;
        BattleGroupPlanSnapshot plan = canInheritSingleGroupFallback
            ? CopyBattlePreparationPlan(request.PlayerBattleGroupPlan)
            : new BattleGroupPlanSnapshot
            {
                EngagementRule = request.PlayerBattleGroupPlan?.EngagementRule ?? BattleEngagementRule.MoveFirst,
                InitialFormationId = request.PlayerBattleGroupPlan?.InitialFormationId ?? ""
            };
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.PlayerBattleGroupPlans[key] = plan;
        }

        return plan;
    }

    private void SyncSelectedBattlePreparationPlanFallback(BattleStartRequest request)
    {
        if (request == null)
        {
            return;
        }

        BattleGroupPlanSnapshot selected = ResolveBattlePreparationGroupPlan(
            request,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        if (selected == null)
        {
            return;
        }

        request.PlayerBattleGroupPlan = CopyBattlePreparationPlan(selected);
    }

    private static BattleGroupPlanSnapshot CopyBattlePreparationPlan(BattleGroupPlanSnapshot source)
    {
        if (source == null)
        {
            return new BattleGroupPlanSnapshot();
        }

        return new BattleGroupPlanSnapshot
        {
            BattleGroupId = source.BattleGroupId ?? "",
            ObjectiveZoneId = source.ObjectiveZoneId ?? "",
            EngagementRule = source.EngagementRule,
            InitialFormationId = source.InitialFormationId ?? "",
            HasObjectiveAnchor = source.HasObjectiveAnchor,
            ObjectiveCellX = source.ObjectiveCellX,
            ObjectiveCellY = source.ObjectiveCellY,
            ObjectiveCellHeight = source.ObjectiveCellHeight,
            ObjectiveWidth = source.ObjectiveWidth,
            ObjectiveHeight = source.ObjectiveHeight
        };
    }

    private void EnsureBattlePreparationEnemyPlanDefaults(BattleStartRequest request)
    {
        if (request == null)
        {
            return;
        }

        request.EnemyBattleGroupPlan ??= new BattleGroupPlanSnapshot();
        request.EnemyBattleGroupPlans ??= new Dictionary<string, BattleGroupPlanSnapshot>(System.StringComparer.Ordinal);
        if (HasAuthoredBattlePreparationPlan(request.EnemyBattleGroupPlan))
        {
            return;
        }

        IReadOnlyList<BattleObjectiveZoneSnapshot> playerDeploymentZones =
            BuildMarkerBackedBattlePreparationDeploymentZones(SemanticDeploymentSide.Player, selectableTarget: false);
        if (playerDeploymentZones.Count == 0)
        {
            return;
        }

        int synced = 0;
        foreach (BattleRuntimeCommandGroupView group in BuildBattlePreparationEnemyGroups())
        {
            if (request.EnemyBattleGroupPlans.TryGetValue(group.GroupKey, out BattleGroupPlanSnapshot existingPlan) &&
                HasAuthoredBattlePreparationPlan(existingPlan))
            {
                continue;
            }

            BattleObjectiveZoneSnapshot zone = ResolveNearestBattlePreparationPlanZone(group, playerDeploymentZones);
            if (zone == null)
            {
                continue;
            }

            BattleGroupPlanSnapshot plan = new()
            {
                EngagementRule = BattleEngagementRule.AttackFirst,
                InitialFormationId = BattlePreparationPlanUiModel.StandardFormationId
            };
            // Enemy sortie defaults use the same objective-first contract as the
            // player's plan, but keep plan-scoped local sensing so defenders do
            // not wait until a player unit is already attacking in melee.
            ApplyBattlePreparationObjectiveZoneToPlan(plan, zone);
            request.EnemyBattleGroupPlans[group.GroupKey] = plan;
            synced++;
        }

        if (synced > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattlePreparationEnemyPlansDefaulted request={request.RequestId} groups={synced}");
        }
    }

    private static bool HasAuthoredBattlePreparationPlan(BattleGroupPlanSnapshot plan)
    {
        return plan != null &&
               (!string.IsNullOrWhiteSpace(plan.ObjectiveZoneId) ||
                !string.IsNullOrWhiteSpace(plan.InitialFormationId) ||
                plan.HasObjectiveAnchor ||
                plan.EngagementRule != BattleEngagementRule.AttackFirst);
    }

    private static BattleObjectiveZoneSnapshot ResolveNearestBattlePreparationPlanZone(
        BattleRuntimeCommandGroupView group,
        IReadOnlyList<BattleObjectiveZoneSnapshot> zones)
    {
        if (zones == null || zones.Count == 0)
        {
            return null;
        }

        if (!TryResolveBattlePreparationGroupAnchor(group, out int anchorX, out int anchorY))
        {
            return zones
                .OrderBy(zone => zone?.Priority ?? int.MaxValue)
                .ThenBy(zone => zone?.ObjectiveZoneId ?? "", System.StringComparer.Ordinal)
                .FirstOrDefault();
        }

        return zones
            .OrderBy(zone => GetBattlePreparationZoneDistance(anchorX, anchorY, zone))
            .ThenBy(zone => zone?.Priority ?? int.MaxValue)
            .ThenBy(zone => zone?.ObjectiveZoneId ?? "", System.StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool TryResolveBattlePreparationGroupAnchor(
        BattleRuntimeCommandGroupView group,
        out int cellX,
        out int cellY)
    {
        foreach (BattleForcePlacementRequest placement in (group?.Forces ?? System.Array.Empty<BattleForceRequest>())
                     .SelectMany(force => force?.PreferredPlacements ?? new List<BattleForcePlacementRequest>())
                     .Where(placement => placement != null))
        {
            cellX = placement.CellX;
            cellY = placement.CellY;
            return true;
        }

        cellX = 0;
        cellY = 0;
        return false;
    }

    private static int GetBattlePreparationZoneDistance(
        int anchorX,
        int anchorY,
        BattleObjectiveZoneSnapshot zone)
    {
        if (zone == null)
        {
            return int.MaxValue;
        }

        int centerX = zone.CellX + System.Math.Max(0, zone.Width - 1) / 2;
        int centerY = zone.CellY + System.Math.Max(0, zone.Height - 1) / 2;
        return System.Math.Abs(anchorX - centerX) + System.Math.Abs(anchorY - centerY);
    }

    private void OpenBattleObjectiveMapDialog()
    {
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);
        if (_battleObjectiveMapDialog == null)
        {
            RefreshBattlePreparationPlanUi("目标选择界面未加载。", "battle_preparation_objective_dialog_missing");
            return;
        }

        BindBattleObjectiveMapDialog();
        _battleObjectiveMapDialog.Open();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationObjectiveMapOpened request={_battlePreparationRequest?.RequestId ?? ""} objectives={_battlePreparationRequest?.ObjectiveZones?.Count ?? 0}");
    }

    private void BindBattleObjectiveMapDialog()
    {
        EnsureSelectedBattlePreparationPlanGroup(_battlePreparationRequest);
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            _battlePreparationRequest,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        _battleObjectiveMapDialog?.Bind(
            BuildBattleObjectiveCompanyOptions(),
            _selectedBattlePreparationPlanGroupKey,
            BuildBattleObjectiveMapCells(),
            (_battlePreparationRequest?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
                .OrderBy(zone => zone?.Priority ?? int.MaxValue)
                .ToArray(),
            plan?.ObjectiveZoneId ?? "",
            BuildBattleObjectiveMapRegions());
    }

    private IReadOnlyList<BattleObjectiveCompanyOption> BuildBattleObjectiveCompanyOptions()
    {
        return BuildBattlePreparationPlayerGroups()
            .Select(group =>
            {
                BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
                    _battlePreparationRequest,
                    group.GroupKey,
                    create: false);
                string objective = ResolveBattlePreparationPlanObjectiveLabel(plan);
                string rule = plan == null ? "未选择规则" : BattlePreparationPlanUiModel.BuildRuleLabel(plan.EngagementRule);
                return new BattleObjectiveCompanyOption
                {
                    GroupKey = group.GroupKey,
                    DisplayName = group.DisplayName,
                    PlanSummary = $"{objective} · {rule}"
                };
            })
            .ToArray();
    }

    private string ResolveBattlePreparationPlanObjectiveLabel(BattleGroupPlanSnapshot plan)
    {
        if (plan == null || string.IsNullOrWhiteSpace(plan.ObjectiveZoneId))
        {
            return "未选择目标";
        }

        BattleObjectiveZoneSnapshot zone = _battlePreparationRequest?.ObjectiveZones?
            .FirstOrDefault(item => string.Equals(item?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
        return zone == null ? "目标已失效" : BattlePreparationPlanUiModel.BuildObjectiveLabel(zone);
    }

    private IReadOnlyList<BattleObjectiveMapCell> BuildBattleObjectiveMapCells()
    {
        if (_activeGridMap != null && _activeGridMap.TopSurfacePositions.Count > 0)
        {
            return _activeGridMap.TopSurfacePositions.Values
                .Distinct()
                .Select(position => _activeGridMap.TryGetSurface(position, out GridCellSurface surface)
                    ? new BattleObjectiveMapCell
                    {
                        X = position.X,
                        Y = position.Y,
                        IsWater = BattleGridTerrainQueries.IsWater(surface),
                        IsWalkable = surface.IsWalkable && surface.MoveCost > 0
                    }
                    : null)
                .Where(cell => cell != null)
                .ToArray();
        }

        return (_deploymentCache?.GetCandidates(WorldSiteAttackDirection.Any) ?? System.Array.Empty<WorldSiteDeploymentCell>())
            .Select(cell => new BattleObjectiveMapCell
            {
                X = cell.Cell.X,
                Y = cell.Cell.Y,
                IsWater = cell.IsWater,
                IsWalkable = true
            })
            .ToArray();
    }

    private IReadOnlyList<BattleObjectiveMapRegion> BuildBattleObjectiveMapRegions()
    {
        HashSet<string> selectableIds = (_battlePreparationRequest?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
            .Where(zone => zone != null && !string.IsNullOrWhiteSpace(zone.ObjectiveZoneId))
            .Select(zone => zone.ObjectiveZoneId)
            .ToHashSet(System.StringComparer.Ordinal);
        var usedIds = new HashSet<string>(System.StringComparer.Ordinal);
        List<BattleObjectiveMapRegion> regions = new();
        int playerDeploymentIndex = 1;
        int enemyDeploymentIndex = 1;
        int sharedDeploymentIndex = 1;

        IEnumerable<SemanticMapMarkerData> deploymentMarkers = (_semanticMapMarkers?.Markers ?? new List<SemanticMapMarkerData>())
            .Where(marker => marker != null && marker.MarkerType == SemanticMapMarkerType.DeploymentZone)
            .OrderBy(marker => marker.DeploymentSide)
            .ThenBy(marker => marker.Priority)
            .ThenBy(marker => marker.SourcePath, System.StringComparer.Ordinal)
            .ThenBy(marker => marker.MarkerId, System.StringComparer.Ordinal);
        foreach (SemanticMapMarkerData marker in deploymentMarkers)
        {
            int markerIndex = marker.DeploymentSide switch
            {
                SemanticDeploymentSide.Player => playerDeploymentIndex++,
                SemanticDeploymentSide.Enemy => enemyDeploymentIndex++,
                _ => sharedDeploymentIndex++
            };
            BattleObjectiveZoneSnapshot zone = BuildBattlePreparationObjectiveZoneFromMarker(
                marker,
                markerIndex,
                usedIds,
                selectableTarget: marker.DeploymentSide == SemanticDeploymentSide.Enemy);
            if (zone == null)
            {
                continue;
            }

            regions.Add(ToBattleObjectiveMapRegion(zone, selectableIds.Contains(zone.ObjectiveZoneId)));
        }

        foreach (BattleObjectiveZoneSnapshot zone in (_battlePreparationRequest?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
                     .Where(zone => zone != null && !regions.Any(region => region.RegionId == zone.ObjectiveZoneId)))
        {
            regions.Add(ToBattleObjectiveMapRegion(zone, selectable: true));
        }

        return regions
            .OrderBy(region => region.Priority)
            .ThenBy(region => region.RegionId, System.StringComparer.Ordinal)
            .ToArray();
    }

    private static BattleObjectiveMapRegion ToBattleObjectiveMapRegion(
        BattleObjectiveZoneSnapshot zone,
        bool selectable)
    {
        return new BattleObjectiveMapRegion
        {
            RegionId = zone.ObjectiveZoneId ?? "",
            DisplayName = zone.DisplayName ?? "",
            DeploymentSide = zone.DeploymentSide ?? "",
            Priority = zone.Priority,
            CellX = zone.CellX,
            CellY = zone.CellY,
            Width = System.Math.Max(1, zone.Width),
            Height = System.Math.Max(1, zone.Height),
            Selectable = selectable
        };
    }

    private void OnBattleObjectiveDialogCompanySelected(string groupKey)
    {
        _selectedBattlePreparationPlanGroupKey = groupKey ?? "";
        ResolveBattlePreparationGroupPlan(_battlePreparationRequest, _selectedBattlePreparationPlanGroupKey, create: true);
        SyncSelectedBattlePreparationPlanFallback(_battlePreparationRequest);
        BindBattleObjectiveMapDialog();
        RefreshBattlePreparationPlanUi("", "battle_preparation_objective_dialog_company");
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationPlanGroupSelected group={_selectedBattlePreparationPlanGroupKey}");
    }

    private void OnBattleObjectiveDialogObjectiveSelected(string objectiveZoneId)
    {
        SelectBattlePreparationObjectiveZone(objectiveZoneId);
        BindBattleObjectiveMapDialog();
    }
}
