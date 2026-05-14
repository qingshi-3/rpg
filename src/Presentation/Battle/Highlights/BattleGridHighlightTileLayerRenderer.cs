using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

internal sealed class BattleGridHighlightTileLayerRenderer
{
    private const string LayerNamePrefix = "RuntimeHighlightLayer";

    private readonly Dictionary<BattleGridHighlightKind, TileMapLayer> _layersByKind = new();
    private readonly Dictionary<BattleGridHighlightKind, HashSet<GridPosition>> _paintedCellsByKind = new();
    private IReadOnlyDictionary<BattleGridHighlightKind, BattleGridHighlightTile> _tilesByKind =
        new Dictionary<BattleGridHighlightKind, BattleGridHighlightTile>();

    public void Configure(
        Node2D owner,
        BattleMapLayer coordinateLayer,
        BattleGridHighlightTileSetSpec tileSetSpec,
        IReadOnlyList<BattleGridHighlightKind> drawOrder,
        System.Func<BattleGridHighlightKind, bool> shouldPulse,
        System.Action<TileMapLayer, BattleGridHighlightKind> applyPulse)
    {
        ClearAll();
        RemoveExistingLayers();
        _tilesByKind = tileSetSpec?.TilesByKind ?? new Dictionary<BattleGridHighlightKind, BattleGridHighlightTile>();

        if (owner == null || coordinateLayer == null || tileSetSpec?.TileSet == null)
        {
            return;
        }

        for (int index = 0; index < drawOrder.Count; index++)
        {
            BattleGridHighlightKind kind = drawOrder[index];
            if (!_tilesByKind.ContainsKey(kind))
            {
                continue;
            }

            var layer = new TileMapLayer
            {
                Name = $"{LayerNamePrefix}{kind}",
                TileSet = tileSetSpec.TileSet,
                ZIndex = index * 2,
                YSortEnabled = false
            };
            owner.AddChild(layer);
            layer.GlobalTransform = coordinateLayer.GlobalTransform;
            _layersByKind[kind] = layer;
            _paintedCellsByKind[kind] = new HashSet<GridPosition>();

            // Runtime highlight layers are presentation-only. They use a TileSet with no physics,
            // navigation, or custom map data, so battle rules continue to read the authored map.
            if (shouldPulse?.Invoke(kind) == true)
            {
                applyPulse?.Invoke(layer, kind);
            }
        }
    }

    public void SetCells(BattleGridHighlightKind kind, IEnumerable<GridPosition> cells)
    {
        if (!_layersByKind.TryGetValue(kind, out TileMapLayer layer) ||
            !_tilesByKind.TryGetValue(kind, out BattleGridHighlightTile tile))
        {
            return;
        }

        HashSet<GridPosition> current = _paintedCellsByKind.TryGetValue(kind, out HashSet<GridPosition> painted)
            ? painted
            : new HashSet<GridPosition>();
        BattleGridHighlightCellDiff diff = BattleGridHighlightCellDiff.Build(current, cells);

        foreach (GridPosition cell in diff.CellsToErase)
        {
            layer.EraseCell(ToTileMapCell(cell));
        }

        foreach (GridPosition cell in diff.CellsToPaint)
        {
            layer.SetCell(ToTileMapCell(cell), tile.SourceId, tile.AtlasCoords, tile.AlternativeTile);
        }

        _paintedCellsByKind[kind] = diff.NextCells;
    }

    public void ClearCells(BattleGridHighlightKind kind)
    {
        if (!_layersByKind.TryGetValue(kind, out TileMapLayer layer))
        {
            return;
        }

        layer.Clear();
        _paintedCellsByKind[kind] = new HashSet<GridPosition>();
    }

    public void ClearAll()
    {
        foreach (TileMapLayer layer in _layersByKind.Values.Where(GodotObject.IsInstanceValid))
        {
            layer.Clear();
        }

        foreach (BattleGridHighlightKind kind in _paintedCellsByKind.Keys.ToArray())
        {
            _paintedCellsByKind[kind] = new HashSet<GridPosition>();
        }
    }

    private void RemoveExistingLayers()
    {
        foreach (TileMapLayer layer in _layersByKind.Values.Where(GodotObject.IsInstanceValid))
        {
            layer.QueueFree();
        }

        _layersByKind.Clear();
        _paintedCellsByKind.Clear();
    }

    private static Vector2I ToTileMapCell(GridPosition cell)
    {
        return new Vector2I(cell.X, cell.Y);
    }
}
