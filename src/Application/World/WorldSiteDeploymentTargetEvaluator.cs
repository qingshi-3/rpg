using System;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteDeploymentTargetEvaluator
{
    private readonly WorldSiteDeploymentService _deploymentService;

    public WorldSiteDeploymentTargetEvaluator(WorldSiteDeploymentService deploymentService = null)
    {
        _deploymentService = deploymentService ?? new WorldSiteDeploymentService();
    }

    public bool CanMoveToGridCell(
        BattleGridMap gridMap,
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        Func<WorldSiteUnitPlacement, bool> canEnterWater,
        out string failureReason)
    {
        if (!CanPlaceOnGridCell(gridMap, site, placementId, cell, canEnterWater, out failureReason))
        {
            return false;
        }

        return _deploymentService.CanMovePlacement(site, definition, placementId, cell, out failureReason);
    }

    public bool TryMoveToGridCell(
        BattleGridMap gridMap,
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        Func<WorldSiteUnitPlacement, bool> canEnterWater,
        out string failureReason)
    {
        if (!CanPlaceOnGridCell(gridMap, site, placementId, cell, canEnterWater, out failureReason))
        {
            return false;
        }

        return _deploymentService.TryMovePlacement(site, definition, placementId, cell, out failureReason);
    }

    public bool CanPlaceOnGridCell(
        BattleGridMap gridMap,
        WorldSiteState site,
        string placementId,
        Vector2I cell,
        Func<WorldSiteUnitPlacement, bool> canEnterWater,
        out string failureReason)
    {
        failureReason = "";
        if (gridMap == null)
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        GridPosition position = new(cell.X, cell.Y);
        if (!gridMap.TryGetTopSurface(position, out GridCellSurface surface) ||
            !WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface(gridMap, surface))
        {
            failureReason = "placement_cell_blocked";
            return false;
        }

        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        bool canUseWater = canEnterWater?.Invoke(placement) == true;
        if (!canUseWater && BattleGridTerrainQueries.IsWater(surface))
        {
            failureReason = "placement_cell_water";
            return false;
        }

        return true;
    }
}
