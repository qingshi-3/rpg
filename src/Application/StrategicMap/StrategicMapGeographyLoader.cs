#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public static class StrategicMapGeographyLoader
{
    public static StrategicMapGeographyDefinition Load(string path)
    {
        using JsonDocument document = StrategicMapJson.ParseRequired(path);
        JsonElement root = document.RootElement;
        int version = StrategicMapJson.RequiredInt(root, "version", path);
        if (version != 3)
        {
            throw new InvalidOperationException($"Unsupported strategic map geography version path={path} expected=3 actual={version}");
        }

        List<ProvinceDefinition> provinces = new();
        foreach (JsonElement element in root.GetProperty("provinces").EnumerateArray())
        {
            provinces.Add(new ProvinceDefinition(
                StrategicMapJson.RequiredString(element, "provinceId", path),
                StrategicMapJson.RequiredString(element, "name", path),
                StrategicMapJson.RequiredString(element, "layoutId", path)));
        }

        List<StrategicLocationDefinition> locations = new();
        foreach (JsonElement feature in root.GetProperty("strategicLocations").GetProperty("features").EnumerateArray())
        {
            JsonElement properties = feature.GetProperty("properties");
            locations.Add(new StrategicLocationDefinition(
                StrategicMapJson.RequiredString(properties, "locationId", path),
                StrategicMapJson.RequiredString(properties, "name", path),
                ParseLocationType(StrategicMapJson.RequiredString(properties, "locationType", path), path),
                StrategicMapJson.OptionalString(properties, "provinceId"),
                StrategicMapJson.ReadPoint(feature.GetProperty("geometry").GetProperty("coordinates"), path)));
        }

        List<LocationGeometryDefinition> geometries = new();
        foreach (JsonElement feature in root.GetProperty("locationGeometries").GetProperty("features").EnumerateArray())
        {
            JsonElement properties = feature.GetProperty("properties");
            string locationId = StrategicMapJson.RequiredString(properties, "locationId", path);
            geometries.Add(new LocationGeometryDefinition(
                locationId,
                StrategicMapJson.RequiredString(properties, "provinceId", path),
                StrategicMapJson.RequiredString(properties, "direction", path),
                new StrategicMapGeometry(StrategicMapJson.ReadPolygons(feature.GetProperty("geometry"), path, locationId))));
        }

        return new StrategicMapGeographyDefinition(version, provinces, locations, geometries);
    }

    private static StrategicLocationType ParseLocationType(string value, string path) => value switch
    {
        "main-city" => StrategicLocationType.MainCity,
        "auxiliary-city" => StrategicLocationType.AuxiliaryCity,
        "gate" => StrategicLocationType.Gate,
        "bridge" => StrategicLocationType.Bridge,
        "ferry" => StrategicLocationType.Ferry,
        "port" => StrategicLocationType.Port,
        "ruin" => StrategicLocationType.Ruin,
        "resource-site" => StrategicLocationType.ResourceSite,
        _ => throw new InvalidOperationException($"Unknown strategic location type value={value} path={path}")
    };
}
