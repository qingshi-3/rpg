using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Maps;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private const string StrategicCityBuildingMapEntityScenePath = "res://scenes/world/sites/StrategicCityBuildingMapEntity.tscn";

    private void RefreshSiteMapEntities(WorldSiteState site, WorldSiteDefinition definition)
    {
        RefreshStrategicCityBuildingEntities();
        ClearChildren(_sitePlacementEntityRoot);
        _sitePlacementEntities.Clear();
        ClearBattleEntities();

        if (site == null || definition == null)
        {
            return;
        }

        if (_unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot rebuild site management units because UnitRoot is missing site={site.SiteId}");
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteManagementInteractionsRebuilt site={site.SiteId} placements={site.UnitPlacements.Count} animated=0 canInteract={CanOpenSiteDetail(site)}");
            return;
        }

        bool legacyPlacementsVisible = ShouldRenderLegacySitePlacementUnits(site);
        if (!legacyPlacementsVisible)
        {
            _unitRoot.Visible = false;
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"SiteManagementInteractionsRebuilt site={site.SiteId} placements={site.UnitPlacements.Count} animated=0 canInteract={CanOpenSiteDetail(site)}");
            return;
        }

        int animatedCount = 0;
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements)
        {
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

        if (_unitRoot != null)
        {
            _unitRoot.Visible = true;
            _unitRoot.PlayIdleForActiveEntities();
        }

        UpdateSiteMapEntities();
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SiteManagementInteractionsRebuilt site={site.SiteId} placements={site.UnitPlacements.Count} animated={animatedCount} canInteract={CanOpenSiteDetail(site)}");
    }

    private bool ShouldRenderLegacySitePlacementUnits(WorldSiteState site)
    {
        if (site == null)
        {
            return false;
        }

        if (_isBattlePreparationActive || _battleRuntimeEnabled)
        {
            return true;
        }

        // Managed-city peacetime corps ownership comes from Strategic Management.
        // Legacy WorldSite placements are battle/deployment cache, not city garrison truth.
        if (CanOpenManagedCityDetail(_siteHudSiteId))
        {
            return false;
        }

        return true;
    }

    private void RefreshStrategicCityBuildingEntities()
    {
        // Confirmed buildings are a Strategic Management read model here; this
        // presentation layer must never become a second construction authority.
        ClearChildren(_cityBuildingRoot);
        _cityBuildingEntities.Clear();
        if (_cityBuildingRoot == null ||
            string.IsNullOrWhiteSpace(_siteHudSiteId) ||
            _coordinateLayer == null ||
            !TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId))
        {
            return;
        }

        StrategicManagementRuntime.EnsureInitialized();
        if (!StrategicManagementRuntime.State.Cities.TryGetValue(cityId, out StrategicCityState city))
        {
            return;
        }

        PackedScene entityScene = ResolveStrategicCityBuildingMapEntityScene();
        if (entityScene == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), "Cannot render strategic city buildings because building entity scene is missing.");
            return;
        }

        int renderedCount = 0;
        foreach (StrategicBuildingInstanceState building in city.Buildings)
        {
            if (building == null ||
                !StrategicManagementRuntime.Definitions.Buildings.TryGetValue(
                    building.BuildingDefinitionId ?? "",
                    out StrategicBuildingDefinition definition))
            {
                GameLog.Warn(
                    nameof(WorldSiteRoot),
                    $"Strategic city building render skipped because definition is missing instance={building?.BuildingInstanceId ?? ""} definition={building?.BuildingDefinitionId ?? ""}");
                continue;
            }

            if (!TryBuildStrategicBuildingDrawRect(building, definition, out Rect2 drawRect))
            {
                GameLog.Warn(
                    nameof(WorldSiteRoot),
                    $"Strategic city building render skipped because footprint is unavailable instance={building.BuildingInstanceId} grid={building.GridX},{building.GridY}");
                continue;
            }

            Node instance = entityScene.Instantiate();
            if (instance is not StrategicCityBuildingMapEntity entity)
            {
                instance?.QueueFree();
                GameLog.Warn(nameof(WorldSiteRoot), "Strategic city building entity scene root is not StrategicCityBuildingMapEntity.");
                continue;
            }

            string instanceId = building.BuildingInstanceId ?? "";
            entity.Name = $"{NormalizeNodeName(instanceId)}CityBuilding";
            Texture2D texture = string.IsNullOrWhiteSpace(definition.IconPath)
                ? null
                : GD.Load<Texture2D>(definition.IconPath);
            entity.SetBuilding(
                instanceId,
                definition.DisplayName,
                texture,
                drawRect);
            _cityBuildingRoot.AddChild(entity);
            _cityBuildingEntities[instanceId] = entity;
            renderedCount++;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"StrategicCityBuildingsRebuilt site={_siteHudSiteId} city={cityId} buildings={city.Buildings.Count} rendered={renderedCount}");
    }

    private PackedScene ResolveStrategicCityBuildingMapEntityScene()
    {
        StrategicCityBuildingMapEntityScene ??= GD.Load<PackedScene>(StrategicCityBuildingMapEntityScenePath);
        return StrategicCityBuildingMapEntityScene;
    }

    private bool TryBuildStrategicBuildingDrawRect(
        StrategicBuildingInstanceState building,
        StrategicBuildingDefinition definition,
        out Rect2 rect)
    {
        rect = default;
        if (building == null || definition == null)
        {
            return false;
        }

        IReadOnlyList<GridPosition> footprintCells = BuildStrategicBuildingFootprintCells(
            new GridPosition(building.GridX, building.GridY),
            new Vector2I(
                System.Math.Max(1, definition.FootprintWidth),
                System.Math.Max(1, definition.FootprintHeight)));
        return TryBuildPolygonBounds(footprintCells.Select(BuildCellPolygonGlobal), out rect);
    }

    private static bool TryBuildPolygonBounds(
        IEnumerable<Vector2[]> polygons,
        out Rect2 rect)
    {
        rect = default;
        Vector2[] points = (polygons ?? System.Array.Empty<Vector2[]>())
            .SelectMany(polygon => polygon ?? System.Array.Empty<Vector2>())
            .ToArray();
        if (points.Length == 0)
        {
            return false;
        }

        float minX = points.Min(point => point.X);
        float minY = points.Min(point => point.Y);
        float maxX = points.Max(point => point.X);
        float maxY = points.Max(point => point.Y);
        rect = new Rect2(minX, minY, maxX - minX, maxY - minY);
        return rect.Size.X > 0.001f && rect.Size.Y > 0.001f;
    }

    private static string NormalizeNodeName(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "Strategic" : value.Trim();
        return normalized
            .Replace(':', '_')
            .Replace('-', '_')
            .Replace(' ', '_');
    }

    private void ExtractSemanticMapMarkers(string mapId)
    {
        _semanticMapMarkers = _semanticMapMarkerExtractor.Extract(_activeSiteMap, mapId);
        foreach (string diagnostic in _semanticMapMarkers.Diagnostics)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"SemanticMapMarkerDiagnostic map={mapId} {diagnostic}");
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"SemanticMapMarkersExtracted map={mapId} markers={_semanticMapMarkers.Markers.Count} diagnostics={_semanticMapMarkers.Diagnostics.Count}");
    }

    private SemanticMapMarkerData[] ResolveSemanticDeploymentZoneMarkers()
    {
        if (_semanticMapMarkers?.Markers == null)
        {
            return System.Array.Empty<SemanticMapMarkerData>();
        }

        return _semanticMapMarkers.Markers
            .Where(marker => marker.MarkerType == SemanticMapMarkerType.DeploymentZone)
            .OrderBy(marker => marker.Priority)
            .ThenBy(marker => marker.MarkerId, System.StringComparer.Ordinal)
            .ToArray();
    }

    private SemanticMapMarkerData[] ResolveSemanticConstructionRegionMarkers()
    {
        if (_semanticMapMarkers?.Markers == null)
        {
            return System.Array.Empty<SemanticMapMarkerData>();
        }

        return _semanticMapMarkers.Markers
            .Where(marker => marker.MarkerType == SemanticMapMarkerType.ConstructionRegion)
            .OrderBy(marker => marker.Priority)
            .ThenBy(marker => marker.MarkerId, System.StringComparer.Ordinal)
            .ToArray();
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

        Vector2I footprintSize = ResolveUnitFootprintSize(placement.UnitTypeId);
        gridOccupant.GridX = placement.CellX;
        gridOccupant.GridY = placement.CellY;
        gridOccupant.GridHeight = placement.CellHeight;
        gridOccupant.FootprintWidth = footprintSize.X;
        gridOccupant.FootprintHeight = footprintSize.Y;
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
        if (TryGetFootprintCenterGlobalPosition(
                gridPosition,
                ResolveUnitFootprintSize(placement.UnitTypeId),
                out Vector2 globalPosition))
        {
            return globalPosition;
        }

        return new Vector2(96.0f, 128.0f + _sitePlacementEntities.Count * 32.0f);
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

    private static bool IsLiveNode(GodotObject node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return false;
        }

        return node is not Node queuedNode || !queuedNode.IsQueuedForDeletion();
    }

    private void RemoveDisposedSitePlacementEntityRefs()
    {
        if (_sitePlacementEntities.Count == 0)
        {
            return;
        }

        foreach (string placementId in _sitePlacementEntities
                     .Where(item => !IsLiveNode(item.Value))
                     .Select(item => item.Key)
                     .ToArray())
        {
            _sitePlacementEntities.Remove(placementId);
        }
    }
}
