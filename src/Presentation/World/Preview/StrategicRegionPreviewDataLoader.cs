using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rpg.Presentation.World.Preview;

public static class StrategicRegionPreviewDataLoader
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    public static StrategicRegionPreviewData LoadFromProjectRoot(string projectRoot)
    {
        return LoadFromProjectRoot(projectRoot, StrategicRegionPreviewConfig.DefaultPreviewBounds);
    }

    public static StrategicRegionPreviewData LoadFromProjectRoot(string projectRoot, Rect2 previewBounds)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Preview project root is required.", nameof(projectRoot));
        }

        string normalizedRoot = Path.GetFullPath(projectRoot);
        string projectPath = Path.Combine(normalizedRoot, "config", "world", "maps", "mock_qinghe_chiyan", "source", "workbench.project.json");
        string geographyPath = Path.Combine(normalizedRoot, "config", "world", "maps", "mock_qinghe_chiyan", "source", "geography.json");
        string outlinesPath = Path.Combine(
            normalizedRoot,
            "assets",
            "textures",
            "world",
            "masks",
            "territory",
            "region_outlines.json");
        string lookupPath = Path.Combine(
            normalizedRoot,
            "assets",
            "textures",
            "world",
            "masks",
            "territory",
            "region_lookup.json");

        using JsonDocument project = ParseRequired(projectPath);
        using JsonDocument geography = ParseRequired(geographyPath);
        using JsonDocument outlines = ParseRequired(outlinesPath);
        using JsonDocument lookup = ParseRequired(lookupPath);

        RequireVersion(project.RootElement, 2, projectPath);
        RequireVersion(geography.RootElement, 3, geographyPath);
        RequireVersion(outlines.RootElement, 2, outlinesPath);
        RequireVersion(lookup.RootElement, 2, lookupPath);

        IReadOnlyList<StrategicRegionPreviewChunk> chunks = LoadChunks(
            normalizedRoot,
            project.RootElement,
            previewBounds,
            projectPath);
        Dictionary<string, (string Name, Vector2 Position)> locationById = LoadProvinceAnchors(
            geography.RootElement,
            previewBounds,
            geographyPath);
        Dictionary<string, IReadOnlyList<Vector2[]>> territoryByCityId = LoadCityTerritories(
            outlines.RootElement,
            locationById.Keys,
            outlinesPath);
        Dictionary<string, int> maskIdByRegionId = LoadMaskIds(lookup.RootElement, lookupPath);
        IReadOnlyList<StrategicRegionPreviewRegion> regions = LoadRegions(
            outlines.RootElement,
            locationById.Keys,
            maskIdByRegionId,
            outlinesPath);

        List<StrategicRegionPreviewCity> cities = locationById
            .Select(pair => new StrategicRegionPreviewCity(
                pair.Key,
                pair.Value.Name,
                pair.Value.Position,
                territoryByCityId.TryGetValue(pair.Key, out IReadOnlyList<Vector2[]> parts)
                    ? parts
                    : throw new InvalidOperationException($"Preview city territory missing city={pair.Key} path={outlinesPath}")))
            .OrderBy(city => city.CityId, StringComparer.Ordinal)
            .ToList();

        if (cities.Count == 0)
        {
            throw new InvalidOperationException($"Preview contains no city locations inside bounds={previewBounds} path={geographyPath}");
        }

        return new StrategicRegionPreviewData(previewBounds, chunks, cities, regions);
    }

    private static JsonDocument ParseRequired(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required strategic region preview data is missing path={path}", path);
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Invalid strategic region preview JSON path={path} reason={exception.Message}", exception);
        }
    }

    private static void RequireVersion(JsonElement root, int expected, string path)
    {
        int actual = root.GetProperty("version").GetInt32();
        if (actual != expected)
        {
            throw new InvalidOperationException($"Unsupported strategic region preview data version path={path} expected={expected} actual={actual}");
        }
    }

    private static IReadOnlyList<StrategicRegionPreviewChunk> LoadChunks(
        string projectRoot,
        JsonElement project,
        Rect2 previewBounds,
        string projectPath)
    {
        JsonElement chunkContract = project.GetProperty("chunk");
        Vector2 chunkSize = new(
            chunkContract.GetProperty("width").GetSingle(),
            chunkContract.GetProperty("height").GetSingle());
        string textureRoot = Path.Combine(projectRoot, "assets", "textures", "world");
        List<StrategicRegionPreviewChunk> chunks = new();
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (JsonElement element in project.GetProperty("chunks").EnumerateArray())
        {
            string chunkId = RequiredString(element, "id", projectPath);
            if (!ids.Add(chunkId))
            {
                throw new InvalidOperationException($"Duplicate preview chunk id={chunkId} path={projectPath}");
            }

            Vector2 origin = ReadCoordinate(element.GetProperty("worldOrigin"), projectPath);
            Rect2 chunkBounds = new(origin, chunkSize);
            if (!chunkBounds.Intersects(previewBounds))
            {
                continue;
            }

            string referencePath = RequiredString(element, "referenceTexturePath", projectPath);
            string absoluteTexturePath = ResolveInside(textureRoot, referencePath, "reference texture");
            if (!File.Exists(absoluteTexturePath))
            {
                throw new FileNotFoundException($"Preview reference texture missing chunk={chunkId} path={absoluteTexturePath}", absoluteTexturePath);
            }

            string resourcePath = $"res://{Path.GetRelativePath(projectRoot, absoluteTexturePath).Replace('\\', '/')}";
            chunks.Add(new StrategicRegionPreviewChunk(chunkId, origin, chunkSize, resourcePath));
        }

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException($"Preview bounds intersect no chunks bounds={previewBounds} path={projectPath}");
        }

        return chunks.OrderBy(chunk => chunk.WorldOrigin.Y).ThenBy(chunk => chunk.WorldOrigin.X).ToList();
    }

    private static Dictionary<string, (string Name, Vector2 Position)> LoadProvinceAnchors(
        JsonElement geography,
        Rect2 previewBounds,
        string geographyPath)
    {
        Dictionary<string, string> provinceNames = geography.GetProperty("provinces")
            .EnumerateArray()
            .ToDictionary(
                province => RequiredString(province, "provinceId", geographyPath),
                province => RequiredString(province, "name", geographyPath),
                StringComparer.Ordinal);
        Dictionary<string, (string Name, Vector2 Position)> cities = new(StringComparer.Ordinal);
        JsonElement features = geography.GetProperty("strategicLocations").GetProperty("features");
        foreach (JsonElement feature in features.EnumerateArray())
        {
            JsonElement properties = feature.GetProperty("properties");
            if (RequiredString(properties, "locationType", geographyPath) != "main-city")
            {
                continue;
            }

            string cityId = RequiredString(properties, "provinceId", geographyPath);
            string name = provinceNames.TryGetValue(cityId, out string provinceName)
                ? provinceName
                : throw new InvalidOperationException($"Preview main city references unknown province province={cityId} path={geographyPath}");
            Vector2 position = ReadCoordinate(feature.GetProperty("geometry").GetProperty("coordinates"), geographyPath);
            if (!previewBounds.HasPoint(position))
            {
                continue;
            }

            if (!cities.TryAdd(cityId, (name, position)))
            {
                throw new InvalidOperationException($"Duplicate preview city id={cityId} path={geographyPath}");
            }
        }

        return cities;
    }

    private static Dictionary<string, IReadOnlyList<Vector2[]>> LoadCityTerritories(
        JsonElement outlines,
        IEnumerable<string> selectedCityIds,
        string outlinesPath)
    {
        HashSet<string> selected = new(selectedCityIds, StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<Vector2[]>> territories = new(StringComparer.Ordinal);
        foreach (JsonElement element in outlines.GetProperty("provinces").EnumerateArray())
        {
            string cityId = RequiredString(element, "provinceId", outlinesPath);
            if (!selected.Contains(cityId))
            {
                continue;
            }

            IReadOnlyList<Vector2[]> parts = ReadPolygonParts(element.GetProperty("geometry"), outlinesPath, cityId);
            if (!territories.TryAdd(cityId, parts))
            {
                throw new InvalidOperationException($"Duplicate preview city territory city={cityId} path={outlinesPath}");
            }
        }

        return territories;
    }

    private static IReadOnlyList<StrategicRegionPreviewRegion> LoadRegions(
        JsonElement outlines,
        IEnumerable<string> selectedCityIds,
        IReadOnlyDictionary<string, int> maskIdByRegionId,
        string outlinesPath)
    {
        HashSet<string> selected = new(selectedCityIds, StringComparer.Ordinal);
        HashSet<string> regionIds = new(StringComparer.Ordinal);
        List<StrategicRegionPreviewRegion> regions = new();
        foreach (JsonElement feature in outlines.GetProperty("locationGeometries").EnumerateArray())
        {
            JsonElement properties = feature.GetProperty("properties");
            string cityId = RequiredString(properties, "provinceId", outlinesPath);
            if (!selected.Contains(cityId))
            {
                continue;
            }

            string regionId = RequiredString(properties, "locationId", outlinesPath);
            if (!regionIds.Add(regionId))
            {
                throw new InvalidOperationException($"Duplicate preview region id={regionId} path={outlinesPath}");
            }

            regions.Add(new StrategicRegionPreviewRegion(
                regionId,
                cityId,
                maskIdByRegionId.TryGetValue(regionId, out int maskId)
                    ? maskId
                    : throw new InvalidOperationException($"Preview region mask id missing region={regionId}"),
                "city-region",
                RequiredString(properties, "direction", outlinesPath),
                ReadPolygonParts(feature.GetProperty("geometry"), outlinesPath, regionId)));
        }

        foreach (string cityId in selected)
        {
            if (!regions.Any(region => region.CityId == cityId))
            {
                throw new InvalidOperationException($"Preview city has no regions city={cityId} path={outlinesPath}");
            }
        }

        return regions.OrderBy(region => region.RegionId, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, int> LoadMaskIds(JsonElement lookup, string lookupPath)
    {
        Dictionary<string, int> maskIds = new(StringComparer.Ordinal);
        foreach (JsonProperty entry in lookup.GetProperty("locations").EnumerateObject())
        {
            if (!int.TryParse(entry.Name, out int maskId) || maskId <= 0 || maskId > 255)
            {
                throw new InvalidOperationException($"Invalid preview region mask id={entry.Name} path={lookupPath}");
            }

            string regionId = RequiredString(entry.Value, "locationId", lookupPath);
            if (!maskIds.TryAdd(regionId, maskId))
            {
                throw new InvalidOperationException($"Duplicate preview mask lookup region={regionId} path={lookupPath}");
            }
        }

        return maskIds;
    }

    private static IReadOnlyList<Vector2[]> ReadPolygonParts(JsonElement geometry, string path, string objectId)
    {
        string type = RequiredString(geometry, "type", path);
        JsonElement coordinates = geometry.GetProperty("coordinates");
        List<Vector2[]> parts = new();
        if (type == "Polygon")
        {
            parts.Add(ReadOuterRing(coordinates, path, objectId));
        }
        else if (type == "MultiPolygon")
        {
            foreach (JsonElement polygon in coordinates.EnumerateArray())
            {
                parts.Add(ReadOuterRing(polygon, path, objectId));
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported preview geometry type={type} object={objectId} path={path}");
        }

        return parts;
    }

    private static Vector2[] ReadOuterRing(JsonElement polygonCoordinates, string path, string objectId)
    {
        JsonElement.ArrayEnumerator rings = polygonCoordinates.EnumerateArray();
        if (!rings.MoveNext())
        {
            throw new InvalidOperationException($"Preview polygon has no outer ring object={objectId} path={path}");
        }

        JsonElement outerRing = rings.Current;
        if (rings.MoveNext())
        {
            throw new InvalidOperationException($"Preview polygon holes are unsupported object={objectId} path={path}");
        }

        List<Vector2> points = outerRing.EnumerateArray().Select(point => ReadCoordinate(point, path)).ToList();
        if (points.Count > 1 && points[0].IsEqualApprox(points[^1]))
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3)
        {
            throw new InvalidOperationException($"Preview polygon needs at least three points object={objectId} path={path}");
        }

        return points.ToArray();
    }

    private static Vector2 ReadCoordinate(JsonElement coordinate, string path)
    {
        JsonElement.ArrayEnumerator values = coordinate.EnumerateArray();
        if (!values.MoveNext())
        {
            throw new InvalidOperationException($"Preview coordinate is missing x path={path}");
        }

        float x = values.Current.GetSingle();
        if (!values.MoveNext())
        {
            throw new InvalidOperationException($"Preview coordinate is missing y path={path}");
        }

        float y = values.Current.GetSingle();
        if (!float.IsFinite(x) || !float.IsFinite(y))
        {
            throw new InvalidOperationException($"Preview coordinate is not finite path={path}");
        }

        return new Vector2(x, y);
    }

    private static string RequiredString(JsonElement element, string propertyName, string path)
    {
        string value = element.GetProperty(propertyName).GetString() ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Preview property is empty property={propertyName} path={path}");
        }

        return value;
    }

    private static string ResolveInside(string root, string relativePath, string purpose)
    {
        string normalizedRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Preview {purpose} escapes configured root path={relativePath}");
        }

        return candidate;
    }
}
