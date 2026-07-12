using System.Collections.Generic;
using Godot;

namespace Rpg.Presentation.World.Preview;

public sealed record StrategicRegionPreviewChunk(
    string ChunkId,
    Vector2 WorldOrigin,
    Vector2 Size,
    string TextureResourcePath);

public sealed record StrategicRegionPreviewCity(
    string CityId,
    string DisplayName,
    Vector2 WorldPosition,
    IReadOnlyList<Vector2[]> TerritoryParts);

public sealed record StrategicRegionPreviewRegion(
    string RegionId,
    string CityId,
    int MaskId,
    string Role,
    string Direction,
    IReadOnlyList<Vector2[]> PolygonParts);

public sealed record StrategicRegionPreviewData(
    Rect2 PreviewBounds,
    IReadOnlyList<StrategicRegionPreviewChunk> Chunks,
    IReadOnlyList<StrategicRegionPreviewCity> Cities,
    IReadOnlyList<StrategicRegionPreviewRegion> Regions);
