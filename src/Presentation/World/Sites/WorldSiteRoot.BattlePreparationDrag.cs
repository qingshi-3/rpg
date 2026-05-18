using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void SyncBattlePreparationRequestPlacement(string placementId, WorldSiteUnitPlacement movedPlacement)
    {
        if (!_isBattlePreparationActive ||
            string.IsNullOrWhiteSpace(placementId) ||
            movedPlacement == null ||
            _battlePreparationRequest == null)
        {
            return;
        }

        foreach (BattleForcePlacementRequest requestPlacement in _battlePreparationRequest.PlayerForces
                     .Concat(_battlePreparationRequest.EnemyForces)
                     .SelectMany(force => force.PreferredPlacements)
                     .Where(placement => placement?.PlacementId == placementId))
        {
            requestPlacement.CellX = movedPlacement.CellX;
            requestPlacement.CellY = movedPlacement.CellY;
            requestPlacement.CellHeight = movedPlacement.CellHeight;
        }
    }

    private bool TryResolveBattlePreparationDragContext(
        string placementId,
        WorldSiteState site,
        out BattlePreparationPlacementDragContext dragContext)
    {
        dragContext = null;
        if (!_isBattlePreparationActive || string.IsNullOrWhiteSpace(placementId))
        {
            return false;
        }

        WorldSiteUnitPlacement sitePlacement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        if (TryResolveBattlePreparationRequestPlacement(
                placementId,
                _battlePreparationRequest?.PlayerForces,
                BattleFaction.Player,
                out BattleForceRequest force,
                out BattleForcePlacementRequest requestPlacement,
                out int forceIndex,
                out BattleFaction fallbackFaction) ||
            TryResolveBattlePreparationRequestPlacement(
                placementId,
                _battlePreparationRequest?.EnemyForces,
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
                FootprintSize = ResolveForceFootprintSize(force),
                CanEnterWater = ResolveForceCanEnterWater(force),
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
            FallbackFaction = ResolveBattleFaction(sitePlacement.FactionId),
            FootprintSize = ResolveUnitFootprintSize(sitePlacement.UnitTypeId),
            CanEnterWater = ResolvePlacementCanEnterWater(sitePlacement),
            SitePlacement = sitePlacement
        };
        return true;
    }

    private static bool TryResolveBattlePreparationRequestPlacement(
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
        foreach (BattleForceRequest candidateForce in forces ?? System.Array.Empty<BattleForceRequest>())
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

    private static bool ShouldRestrictBattlePreparationDeploymentZone(BattlePreparationPlacementDragContext dragContext)
    {
        // DeploymentZone semantic markers are authored start-zone constraints for both sides;
        // enemy and player placements must enter the same footprint validation path.
        return dragContext != null;
    }

    private IReadOnlyList<GridPosition> BuildSiteDeploymentDragFootprintCells(
        BattlePreparationPlacementDragContext dragContext,
        WorldSiteUnitPlacement placement,
        GridPosition gridPosition)
    {
        if (dragContext != null)
        {
            return BattleFootprintCells.Enumerate(gridPosition, dragContext.FootprintSize.X, dragContext.FootprintSize.Y);
        }

        return BuildSitePlacementFootprintCells(placement, gridPosition);
    }

    private bool TryMoveBattlePreparationPlacement(
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

        if (_activeGridMap?.TryGetTopSurfacePosition(gridPosition, out GridSurfacePosition surfacePosition) != true)
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        if (dragContext.SitePlacement != null &&
            !_deploymentTargetEvaluator.TryMoveToGridCell(
                _activeGridMap,
                site,
                definition,
                dragContext.PlacementId,
                new Vector2I(gridPosition.X, gridPosition.Y),
                ResolvePlacementCanEnterWater,
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

        if (dragContext.RequestPlacement == null)
        {
            failureReason = "missing_placement";
            return false;
        }

        // Roster-spawned units only exist in the battle request until combat starts;
        // map dragging must keep that request placement authoritative.
        dragContext.RequestPlacement.CellX = surfacePosition.X;
        dragContext.RequestPlacement.CellY = surfacePosition.Y;
        dragContext.RequestPlacement.CellHeight = surfacePosition.Height;

        if (draggedEntity is BattleEntity battleEntity)
        {
            SyncBattlePreparationGridOccupant(battleEntity, dragContext, surfacePosition);
            SetDeploymentDragEntityToFootprintCenter(
                battleEntity,
                new GridPosition(surfacePosition.X, surfacePosition.Y),
                dragContext.FootprintSize);
        }

        return true;
    }

    private void SyncBattlePreparationGridOccupant(
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
        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
    }

    private static void SetDeploymentDragComponent(BattleEntity entity, string placementId, bool dragEnabled)
    {
        if (entity == null)
        {
            return;
        }

        WorldSiteDeploymentDragComponent component =
            entity.GetComponent<WorldSiteDeploymentDragComponent>() ??
            entity.GetNodeOrNull<WorldSiteDeploymentDragComponent>("WorldSiteDeploymentDragComponent");
        if (component == null)
        {
            component = new WorldSiteDeploymentDragComponent
            {
                Name = "WorldSiteDeploymentDragComponent"
            };
            entity.AddChild(component);
        }

        component.Configure(placementId, dragEnabled);
    }

    private static bool IsDeploymentDragEnabled(Node2D entity, string placementId)
    {
        if (entity is not BattleEntity battleEntity)
        {
            return false;
        }

        WorldSiteDeploymentDragComponent component =
            battleEntity.GetComponent<WorldSiteDeploymentDragComponent>() ??
            battleEntity.GetNodeOrNull<WorldSiteDeploymentDragComponent>("WorldSiteDeploymentDragComponent");
        return component?.CanDragPlacement(placementId) == true;
    }

    private void SetAllDeploymentDragEnabled(bool enabled)
    {
        int updated = 0;
        foreach (Node2D entity in _sitePlacementEntities.Values)
        {
            WorldSiteDeploymentDragComponent component = entity is BattleEntity battleEntity
                ? battleEntity.GetComponent<WorldSiteDeploymentDragComponent>() ??
                  battleEntity.GetNodeOrNull<WorldSiteDeploymentDragComponent>("WorldSiteDeploymentDragComponent")
                : null;
            if (component == null)
            {
                continue;
            }

            component.SetDragEnabled(enabled);
            updated++;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"DeploymentDragComponentsToggled enabled={enabled} count={updated}");
    }

    private void BeginBattlePreparationRosterDrag(BattleForceRequest force, int forceIndex, BattleFaction fallbackFaction)
    {
        if (!_isBattlePreparationActive || force == null || forceIndex < 0 || forceIndex >= force.Count)
        {
            return;
        }

        _draggedBattleForceId = force.ForceId ?? "";
        _draggedBattleForceIndex = forceIndex;
        _draggedBattleForceFallbackFaction = fallbackFaction;
        BattleForcePlacementRequest removedPlacement = forceIndex < (force.PreferredPlacements?.Count ?? 0)
            ? force.PreferredPlacements[forceIndex]
            : null;
        RemoveBattlePreparationPreferredPlacement(force, forceIndex);
        ClearDraggedBattleRosterEntity();
        // Roster dragging is only a live preview change; avoid rebuilding unrelated units so their idle animations keep advancing.
        RemoveBattlePreparationPlacementEntity(removedPlacement?.PlacementId);

        _draggedBattleRosterEntity = _battleUnitFactory.Create(
            force,
            forceIndex,
            fallbackFaction,
            new GridPosition(0, 0));
        if (_draggedBattleRosterEntity != null && _unitRoot != null)
        {
            _unitRoot.AddChild(_draggedBattleRosterEntity);
            RaiseDeploymentDragEntity(_draggedBattleRosterEntity);
            UpdateBattlePreparationRosterDragPreview();
        }

        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationRosterDragStarted force={force.ForceId} index={forceIndex}");
        GetViewport().SetInputAsHandled();
    }

    private void HandleBattlePreparationRosterDragInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
        {
            if (_draggedBattleRosterEntity != null && IsLiveNode(_draggedBattleRosterEntity))
            {
                UpdateBattlePreparationRosterDragPreview();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            return;
        }

        string forceId = _draggedBattleForceId;
        int forceIndex = _draggedBattleForceIndex;
        BattleForceRequest force = FindBattlePreparationForce(forceId, _draggedBattleForceFallbackFaction);
        bool placed = TryPlaceBattlePreparationRosterUnit(force, forceIndex, out string failureReason);
        ClearBattlePreparationRosterDragState();
        RefreshBattlePreparationUi(placed ? "单位已部署。" : FormatPlacementFailure(failureReason));
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationRosterDragEnded force={forceId} index={forceIndex} placed={placed} reason={failureReason}");
        GetViewport().SetInputAsHandled();
    }

    private void UpdateBattlePreparationRosterDragPreview()
    {
        BattleForceRequest force = FindBattlePreparationForce(_draggedBattleForceId, _draggedBattleForceFallbackFaction);
        Vector2I footprintSize = ResolveForceFootprintSize(force);
        if (!TryResolveMouseFootprintAnchor(footprintSize, out GridPosition gridPosition))
        {
            SetBattlePreparationDragFootprintPreview(System.Array.Empty<GridPosition>());
            return;
        }

        IReadOnlyList<GridPosition> footprintCells = BuildBattlePreparationFootprintCells(force, gridPosition);
        SetDeploymentDragEntityToFootprintCenter(
            _draggedBattleRosterEntity,
            gridPosition,
            footprintSize);
        SetBattlePreparationDragFootprintPreview(footprintCells);
    }

    private bool TryPlaceBattlePreparationRosterUnit(
        BattleForceRequest force,
        int forceIndex,
        out string failureReason)
    {
        failureReason = "";
        if (!TryResolveBattlePreparationRosterDrop(
                force,
                forceIndex,
                out GridPosition gridPosition,
                out int cellHeight,
                out failureReason))
        {
            return false;
        }

        EnsurePreferredPlacementSlot(force, forceIndex);
        force.PreferredPlacements[forceIndex] = new BattleForcePlacementRequest
        {
            PlacementId = BuildBattlePreparationPlacementId(force, forceIndex),
            CellX = gridPosition.X,
            CellY = gridPosition.Y,
            CellHeight = cellHeight
        };
        return true;
    }

    private bool TryResolveBattlePreparationRosterDrop(
        BattleForceRequest force,
        int forceIndex,
        out GridPosition gridPosition,
        out int cellHeight,
        out string failureReason)
    {
        gridPosition = default;
        cellHeight = 0;
        failureReason = "";
        if (force == null || forceIndex < 0 || forceIndex >= force.Count)
        {
            failureReason = "battle_force_missing";
            return false;
        }

        Vector2I footprintSize = ResolveForceFootprintSize(force);
        if (!TryResolveMouseFootprintAnchor(footprintSize, out gridPosition) ||
            _activeGridMap?.TryGetTopSurfacePosition(gridPosition, out GridSurfacePosition surface) != true)
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        cellHeight = surface.Height;
        bool canEnterWater = ResolveForceCanEnterWater(force);
        IReadOnlyList<GridPosition> footprintCells = BuildBattlePreparationFootprintCells(force, gridPosition);
        SemanticDeploymentSide deploymentSide = ResolveBattlePreparationDeploymentSide(force.FactionId, _draggedBattleForceFallbackFaction);
        if (!IsBattlePreparationFootprintDeployable(
                footprintCells,
                deploymentSide,
                force.FactionId,
                ResolveBattlePreparationDeploymentDirection(deploymentSide, force.FactionId),
                canEnterWater,
                out failureReason))
        {
            return false;
        }

        if (IsBattlePreparationFootprintOccupied(footprintCells, force.ForceId, forceIndex))
        {
            failureReason = "placement_cell_occupied";
            return false;
        }

        return true;
    }

    private void SetBattlePreparationDragFootprintPreview(IEnumerable<GridPosition> footprintCells)
    {
        GridPosition[] cells = footprintCells?.Distinct().ToArray() ?? System.Array.Empty<GridPosition>();
        _highlightOverlay?.SetCells(BattleGridHighlightKind.Hover, cells);
    }

    private BattleForceRequest FindBattlePreparationForce(string forceId, BattleFaction preferredFallbackFaction)
    {
        if (string.IsNullOrWhiteSpace(forceId))
        {
            return null;
        }

        IEnumerable<BattleForceRequest> preferredForces = preferredFallbackFaction == BattleFaction.Enemy
            ? _battlePreparationRequest?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>()
            : _battlePreparationRequest?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>();
        IEnumerable<BattleForceRequest> fallbackForces = preferredFallbackFaction == BattleFaction.Enemy
            ? _battlePreparationRequest?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>()
            : _battlePreparationRequest?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>();

        return preferredForces
            .Concat(fallbackForces)
            .FirstOrDefault(force => force != null && force.ForceId == forceId);
    }

    private static void EnsurePreferredPlacementSlot(BattleForceRequest force, int forceIndex)
    {
        while (force.PreferredPlacements.Count <= forceIndex)
        {
            force.PreferredPlacements.Add(null);
        }
    }

    private static void RemoveBattlePreparationPreferredPlacement(BattleForceRequest force, int forceIndex)
    {
        if (force == null || forceIndex < 0 || forceIndex >= force.PreferredPlacements.Count)
        {
            return;
        }

        force.PreferredPlacements[forceIndex] = null;
    }

    private static string BuildBattlePreparationPlacementId(BattleForceRequest force, int forceIndex)
    {
        string forceId = string.IsNullOrWhiteSpace(force?.ForceId) ? force?.UnitDefinitionId ?? "force" : force.ForceId;
        return $"battle_deploy:{forceId}:{forceIndex + 1}";
    }

    private void ClearBattlePreparationRosterDragState()
    {
        ClearDraggedBattleRosterEntity();
        _draggedBattleForceId = "";
        _draggedBattleForceIndex = -1;
        _draggedBattleForceFallbackFaction = BattleFaction.Neutral;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
    }

    private void ClearDraggedBattleRosterEntity()
    {
        if (_draggedBattleRosterEntity != null && IsLiveNode(_draggedBattleRosterEntity))
        {
            _draggedBattleRosterEntity.GetParent()?.RemoveChild(_draggedBattleRosterEntity);
            _draggedBattleRosterEntity.QueueFree();
        }

        _draggedBattleRosterEntity = null;
    }

    private void RemoveBattlePreparationPlacementEntity(string placementId)
    {
        if (string.IsNullOrWhiteSpace(placementId) ||
            !_sitePlacementEntities.Remove(placementId, out Node2D entity) ||
            !IsLiveNode(entity))
        {
            return;
        }

        entity.GetParent()?.RemoveChild(entity);
        entity.QueueFree();
    }
}
