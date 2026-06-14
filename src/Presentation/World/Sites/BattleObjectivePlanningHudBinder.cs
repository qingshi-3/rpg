using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.Maps;
using Rpg.Application.World;
using Rpg.Definitions.Maps;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleObjectivePlanningHudBinder
{
    public delegate BattleGroupPlanSnapshot ResolvePlanDelegate(string groupKey, bool create);

    public delegate BattleObjectiveZoneSnapshot BuildMarkerZoneDelegate(
        SemanticMapMarkerData marker,
        int index,
        HashSet<string> usedIds,
        bool selectableTarget);

    public void BindDialog(
        BattleObjectiveMapDialog dialog,
        BattleStartRequest request,
        string selectedGroupKey,
        IReadOnlyList<BattleRuntimeCommandGroupView> playerGroups,
        ResolvePlanDelegate resolvePlan,
        BattleGridMap activeGridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        IEnumerable<SemanticMapMarkerData> semanticMarkers,
        BuildMarkerZoneDelegate buildMarkerZone)
    {
        if (dialog == null)
        {
            return;
        }

        BattleGroupPlanSnapshot plan = resolvePlan?.Invoke(selectedGroupKey ?? "", create: false);
        dialog.Bind(
            BuildBattleObjectiveCompanyOptions(request, playerGroups, resolvePlan),
            selectedGroupKey ?? "",
            BuildBattleObjectiveMapCells(activeGridMap, deploymentCache),
            (request?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
                .OrderBy(zone => zone?.Priority ?? int.MaxValue)
                .ToArray(),
            plan?.ObjectiveZoneId ?? "",
            BuildBattleObjectiveMapRegions(request, semanticMarkers, buildMarkerZone));
    }

    public void BindThumbnail(
        BattlePreparationObjectiveThumbnail thumbnail,
        BattleStartRequest request,
        string selectedGroupDisplayName,
        string selectedGroupKey,
        ResolvePlanDelegate resolvePlan,
        BattleGridMap activeGridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        IEnumerable<SemanticMapMarkerData> semanticMarkers,
        BuildMarkerZoneDelegate buildMarkerZone)
    {
        if (thumbnail == null)
        {
            return;
        }

        BattleGroupPlanSnapshot plan = resolvePlan?.Invoke(selectedGroupKey ?? "", create: false);
        thumbnail.Bind(
            selectedGroupDisplayName ?? "褰撳墠閮ㄩ槦",
            BuildBattleObjectiveMapCells(activeGridMap, deploymentCache),
            (request?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
                .OrderBy(zone => zone?.Priority ?? int.MaxValue)
                .ToArray(),
            plan?.ObjectiveZoneId ?? "",
            BuildBattleObjectiveMapRegions(request, semanticMarkers, buildMarkerZone));
    }

    private static IReadOnlyList<BattleObjectiveCompanyOption> BuildBattleObjectiveCompanyOptions(
        BattleStartRequest request,
        IReadOnlyList<BattleRuntimeCommandGroupView> playerGroups,
        ResolvePlanDelegate resolvePlan)
    {
        return (playerGroups ?? System.Array.Empty<BattleRuntimeCommandGroupView>())
            .Select(group =>
            {
                BattleGroupPlanSnapshot plan = resolvePlan?.Invoke(group.GroupKey, create: false);
                string objective = ResolveBattlePreparationPlanObjectiveLabel(request, plan);
                string rule = plan == null ? "未选择规则" : BattlePreparationPlanUiModel.BuildRuleLabel(plan.EngagementRule);
                return new BattleObjectiveCompanyOption
                {
                    GroupKey = group.GroupKey,
                    DisplayName = group.DisplayName,
                    PlanSummary = $"{objective} 路 {rule}"
                };
            })
            .ToArray();
    }

    private static string ResolveBattlePreparationPlanObjectiveLabel(
        BattleStartRequest request,
        BattleGroupPlanSnapshot plan)
    {
        if (plan == null || string.IsNullOrWhiteSpace(plan.ObjectiveZoneId))
        {
            return "未选择目标";
        }

        BattleObjectiveZoneSnapshot zone = request?.ObjectiveZones?
            .FirstOrDefault(item => string.Equals(item?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
        return zone == null ? "目标已失效" : BattlePreparationPlanUiModel.BuildObjectiveLabel(zone);
    }

    private static IReadOnlyList<BattleObjectiveMapCell> BuildBattleObjectiveMapCells(
        BattleGridMap activeGridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache)
    {
        if (activeGridMap != null && activeGridMap.TopSurfacePositions.Count > 0)
        {
            return activeGridMap.TopSurfacePositions.Values
                .Distinct()
                .Select(position => activeGridMap.TryGetSurface(position, out GridCellSurface surface)
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

        return (deploymentCache?.GetCandidates(WorldSiteAttackDirection.Any) ?? System.Array.Empty<WorldSiteDeploymentCell>())
            .Select(cell => new BattleObjectiveMapCell
            {
                X = cell.Cell.X,
                Y = cell.Cell.Y,
                IsWater = cell.IsWater,
                IsWalkable = true
            })
            .ToArray();
    }

    private static IReadOnlyList<BattleObjectiveMapRegion> BuildBattleObjectiveMapRegions(
        BattleStartRequest request,
        IEnumerable<SemanticMapMarkerData> semanticMarkers,
        BuildMarkerZoneDelegate buildMarkerZone)
    {
        HashSet<string> selectableIds = (request?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
            .Where(zone => zone != null && !string.IsNullOrWhiteSpace(zone.ObjectiveZoneId))
            .Select(zone => zone.ObjectiveZoneId)
            .ToHashSet(System.StringComparer.Ordinal);
        var usedIds = new HashSet<string>(System.StringComparer.Ordinal);
        List<BattleObjectiveMapRegion> regions = new();
        int playerDeploymentIndex = 1;
        int enemyDeploymentIndex = 1;
        int sharedDeploymentIndex = 1;

        IEnumerable<SemanticMapMarkerData> deploymentMarkers = (semanticMarkers ?? Enumerable.Empty<SemanticMapMarkerData>())
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
            BattleObjectiveZoneSnapshot zone = buildMarkerZone?.Invoke(
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

        foreach (BattleObjectiveZoneSnapshot zone in (request?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
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
}
