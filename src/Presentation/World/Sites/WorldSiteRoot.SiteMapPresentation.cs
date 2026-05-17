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
    private void RefreshSiteMapEntities(WorldSiteState site, WorldSiteDefinition definition)
    {
        ClearChildren(_sitePlacementEntityRoot);
        _sitePlacementEntities.Clear();
        _siteFacilitySlotEntities.Clear();
        _siteFacilitySlotLayouts.Clear();
        ClearBattleEntities();

        if (site == null || definition == null)
        {
            return;
        }

        RefreshFacilitySlotEntities(site, definition);

        if (_unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot rebuild site management units because UnitRoot is missing site={site.SiteId}");
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteManagementInteractionsRebuilt site={site.SiteId} facility_slots={_siteFacilitySlotEntities.Count} placements={site.UnitPlacements.Count} animated=0 canInteract={CanOpenSiteDetail(site)}");
            return;
        }

        int animatedCount = 0;
        bool explorationActive = IsSiteExplorationActive(site, definition);
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
            if (explorationActive && IsPlayerArmySitePlacement(placement))
            {
                continue;
            }

            BattleEntity entity = CreateSitePlacementUnitEntity(placement, site);
            if (entity == null)
            {
                continue;
            }

            string placementId = placement.PlacementId;
            _unitRoot.AddChild(entity);
            entity.GlobalPosition = ResolvePlacementEntityGlobalPosition(placement);
            ConfigureSitePlacementUnitEntity(entity, placement, CanOpenSiteDetail(site));
            _sitePlacementEntities[placementId] = entity;
            animatedCount++;
        }

        RefreshSiteExplorationPresentation(site, definition);

        if (_unitRoot != null)
        {
            _unitRoot.Visible = true;
            _unitRoot.PlayIdleForActiveEntities();
        }

        UpdateSiteMapEntities();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteManagementInteractionsRebuilt site={site.SiteId} facility_slots={_siteFacilitySlotEntities.Count} placements={site.UnitPlacements.Count} animated={animatedCount} canInteract={CanOpenSiteDetail(site)}");
    }

    private void RefreshFacilitySlotEntities(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (_sitePlacementEntityRoot == null || site == null || definition == null)
        {
            return;
        }

        Node slotsRoot = _activeSiteMap?.GetNodeOrNull<Node>(FacilitySlotsRootName);
        if (slotsRoot == null)
        {
            if (definition.FacilitySlots.Count > 0)
            {
                GameLog.Warn(nameof(WorldSiteRoot), $"Missing facility slot scene root site={site.SiteId} root={FacilitySlotsRootName}");
            }

            return;
        }

        var occupiedCells = new Dictionary<GridPosition, string>();
        foreach (WorldFacilitySlotEntity entity in slotsRoot.GetChildren().OfType<WorldFacilitySlotEntity>())
        {
            entity.SetFootprintPolygons(System.Array.Empty<Vector2[]>());
            string slotId = string.IsNullOrWhiteSpace(entity.SlotId) ? entity.Name : entity.SlotId;
            FacilitySlotDefinition slot = definition.FacilitySlots.FirstOrDefault(item => item.SlotId == slotId);
            if (slot == null)
            {
                entity.BindState(slotId, false, false, false, false, false, "slot_definition_missing");
                GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot scene node has no definition site={site.SiteId} slot={slotId}");
                continue;
            }

            string configurationError = "";
            if (!TrySnapFacilitySlotEntity(entity, slot, occupiedCells, out WorldFacilitySlotRuntimeLayout layout, out string layoutFailureReason))
            {
                configurationError = layoutFailureReason;
                GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot layout invalid site={site.SiteId} slot={slot.SlotId} reason={layoutFailureReason}");
            }
            else
            {
                _siteFacilitySlotLayouts[slot.SlotId] = layout;
            }

            FacilityInstance facility = ResolveFacilityInSlot(site, slot.SlotId);
            IReadOnlyList<WorldActionViewModel> buildActions = facility == null
                ? ResolveBuildActionsForSlot(site, slot)
                : System.Array.Empty<WorldActionViewModel>();
            int enabledBuildActionCount = buildActions.Count(action => action.IsEnabled);
            bool canInteract = string.IsNullOrWhiteSpace(configurationError) &&
                               (facility != null || buildActions.Count > 0);

            entity.BindState(
                slot.SlotId,
                facility != null,
                facility?.State == FacilityState.Building,
                enabledBuildActionCount > 0,
                canInteract,
                _selectedFacilitySlotId == slot.SlotId,
                configurationError,
                BuildFacilitySlotMapHint(facility, buildActions.Count, enabledBuildActionCount, configurationError));
            _siteFacilitySlotEntities[slot.SlotId] = entity;
        }

        foreach (FacilitySlotDefinition slot in definition.FacilitySlots)
        {
            if (!_siteFacilitySlotEntities.ContainsKey(slot.SlotId))
            {
                GameLog.Warn(nameof(WorldSiteRoot), $"Facility slot definition has no scene entity site={site.SiteId} slot={slot.SlotId}");
            }
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"FacilitySlotsRegistered site={site.SiteId} rootVisible={(slotsRoot is CanvasItem item ? item.Visible : null)} definitions={definition.FacilitySlots.Count} sceneEntities={_siteFacilitySlotEntities.Count} layouts={_siteFacilitySlotLayouts.Count} footprints={BuildFacilitySlotFootprintLogSummary()} selectedSlot={_selectedFacilitySlotId}");
    }

    private static string BuildFacilitySlotMapHint(
        FacilityInstance facility,
        int buildActionCount,
        int enabledBuildActionCount,
        string configurationError)
    {
        if (!string.IsNullOrWhiteSpace(configurationError))
        {
            return "配置错误";
        }

        if (facility != null)
        {
            return facility.State == FacilityState.Building ? "建造中" : "已建";
        }

        if (enabledBuildActionCount > 0)
        {
            return $"可建 {enabledBuildActionCount}";
        }

        return buildActionCount > 0 ? "条件不足" : "";
    }

    private void UpdateSiteMapEntities()
    {
        if (_siteHudRoot?.Visible != true || string.IsNullOrWhiteSpace(_siteHudSiteId))
        {
            return;
        }

        if (_unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (site == null)
        {
            return;
        }

        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
            if (_sitePlacementEntities.TryGetValue(placement.PlacementId, out Node2D entity) &&
                IsLiveNode(entity) &&
                placement.PlacementId != _draggedPlacementId)
            {
                entity.GlobalPosition = ResolvePlacementEntityGlobalPosition(placement);
                if (entity is BattleEntity battleEntity)
                {
                    SyncSitePlacementGridOccupant(battleEntity, placement);
                }
            }
        }

        RemoveDisposedSitePlacementEntityRefs();

        SyncSiteExplorationMarkerPositions(site);
    }

    private BattleEntity CreateSitePlacementUnitEntity(WorldSiteUnitPlacement placement, WorldSiteState site)
    {
        if (placement == null)
        {
            return null;
        }

        var force = new BattleForceRequest
        {
            ForceId = placement.PlacementId,
            SourceKind = placement.SourceKind,
            SourceId = placement.SourceId,
            UnitDefinitionId = placement.UnitTypeId,
            Count = 1,
            FactionId = placement.FactionId
        };
        force.PreferredPlacements.Add(new BattleForcePlacementRequest
        {
            PlacementId = placement.PlacementId,
            CellX = placement.CellX,
            CellY = placement.CellY,
            CellHeight = placement.CellHeight
        });

        var fallbackPosition = new GridPosition(placement.CellX, placement.CellY);
        BattleEntity entity = _battleUnitFactory.Create(
            force,
            0,
            ResolveBattleFaction(placement.FactionId),
            fallbackPosition);
        if (entity == null)
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Site placement unit skipped because animated unit could not be created site={site?.SiteId ?? ""} placement={placement.PlacementId} unit={placement.UnitTypeId}");
            return null;
        }

        entity.Name = $"{placement.PlacementId.Replace(':', '_').Replace('-', '_')}SiteUnit";
        return entity;
    }

    private void ConfigureSitePlacementUnitEntity(BattleEntity entity, WorldSiteUnitPlacement placement, bool dragEnabled)
    {
        if (entity == null || placement == null)
        {
            return;
        }

        entity.InputPickable = false;
        SetDeploymentDragComponent(entity, placement.PlacementId, dragEnabled);
        entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        entity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(placement.PlacementId == _selectedPlacementId);

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant != null)
        {
            SyncSitePlacementGridOccupant(entity, placement);
        }
    }

    private void SyncSitePlacementGridOccupant(BattleEntity entity, WorldSiteUnitPlacement placement)
    {
        if (entity == null || placement == null)
        {
            return;
        }

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        gridOccupant.GridX = placement.CellX;
        gridOccupant.GridY = placement.CellY;
        gridOccupant.GridHeight = placement.CellHeight;
        gridOccupant.UseExplicitHeight = placement.CellHeight > 0;
        ResolveEntitySurfaceHeight(gridOccupant);
        ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
    }

    private static void SetSitePlacementSelected(Node2D entity, bool selected)
    {
        if (entity is BattleEntity battleEntity)
        {
            battleEntity.GetComponent<BattleUnitPresentationComponent>()?.SetSelected(selected);
        }
    }

    private Vector2 ResolvePlacementEntityGlobalPosition(WorldSiteUnitPlacement placement)
    {
        GridPosition gridPosition = new(placement.CellX, placement.CellY);
        if (TryGetCellGlobalPosition(gridPosition, out Vector2 globalPosition))
        {
            return globalPosition;
        }

        return GlobalPosition + new Vector2(96.0f, 128.0f + _sitePlacementEntities.Count * 32.0f);
    }

    private bool TrySnapFacilitySlotEntity(
        WorldFacilitySlotEntity entity,
        FacilitySlotDefinition slot,
        Dictionary<GridPosition, string> occupiedCells,
        out WorldFacilitySlotRuntimeLayout layout,
        out string failureReason)
    {
        layout = null;
        failureReason = "";
        if (entity == null || slot == null)
        {
            failureReason = "slot_entity_missing";
            return false;
        }

        if (_coordinateLayer == null || _activeGridMap == null)
        {
            failureReason = "grid_missing";
            return false;
        }

        if (!TryResolveGridCellAtGlobalPosition(entity.GlobalPosition, out GridPosition anchorCell))
        {
            failureReason = "slot_anchor_cell_invalid";
            return false;
        }

        Vector2I footprintSize = ResolveFacilitySlotFootprintSize(entity);
        var footprintCells = new List<GridPosition>(footprintSize.X * footprintSize.Y);
        for (int y = 0; y < footprintSize.Y; y++)
        {
            for (int x = 0; x < footprintSize.X; x++)
            {
                GridPosition cell = new(anchorCell.X + x, anchorCell.Y + y);
                if (!_activeGridMap.TryGetCell(cell, out _))
                {
                    failureReason = $"footprint_cell_missing anchor={anchorCell} size={footprintSize.X}x{footprintSize.Y} cell={cell}";
                    return false;
                }

                footprintCells.Add(cell);
            }
        }

        foreach (GridPosition cell in footprintCells)
        {
            if (occupiedCells.TryGetValue(cell, out string occupiedSlotId))
            {
                failureReason = $"footprint_overlap other={occupiedSlotId} cell={cell}";
                return false;
            }
        }

        GridPosition sortCell = ResolveFacilitySlotSortCell(entity, footprintCells);
        GridSurfacePosition sortSurface = ResolveFacilitySlotSortSurface(sortCell);
        if (!TryGetCellGlobalPosition(anchorCell, out Vector2 rootGlobalPosition))
        {
            failureReason = $"anchor_cell_invalid cell={anchorCell}";
            return false;
        }

        entity.ApplySnappedLayout(rootGlobalPosition);
        entity.SetFootprintPolygons(footprintCells.Select(BuildCellPolygonGlobal).ToArray());
        int zIndex = ApplyFacilitySlotRenderSort(entity, sortSurface);

        layout = new WorldFacilitySlotRuntimeLayout
        {
            SlotId = slot.SlotId,
            SortCell = sortCell,
            SortSurface = sortSurface,
            FootprintWidth = footprintSize.X,
            FootprintHeight = footprintSize.Y,
            ZIndex = zIndex
        };
        layout.FootprintCells.AddRange(footprintCells);

        foreach (GridPosition cell in footprintCells)
        {
            occupiedCells[cell] = slot.SlotId;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Facility slot footprint snapped slot={slot.SlotId} anchor={anchorCell} size={footprintSize.X}x{footprintSize.Y} cells={footprintCells.Count} sort={sortSurface}");
        return true;
    }

    private static Vector2I ResolveFacilitySlotFootprintSize(WorldFacilitySlotEntity entity)
    {
        return new Vector2I(
            System.Math.Clamp(entity?.FootprintWidth ?? 1, 1, 12),
            System.Math.Clamp(entity?.FootprintHeight ?? 1, 1, 12));
    }

    private string BuildFacilitySlotFootprintLogSummary()
    {
        if (_siteFacilitySlotLayouts.Count == 0)
        {
            return "";
        }

        return string.Join(
            ",",
            _siteFacilitySlotLayouts.Values
                .OrderBy(layout => layout.SlotId)
                .Select(layout => $"{layout.SlotId}:{layout.FootprintWidth}x{layout.FootprintHeight}"));
    }

    private Vector2[] BuildCellPolygonGlobal(GridPosition cell)
    {
        var origin = new Vector2I(cell.X, cell.Y);
        Vector2 center = _coordinateLayer.MapToLocal(origin);
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => _coordinateLayer.ToGlobal(point))
            .ToArray();
    }

    private bool TryResolveGridCellAtGlobalPosition(Vector2 globalPosition, out GridPosition gridPosition)
    {
        gridPosition = default;
        if (_coordinateLayer == null || _activeGridMap == null)
        {
            return false;
        }

        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(globalPosition));
        gridPosition = new GridPosition(tilePosition.X, tilePosition.Y);
        return _activeGridMap.TryGetCell(gridPosition, out _);
    }

    private GridPosition ResolveFacilitySlotSortCell(WorldFacilitySlotEntity entity, IReadOnlyList<GridPosition> footprintCells)
    {
        if (entity?.UseLowestFootprintCellAsSortAnchor != false || !TryResolveGridCellAtGlobalPosition(entity.GlobalPosition, out GridPosition rootCell))
        {
            return footprintCells
                .OrderByDescending(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .First();
        }

        return rootCell;
    }

    private GridSurfacePosition ResolveFacilitySlotSortSurface(GridPosition sortCell)
    {
        if (_activeGridMap?.TryGetTopSurfacePosition(sortCell, out GridSurfacePosition topSurface) == true)
        {
            return topSurface;
        }

        if (_activeGridMap?.TryGetCell(sortCell, out GridCell cell) == true)
        {
            return new GridSurfacePosition(sortCell, cell.Height);
        }

        return new GridSurfacePosition(sortCell, 0);
    }

    private int ApplyFacilitySlotRenderSort(WorldFacilitySlotEntity entity, GridSurfacePosition sortSurface)
    {
        int zIndex = ResolveFacilitySlotZIndex(sortSurface);
        if (entity == null)
        {
            return zIndex;
        }

        entity.ZAsRelative = false;
        entity.ZIndex = zIndex;
        return zIndex;
    }

    private int ResolveFacilitySlotZIndex(GridSurfacePosition sortSurface)
    {
        int zIndex = BattleRenderSortPolicy.GetUnitZIndex(sortSurface.Height);
        if (_activeSiteMap is not BattleMapView battleMapView)
        {
            return zIndex;
        }

        if (battleMapView.RenderSortCache?.TryGetYSortOriginUnitZIndex(sortSurface, out int ySortOriginZIndex) == true)
        {
            return ySortOriginZIndex;
        }

        return TryResolveObjectLayerZIndex(battleMapView, sortSurface, out int objectLayerZIndex)
            ? objectLayerZIndex
            : zIndex;
    }

    private static bool TryResolveObjectLayerZIndex(BattleMapView mapView, GridSurfacePosition sortSurface, out int zIndex)
    {
        zIndex = 0;
        if (mapView == null)
        {
            return false;
        }

        Vector2I tilePosition = new(sortSurface.X, sortSurface.Y);
        bool foundExactObjectTile = false;
        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView)
                     .Where(layer => layer.Role == LayerRole.Object && layer.Height == sortSurface.Height))
        {
            TileData tileData = layer.GetCellTileData(tilePosition);
            if (tileData == null)
            {
                continue;
            }

            int candidateZIndex = layer.ZIndex + tileData.ZIndex;
            zIndex = foundExactObjectTile ? Mathf.Max(zIndex, candidateZIndex) : candidateZIndex;
            foundExactObjectTile = true;
        }

        if (foundExactObjectTile)
        {
            return true;
        }

        bool foundObjectLayer = false;
        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView)
                     .Where(layer => layer.Role == LayerRole.Object && layer.Height == sortSurface.Height))
        {
            zIndex = foundObjectLayer ? Mathf.Max(zIndex, layer.ZIndex) : layer.ZIndex;
            foundObjectLayer = true;
        }

        return foundObjectLayer;
    }
}
