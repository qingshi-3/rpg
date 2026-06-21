using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void SelectPlacement(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        RefreshSitePlacementUi("");
        GameLog.Info(nameof(WorldSiteRoot), $"Site placement selected site={_siteHudSiteId} placement={_selectedPlacementId}");
    }

    private void OnPlacementEntityPressed(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        UpdateSitePeacetimePanelVisibility("placement_selected");
        _draggedPlacementId = _selectedPlacementId;
        if (_sitePlacementEntities.TryGetValue(_draggedPlacementId, out Node2D entity))
        {
            _draggedPlacementOriginGlobalPosition = entity.GlobalPosition;
            SetSitePlacementSelected(entity, true);
            RaiseDeploymentDragEntity(entity);
            UpdateSiteDeploymentDragPreview(entity);
        }

        _siteSelectionLabel.Text = $"正在调整：{BuildPlacementDisplayName(_selectedPlacementId)}";
        GetViewport().SetInputAsHandled();
    }

    private bool TryHandleSiteContextClearInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            string.IsNullOrWhiteSpace(_selectedPlacementId) ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            IsPointerOverSiteHud(@event) ||
            TryResolvePlacementUnderPointer(out _))
        {
            return false;
        }

        string previousPlacementId = _selectedPlacementId;
        _selectedPlacementId = "";
        RefreshSiteManagementUi();
        GameLog.Info(nameof(WorldSiteRoot), $"Site placement selection cleared site={_siteHudSiteId} previousPlacement={previousPlacementId}");
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void HandleSiteDeploymentDragInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_draggedBattlePreparationGroupKey))
        {
            HandleBattlePreparationCompanyDragInput(@event);
            return;
        }

        if (string.IsNullOrWhiteSpace(_draggedPlacementId))
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } &&
                !IsPointerOverSiteHud(@event) &&
                TryResolvePlacementUnderPointer(out string pressedPlacementId))
            {
                OnPlacementEntityPressed(pressedPlacementId);
            }

            return;
        }

        if (@event is InputEventMouseMotion)
        {
            if (_sitePlacementEntities.TryGetValue(_draggedPlacementId, out Node2D entity))
            {
                UpdateSiteDeploymentDragPreview(entity);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            return;
        }

        string placementId = _draggedPlacementId;
        _sitePlacementEntities.TryGetValue(placementId, out Node2D draggedEntity);
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        bool canDrop = TryEvaluateSiteDeploymentTarget(
            placementId,
            site,
            definition,
            out GridPosition gridPosition,
            out bool hasGridPosition,
            out string failureReason);
        _draggedPlacementId = "";
        ClearSiteDeploymentDragPreview(draggedEntity);

        if (!canDrop)
        {
            ReturnDraggedPlacementToOrigin(draggedEntity);
            RefreshSitePlacementUi(FormatPlacementFailure(failureReason));
            GameLog.Info(nameof(WorldSiteRoot), $"Site placement drag cancelled site={_siteHudSiteId} placement={placementId} hasGrid={hasGridPosition} reason={failureReason}");
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_isBattlePreparationActive &&
            _battlePreparationDeploymentDragController.TryResolveDragContext(placementId, site, out BattlePreparationPlacementDragContext dragContext))
        {
            if (_battlePreparationDeploymentDragController.TryMovePlacement(
                    dragContext,
                    site,
                    definition,
                    draggedEntity,
                    gridPosition,
                    out failureReason))
            {
                RefreshBattlePreparationAfterSinglePlacementDrag("部署位置已更新。");
            }
            else
            {
                ReturnDraggedPlacementToOrigin(draggedEntity);
                RefreshSitePlacementUi(FormatPlacementFailure(failureReason));
                GameLog.Info(nameof(WorldSiteRoot), $"Battle preparation placement drag rejected site={_siteHudSiteId} placement={placementId} cell={gridPosition} reason={failureReason}");
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (_deploymentTargetEvaluator.TryMoveToGridCell(
                _activeGridMap,
                site,
                definition,
                placementId,
                new Vector2I(gridPosition.X, gridPosition.Y),
                ResolvePlacementCanEnterWater,
                out failureReason))
        {
            WorldSiteUnitPlacement movedPlacement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
            if (draggedEntity is BattleEntity battleEntity && movedPlacement != null)
            {
                SyncSitePlacementGridOccupant(battleEntity, movedPlacement);
                _battlePreparationDeploymentDragController.SyncRequestPlacement(placementId, movedPlacement);
            }
            else
            {
                RestoreDeploymentEntityRenderSort(draggedEntity);
            }

            RefreshSitePlacementUi(_isBattlePreparationActive ? "部署位置已更新。" : "驻军位置已更新。");
        }
        else
        {
            ReturnDraggedPlacementToOrigin(draggedEntity);
            RefreshSitePlacementUi(FormatPlacementFailure(failureReason));
            GameLog.Info(nameof(WorldSiteRoot), $"Site placement drag rejected site={_siteHudSiteId} placement={placementId} cell={gridPosition} reason={failureReason}");
        }

        GetViewport().SetInputAsHandled();
    }

    private void RefreshSitePlacementUi(string notice)
    {
        if (_isBattlePreparationActive)
        {
            RefreshBattlePreparationPlanUi(notice, "battle_preparation_site_placement");
            return;
        }

        RefreshSiteManagementUi(notice);
    }

    private void RefreshBattlePreparationAfterSinglePlacementDrag(string notice)
    {
        RefreshBattlePreparationPlanUi(notice, "battle_preparation_single_placement_drag");
    }


    private void UpdateSiteDeploymentDragPreview(Node2D entity)
    {
        if (entity == null)
        {
            return;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        TryEvaluateSiteDeploymentTarget(
            _draggedPlacementId,
            site,
            definition,
            out GridPosition gridPosition,
            out bool hasGridPosition,
            out _);

        _battlePreparationDeploymentDragController.TryResolveDragContext(_draggedPlacementId, site, out BattlePreparationPlacementDragContext dragContext);
        WorldSiteUnitPlacement placement = dragContext?.SitePlacement ??
                                           site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == _draggedPlacementId);
        Vector2I footprintSize = dragContext?.FootprintSize ?? ResolveUnitFootprintSize(placement?.UnitTypeId);
        SetBattlePreparationDragFootprintPreview(
            hasGridPosition
                ? dragContext != null
                    ? _battlePreparationDeploymentDragController.BuildFootprintCells(dragContext, gridPosition)
                    : BuildSitePlacementFootprintCells(placement, gridPosition)
                : System.Array.Empty<GridPosition>());
        if (hasGridPosition)
        {
            SetDeploymentDragEntityToFootprintCenter(entity, gridPosition, footprintSize);
        }
    }


    private bool TryEvaluateSiteDeploymentTarget(
        string placementId,
        WorldSiteState site,
        WorldSiteDefinition definition,
        out GridPosition gridPosition,
        out bool hasGridPosition,
        out string failureReason)
    {
        gridPosition = default;
        hasGridPosition = false;
        failureReason = "";

        WorldSiteUnitPlacement activePlacement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        _battlePreparationDeploymentDragController.TryResolveDragContext(placementId, site, out BattlePreparationPlacementDragContext dragContext);
        Vector2I footprintSize = dragContext?.FootprintSize ?? ResolveUnitFootprintSize(activePlacement?.UnitTypeId);
        if (!TryResolveMouseFootprintAnchor(footprintSize, out gridPosition))
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        hasGridPosition = true;
        if (_isBattlePreparationActive && dragContext != null)
        {
            IReadOnlyList<GridPosition> dragFootprintCells = _battlePreparationDeploymentDragController.BuildFootprintCells(
                dragContext,
                gridPosition);
            if (!IsBattlePreparationFootprintOnValidTerrain(dragFootprintCells, dragContext.CanEnterWater, out failureReason))
            {
                return false;
            }

            SemanticDeploymentSide dragDeploymentSide = ResolveBattlePreparationDeploymentSide(dragContext.FactionId, dragContext.FallbackFaction);
            if (BattlePreparationDeploymentDragController.ShouldRestrictDeploymentZone(dragContext) &&
                !IsBattlePreparationFootprintDeployable(
                    dragFootprintCells,
                    dragDeploymentSide,
                    dragContext.FactionId,
                    ResolveBattlePreparationDeploymentDirection(
                        dragDeploymentSide,
                        dragContext.FactionId),
                    dragContext.CanEnterWater,
                    out failureReason))
            {
                return false;
            }

            if (IsBattlePreparationFootprintOccupied(dragFootprintCells, dragContext.ForceId, dragContext.ForceIndex))
            {
                failureReason = "placement_cell_occupied";
                return false;
            }

            return true;
        }

        if (!_deploymentTargetEvaluator.CanMoveToGridCell(
                _activeGridMap,
                site,
                definition,
                placementId,
                new Vector2I(gridPosition.X, gridPosition.Y),
                ResolvePlacementCanEnterWater,
                out failureReason))
        {
            return false;
        }

        IReadOnlyList<GridPosition> footprintCells = BuildSitePlacementFootprintCells(activePlacement, gridPosition);
        bool canEnterWater = ResolvePlacementCanEnterWater(activePlacement);
        SemanticDeploymentSide deploymentSide = ResolveBattlePreparationDeploymentSide(
            activePlacement?.FactionId,
            ResolveBattleFaction(activePlacement?.FactionId));
        WorldSiteAttackDirection direction = _isBattlePreparationActive
            ? ResolveBattlePreparationDeploymentDirection(deploymentSide, activePlacement?.FactionId)
            : activePlacement?.AttackDirection ?? _battlePreparationRequest?.AttackDirection ?? WorldSiteAttackDirection.Any;
        if (_isBattlePreparationActive &&
            !IsBattlePreparationFootprintDeployable(footprintCells, deploymentSide, activePlacement?.FactionId ?? "", direction, canEnterWater, out failureReason))
        {
            return false;
        }

        if (IsSiteDeploymentFootprintOccupied(site, placementId, footprintCells))
        {
            failureReason = "placement_cell_occupied";
            return false;
        }

        return true;
    }


    private void ClearSiteDeploymentDragPreview(Node2D entity)
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid);
        SetSitePlacementSelected(entity, false);
    }

    private static void RaiseDeploymentDragEntity(Node2D entity)
    {
        if (entity == null)
        {
            return;
        }

        // Deployment drags cross terrain/object layers, so the entity is temporarily above map rendering until drop/cancel restores surface sort.
        entity.ZAsRelative = false;
        entity.ZIndex = DeploymentDragZIndex;
    }

    private void RestoreDeploymentEntityRenderSort(Node2D entity)
    {
        if (entity is not BattleEntity battleEntity)
        {
            return;
        }

        GridOccupantComponent gridOccupant = battleEntity.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(battleEntity, gridOccupant.SurfacePosition);
    }

    private void ReturnDraggedPlacementToOrigin(Node2D entity)
    {
        if (entity == null)
        {
            return;
        }

        entity.GlobalPosition = _draggedPlacementOriginGlobalPosition;
        SetSitePlacementSelected(entity, false);
        RestoreDeploymentEntityRenderSort(entity);
    }

    private void RefreshSelectedSlotLabel(WorldSiteState site)
    {
        if (!string.IsNullOrWhiteSpace(_selectedPlacementId))
        {
            WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == _selectedPlacementId);
            _siteSelectionLabel.Text = placement == null
                ? ""
                : $"已选择：{BuildPlacementDisplayName(placement)}\n位置：{placement.CellX}, {placement.CellY}";
            return;
        }

        _siteSelectionLabel.Text = "";
    }

    private bool TryResolvePlacementUnderPointer(out string placementId)
    {
        placementId = "";
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);

        if (TryGetMouseGridPosition(out GridPosition gridPosition))
        {
            if (_isBattlePreparationActive &&
                TryResolveBattlePreparationPlacementUnderGrid(gridPosition, out placementId))
            {
                return true;
            }

            WorldSiteUnitPlacement placement = site?.UnitPlacements
                .Where(item => item.CellX == gridPosition.X && item.CellY == gridPosition.Y)
                .OrderByDescending(item => item.CellHeight)
                .FirstOrDefault();
            if (placement != null &&
                _sitePlacementEntities.TryGetValue(placement.PlacementId, out Node2D gridEntity) &&
                IsDeploymentDragEnabled(gridEntity, placement.PlacementId))
            {
                placementId = placement.PlacementId;
                return true;
            }
        }

        Vector2 pointerGlobal = GetWorldViewportMousePosition();
        float maxDistanceSquared = SitePlacementPickRadiusPixels * SitePlacementPickRadiusPixels;
        KeyValuePair<string, Node2D>? nearest = _sitePlacementEntities
            .Where(item =>
                item.Value != null &&
                IsDeploymentDragEnabled(item.Value, item.Key) &&
                item.Value.GlobalPosition.DistanceSquaredTo(pointerGlobal) <= maxDistanceSquared)
            .OrderBy(item => item.Value.GlobalPosition.DistanceSquaredTo(pointerGlobal))
            .Select(item => (KeyValuePair<string, Node2D>?)item)
            .FirstOrDefault();
        if (nearest.HasValue)
        {
            placementId = nearest.Value.Key;
            return true;
        }

        return false;
    }

    private bool TryResolveBattlePreparationPlacementUnderGrid(GridPosition gridPosition, out string placementId)
    {
        placementId = "";
        BattleForceRequest[] forces = (_battlePreparationRequest?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
            .Concat(_battlePreparationRequest?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>())
            .ToArray();
        foreach (BattleForceRequest force in forces)
        {
            for (int index = 0; index < (force?.PreferredPlacements?.Count ?? 0); index++)
            {
                BattleForcePlacementRequest placement = force.PreferredPlacements[index];
                if (placement == null)
                {
                    continue;
                }

                GridPosition anchor = new(placement.CellX, placement.CellY);
                if (!BuildBattlePreparationFootprintCells(force, anchor).Contains(gridPosition))
                {
                    continue;
                }

                if (_sitePlacementEntities.TryGetValue(placement.PlacementId, out Node2D entity) &&
                    IsDeploymentDragEnabled(entity, placement.PlacementId))
                {
                    placementId = placement.PlacementId;
                    return true;
                }
            }
        }

        return false;
    }


    private bool IsPointerOverSiteHud(InputEvent @event)
    {
        if (_siteHudRoot?.Visible != true)
        {
            return false;
        }

        Vector2 screenPosition = @event switch
        {
            InputEventMouseButton mouseButton => mouseButton.Position,
            InputEventMouseMotion mouseMotion => mouseMotion.Position,
            _ => new Vector2(float.NaN, float.NaN)
        };
        return !float.IsNaN(screenPosition.X) &&
               (IsScreenPointInsideControl(_sitePeacetimePanel, screenPosition) ||
                IsScreenPointInsideControl(_battlePreparationRosterDock, screenPosition) ||
                IsScreenPointInsideControl(_battlePreparationPlanBar, screenPosition) ||
                IsScreenPointInsideControl(_battlePreparationObjectiveThumbnailDock, screenPosition));
    }

    private static bool IsScreenPointInsideControl(Control control, Vector2 screenPosition)
    {
        if (!IsLiveNode(control))
        {
            return false;
        }

        return control.Visible && control.GetGlobalRect().HasPoint(screenPosition);
    }
}
