using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void ResolveMainWorldViewportNodes()
    {
        _mainWorldViewportHost = GetNodeOrNull<Control>(MainWorldViewportHostPath);
        _mainWorldViewport = GetNodeOrNull<SubViewport>(MainWorldViewportPath);
        _worldMapOverlay = GetNodeOrNull<Control>(WorldMapOverlayPath);

        if (_mainWorldViewportHost == null || _mainWorldViewport == null || _worldMapOverlay == null)
        {
            GameLog.Error(
                nameof(StrategicWorldRoot),
                $"MainWorldViewportMissing host={_mainWorldViewportHost != null} viewport={_mainWorldViewport != null} overlay={_worldMapOverlay != null}");
            return;
        }

        _mainWorldViewportHost.MouseFilter = MouseFilterEnum.Pass;
        _worldMapOverlay.MouseFilter = MouseFilterEnum.Pass;
        if (!_worldMapOverlaySignalsConnected)
        {
            // Godot C# logs an error when event -= disconnects a callable that was
            // never connected, so overlay signals are connected once per root instance.
            _worldMapOverlay.GuiInput += OnWorldMapOverlayGuiInput;
            _worldMapOverlay.Draw += DrawWorldMapOverlay;
            _worldMapOverlaySignalsConnected = true;
        }

        _worldMapDynamicOverlay = GetNodeOrNull<Control>(new NodePath("MainWorldViewportHost/MainWorldViewport/WorldMapOverlay/WorldMapDynamicOverlay"));
        if (_worldMapDynamicOverlay == null)
        {
            _worldMapDynamicOverlay = new Control
            {
                Name = "WorldMapDynamicOverlay",
                MouseFilter = MouseFilterEnum.Ignore
            };
            _worldMapOverlay.AddChild(_worldMapDynamicOverlay);
        }

        _worldMapDynamicOverlay.MouseFilter = MouseFilterEnum.Ignore;
        _worldMapDynamicOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _worldMapDynamicOverlay.Position = Vector2.Zero;
        _worldMapDynamicOverlay.Size = _worldMapOverlay.Size;
        if (!_worldMapDynamicOverlaySignalsConnected)
        {
            _worldMapDynamicOverlay.Draw += DrawWorldMapDynamicOverlay;
            _worldMapDynamicOverlaySignalsConnected = true;
        }
    }

    private void ResolveWorldMapNodes()
    {
        // The world map subtree is authored in StrategicWorldMap.tscn; the root
        // only binds the instance and does not rebuild missing map nodes at runtime.
        _worldMapRoot = GetNodeOrNull<Node2D>(WorldMapRootPath);
        if (_worldMapRoot == null)
        {
            GameLog.Error(nameof(StrategicWorldRoot), $"WorldMapRootMissing path={WorldMapRootPath}");
            return;
        }

        _worldMapRoot.ZIndex = System.Math.Min(_worldMapRoot.ZIndex, -20);

        _siteAnchorRoot = GetNodeOrNull<Node2D>(SiteAnchorRootPath);
        _siteVisualLayer = GetNodeOrNull<TileMapLayer>(SiteVisualLayerPath);
        _armySpawnPointRoot = GetNodeOrNull<Node2D>(ArmySpawnPointRootPath);
        _ = GetNodeOrNull<Node2D>(EncounterZoneRootPath);
        EnsureStrategicNavigationLayerIsStable();
    }

    private void EnsureStrategicNavigationLayerIsStable()
    {
        _strategicNavigationRoot =
            _mainWorldViewport?.GetNodeOrNull<Node2D>(StrategicNavigationRootName) ??
            GetNodeOrNull<Node2D>(StrategicNavigationRootName);
        if (_strategicNavigationRoot == null)
        {
            _strategicNavigationRoot = new Node2D
            {
                Name = StrategicNavigationRootName
            };
            Node navigationParent = _mainWorldViewport != null ? _mainWorldViewport : this;
            navigationParent.AddChild(_strategicNavigationRoot);
        }

        _strategicNavigationRoot.Visible = true;
        _strategicNavigationRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _strategicNavigationRoot.GlobalPosition = Vector2.Zero;
        _strategicNavigationRoot.GlobalScale = Vector2.One;
        _strategicNavigationRoot.GlobalRotation = 0.0f;

        _strategicNavigationTileLayer =
            _strategicNavigationRoot.GetNodeOrNull<TileMapLayer>(StrategicNavigationLayerName) ??
            _worldMapRoot?.GetNodeOrNull<TileMapLayer>(StrategicNavigationLayerName) ??
            _strategicNavigationRoot.GetNodeOrNull<TileMapLayer>(StrategicNavigationTileLayerName) ??
            _worldMapRoot?.GetNodeOrNull<TileMapLayer>(StrategicNavigationTileLayerName);
        if (_strategicNavigationTileLayer == null)
        {
            return;
        }

        if (_strategicNavigationTileLayer.GetParent() != _strategicNavigationRoot)
        {
            Transform2D navigationLayerTransform = _strategicNavigationTileLayer.Transform;
            _strategicNavigationTileLayer.Reparent(_strategicNavigationRoot, false);
            _strategicNavigationTileLayer.Transform = navigationLayerTransform;
            GameLog.Info(
                nameof(StrategicWorldRoot),
                $"StrategicNavigationLayerStabilized layer={_strategicNavigationTileLayer.Name} parent={StrategicNavigationRootName}");
        }

        _strategicNavigationTileLayer.Visible = true;
    }

    private void ResolveWorldCamera()
    {
        _worldCamera = GetNodeOrNull<MapCameraController>(WorldCameraPath);
        if (_worldCamera == null)
        {
            GameLog.Error(nameof(StrategicWorldRoot), $"WorldCameraMissing path={WorldCameraPath}");
            return;
        }

        _worldCamera.UseViewportCamera = false;
        _worldCamera.Enabled = false;
        _worldCamera.ProcessPriority = -20;
    }

    private void ConfigureWorldCamera()
    {
        if (_worldCamera == null)
        {
            return;
        }

        _worldCamera.SetViewportSizeOverride(GetMapBounds().Size);
        if (TryCalculateStrategicMapBounds(out Rect2 mapBounds))
        {
            _worldCamera.SetMapBounds(mapBounds);
            if (_worldCamera.GlobalPosition == Vector2.Zero)
            {
                _worldCamera.FocusOn(mapBounds.GetCenter());
            }

            GameLog.Info(nameof(StrategicWorldRoot), $"StrategicWorldCameraConfigured bounds={mapBounds}");
            return;
        }

        if (_worldCamera.ClearMapBoundsAndApplyConfiguredFallback("strategic_map_bounds_missing"))
        {
            GameLog.Warn(nameof(StrategicWorldRoot), "StrategicWorldCameraUsingConfiguredBoundsFallback");
            return;
        }

        _worldCamera.ClearMapBounds();
        GameLog.Warn(nameof(StrategicWorldRoot), "StrategicWorldCameraBoundsMissing");
    }

    private bool UpdateWorldCameraView(bool force = false)
    {
        if (_worldCamera == null || _worldMapRoot == null)
        {
            return false;
        }

        Rect2 mapViewBounds = GetMapBounds();
        _worldCamera.SetViewportSizeOverride(mapViewBounds.Size);

        Vector2 zoom = _worldCamera.Zoom;
        _worldMapRoot.GlobalScale = zoom;
        _worldMapRoot.GlobalPosition = mapViewBounds.Size * 0.5f - _worldCamera.GlobalPosition * zoom;

        bool changed = force ||
                       _lastWorldMapRootPosition.DistanceSquaredTo(_worldMapRoot.GlobalPosition) > 0.001f ||
                       _lastWorldMapRootScale.DistanceSquaredTo(_worldMapRoot.GlobalScale) > 0.0001f;
        if (!changed)
        {
            return false;
        }

        _lastWorldMapRootPosition = _worldMapRoot.GlobalPosition;
        _lastWorldMapRootScale = _worldMapRoot.GlobalScale;
        if (_worldMapOverlay != null)
        {
            // Panning only needs a transform sync for the shared world canvas; the
            // viewport sizing/layout stays on the slower rebuild path so drag does
            // not relayout the world UI tree every frame.
            _worldMapOverlay.Position = _worldMapRoot.GlobalPosition;
            _worldMapOverlay.Scale = _worldMapRoot.GlobalScale;
            if (_worldMapDynamicOverlay != null)
            {
                _worldMapDynamicOverlay.Position = Vector2.Zero;
                _worldMapDynamicOverlay.Scale = Vector2.One;
            }
        }

        SyncSiteNameOverlay();

        return true;
    }

    private void QueueStrategicOverlayRedraw()
    {
        _worldMapDynamicOverlay?.QueueRedraw();
    }

    private void QueueStrategicStaticOverlayRedraw()
    {
        _worldMapOverlay?.QueueRedraw();
    }

    private void UpdateMainWorldViewportLayout(Rect2 mapBounds)
    {
        Vector2 viewportSize = new(
            Mathf.Max(1.0f, mapBounds.Size.X),
            Mathf.Max(1.0f, mapBounds.Size.Y));
        Vector2I viewportSizeI = new(
            Mathf.RoundToInt(viewportSize.X),
            Mathf.RoundToInt(viewportSize.Y));

        if (_mainWorldViewport != null && _mainWorldViewport.Size != viewportSizeI)
        {
            _mainWorldViewport.Size = viewportSizeI;
        }

        if (_worldMapOverlay == null)
        {
            return;
        }

        _worldMapOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _worldMapOverlay.Position = Vector2.Zero;
        _worldMapOverlay.Size = viewportSize;
        if (_worldMapDynamicOverlay != null)
        {
            _worldMapDynamicOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            _worldMapDynamicOverlay.Position = Vector2.Zero;
            _worldMapDynamicOverlay.Size = viewportSize;
        }
    }

    private bool TryCalculateStrategicMapBounds(out Rect2 bounds)
    {
        bounds = default;
        bool hasPoint = false;

        foreach (TileMapLayer layer in GetStrategicMapTileLayers())
        {
            foreach (Vector2I cell in layer.GetUsedCells())
            {
                foreach (Vector2 point in BuildTileCellMapPolygon(layer, cell))
                {
                    ExpandBounds(point, ref bounds, ref hasPoint);
                }
            }
        }

        if (_siteVisualLayer != null)
        {
            foreach (Vector2I cell in _siteVisualLayer.GetUsedCells())
            {
                foreach (Vector2 point in BuildTileCellMapPolygon(_siteVisualLayer, cell))
                {
                    ExpandBounds(point, ref bounds, ref hasPoint);
                }
            }
        }

        return hasPoint;
    }

    private Vector2[] BuildTileCellMapPolygon(TileMapLayer layer, Vector2I cell)
    {
        Vector2 center = layer.MapToLocal(cell);
        Vector2 stepX = layer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = layer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return new[]
        {
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[0])),
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[1])),
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[2])),
            _worldMapRoot.ToLocal(layer.ToGlobal(localPoints[3]))
        };
    }

    private IEnumerable<TileMapLayer> GetStrategicMapTileLayers()
    {
        if (_worldMapRoot == null)
        {
            yield break;
        }

        foreach (Node child in _worldMapRoot.GetChildren())
        {
            if (child is TileMapLayer tileMapLayer)
            {
                yield return tileMapLayer;
            }
        }
    }

    private static string FormatArmyUnitsForLog(WorldArmyState army)
    {
        return army?.GarrisonUnits == null
            ? "none"
            : string.Join(",", army.GarrisonUnits.Where(unit => unit != null).Select(unit => $"{unit.UnitTypeId}:{unit.Count}"));
    }

    private static void ExpandBounds(Vector2 point, ref Rect2 bounds, ref bool hasPoint)
    {
        if (!hasPoint)
        {
            bounds = new Rect2(point, Vector2.Zero);
            hasPoint = true;
            return;
        }

        bounds = bounds.Expand(point);
    }

    private void RebuildSiteVisualFootprints()
    {
        _siteVisualFootprints.Clear();
        if (Definition == null || _worldMapRoot == null)
        {
            return;
        }

        if (_siteVisualLayer == null)
        {
            ReportSiteVisualFootprintFailure("layer", "missing_site_visual_layer");
            return;
        }

        HashSet<Vector2I> usedCells = new();
        foreach (Vector2I cell in _siteVisualLayer.GetUsedCells())
        {
            usedCells.Add(cell);
        }

        if (usedCells.Count == 0)
        {
            ReportSiteVisualFootprintFailure("layer", "empty_site_visual_layer");
            return;
        }

        foreach (WorldSiteDefinition site in Definition.SiteDefinitions)
        {
            if (TryBuildSiteVisualFootprint(site, usedCells, out SiteVisualFootprint footprint, out string failureReason))
            {
                _siteVisualFootprints[site.Id] = footprint;
                continue;
            }

            ReportSiteVisualFootprintFailure(site?.Id ?? "", failureReason);
        }

        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"SiteVisualFootprintsBuilt count={_siteVisualFootprints.Count} layer={_siteVisualLayer.Name}");
    }

    private bool TryBuildSiteVisualFootprint(
        WorldSiteDefinition site,
        HashSet<Vector2I> usedCells,
        out SiteVisualFootprint footprint,
        out string failureReason)
    {
        footprint = null;
        failureReason = "";
        if (site == null)
        {
            failureReason = "missing_site_definition";
            return false;
        }

        Vector2 mapPosition = GetSiteMapPosition(site);
        Vector2 layerLocalPosition = _siteVisualLayer.ToLocal(_worldMapRoot.ToGlobal(mapPosition));
        Vector2I startCell = _siteVisualLayer.LocalToMap(layerLocalPosition);
        if (!usedCells.Contains(startCell) || _siteVisualLayer.GetCellSourceId(startCell) < 0)
        {
            failureReason = $"anchor_cell_empty cell={startCell}";
            return false;
        }

        HashSet<Vector2I> cells = new();
        Queue<Vector2I> queue = new();
        cells.Add(startCell);
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();
            foreach (Vector2I direction in SiteVisualScanDirections)
            {
                Vector2I next = current + direction;
                if (!usedCells.Contains(next) ||
                    _siteVisualLayer.GetCellSourceId(next) < 0 ||
                    !cells.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        Rect2 mapBounds = default;
        bool hasPoint = false;
        foreach (Vector2I cell in cells)
        {
            foreach (Vector2 point in BuildTileCellMapPolygon(_siteVisualLayer, cell))
            {
                ExpandBounds(point, ref mapBounds, ref hasPoint);
            }
        }

        if (!hasPoint)
        {
            failureReason = "empty_footprint_bounds";
            return false;
        }

        footprint = new SiteVisualFootprint(site.Id, cells, mapBounds);
        return true;
    }

    private void ReportSiteVisualFootprintFailure(string siteId, string reason)
    {
        string key = $"{siteId}:{reason}";
        if (!_reportedSiteVisualFootprintFailures.Add(key))
        {
            return;
        }

        GameLog.Warn(nameof(StrategicWorldRoot), $"SiteVisualFootprintMissing site={siteId} reason={reason}");
    }

    private void ConfigureStrategicNavigationContext()
    {
        EnsureStrategicNavigationLayerIsStable();
        TileMapLayer navigationTileLayer = _strategicNavigationTileLayer;
        int navigationCellCount = navigationTileLayer?.GetUsedCells().Count ?? 0;
        if (navigationTileLayer == null || navigationCellCount == 0)
        {
            string reason = navigationTileLayer == null
                ? "strategic_navigation_tile_layer_missing"
                : "strategic_navigation_tile_layer_empty";
            _strategicNavigationContext = StrategicNavigationContext.CreateUnavailable(reason);
            GameLog.Error(nameof(StrategicWorldRoot), $"StrategicNavigationUnavailable reason={reason}");
            return;
        }

        _strategicNavigationContext = StrategicNavigationContext.CreateStrategicGrid(
            navigationTileLayer,
            _strategicNavigationRoot);
        _reportedStrategicNavigationNotSynchronized = false;
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"StrategicNavigationConfigured provider={_strategicNavigationContext.PrimaryProviderId} version={_strategicNavigationContext.Version} layer={_strategicNavigationTileLayer.Name} cells={navigationCellCount}");
    }

    private void SyncDefinitionMapPositionsFromAnchors()
    {
        if (Definition == null || _siteAnchorRoot == null)
        {
            return;
        }

        foreach (WorldSiteDefinition site in Definition.SiteDefinitions)
        {
            if (_siteAnchorRoot.GetNodeOrNull<Node2D>(site.Id) is not { } anchor)
            {
                continue;
            }

            Vector2 anchorPosition = _worldMapRoot.ToLocal(anchor.GlobalPosition);
            if (site.MapPosition.DistanceSquaredTo(anchorPosition) <= 0.001f)
            {
                continue;
            }

            site.MapPosition = anchorPosition;
            GameLog.Info(nameof(StrategicWorldRoot), $"StrategicSiteAnchorSynced site={site.Id} position={anchorPosition}");
        }
    }

    private static Node2D GetOrCreateNode2D(Node parent, string name)
    {
        Node2D node = parent.GetNodeOrNull<Node2D>(name);
        if (node != null)
        {
            return node;
        }

        node = new Node2D { Name = name };
        parent.AddChild(node);
        return node;
    }
}
