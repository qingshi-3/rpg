using Godot;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private bool TryHandleStrategicBuildingPlacementInput(InputEvent @event)
    {
        if (string.IsNullOrWhiteSpace(_selectedStrategicBuildingDefinitionId))
        {
            return false;
        }

        if (@event is InputEventMouseMotion)
        {
            if (IsPointerOverSiteHud(@event))
            {
                ClearStrategicBuildingPlacementPreview();
                return false;
            }

            UpdateStrategicBuildingPlacementPreview();
            return false;
        }

        if (@event.IsActionPressed("ui_cancel") ||
            @event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            CancelStrategicBuildingPlacement();
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (_battleRuntimeEnabled ||
            _isBattlePreparationActive ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            IsPointerOverSiteHud(@event))
        {
            return false;
        }

        StrategicManagementRuntime.EnsureInitialized();
        if (!StrategicManagementRuntime.Definitions.Buildings.TryGetValue(
                _selectedStrategicBuildingDefinitionId,
                out StrategicBuildingDefinition building))
        {
            string missingBuildingId = _selectedStrategicBuildingDefinitionId;
            _selectedStrategicBuildingDefinitionId = "";
            ClearStrategicBuildingPlacementPreview();
            RefreshSiteManagementUi($"建筑放置失败：{StrategicManagementDashboardPanelBinder.FormatFailureReason(StrategicFailureReasons.MissingBuilding)}");
            GameLog.Warn(nameof(WorldSiteRoot), $"Strategic building placement missing definition building={missingBuildingId}");
            GetViewport().SetInputAsHandled();
            return true;
        }

        Vector2I footprintSize = new(
            System.Math.Max(1, building.FootprintWidth),
            System.Math.Max(1, building.FootprintHeight));
        if (!TryResolveMouseFootprintAnchor(footprintSize, out GridPosition gridPosition))
        {
            ClearStrategicBuildingPlacementPreview();
            RefreshSiteManagementUi("建筑放置失败：无法识别地图格子");
            GetViewport().SetInputAsHandled();
            return true;
        }

        TrySubmitStrategicBuildingPlacement(
            _selectedStrategicBuildingDefinitionId,
            gridPosition.X,
            gridPosition.Y);
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void TrySubmitStrategicBuildingPlacement(
        string buildingDefinitionId,
        int gridX,
        int gridY)
    {
        if (!TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            RefreshSiteManagementUi(BuildStrategicManagementCityUnavailableNotice(_siteHudSiteId));
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        if (!_strategicBuildingPlacementResolver.TryResolveStrategicBuildingPlacement(
                StrategicManagementRuntime.Definitions,
                StrategicManagementRuntime.State,
                StrategicManagementRuntime.Rules,
                cityId,
                buildingDefinitionId,
                ResolveSemanticConstructionRegionMarkers(),
                gridX,
                gridY,
                out string constructionRegionId,
                out string failureReason))
        {
            RefreshSiteManagementUi($"建筑放置失败：{StrategicManagementDashboardPanelBinder.FormatFailureReason(failureReason)}");
            return;
        }

        StrategicCommandResult result = StrategicManagementRuntime.Commands.BuildCityBuilding(
            StrategicManagementRuntime.State,
            cityId,
            buildingDefinitionId,
            constructionRegionId,
            gridX,
            gridY);
        if (result?.Success == true)
        {
            _selectedStrategicBuildingDefinitionId = "";
            ClearStrategicBuildingPlacementPreview();
            StrategicManagementRuntime.SaveCurrentState();
        }

        HandleStrategicManagementCommandResult("建设建筑", result);
    }

    private void UpdateStrategicBuildingPlacementPreview()
    {
        if (string.IsNullOrWhiteSpace(_selectedStrategicBuildingDefinitionId) ||
            !TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            ClearStrategicBuildingPlacementPreview();
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        if (!StrategicManagementRuntime.Definitions.Buildings.TryGetValue(
                _selectedStrategicBuildingDefinitionId,
                out StrategicBuildingDefinition building))
        {
            ClearStrategicBuildingPlacementPreview();
            return;
        }

        Vector2I footprintSize = new(
            System.Math.Max(1, building.FootprintWidth),
            System.Math.Max(1, building.FootprintHeight));
        if (!TryResolveMouseFootprintAnchor(footprintSize, out GridPosition gridPosition))
        {
            ClearStrategicBuildingPlacementPreview();
            return;
        }

        IReadOnlyList<GridPosition> footprintCells = BuildStrategicBuildingFootprintCells(gridPosition, footprintSize);
        bool buildable = _strategicBuildingPlacementResolver.TryResolveStrategicBuildingPlacement(
            StrategicManagementRuntime.Definitions,
            StrategicManagementRuntime.State,
            StrategicManagementRuntime.Rules,
            cityId,
            building.BuildingDefinitionId,
            ResolveSemanticConstructionRegionMarkers(),
            gridPosition.X,
            gridPosition.Y,
            out _,
            out _);
        Texture2D previewTexture = string.IsNullOrWhiteSpace(building.IconPath)
            ? null
            : GD.Load<Texture2D>(building.IconPath);
        SetStrategicBuildingPlacementPreview(footprintCells, buildable, previewTexture);
    }

    private void SetStrategicBuildingPlacementPreview(
        IReadOnlyList<GridPosition> footprintCells,
        bool buildable,
        Texture2D previewTexture)
    {
        if (_strategicBuildingPlacementPreview == null)
        {
            return;
        }

        // Reuse the picker icon's AtlasTexture so single-building atlas regions
        // and mouse-follow previews cannot drift apart.
        SuppressStrategicBuildingAutoHover();
        _strategicBuildingPlacementPreview.SetPreview(
            (footprintCells ?? System.Array.Empty<GridPosition>())
                .Select(BuildCellPolygonGlobal),
            buildable,
            previewTexture);
    }

    private void ClearStrategicBuildingPlacementPreview()
    {
        _strategicBuildingPlacementPreview?.ClearPreview();
        if (string.IsNullOrWhiteSpace(_selectedStrategicBuildingDefinitionId))
        {
            RestoreStrategicBuildingAutoHover();
        }
        else
        {
            SuppressStrategicBuildingAutoHover();
        }
    }

    private void SuppressStrategicBuildingAutoHover()
    {
        // Building placement owns its own footprint frame; the generic map hover
        // would otherwise add a misleading 1x1 white diamond under the preview.
        _highlightOverlay?.SetCells(BattleGridHighlightKind.Hover, System.Array.Empty<GridPosition>());
    }

    private void RestoreStrategicBuildingAutoHover()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover);
    }

    private void CancelStrategicBuildingPlacement()
    {
        _selectedStrategicBuildingDefinitionId = "";
        ClearStrategicBuildingPlacementPreview();
        RefreshSiteManagementUi("已取消建筑放置。");
    }

    private static IReadOnlyList<GridPosition> BuildStrategicBuildingFootprintCells(
        GridPosition anchor,
        Vector2I footprintSize)
    {
        List<GridPosition> cells = new();
        for (int y = 0; y < System.Math.Max(0, footprintSize.Y); y++)
        {
            for (int x = 0; x < System.Math.Max(0, footprintSize.X); x++)
            {
                cells.Add(new GridPosition(anchor.X + x, anchor.Y + y));
            }
        }

        return cells;
    }
}
