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
    private void OnFacilitySlotEntityPressed(string slotId)
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        FacilitySlotDefinition slot = definition?.FacilitySlots.FirstOrDefault(item => item.SlotId == slotId);
        if (site == null || slot == null)
        {
            StrategicWorldRuntime.LastNotice = "建筑点状态已失效。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            return;
        }

        _selectedPlacementId = "";
        _selectedFacilitySlotId = slot.SlotId;

        if (_siteFacilitySlotEntities.TryGetValue(slot.SlotId, out WorldFacilitySlotEntity slotEntity) &&
            slotEntity.HasConfigurationError)
        {
            string notice = $"{slot.DisplayName}配置错误：{slotEntity.ConfigurationError}";
            RefreshSiteManagementUi(notice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot selection blocked by configuration site={site.SiteId} slot={slot.SlotId} reason={slotEntity.ConfigurationError}");
            GetViewport().SetInputAsHandled();
            return;
        }

        FacilityInstance existingFacility = ResolveFacilityInSlot(site, slot.SlotId);
        if (existingFacility != null)
        {
            StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
            string facilityName = queries.GetFacility(existingFacility.FacilityId)?.DisplayName ?? existingFacility.FacilityId;
            string notice = $"{slot.DisplayName}已有{facilityName}，状态：{GetFacilityStateLabel(existingFacility.State)}。";
            RefreshSiteManagementUi(notice);
            GameLog.Info(nameof(WorldSiteRoot), $"Facility slot selected site={site.SiteId} slot={slot.SlotId} facility={existingFacility.FacilityId} state={existingFacility.State}");
            GetViewport().SetInputAsHandled();
            return;
        }

        IReadOnlyList<WorldActionViewModel> buildActions = ResolveBuildActionsForSlot(site, slot);
        if (buildActions.Count == 0)
        {
            string notice = $"{slot.DisplayName}暂时没有可建建筑。";
            RefreshSiteManagementUi(notice);
            GameLog.Info(nameof(WorldSiteRoot), $"Facility slot selected without action site={site.SiteId} slot={slot.SlotId}");
            GetViewport().SetInputAsHandled();
            return;
        }

        bool hasEnabledBuildAction = buildActions.Any(action => action.IsEnabled);
        string selectedNotice = hasEnabledBuildAction
            ? $"{slot.DisplayName}已选中。请在右侧选择要建造的建筑。"
            : $"{slot.DisplayName}已选中，但当前资源或条件不足。可在右侧查看不可建原因。";
        RefreshSiteManagementUi(selectedNotice);
        GameLog.Info(nameof(WorldSiteRoot), $"Facility slot selected for build site={site.SiteId} slot={slot.SlotId} actions={buildActions.Count} enabled={hasEnabledBuildAction}");
        GetViewport().SetInputAsHandled();
    }

    private void SelectPlacement(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        _selectedFacilitySlotId = "";
        RefreshSitePlacementUi("");
        GameLog.Info(nameof(WorldSiteRoot), $"Site placement selected site={_siteHudSiteId} placement={_selectedPlacementId}");
    }

    private void OnPlacementEntityPressed(string placementId)
    {
        _selectedPlacementId = placementId ?? "";
        _selectedFacilitySlotId = "";
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

    private bool TryHandleFacilitySlotInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            IsPointerOverSiteHud(@event))
        {
            return false;
        }

        if (!TryResolveFacilitySlotUnderPointer(out string slotId))
        {
            return false;
        }

        OnFacilitySlotEntityPressed(slotId);
        return true;
    }

    private bool TryHandleSiteContextClearInput(InputEvent @event)
    {
        if (_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            string.IsNullOrWhiteSpace(_selectedFacilitySlotId) ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            IsPointerOverSiteHud(@event) ||
            TryResolvePlacementUnderPointer(out _))
        {
            return false;
        }

        string previousSlotId = _selectedFacilitySlotId;
        _selectedPlacementId = "";
        _selectedFacilitySlotId = "";
        RefreshSiteManagementUi();
        GameLog.Info(nameof(WorldSiteRoot), $"Site facility slot selection cleared site={_siteHudSiteId} previousSlot={previousSlotId}");
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
            TryResolveBattlePreparationDragContext(placementId, site, out BattlePreparationPlacementDragContext dragContext))
        {
            if (TryMoveBattlePreparationPlacement(
                    dragContext,
                    site,
                    definition,
                    draggedEntity,
                    gridPosition,
                    out failureReason))
            {
                RefreshSitePlacementUi("閮ㄧ讲浣嶇疆宸叉洿鏂般€?");
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
                SyncBattlePreparationRequestPlacement(placementId, movedPlacement);
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
            RefreshBattlePreparationUi(notice);
            return;
        }

        RefreshSiteManagementUi(notice);
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

        TryResolveBattlePreparationDragContext(_draggedPlacementId, site, out BattlePreparationPlacementDragContext dragContext);
        WorldSiteUnitPlacement placement = dragContext?.SitePlacement ??
                                           site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == _draggedPlacementId);
        Vector2I footprintSize = dragContext?.FootprintSize ?? ResolveUnitFootprintSize(placement?.UnitTypeId);
        SetBattlePreparationDragFootprintPreview(
            hasGridPosition
                ? BuildSiteDeploymentDragFootprintCells(dragContext, placement, gridPosition)
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
        TryResolveBattlePreparationDragContext(placementId, site, out BattlePreparationPlacementDragContext dragContext);
        Vector2I footprintSize = dragContext?.FootprintSize ?? ResolveUnitFootprintSize(activePlacement?.UnitTypeId);
        if (!TryResolveMouseFootprintAnchor(footprintSize, out gridPosition))
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        hasGridPosition = true;
        if (_isBattlePreparationActive && dragContext != null)
        {
            IReadOnlyList<GridPosition> dragFootprintCells = BuildSiteDeploymentDragFootprintCells(
                dragContext,
                activePlacement,
                gridPosition);
            if (!IsBattlePreparationFootprintOnValidTerrain(dragFootprintCells, dragContext.CanEnterWater, out failureReason))
            {
                return false;
            }

            SemanticDeploymentSide dragDeploymentSide = ResolveBattlePreparationDeploymentSide(dragContext.FactionId, dragContext.FallbackFaction);
            if (ShouldRestrictBattlePreparationDeploymentZone(dragContext) &&
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

    private void ExecuteSiteAction(WorldActionViewModel action, string targetSlotId = "")
    {
        if (action == null)
        {
            return;
        }

        WorldActionRequest request = new()
        {
            ActionId = action.ActionId,
            ActorFactionId = StrategicWorldRuntime.State.PlayerFactionId,
            SourceSiteId = _siteHudSiteId,
            TargetSiteId = action.TargetSiteId,
            TargetSlotId = targetSlotId ?? ""
        };

        string returnScenePath = string.IsNullOrWhiteSpace(_siteHudReturnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : _siteHudReturnScenePath;
        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(ResolveSiteState(_siteHudSiteId));
        WorldActionResult result = _worldActionResolver.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            returnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath);

        StrategicWorldRuntime.LastNotice = result.Message;
        _battleLauncher.ApplyModeTransitionRollbackEvent(rollback, result.Events);
        if (!result.Success)
        {
            RefreshSiteManagementUi(result.Message);
            return;
        }

        if (result.BattleStartRequest != null)
        {
            WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
                StrategicWorldRuntime.State,
                result.BattleStartRequest,
                rollback,
                ApplyBattleStartRequest,
                ActivateBattleRuntime,
                () => _battleStartBlockedReason,
                ClearBattleEntities,
                null,
                enabled => SetBattleRuntimeEnabled(enabled));
            if (!launch.Success)
            {
                StrategicWorldRuntime.LastNotice = "无法进入自动战斗。";
                RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
                GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter site battle request={result.BattleStartRequest.RequestId} target={result.BattleStartRequest.TargetSiteId} reason={launch.FailureReason}");
            }

            return;
        }

        RefreshSiteManagementUi(result.Message);
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

        if (!string.IsNullOrWhiteSpace(_selectedFacilitySlotId))
        {
            WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId ?? _siteHudSiteId);
            FacilitySlotDefinition slot = definition?.FacilitySlots.FirstOrDefault(item => item.SlotId == _selectedFacilitySlotId);
            if (slot == null)
            {
                _siteSelectionLabel.Text = "";
                return;
            }

            if (_siteFacilitySlotEntities.TryGetValue(slot.SlotId, out WorldFacilitySlotEntity slotEntity) &&
                slotEntity.HasConfigurationError)
            {
                _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n配置错误：{slotEntity.ConfigurationError}";
                return;
            }

            StrategicWorldDefinitionQueries queries = new(StrategicWorldRuntime.Definition);
            FacilityInstance facility = ResolveFacilityInSlot(site, slot.SlotId);
            if (facility != null)
            {
                string facilityName = queries.GetFacility(facility.FacilityId)?.DisplayName ?? facility.FacilityId;
                _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n{facilityName} · {GetFacilityStateLabel(facility.State)}";
                return;
            }

            IReadOnlyList<WorldActionViewModel> buildActions = ResolveBuildActionsForSlot(site, slot);
            if (buildActions.Count == 0)
            {
                _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n暂无可建建筑。";
                return;
            }

            int enabledCount = buildActions.Count(action => action.IsEnabled);
            string state = enabledCount > 0
                ? $"可建 {enabledCount}/{buildActions.Count} 项。请在“可建建筑”中选择。"
                : $"当前 {buildActions.Count} 项建筑都不可建，请查看按钮原因。";
            _siteSelectionLabel.Text = $"已选择建筑点：{slot.DisplayName}\n{state}";
            return;
        }

        _siteSelectionLabel.Text = "";
    }

    private bool TryResolveFacilitySlotUnderPointer(out string slotId)
    {
        slotId = "";
        if (_siteFacilitySlotEntities.Count == 0 ||
            _activeSiteMap?.GetNodeOrNull<CanvasItem>(FacilitySlotsRootName)?.Visible == false)
        {
            return false;
        }

        if (TryGetMouseGridPosition(out GridPosition gridPosition))
        {
            KeyValuePair<string, WorldFacilitySlotRuntimeLayout>? layoutHit = _siteFacilitySlotLayouts
                .Where(item => item.Value.FootprintCells.Contains(gridPosition) &&
                               _siteFacilitySlotEntities.ContainsKey(item.Key))
                .OrderByDescending(item => item.Value.ZIndex)
                .Select(item => (KeyValuePair<string, WorldFacilitySlotRuntimeLayout>?)item)
                .FirstOrDefault();
            if (layoutHit.HasValue)
            {
                slotId = layoutHit.Value.Key;
                return true;
            }
        }

        return false;
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
               (IsScreenPointInsideControl(_siteHudTopBar, screenPosition) ||
                IsScreenPointInsideControl(_sitePeacetimePanel, screenPosition) ||
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
