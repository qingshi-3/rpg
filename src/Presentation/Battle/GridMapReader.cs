using System.Linq;
using Godot;
using Rpg.Definitions.Maps;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle;

public static class GridMapReader
{
    private const string WalkableCustomData = "Walkable";
    private const string TerrainTagCustomData = "TerrainTag";

    public static BattleGridMap Read(BattleMapView mapView)
    {
        var gridMap = new BattleGridMap();
        int layerCount = 0;
        int tileCount = 0;

        foreach (BattleMapLayer layer in BattleMapLayerQueries.EnumerateBattleMapLayers(mapView))
        {
            tileCount += ReadLayer(gridMap, layer);
            layerCount++;
        }

        gridMap.RebuildTopSurfaceIndex();
        int connectionCount = ReadConnections(gridMap, mapView);
        int coveredSurfaceCount = gridMap.Surfaces.Values.Count(surface =>
            surface.HasFoundation && !gridMap.IsTopSurface(surface.SurfacePosition));
        int walkableTopSurfaceCount = gridMap.TopSurfacePositions.Values.Count(position =>
            gridMap.TryGetSurface(position, out GridCellSurface surface) && surface.IsWalkable);

        GameLog.Info(
            nameof(GridMapReader),
            $"Read grid map layers={layerCount} tiles={tileCount} cells={gridMap.Cells.Count} surfaces={gridMap.Surfaces.Count} topSurfaces={gridMap.TopSurfacePositions.Count} coveredSurfaces={coveredSurfaceCount} walkableSurfaces={gridMap.Surfaces.Values.Count(surface => surface.IsWalkable)} walkableTopSurfaces={walkableTopSurfaceCount} waterSurfaces={gridMap.Surfaces.Values.Count(surface => string.Equals(surface.TerrainTag, "water", System.StringComparison.OrdinalIgnoreCase))} connections={connectionCount} walkable={gridMap.Cells.Values.Count(cell => cell.IsWalkable)} water={gridMap.Cells.Values.Count(cell => string.Equals(cell.TerrainTag, "water", System.StringComparison.OrdinalIgnoreCase))} canStandOn={gridMap.Cells.Values.Count(cell => cell.CanStandOn)} obstacles={gridMap.Cells.Values.Count(cell => cell.IsObstacle)} noFoundation={gridMap.Cells.Values.Count(cell => !cell.HasFoundation)}");

        return gridMap;
    }

    private static int ReadLayer(BattleGridMap gridMap, BattleMapLayer layer)
    {
        TileCustomDataAvailability customDataAvailability = TileCustomDataAvailability.From(layer.TileSet);
        int tileCount = 0;

        foreach (Vector2I tilePosition in layer.GetUsedCells())
        {
            var position = new GridPosition(tilePosition.X, tilePosition.Y);
            GridCell cell = gridMap.GetOrCreateCell(position);
            Vector2I atlasCoords = layer.GetCellAtlasCoords(tilePosition);
            TileData tileData = layer.GetCellTileData(tilePosition);
            // Navigation input is intentionally simple: only foundation-like layers read Walkable.
            // Object-layer blocking comes from authored layer semantics, not per-tile obstacle flags.
            bool isObjectObstacle = layer.Role == LayerRole.Object && layer.AffectsWalkability;
            bool affectsWalkability = layer.Role == LayerRole.Object
                ? isObjectObstacle
                : layer.AffectsWalkability;
            bool walkable = layer.Role == LayerRole.Object
                ? false
                : ReadBool(tileData, customDataAvailability.WalkableKey, false);

            var layerData = new GridCellLayerData(
                layer.Name,
                layer.Role,
                layer.Height,
                affectsWalkability,
                layer.AffectsLineOfSight,
                layer.IsHeightTransitionLayer,
                layer.IsVisualOnly || layer.Role == LayerRole.Detail,
                walkable,
                1,
                false,
                isObjectObstacle,
                ReadString(tileData, customDataAvailability.TerrainTagKey, ""),
                layer.GetCellSourceId(tilePosition),
                atlasCoords.X,
                atlasCoords.Y,
                layer.GetCellAlternativeTile(tilePosition));

            cell.AddLayer(layerData);
            AddLayerToSurface(gridMap, position, layerData);

            tileCount++;
        }

        return tileCount;
    }

    private static void AddLayerToSurface(
        BattleGridMap gridMap,
        GridPosition position,
        GridCellLayerData layer)
    {
        if (layer.IsVisualOnly || layer.Role == LayerRole.Detail)
        {
            return;
        }

        if (layer.Role == LayerRole.Foundation)
        {
            gridMap.GetOrCreateSurface(position, layer.Height).AddLayer(layer);
            return;
        }

        if (layer.Role == LayerRole.Object &&
            gridMap.TryGetSurface(new GridSurfacePosition(position, layer.Height), out GridCellSurface surface))
        {
            surface.AddLayer(layer);
        }
    }

    private static int ReadConnections(BattleGridMap gridMap, BattleMapView mapView)
    {
        int edgeCount = 0;

        foreach (BattleMapConnectionConfig config in EnumerateConnectionConfigs(mapView))
        {
            if (config.Connections == null)
            {
                continue;
            }

            foreach (BattleMapConnection connection in config.Connections)
            {
                edgeCount += AddConnection(gridMap, connection);
            }
        }

        return edgeCount;
    }

