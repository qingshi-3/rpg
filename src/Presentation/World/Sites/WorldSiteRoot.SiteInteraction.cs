using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
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

        if (!string.IsNullOrWhiteSpace(_draggedBattleForceId))
        {
            HandleBattlePreparationRosterDragInput(@event);
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

        WorldSiteUnitPlacement placement = site?.UnitPlacements.FirstOrDefault(item => item.PlacementId == _draggedPlacementId);
        Vector2I footprintSize = ResolveUnitFootprintSize(placement?.UnitTypeId);
        SetBattlePreparationDragFootprintPreview(
            hasGridPosition ? BuildSitePlacementFootprintCells(placement, gridPosition) : System.Array.Empty<GridPosition>());
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
        if (!TryResolveMouseFootprintAnchor(ResolveUnitFootprintSize(activePlacement?.UnitTypeId), out gridPosition))
        {
            failureReason = "placement_cell_invalid";
            return false;
        }

        hasGridPosition = true;
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
        WorldSiteAttackDirection direction = activePlacement?.AttackDirection ?? _battlePreparationRequest?.AttackDirection ?? WorldSiteAttackDirection.Any;
        if (!IsBattlePreparationFootprintDeployable(footprintCells, direction, canEnterWater, out failureReason))
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
            TargetSlotId = targetSlotId ?? "",
            ThreatId = action.ThreatId
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

    private string ResolveSelectedThreatId(WorldSiteState site)
    {
        return site?.PendingThreatIds
            .Select(id => StrategicWorldRuntime.State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .FirstOrDefault(threat => threat?.Stage == ThreatStage.Attacking)
            ?.Id ?? "";
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

    private void BeginBattlePreparationRosterDrag(BattleForceRequest force, int forceIndex)
    {
        if (!_isBattlePreparationActive || force == null || forceIndex < 0 || forceIndex >= force.Count)
        {
            return;
        }

        _draggedBattleForceId = force.ForceId ?? "";
        _draggedBattleForceIndex = forceIndex;
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
            BattleFaction.Player,
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
        BattleForceRequest force = FindBattlePreparationPlayerForce(forceId);
        bool placed = TryPlaceBattlePreparationRosterUnit(force, forceIndex, out string failureReason);
        ClearBattlePreparationRosterDragState();
        RefreshBattlePreparationUi(placed ? "单位已部署。" : FormatPlacementFailure(failureReason));
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationRosterDragEnded force={forceId} index={forceIndex} placed={placed} reason={failureReason}");
        GetViewport().SetInputAsHandled();
    }

    private void UpdateBattlePreparationRosterDragPreview()
    {
        BattleForceRequest force = FindBattlePreparationPlayerForce(_draggedBattleForceId);
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
        if (!IsBattlePreparationFootprintDeployable(
                footprintCells,
                _battlePreparationRequest?.AttackDirection ?? WorldSiteAttackDirection.Any,
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

    private BattleForceRequest FindBattlePreparationPlayerForce(string forceId)
    {
        return _battlePreparationRequest?.PlayerForces?
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
                IsScreenPointInsideControl(_sitePeacetimePanel, screenPosition));
    }

    private bool IsPointerOverSiteExplorationHud(InputEvent @event)
    {
        Vector2 screenPosition = @event switch
        {
            InputEventMouseButton mouseButton => mouseButton.Position,
            InputEventMouseMotion mouseMotion => mouseMotion.Position,
            _ => new Vector2(float.NaN, float.NaN)
        };

        return !float.IsNaN(screenPosition.X) &&
               IsScreenPointInsideControl(_siteExplorationHudPanel, screenPosition);
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
