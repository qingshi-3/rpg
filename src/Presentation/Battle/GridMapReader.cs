using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

public static class GridMapReader
{
    public static BattleGridMap Read(BattleMapView mapView)
    {
        var gridMap = new BattleGridMap();

        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView))
        {
            ReadLayer(gridMap, layer);
        }

        return gridMap;
    }

    private static void ReadLayer(BattleGridMap gridMap, BattleMapLayer layer)
    {
        foreach (Vector2I tilePosition in layer.GetUsedCells())
        {
            var position = new GridPosition(tilePosition.X, tilePosition.Y);
            GridCell cell = gridMap.GetOrCreateCell(position);
            Vector2I atlasCoords = layer.GetCellAtlasCoords(tilePosition);

            cell.AddLayer(new GridCellLayerData(
                layer.Name,
                layer.Role,
                layer.Height,
                layer.AffectsWalkability,
                layer.AffectsLineOfSight,
                layer.IsHeightTransitionLayer,
                layer.IsVisualOnly,
                layer.GetCellSourceId(tilePosition),
                atlasCoords.X,
                atlasCoords.Y,
                layer.GetCellAlternativeTile(tilePosition)));
        }
    }

}