    private static int AddConnection(BattleGridMap gridMap, BattleMapConnection connection)
    {
        if (connection == null)
        {
            return 0;
        }

        BattleMapConnectionPoint[] sideA = connection.SideA?.Points?
            .Where(point => point != null)
            .ToArray() ?? System.Array.Empty<BattleMapConnectionPoint>();
        BattleMapConnectionPoint[] sideB = connection.SideB?.Points?
            .Where(point => point != null)
            .ToArray() ?? System.Array.Empty<BattleMapConnectionPoint>();

        string id = string.IsNullOrWhiteSpace(connection.Id) ? "<未命名连接>" : connection.Id;
        if (sideA.Length == 0 || sideB.Length == 0)
        {
            GameLog.Warn(
                nameof(GridMapReader),
                $"Map connection ignored id={id} reason=连接两侧都必须至少有一个点");
            return 0;
        }

        int edgeCount = 0;
        foreach (BattleMapConnectionPoint pointA in sideA)
        {
            GridSurfacePosition surfaceA = pointA.ToSurfacePosition();
            if (!IsValidConnectionSurface(gridMap, surfaceA, id))
            {
                continue;
            }

            foreach (BattleMapConnectionPoint pointB in sideB)
            {
                GridSurfacePosition surfaceB = pointB.ToSurfacePosition();
                if (!IsValidConnectionSurface(gridMap, surfaceB, id))
                {
                    continue;
                }

                gridMap.AddSurfaceConnection(surfaceA, surfaceB, connection.MoveCost, id);
                edgeCount++;

                if (connection.Bidirectional)
                {
                    gridMap.AddSurfaceConnection(surfaceB, surfaceA, connection.MoveCost, id);
                    edgeCount++;
                }
            }
        }

        GameLog.Info(
            nameof(GridMapReader),
            $"Map connection loaded id={id} type={connection.Type} sideA={sideA.Length} sideB={sideB.Length} bidirectional={connection.Bidirectional} edges={edgeCount}");
        return edgeCount;
    }

    private static bool IsValidConnectionSurface(
        BattleGridMap gridMap,
        GridSurfacePosition position,
        string connectionId)
    {
        if (!gridMap.TryGetSurface(position, out GridCellSurface surface))
        {
            GameLog.Warn(
                nameof(GridMapReader),
                $"Map connection point ignored id={connectionId} point={position} reason=没有对应高度的可走面 nearest={DescribeNearestValidConnectionSurfaces(gridMap, position, 5)}");
            return false;
        }

        if (!surface.HasFoundation || !surface.IsWalkable || surface.MoveCost <= 0)
        {
            GameLog.Warn(
                nameof(GridMapReader),
                $"Map connection point ignored id={connectionId} point={position} reason=可走面不可进入 walkable={surface.IsWalkable} moveCost={surface.MoveCost} foundation={surface.HasFoundation} nearest={DescribeNearestValidConnectionSurfaces(gridMap, position, 5)}");
            return false;
        }

        if (!gridMap.IsTopSurface(position))
        {
            GameLog.Warn(
                nameof(GridMapReader),
                $"Map connection point ignored id={connectionId} point={position} reason=covered_by_higher_foundation nearest={DescribeNearestValidConnectionSurfaces(gridMap, position, 5)}");
            return false;
        }

        return true;
    }

    private static string DescribeNearestValidConnectionSurfaces(
        BattleGridMap gridMap,
        GridSurfacePosition origin,
        int count)
    {
        GridSurfacePosition[] candidates = gridMap.Surfaces.Values
            .Where(surface => surface.Height == origin.Height)
            .Where(surface => surface.HasFoundation && surface.IsWalkable && surface.MoveCost > 0)
            .Where(surface => gridMap.IsTopSurface(surface.SurfacePosition))
            .OrderBy(surface => System.Math.Abs(surface.Position.X - origin.X) + System.Math.Abs(surface.Position.Y - origin.Y))
            .ThenBy(surface => surface.Position.Y)
            .ThenBy(surface => surface.Position.X)
            .Take(count)
            .Select(surface => surface.SurfacePosition)
            .ToArray();

        return candidates.Length == 0 ? "none" : string.Join(", ", candidates);
    }

    private static System.Collections.Generic.IEnumerable<BattleMapConnectionConfig> EnumerateConnectionConfigs(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is BattleMapConnectionConfig config)
            {
                yield return config;
            }

            foreach (BattleMapConnectionConfig descendant in EnumerateConnectionConfigs(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool ReadBool(TileData tileData, string key, bool defaultValue)
    {
        return tileData == null || string.IsNullOrEmpty(key)
            ? defaultValue
            : tileData.GetCustomData(key).AsBool();
    }

    private static string ReadString(TileData tileData, string key, string defaultValue)
    {
        return tileData == null || string.IsNullOrEmpty(key)
            ? defaultValue
            : tileData.GetCustomData(key).AsString();
    }

    private readonly struct TileCustomDataAvailability
    {
        private TileCustomDataAvailability(
            string walkableKey,
            string terrainTagKey)
        {
            WalkableKey = walkableKey;
            TerrainTagKey = terrainTagKey;
        }

        public string WalkableKey { get; }
        public string TerrainTagKey { get; }

        public static TileCustomDataAvailability From(TileSet tileSet)
        {
            return new TileCustomDataAvailability(
                ResolveLayerName(tileSet, WalkableCustomData),
                ResolveLayerName(tileSet, TerrainTagCustomData));
        }

        private static string ResolveLayerName(TileSet tileSet, string key)
        {
            if (tileSet == null)
            {
                return "";
            }

            if (tileSet.GetCustomDataLayerByName(key) >= 0)
            {
                return key;
            }

            string keyWithTrailingSpace = $"{key} ";
            return tileSet.GetCustomDataLayerByName(keyWithTrailingSpace) >= 0
                ? keyWithTrailingSpace
                : "";
        }
    }
}
