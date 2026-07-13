#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public static class StrategicMapRegionLookupLoader
{
    public static StrategicMapRegionLookupDefinition Load(string path)
    {
        using JsonDocument document = StrategicMapJson.ParseRequired(path);
        JsonElement root = document.RootElement;
        int version = StrategicMapJson.RequiredInt(root, "version", path);
        if (version != 3)
        {
            throw new InvalidOperationException($"Unsupported strategic map region lookup version path={path} expected=3 actual={version}");
        }
        string encoding = StrategicMapJson.RequiredString(root, "encoding", path);
        if (!string.Equals(encoding, "rgb24-location-code-v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported strategic map region encoding={encoding} path={path}");
        }

        List<StrategicMapRegionLookupEntry> entries = new();
        foreach (JsonProperty property in root.GetProperty("locations").EnumerateObject())
        {
            if (!int.TryParse(property.Name, out int maskId) || maskId is <= 0 or > 16777215)
            {
                throw new InvalidOperationException($"Strategic map region mask id is invalid id={property.Name} path={path}");
            }

            entries.Add(new StrategicMapRegionLookupEntry(
                maskId,
                StrategicMapJson.RequiredString(property.Value, "locationId", path),
                StrategicMapJson.RequiredString(property.Value, "provinceId", path)));
        }

        return new StrategicMapRegionLookupDefinition(
            version,
            entries.OrderBy(entry => entry.MaskId).ToArray());
    }

    public static void ValidateAgainstCanonical(
        StrategicMapRegionLookupDefinition lookup,
        StrategicMapGeographyDefinition geography)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(geography);
        Dictionary<string, StrategicLocationDefinition> locations = geography.Locations
            .ToDictionary(location => location.LocationId, StringComparer.Ordinal);
        HashSet<string> geometryLocationIds = geography.LocationGeometries
            .Select(geometry => geometry.LocationId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> maskIds = new();
        HashSet<string> lookupLocationIds = new(StringComparer.Ordinal);

        foreach (StrategicMapRegionLookupEntry entry in lookup.Entries)
        {
            if (!maskIds.Add(entry.MaskId))
            {
                throw new InvalidOperationException($"Strategic map region lookup has duplicate mask id={entry.MaskId}");
            }
            if (!lookupLocationIds.Add(entry.LocationId))
            {
                throw new InvalidOperationException($"Strategic map region lookup has duplicate location id={entry.LocationId}");
            }
            if (!locations.TryGetValue(entry.LocationId, out StrategicLocationDefinition? location))
            {
                throw new InvalidOperationException($"Strategic map region lookup location is unknown maskId={entry.MaskId} locationId={entry.LocationId}");
            }
            if (!string.Equals(location.ProvinceId, entry.ProvinceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Strategic map region lookup province mismatch maskId={entry.MaskId} locationId={entry.LocationId} provinceId={entry.ProvinceId}");
            }
        }

        if (!geometryLocationIds.SetEquals(lookupLocationIds))
        {
            throw new InvalidOperationException("Strategic map region lookup must contain exactly one entry for every canonical location geometry.");
        }
    }
}
