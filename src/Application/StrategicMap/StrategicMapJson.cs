#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

internal static class StrategicMapJson
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    public static JsonDocument ParseRequired(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required strategic map data is missing path={path}", path);
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path), Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Invalid strategic map JSON path={path} reason={exception.Message}", exception);
        }
    }

    public static int RequiredInt(JsonElement element, string propertyName, string path) =>
        element.GetProperty(propertyName).TryGetInt32(out int value)
            ? value
            : throw new InvalidOperationException($"Strategic map integer is invalid property={propertyName} path={path}");

    public static double RequiredNumber(JsonElement element, string propertyName, string path)
    {
        double value = element.GetProperty(propertyName).GetDouble();
        return double.IsFinite(value)
            ? value
            : throw new InvalidOperationException($"Strategic map number is not finite property={propertyName} path={path}");
    }

    public static string RequiredString(JsonElement element, string propertyName, string path)
    {
        string value = element.GetProperty(propertyName).GetString() ?? "";
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Strategic map property is empty property={propertyName} path={path}")
            : value;
    }

    public static string? OptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        string value = property.GetString() ?? "";
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static StrategicMapPoint ReadPoint(JsonElement element, string path)
    {
        JsonElement.ArrayEnumerator values = element.EnumerateArray();
        if (!values.MoveNext())
        {
            throw new InvalidOperationException($"Strategic map coordinate is missing x path={path}");
        }
        double x = values.Current.GetDouble();
        if (!values.MoveNext())
        {
            throw new InvalidOperationException($"Strategic map coordinate is missing y path={path}");
        }
        double y = values.Current.GetDouble();
        if (!double.IsFinite(x) || !double.IsFinite(y) || values.MoveNext())
        {
            throw new InvalidOperationException($"Strategic map coordinate must contain exactly two finite values path={path}");
        }
        return new StrategicMapPoint(x, y);
    }

    public static IReadOnlyList<StrategicMapPolygon> ReadPolygons(JsonElement geometry, string path, string objectId)
    {
        string type = RequiredString(geometry, "type", path);
        JsonElement coordinates = geometry.GetProperty("coordinates");
        List<StrategicMapPolygon> polygons = new();
        if (type == "Polygon")
        {
            polygons.Add(ReadPolygon(coordinates, path, objectId));
        }
        else if (type == "MultiPolygon")
        {
            foreach (JsonElement polygon in coordinates.EnumerateArray())
            {
                polygons.Add(ReadPolygon(polygon, path, objectId));
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported strategic map geometry type={type} object={objectId} path={path}");
        }
        return polygons;
    }

    private static StrategicMapPolygon ReadPolygon(JsonElement coordinates, string path, string objectId)
    {
        List<IReadOnlyList<StrategicMapPoint>> rings = new();
        foreach (JsonElement ringElement in coordinates.EnumerateArray())
        {
            List<StrategicMapPoint> ring = new();
            foreach (JsonElement pointElement in ringElement.EnumerateArray())
            {
                ring.Add(ReadPoint(pointElement, path));
            }
            rings.Add(ring);
        }
        if (rings.Count == 0)
        {
            throw new InvalidOperationException($"Strategic map polygon has no rings object={objectId} path={path}");
        }
        return new StrategicMapPolygon(rings);
    }
}
