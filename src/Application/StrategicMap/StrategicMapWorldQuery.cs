#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public static class StrategicMapWorldQuery
{
    public static StrategicMapWorldRect GetWorldBounds(StrategicMapChunkManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new StrategicMapWorldRect(0d, 0d, manifest.WorldWidth, manifest.WorldHeight);
    }

    public static StrategicMapChunkDefinition? ResolveChunkAtWorldPosition(
        StrategicMapChunkManifest manifest,
        StrategicMapPoint worldPosition)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!worldPosition.IsFinite || !GetWorldBounds(manifest).Contains(worldPosition))
        {
            return null;
        }

        int x = (int)Math.Floor(worldPosition.X / manifest.ChunkWidth);
        int y = (int)Math.Floor(worldPosition.Y / manifest.ChunkHeight);
        StrategicMapChunkCoordinate coordinate = new(x, y);
        return manifest.Chunks.SingleOrDefault(chunk => chunk.Coordinate == coordinate);
    }

    public static IReadOnlyList<StrategicMapChunkDefinition> SelectVisibleChunks(
        StrategicMapChunkManifest manifest,
        StrategicMapWorldRect visibleWorldRect,
        double preloadMargin)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!visibleWorldRect.IsValid || !double.IsFinite(preloadMargin) || preloadMargin < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(preloadMargin), "Strategic map visibility inputs must be finite and non-negative.");
        }

        StrategicMapWorldRect selection = visibleWorldRect.Expanded(preloadMargin);
        return manifest.Chunks
            .Where(chunk => ChunkBounds(manifest, chunk).Intersects(selection))
            .OrderBy(chunk => chunk.Coordinate.Y)
            .ThenBy(chunk => chunk.Coordinate.X)
            .ThenBy(chunk => chunk.ChunkId, StringComparer.Ordinal)
            .ToArray();
    }

    public static StrategicMapWorldRect ChunkBounds(
        StrategicMapChunkManifest manifest,
        StrategicMapChunkDefinition chunk)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(chunk);
        return new StrategicMapWorldRect(
            chunk.WorldOrigin.X,
            chunk.WorldOrigin.Y,
            manifest.ChunkWidth,
            manifest.ChunkHeight);
    }
}
