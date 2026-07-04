using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay
{
    public override void _Process(double delta)
    {
        if (!HoverEnabled || _battleMapView == null || _gridMap == null || _coordinateLayer == null)
        {
            SetHoveredEntity(null);
            if (!_hoverOverrideActive)
            {
                SetAutoHoverCell(null);
            }

            return;
        }

        if (_hoverOverrideActive)
        {
            SetHoveredEntity(null);
            return;
        }

        Vector2 mouseGlobal = _battleMapView.GetGlobalMousePosition();
        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(mouseGlobal));
        var position = new GridPosition(tilePosition.X, tilePosition.Y);

        if (TryResolveHoveredEntityFootprint(position, out IReadOnlyList<GridPosition> footprintCells, out BattleEntity entity))
        {
            SetHoveredEntity(entity);
            SetHoverCells(footprintCells, overrideActive: false);
            return;
        }

        SetHoveredEntity(null);
        SetAutoHoverCell(_gridMap.TryGetCell(position, out _) ? position : null);
    }

    private void OnSiteMapLoaded(Node activeSiteMap)
    {
        _battleMapView = activeSiteMap as BattleMapView;
        _battleMapView?.EnsureRuntimeData();
        _gridMap = _siteRoot?.ActiveGridMap ?? _battleMapView?.GridMap;
        _coordinateLayer = _battleMapView?.CoordinateLayer;
        _hoverCells.Clear();
        _hoverOverrideActive = false;
        _pathCells.Clear();
        _cellsByKind.Remove(BattleGridHighlightKind.Path);
        ConfigureTileLayers();
        ApplyAllCellLayers();
        RebuildDynamicOverlay();
    }

    private void SetAutoHoverCell(GridPosition? position)
    {
        SetHoverCells(
            position.HasValue ? new[] { position.Value } : System.Array.Empty<GridPosition>(),
            overrideActive: false);
    }

    private bool TryResolveHoveredEntityFootprint(
        GridPosition position,
        out IReadOnlyList<GridPosition> footprintCells,
        out BattleEntity entity)
    {
        footprintCells = System.Array.Empty<GridPosition>();
        entity = _siteRoot?.FindEntityAt(position);
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return false;
        }

        footprintCells = BattleFootprintCells.Enumerate(
            gridOccupant.Position,
            gridOccupant.FootprintWidth,
            gridOccupant.FootprintHeight);
        return footprintCells.Count > 0;
    }

    private void SetHoveredEntity(BattleEntity entity)
    {
        _siteRoot?.SetHoveredBattleRuntimeEntity(entity?.EntityId ?? "");
    }

    private void SetHoverCells(IEnumerable<GridPosition> cells, bool overrideActive)
    {
        if (SetHoverCellsState(cells, overrideActive))
        {
            RebuildDynamicOverlay();
        }
    }

    private bool SetHoverCellsState(IEnumerable<GridPosition> cells, bool overrideActive)
    {
        HashSet<GridPosition> nextCells = cells?.ToHashSet() ?? new HashSet<GridPosition>();
        if (_hoverOverrideActive == overrideActive && _hoverCells.SetEquals(nextCells))
        {
            return false;
        }

        _hoverOverrideActive = overrideActive;
        _hoverCells.Clear();
        foreach (GridPosition cell in nextCells)
        {
            _hoverCells.Add(cell);
        }

        return true;
    }

    private void SetPathState(IReadOnlyList<GridPosition> orderedCells)
    {
        _pathCells.Clear();
        _pathCells.AddRange(orderedCells);
        HashSet<GridPosition> pathCells = orderedCells.Skip(1).ToHashSet();
        _cellsByKind[BattleGridHighlightKind.Path] = pathCells;
        _tileLayerRenderer.SetCells(BattleGridHighlightKind.Path, pathCells);
    }
}
