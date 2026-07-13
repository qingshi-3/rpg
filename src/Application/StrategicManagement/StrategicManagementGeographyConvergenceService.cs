#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.StrategicMap;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.StrategicMap;
using ManagementLocationDefinition = Rpg.Definitions.StrategicManagement.StrategicLocationDefinition;

namespace Rpg.Application.StrategicManagement;

public static class StrategicManagementGeographyConvergenceService
{
    public static void Converge(
        StrategicManagementDefinitionSet definitions,
        StrategicMapCanonicalDefinition canonical) =>
        Converge(definitions, canonical, definitions.Scenario);

    public static void Converge(
        StrategicManagementDefinitionSet definitions,
        StrategicMapCanonicalDefinition canonical,
        StrategicManagementScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(scenario);
        StrategicMapValidator.ThrowIfInvalid(canonical);

        Dictionary<string, StrategicScenarioProvinceStart> scenarioProvinces = scenario.Provinces
            .ToDictionary(province => province.ProvinceId, StringComparer.Ordinal);

        Dictionary<string, StrategicManagementProvinceReference> provinces = canonical.Geography.Provinces
            .ToDictionary(
                province => province.ProvinceId,
                province => new StrategicManagementProvinceReference(
                    province.ProvinceId,
                    province.LayoutId,
                    scenarioProvinces.TryGetValue(province.ProvinceId, out StrategicScenarioProvinceStart? start)
                        ? start.Role
                        : throw new InvalidOperationException($"Strategic scenario is missing ProvinceId={province.ProvinceId}")),
                StringComparer.Ordinal);
        Dictionary<string, StrategicManagementCityReference> cities = new(StringComparer.Ordinal);

        foreach (Rpg.Definitions.StrategicMap.StrategicLocationDefinition location in canonical.Geography.Locations
                     .Where(location => location.LocationType is StrategicLocationType.MainCity or StrategicLocationType.AuxiliaryCity))
        {
            string provinceId = location.ProvinceId ?? "";
            if (!provinces.TryGetValue(provinceId, out StrategicManagementProvinceReference? province))
            {
                throw new InvalidOperationException(
                    $"Strategic management canonical city references unknown province ProvinceId={provinceId} LocationId={location.LocationId}");
            }
            if (string.IsNullOrWhiteSpace(province.LayoutId))
            {
                throw new InvalidOperationException(
                    $"Strategic management canonical city has empty layout ProvinceId={provinceId} LocationId={location.LocationId} LayoutId=<empty>");
            }
            if (!cities.TryAdd(location.LocationId, new StrategicManagementCityReference(
                    provinceId,
                    location.LocationId,
                    province.LayoutId,
                    location.LocationType,
                    province.ScenarioRole)))
            {
                throw new InvalidOperationException(
                    $"Strategic management canonical city is ambiguous ProvinceId={provinceId} LocationId={location.LocationId} LayoutId={province.LayoutId}");
            }
        }

        foreach (StrategicManagementCityReference city in cities.Values)
        {
            if (!definitions.Locations.TryGetValue(city.LocationId, out ManagementLocationDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Strategic management city definition is missing ProvinceId={city.ProvinceId} LocationId={city.LocationId} LayoutId={city.LayoutId}");
            }
            if (!string.Equals(definition.LocationId, city.LocationId, StringComparison.Ordinal) ||
                definition.Kind != StrategicLocationKind.City)
            {
                throw new InvalidOperationException(
                    $"Strategic management city definition mismatches canonical identity ProvinceId={city.ProvinceId} LocationId={city.LocationId} LayoutId={city.LayoutId}");
            }
        }

        foreach (ManagementLocationDefinition definition in definitions.Locations.Values
                     .Where(location => location.Kind == StrategicLocationKind.City))
        {
            if (!cities.ContainsKey(definition.LocationId))
            {
                throw new InvalidOperationException(
                    $"Strategic management city definition is outside canonical geography ProvinceId=<missing> LocationId={definition.LocationId} LayoutId=<missing>");
            }
        }

        definitions.CanonicalGeography = new StrategicManagementCanonicalGeographyReference(provinces, cities);
    }
}
