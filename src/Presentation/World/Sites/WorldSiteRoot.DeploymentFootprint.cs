using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void SetDeploymentDragEntityToFootprintCenter(Node2D entity, GridPosition anchor, Vector2I footprintSize)
    {
        if (entity == null)
        {
            return;
        }

        if (TryGetFootprintCenterGlobalPosition(anchor, footprintSize, out Vector2 globalPosition))
        {
            entity.GlobalPosition = globalPosition;
        }
    }

    private static IReadOnlyList<GridPosition> BuildBattlePreparationFootprintCells(
        BattleForceRequest force,
        GridPosition gridPosition)
    {
        return BattleFootprintCells.Enumerate(
            gridPosition,
            force?.FootprintWidth ?? 1,
            force?.FootprintHeight ?? 1);
    }

    private static Vector2I ResolveForceFootprintSize(BattleForceRequest force)
    {
        return new Vector2I(
            BattleFootprintCells.NormalizeSize(force?.FootprintWidth ?? 1),
            BattleFootprintCells.NormalizeSize(force?.FootprintHeight ?? 1));
    }

    private IReadOnlyList<GridPosition> BuildSitePlacementFootprintCells(
        WorldSiteUnitPlacement placement,
        GridPosition gridPosition)
    {
        Vector2I footprintSize = ResolveUnitFootprintSize(placement?.UnitTypeId);
        return BattleFootprintCells.Enumerate(gridPosition, footprintSize.X, footprintSize.Y);
    }

    private Vector2I ResolveUnitFootprintSize(string unitDefinitionId)
    {
        if (!_battleUnitFactory.TryGetUnitDefinition(unitDefinitionId, out BattleUnitDefinition definition))
        {
            return new Vector2I(1, 1);
        }

        return new Vector2I(
            BattleFootprintCells.NormalizeSize(definition.FootprintWidth),
            BattleFootprintCells.NormalizeSize(definition.FootprintHeight));
    }

    private bool IsBattlePreparationFootprintDeployable(
        IEnumerable<GridPosition> footprintCells,
        WorldSiteAttackDirection direction,
        bool canEnterWater,
        out string failureReason)
    {
        failureReason = "";
        WorldSiteDeploymentCell[] candidates = (_deploymentCache?.GetCandidates(direction) ??
                                                System.Array.Empty<WorldSiteDeploymentCell>())
            .Concat(_deploymentCache?.GetCandidates(WorldSiteAttackDirection.Any) ?? System.Array.Empty<WorldSiteDeploymentCell>())
            .ToArray();

        foreach (GridPosition cell in footprintCells ?? System.Array.Empty<GridPosition>())
        {
            if (_activeGridMap?.TryGetTopSurfacePosition(cell, out _) != true)
            {
                failureReason = "placement_cell_invalid";
                return false;
            }

            bool hasCandidate = candidates.Any(item =>
                item.Cell.X == cell.X &&
                item.Cell.Y == cell.Y &&
                CanUseDeploymentCell(item, canEnterWater));
            if (!hasCandidate)
            {
                failureReason = "placement_cell_not_deployable";
                return false;
            }
        }

        return true;
    }

    private bool IsBattlePreparationFootprintOccupied(
        IEnumerable<GridPosition> footprintCells,
        string forceId,
        int forceIndex)
    {
        HashSet<GridPosition> targetCells = footprintCells?.ToHashSet() ?? new HashSet<GridPosition>();
        foreach (BattleForceRequest force in (_battlePreparationRequest?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
                     .Concat(_battlePreparationRequest?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>()))
        {
            for (int index = 0; index < (force?.PreferredPlacements?.Count ?? 0); index++)
            {
                BattleForcePlacementRequest placement = force.PreferredPlacements[index];
                if (placement == null ||
                    (force.ForceId == forceId && index == forceIndex))
                {
                    continue;
                }

                GridPosition otherAnchor = new(placement.CellX, placement.CellY);
                if (BuildBattlePreparationFootprintCells(force, otherAnchor).Any(targetCells.Contains))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsSiteDeploymentFootprintOccupied(
        WorldSiteState site,
        string activePlacementId,
        IEnumerable<GridPosition> footprintCells)
    {
        HashSet<GridPosition> targetCells = footprintCells?.ToHashSet() ?? new HashSet<GridPosition>();
        foreach (WorldSiteUnitPlacement placement in site?.UnitPlacements ?? Enumerable.Empty<WorldSiteUnitPlacement>())
        {
            if (placement == null || placement.PlacementId == activePlacementId)
            {
                continue;
            }

            GridPosition otherAnchor = new(placement.CellX, placement.CellY);
            if (BuildSitePlacementFootprintCells(placement, otherAnchor).Any(targetCells.Contains))
            {
                return true;
            }
        }

        return false;
    }
}
