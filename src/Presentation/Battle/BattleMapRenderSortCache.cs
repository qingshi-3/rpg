using System.Collections.Generic;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle;

public sealed class BattleMapRenderSortCache
{
    private readonly Dictionary<GridSurfacePosition, int> _ySortOriginUnitZIndexBySurface;

    private BattleMapRenderSortCache(Dictionary<GridSurfacePosition, int> ySortOriginUnitZIndexBySurface)
    {
        _ySortOriginUnitZIndexBySurface = ySortOriginUnitZIndexBySurface ?? new Dictionary<GridSurfacePosition, int>();
    }

    public static BattleMapRenderSortCache Empty { get; } = new(new Dictionary<GridSurfacePosition, int>());

    public int YSortOriginSurfaceCount => _ySortOriginUnitZIndexBySurface.Count;

    public bool TryGetYSortOriginUnitZIndex(GridSurfacePosition surfacePosition, out int zIndex)
    {
        return _ySortOriginUnitZIndexBySurface.TryGetValue(surfacePosition, out zIndex);
    }

    public static BattleMapRenderSortCache Build(BattleMapView mapView)
    {
        if (mapView == null)
        {
            return Empty;
        }

        var ySortOriginUnitZIndexBySurface = new Dictionary<GridSurfacePosition, int>();
        int scannedObjectLayerCount = 0;
        int flaggedTileCount = 0;
        int ySortEnabledLayerCount = 0;

        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView))
        {
            if (layer.Role != LayerRole.Object)
            {
                continue;
            }

            scannedObjectLayerCount++;
            bool layerHasYSortOriginTiles = false;

            foreach (Vector2I tilePosition in layer.GetUsedCells())
            {
                TileData tileData = layer.GetCellTileData(tilePosition);
                if (!UsesYSortOrigin(tileData))
                {
                    continue;
                }

                var surfacePosition = new GridSurfacePosition(tilePosition.X, tilePosition.Y, layer.Height);
                ySortOriginUnitZIndexBySurface[surfacePosition] = layer.ZIndex + tileData.ZIndex;
                layerHasYSortOriginTiles = true;
                flaggedTileCount++;
            }

            if (layerHasYSortOriginTiles && !layer.YSortEnabled)
            {
                layer.YSortEnabled = true;
                ySortEnabledLayerCount++;
            }
        }

        GameLog.Info(
            nameof(BattleMapRenderSortCache),
            $"Built render sort cache map={mapView.GetPath()} objectLayers={scannedObjectLayerCount} ySortOriginTiles={flaggedTileCount} surfaces={ySortOriginUnitZIndexBySurface.Count} runtimeYSortLayers={ySortEnabledLayerCount}");

        return new BattleMapRenderSortCache(ySortOriginUnitZIndexBySurface);
    }

    private static bool UsesYSortOrigin(TileData tileData)
    {
        return tileData != null && tileData.YSortOrigin != 0;
    }
}
