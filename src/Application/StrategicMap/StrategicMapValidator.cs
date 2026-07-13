#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public sealed record StrategicMapValidationIssue(string Code, string ObjectId, string Message);

public sealed class StrategicMapValidationException : InvalidOperationException
{
    public StrategicMapValidationException(IReadOnlyList<StrategicMapValidationIssue> issues)
        : base($"Strategic map validation failed: {string.Join("; ", issues.Select(issue => $"{issue.Code}:{issue.ObjectId}"))}")
    {
        Issues = issues;
    }

    public IReadOnlyList<StrategicMapValidationIssue> Issues { get; }
}

public static class StrategicMapValidator
{
    public static IReadOnlyList<StrategicMapValidationIssue> Validate(StrategicMapCanonicalDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        List<StrategicMapValidationIssue> issues = new();
        ValidateChunks(definition.ChunkManifest, issues);
        ValidateGeography(definition.ChunkManifest, definition.Geography, issues);
        return issues;
    }

    public static void ThrowIfInvalid(StrategicMapCanonicalDefinition definition)
    {
        IReadOnlyList<StrategicMapValidationIssue> issues = Validate(definition);
        if (issues.Count > 0)
        {
            throw new StrategicMapValidationException(issues);
        }
    }

    private static void ValidateChunks(StrategicMapChunkManifest manifest, List<StrategicMapValidationIssue> issues)
    {
        if (manifest.WorldWidth <= 0 || manifest.WorldHeight <= 0 || manifest.ChunkWidth <= 0 || manifest.ChunkHeight <= 0)
        {
            Add(issues, "CHUNK_DIMENSIONS_INVALID", "manifest", "World and chunk dimensions must be positive.");
            return;
        }
        if (manifest.WorldWidth % manifest.ChunkWidth != 0 || manifest.WorldHeight % manifest.ChunkHeight != 0)
        {
            Add(issues, "CHUNK_GRID_INCOMPLETE", "manifest", "World dimensions must be divisible by chunk dimensions.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<StrategicMapChunkCoordinate> coordinates = new();
        foreach (StrategicMapChunkDefinition chunk in manifest.Chunks)
        {
            if (!ids.Add(chunk.ChunkId)) Add(issues, "CHUNK_ID_DUPLICATE", chunk.ChunkId, "Chunk ids must be unique.");
            if (!coordinates.Add(chunk.Coordinate)) Add(issues, "CHUNK_COORDINATE_DUPLICATE", chunk.ChunkId, "Chunk coordinates must be unique.");
            if (chunk.Coordinate.X < 0 || chunk.Coordinate.Y < 0)
            {
                Add(issues, "CHUNK_COORDINATE_NEGATIVE", chunk.ChunkId, "Chunk coordinates cannot be negative.");
                continue;
            }

            double expectedX = chunk.Coordinate.X * manifest.ChunkWidth;
            double expectedY = chunk.Coordinate.Y * manifest.ChunkHeight;
            if (chunk.WorldOrigin.X != expectedX || chunk.WorldOrigin.Y != expectedY)
            {
                Add(issues, "CHUNK_ORIGIN_MISMATCH", chunk.ChunkId, "Chunk origin must be derived from its coordinate and manifest dimensions.");
            }
            if (chunk.WorldOrigin.X + manifest.ChunkWidth > manifest.WorldWidth || chunk.WorldOrigin.Y + manifest.ChunkHeight > manifest.WorldHeight)
            {
                Add(issues, "CHUNK_OUTSIDE_WORLD", chunk.ChunkId, "Chunk bounds exceed the canonical world bounds.");
            }
        }

        int expectedColumns = (int)(manifest.WorldWidth / manifest.ChunkWidth);
        int expectedRows = (int)(manifest.WorldHeight / manifest.ChunkHeight);
        for (int y = 0; y < expectedRows; y++)
        {
            for (int x = 0; x < expectedColumns; x++)
            {
                if (!coordinates.Contains(new StrategicMapChunkCoordinate(x, y)))
                {
                    Add(issues, "CHUNK_COORDINATE_MISSING", $"{x},{y}", "Canonical chunk grid has a missing coordinate.");
                }
            }
        }
    }

    private static void ValidateGeography(
        StrategicMapChunkManifest manifest,
        StrategicMapGeographyDefinition geography,
        List<StrategicMapValidationIssue> issues)
    {
        Dictionary<string, ProvinceDefinition> provinces = new(StringComparer.Ordinal);
        foreach (ProvinceDefinition province in geography.Provinces)
        {
            if (!provinces.TryAdd(province.ProvinceId, province))
            {
                Add(issues, "PROVINCE_ID_DUPLICATE", province.ProvinceId, "Province ids must be unique.");
            }
        }

        Dictionary<string, StrategicLocationDefinition> locations = new(StringComparer.Ordinal);
        Dictionary<string, int> mainCityCounts = new(StringComparer.Ordinal);
        foreach (StrategicLocationDefinition location in geography.Locations)
        {
            if (!locations.TryAdd(location.LocationId, location))
            {
                Add(issues, "LOCATION_ID_DUPLICATE", location.LocationId, "Location ids must be unique.");
            }
            if (!InsideWorld(location.WorldPosition, manifest))
            {
                Add(issues, "LOCATION_OUTSIDE_WORLD", location.LocationId, "Location position must be finite and inside world bounds.");
            }

            bool city = IsCity(location.LocationType);
            if (city && (location.ProvinceId is null || !provinces.ContainsKey(location.ProvinceId)))
            {
                Add(issues, "LOCATION_PROVINCE_UNKNOWN", location.LocationId, "Each city must reference a declared province.");
            }
            if (location.LocationType == StrategicLocationType.MainCity && location.ProvinceId is not null)
            {
                mainCityCounts[location.ProvinceId] = mainCityCounts.GetValueOrDefault(location.ProvinceId) + 1;
            }
        }
        foreach (ProvinceDefinition province in geography.Provinces)
        {
            if (mainCityCounts.GetValueOrDefault(province.ProvinceId) != 1)
            {
                Add(issues, "PROVINCE_MAIN_CITY_COUNT", province.ProvinceId, "Each province must contain exactly one main city.");
            }
        }

        Dictionary<string, int> geometryCounts = new(StringComparer.Ordinal);
        foreach (LocationGeometryDefinition geometry in geography.LocationGeometries)
        {
            geometryCounts[geometry.LocationId] = geometryCounts.GetValueOrDefault(geometry.LocationId) + 1;
            if (!locations.TryGetValue(geometry.LocationId, out StrategicLocationDefinition? location) || !IsCity(location.LocationType))
            {
                Add(issues, "LOCATION_GEOMETRY_CITY_UNKNOWN", geometry.LocationId, "Location geometry must reference a main or auxiliary city.");
            }
            else if (!string.Equals(location.ProvinceId, geometry.ProvinceId, StringComparison.Ordinal))
            {
                Add(issues, "LOCATION_GEOMETRY_PROVINCE_MISMATCH", geometry.LocationId, "Location geometry and city must reference the same province.");
            }
            ValidateGeometry(manifest, geometry, issues);
        }
        foreach (StrategicLocationDefinition location in geography.Locations.Where(location => IsCity(location.LocationType)))
        {
            if (geometryCounts.GetValueOrDefault(location.LocationId) != 1)
            {
                Add(issues, "CITY_LOCATION_GEOMETRY_COUNT", location.LocationId, "Each city must have exactly one visual geometry.");
            }
        }
    }

    private static void ValidateGeometry(
        StrategicMapChunkManifest manifest,
        LocationGeometryDefinition definition,
        List<StrategicMapValidationIssue> issues)
    {
        if (definition.Geometry.Polygons.Count == 0)
        {
            Add(issues, "LOCATION_GEOMETRY_EMPTY", definition.LocationId, "Location geometry must contain at least one polygon.");
        }
        foreach (StrategicMapPolygon polygon in definition.Geometry.Polygons)
        {
            if (polygon.Rings.Count == 0)
            {
                Add(issues, "LOCATION_GEOMETRY_EMPTY", definition.LocationId, "Location geometry polygon must contain a ring.");
                continue;
            }
            foreach (IReadOnlyList<StrategicMapPoint> ring in polygon.Rings)
            {
                if (ring.Count < 4 || ring[0] != ring[^1])
                {
                    Add(issues, "LOCATION_GEOMETRY_RING_OPEN", definition.LocationId, "Location geometry rings require at least four points and explicit closure.");
                }
                if (ring.Any(point => !InsideWorld(point, manifest)))
                {
                    Add(issues, "LOCATION_GEOMETRY_OUTSIDE_WORLD", definition.LocationId, "Location geometry coordinates must be finite and inside world bounds.");
                }
            }
        }
    }

    private static bool InsideWorld(StrategicMapPoint point, StrategicMapChunkManifest manifest) =>
        point.IsFinite && point.X >= 0 && point.Y >= 0 && point.X <= manifest.WorldWidth && point.Y <= manifest.WorldHeight;

    private static bool IsCity(StrategicLocationType type) =>
        type is StrategicLocationType.MainCity or StrategicLocationType.AuxiliaryCity;

    private static void Add(List<StrategicMapValidationIssue> issues, string code, string objectId, string message) =>
        issues.Add(new StrategicMapValidationIssue(code, objectId, message));
}
