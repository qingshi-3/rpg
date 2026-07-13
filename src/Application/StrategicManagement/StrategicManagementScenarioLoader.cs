#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rpg.Application.StrategicMap;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicManagement;

public static class StrategicManagementScenarioLoader
{
    public const int CurrentVersion = 1;

    public static StrategicManagementScenarioDefinition LoadSelected(
        string projectRoot,
        string scenarioResourcePath,
        StrategicMapPackageManifest package,
        StrategicMapCanonicalDefinition canonical)
    {
        string path = StrategicMapPackageLoader.ResolveProjectPath(projectRoot, scenarioResourcePath);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        int version = RequiredInt(root, "version", path);
        if (version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported strategic scenario version path={path} expected={CurrentVersion} actual={version}");
        }

        List<StrategicScenarioProvinceStart> provinces = root.GetProperty("provinces").EnumerateArray()
            .Select(element => new StrategicScenarioProvinceStart(
                RequiredString(element, "provinceId", path),
                ParseProvinceRole(RequiredString(element, "role", path), path),
                RequiredString(element, "ownerFactionId", path),
                ParseControl(RequiredString(element, "control", path), path)))
            .ToList();
        List<StrategicScenarioLocationStart> locations = root.GetProperty("locations").EnumerateArray()
            .Select(element => new StrategicScenarioLocationStart(
                RequiredString(element, "locationId", path),
                RequiredString(element, "ownerFactionId", path),
                ParseControl(RequiredString(element, "control", path), path)))
            .ToList();
        List<StrategicScenarioResourceStart> resources = root.GetProperty("resources").EnumerateArray()
            .Select(element => new StrategicScenarioResourceStart(
                RequiredString(element, "factionId", path),
                RequiredString(element, "resourceId", path),
                RequiredInt(element, "amount", path)))
            .ToList();
        StrategicManagementScenarioDefinition scenario = new(
            version,
            RequiredString(root, "scenarioId", path),
            RequiredString(root, "mapId", path),
            RequiredInt(root, "packageCompatibilityRevision", path),
            RequiredInt(root, "scenarioContentRevision", path),
            RequiredInt(root, "defaultCityForceCapacity", path),
            RequiredInt(root, "defaultCityReserveForces", path),
            provinces,
            locations,
            resources);
        Validate(scenario, package, canonical, path);
        return scenario;
    }

    private static void Validate(
        StrategicManagementScenarioDefinition scenario,
        StrategicMapPackageManifest package,
        StrategicMapCanonicalDefinition canonical,
        string path)
    {
        if (!string.Equals(scenario.MapId, package.MapId, StringComparison.Ordinal) ||
            scenario.PackageCompatibilityRevision != package.CompatibilityRevision ||
            scenario.ScenarioContentRevision <= 0 || scenario.DefaultCityForceCapacity <= 0 ||
            scenario.DefaultCityReserveForces < 0 || scenario.DefaultCityReserveForces > scenario.DefaultCityForceCapacity)
        {
            throw new InvalidOperationException($"Strategic scenario package compatibility mismatch ScenarioId={scenario.ScenarioId} MapId={scenario.MapId} path={path}");
        }
        ValidateComposition(scenario, canonical, path);
    }

    public static void ValidateComposition(
        StrategicManagementScenarioDefinition scenario,
        StrategicMapCanonicalDefinition canonical,
        string source)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(canonical);
        Dictionary<string, StrategicScenarioProvinceStart> starts;
        try
        {
            starts = scenario.Provinces.ToDictionary(item => item.ProvinceId, StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"Strategic scenario contains duplicate ProvinceId source={source}", exception);
        }
        HashSet<string> canonicalProvinces = canonical.Geography.Provinces.Select(item => item.ProvinceId).ToHashSet(StringComparer.Ordinal);
        if (!starts.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(canonicalProvinces))
        {
            throw new InvalidOperationException($"Strategic scenario province coverage mismatch ScenarioId={scenario.ScenarioId} MapId={scenario.MapId} source={source}");
        }

        Dictionary<string, Rpg.Definitions.StrategicMap.StrategicLocationDefinition> canonicalLocations = canonical.Geography.Locations
            .GroupBy(item => item.LocationId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> assignedLocations = new(StringComparer.Ordinal);
        foreach (StrategicScenarioLocationStart location in scenario.Locations)
        {
            if (!assignedLocations.Add(location.LocationId))
            {
                throw new InvalidOperationException($"Strategic scenario contains duplicate LocationId={location.LocationId} source={source}");
            }
            if (!canonicalLocations.TryGetValue(location.LocationId, out Rpg.Definitions.StrategicMap.StrategicLocationDefinition? canonicalLocation))
            {
                throw new InvalidOperationException($"Strategic scenario references unknown LocationId={location.LocationId} source={source}");
            }
            if (canonicalLocation.LocationType is StrategicLocationType.MainCity or StrategicLocationType.AuxiliaryCity &&
                canonicalLocation.ProvinceId is not null && starts.ContainsKey(canonicalLocation.ProvinceId))
            {
                throw new InvalidOperationException(
                    $"Strategic scenario assigns city through both province and location LocationId={location.LocationId} ProvinceId={canonicalLocation.ProvinceId} source={source}");
            }
        }

        HashSet<(string FactionId, string ResourceId)> assignedResources = new();
        foreach (StrategicScenarioResourceStart resource in scenario.Resources)
        {
            if (resource.Amount < 0)
            {
                throw new InvalidOperationException($"Strategic scenario resource amount is negative FactionId={resource.FactionId} ResourceId={resource.ResourceId} source={source}");
            }
            if (!assignedResources.Add((resource.FactionId, resource.ResourceId)))
            {
                throw new InvalidOperationException($"Strategic scenario contains duplicate resource assignment FactionId={resource.FactionId} ResourceId={resource.ResourceId} source={source}");
            }
        }
    }

    private static StrategicScenarioProvinceRole ParseProvinceRole(string value, string path) => value switch
    {
        "player-start" => StrategicScenarioProvinceRole.PlayerStart,
        "first-hostile" => StrategicScenarioProvinceRole.FirstHostile,
        "neutral" => StrategicScenarioProvinceRole.Neutral,
        _ => throw new InvalidOperationException($"Unknown strategic scenario province role={value} path={path}")
    };

    private static StrategicScenarioControl ParseControl(string value, string path) => value switch
    {
        "player-held" => StrategicScenarioControl.PlayerHeld,
        "enemy-held" => StrategicScenarioControl.EnemyHeld,
        "neutral" => StrategicScenarioControl.Neutral,
        _ => throw new InvalidOperationException($"Unknown strategic scenario control={value} path={path}")
    };

    private static int RequiredInt(JsonElement element, string propertyName, string path) =>
        element.GetProperty(propertyName).TryGetInt32(out int value)
            ? value
            : throw new InvalidOperationException($"Strategic scenario integer is invalid property={propertyName} path={path}");

    private static string RequiredString(JsonElement element, string propertyName, string path)
    {
        string value = element.GetProperty(propertyName).GetString() ?? "";
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Strategic scenario property is empty property={propertyName} path={path}")
            : value;
    }
}
