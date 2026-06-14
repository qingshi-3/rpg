using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattlePreparationDeploymentDragController
{
    private readonly Func<bool> _isBattlePreparationActive;
    private readonly Func<BattleStartRequest> _getBattlePreparationRequest;
    private readonly Func<BattleGridMap> _getActiveGridMap;
    private readonly WorldSiteDeploymentTargetEvaluator _deploymentTargetEvaluator;
    private readonly Func<BattleForceRequest, Vector2I> _resolveForceFootprintSize;
    private readonly Func<BattleForceRequest, bool> _resolveForceCanEnterWater;
    private readonly Func<string, Vector2I> _resolveUnitFootprintSize;
    private readonly Func<WorldSiteUnitPlacement, bool> _resolvePlacementCanEnterWater;
    private readonly Func<string, BattleFaction> _resolveBattleFaction;
    private readonly Action<Node2D, GridPosition, Vector2I> _setDeploymentDragEntityToFootprintCenter;
    private readonly Action<GridOccupantComponent> _resolveEntitySurfaceHeight;
    private readonly Action<BattleEntity, GridSurfacePosition> _applyEntityRenderSort;

    public BattlePreparationDeploymentDragController(
        Func<bool> isBattlePreparationActive,
        Func<BattleStartRequest> getBattlePreparationRequest,
        Func<BattleGridMap> getActiveGridMap,
        WorldSiteDeploymentTargetEvaluator deploymentTargetEvaluator,
        Func<BattleForceRequest, Vector2I> resolveForceFootprintSize,
        Func<BattleForceRequest, bool> resolveForceCanEnterWater,
        Func<string, Vector2I> resolveUnitFootprintSize,
        Func<WorldSiteUnitPlacement, bool> resolvePlacementCanEnterWater,
        Func<string, BattleFaction> resolveBattleFaction,
        Action<Node2D, GridPosition, Vector2I> setDeploymentDragEntityToFootprintCenter,
        Action<GridOccupantComponent> resolveEntitySurfaceHeight,
        Action<BattleEntity, GridSurfacePosition> applyEntityRenderSort)
    {
        _isBattlePreparationActive = isBattlePreparationActive;
        _getBattlePreparationRequest = getBattlePreparationRequest;
        _getActiveGridMap = getActiveGridMap;
        _deploymentTargetEvaluator = deploymentTargetEvaluator;
        _resolveForceFootprintSize = resolveForceFootprintSize;
        _resolveForceCanEnterWater = resolveForceCanEnterWater;
        _resolveUnitFootprintSize = resolveUnitFootprintSize;
        _resolvePlacementCanEnterWater = resolvePlacementCanEnterWater;
        _resolveBattleFaction = resolveBattleFaction;
        _setDeploymentDragEntityToFootprintCenter = setDeploymentDragEntityToFootprintCenter;
        _resolveEntitySurfaceHeight = resolveEntitySurfaceHeight;
        _applyEntityRenderSort = applyEntityRenderSort;
    }

    public void SyncRequestPlacement(string placementId, WorldSiteUnitPlacement movedPlacement)
    {
        BattleStartRequest request = _getBattlePreparationRequest();
        if (!_isBattlePreparationActive() ||
            string.IsNullOrWhiteSpace(placementId) ||
            movedPlacement == null ||
            request == null)
        {
            return;
        }

        foreach (BattleForcePlacementRequest requestPlacement in request.PlayerForces
                     .Concat(request.EnemyForces)
                     .SelectMany(force => force.PreferredPlacements)
                     .Where(placement => placement?.PlacementId == placementId))
        {
            requestPlacement.CellX = movedPlacement.CellX;
            requestPlacement.CellY = movedPlacement.CellY;
            requestPlacement.CellHeight = movedPlacement.CellHeight;
        }
    }

    public bool TryResolveDragContext(
        string placementId,
        WorldSiteState site,
        out BattlePreparationPlacementDragContext dragContext)
    {
        dragContext = null;
        if (!_isBattlePreparationActive() || string.IsNullOrWhiteSpace(placementId))
        {
            return false;
        }

        BattleStartRequest request = _getBattlePreparationRequest();
        WorldSiteUnitPlacement sitePlacement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        if (TryResolveRequestPlacement(
                placementId,
                request?.PlayerForces,
                BattleFaction.Player,
                out BattleForceRequest force,
                out BattleForcePlacementRequest requestPlacement,
                out int forceIndex,
                out BattleFaction fallbackFaction) ||
            TryResolveRequestPlacement(
                placementId,
                request?.EnemyForces,
                BattleFaction.Enemy,
                out force,
                out requestPlacement,
                out forceIndex,
                out fallbackFaction))
        {
            dragContext = new BattlePreparationPlacementDragContext
            {
                PlacementId = placementId,
                ForceId = force?.ForceId ?? "",
                ForceIndex = forceIndex,
                UnitTypeId = force?.UnitDefinitionId ?? sitePlacement?.UnitTypeId ?? "",
                FactionId = string.IsNullOrWhiteSpace(force?.FactionId) ? sitePlacement?.FactionId ?? "" : force.FactionId,
                FallbackFaction = fallbackFaction,
                FootprintSize = _resolveForceFootprintSize(force),
                CanEnterWater = _resolveForceCanEnterWater(force),
                RequestPlacement = requestPlacement,
                SitePlacement = sitePlacement
            };
            return true;
        }

        if (sitePlacement == null)
        {
            return false;
        }

        dragContext = new BattlePreparationPlacementDragContext
        {
            PlacementId = placementId,
            ForceId = sitePlacement.SourceId ?? "",
            ForceIndex = sitePlacement.UnitIndex - 1,
            UnitTypeId = sitePlacement.UnitTypeId ?? "",
            FactionId = sitePlacement.FactionId ?? "",
            FallbackFaction = _resolveBattleFaction(sitePlacement.FactionId),
            FootprintSize = _resolveUnitFootprintSize(sitePlacement.UnitTypeId),
            CanEnterWater = _resolvePlacementCanEnterWater(sitePlacement),
            SitePlacement = sitePlacement
        };
        return true;
    }

    public static bool ShouldRestrictDeploymentZone(BattlePreparationPlacementDragContext dragContext)
    {
        // DeploymentZone semantic markers are authored start-zone constraints for both sides;
        // enemy and player placements must enter the same footprint validation path.
        return dragContext != null;
    }

    public IReadOnlyList<GridPosition> BuildFootprintCells(
        BattlePreparationPlacementDragContext dragContext,
        GridPosition gridPosition)
    {
        return dragContext == null
            ? Array.Empty<GridPosition>()
            : BattleFootprintCells.Enumerate(gridPosition, dragContext.FootprintSize.X, dragContext.FootprintSize.Y);
    }

    public bool TryMovePlacement(
        BattlePreparationPlacementDragContext dragContext,
        WorldSiteState site,
        WorldSiteDefinition definition,
        Node2D draggedEntity,
        GridPosition gridPosition,
        out string failureReason)
    {
        failureReason = "";
        if (dragContext == null)
        {
            failureReason = "missing_placement";
            return false;
        }

        if (dragContext.RequestPlacement == null)
        {
            // Battle preparation map drags are request-authoritative. Site rows may mirror
            // that request only after the active battle placement has been resolved.
            failureReason = "missing_placement";
            return false;
        }

        BattleGridMap activeGridMap = _getActiveGridMap();
        if (activeGridMap?.TryGetTopSurfacePosition(gridPosition, out GridSurfacePosition surfacePosition) != true)
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        if (dragContext.SitePlacement != null &&
            !_deploymentTargetEvaluator.TryMoveToGridCell(
                activeGridMap,
                site,
                definition,
                dragContext.PlacementId,
                new Vector2I(gridPosition.X, gridPosition.Y),
                _resolvePlacementCanEnterWater,
                out failureReason))
        {
            return false;
        }

        WorldSiteUnitPlacement movedPlacement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == dragContext.PlacementId);
        if (movedPlacement != null)
        {
            surfacePosition = new GridSurfacePosition(movedPlacement.CellX, movedPlacement.CellY, movedPlacement.CellHeight);
            dragContext.SitePlacement = movedPlacement;
        }

        // Roster-spawned units only exist in the battle request until combat starts;
        // map dragging must keep that request placement authoritative.
        dragContext.RequestPlacement.CellX = surfacePosition.X;
        dragContext.RequestPlacement.CellY = surfacePosition.Y;
        dragContext.RequestPlacement.CellHeight = surfacePosition.Height;

        if (draggedEntity is BattleEntity battleEntity)
        {
            SyncGridOccupant(battleEntity, dragContext, surfacePosition);
            _setDeploymentDragEntityToFootprintCenter(
                battleEntity,
                new GridPosition(surfacePosition.X, surfacePosition.Y),
                dragContext.FootprintSize);
        }

        return true;
    }

    private static bool TryResolveRequestPlacement(
        string placementId,
        IEnumerable<BattleForceRequest> forces,
        BattleFaction fallbackFaction,
        out BattleForceRequest force,
        out BattleForcePlacementRequest placement,
        out int forceIndex,
        out BattleFaction resolvedFallbackFaction)
    {
        force = null;
        placement = null;
        forceIndex = -1;
        resolvedFallbackFaction = fallbackFaction;
        foreach (BattleForceRequest candidateForce in forces ?? Array.Empty<BattleForceRequest>())
        {
            for (int index = 0; index < (candidateForce?.PreferredPlacements?.Count ?? 0); index++)
            {
                BattleForcePlacementRequest candidatePlacement = candidateForce.PreferredPlacements[index];
                if (candidatePlacement?.PlacementId != placementId)
                {
                    continue;
                }

                force = candidateForce;
                placement = candidatePlacement;
                forceIndex = index;
                return true;
            }
        }

        return false;
    }

    private void SyncGridOccupant(
        BattleEntity entity,
        BattlePreparationPlacementDragContext dragContext,
        GridSurfacePosition surfacePosition)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null || dragContext == null)
        {
            return;
        }

        gridOccupant.GridX = surfacePosition.X;
        gridOccupant.GridY = surfacePosition.Y;
        gridOccupant.GridHeight = surfacePosition.Height;
        gridOccupant.FootprintWidth = dragContext.FootprintSize.X;
        gridOccupant.FootprintHeight = dragContext.FootprintSize.Y;
        gridOccupant.UseExplicitHeight = surfacePosition.Height > 0;
        _resolveEntitySurfaceHeight(gridOccupant);
        _applyEntityRenderSort(entity, gridOccupant.SurfacePosition);
    }
}
