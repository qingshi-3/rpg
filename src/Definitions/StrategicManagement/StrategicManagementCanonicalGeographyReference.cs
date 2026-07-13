#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Definitions.StrategicManagement;

public sealed record StrategicManagementProvinceReference(
    string ProvinceId,
    string LayoutId,
    StrategicScenarioProvinceRole ScenarioRole);

public sealed record StrategicManagementCityReference(
    string ProvinceId,
    string LocationId,
    string LayoutId,
    StrategicLocationType LocationType,
    StrategicScenarioProvinceRole ScenarioRole);

// This is a read-only projection of canonical StrategicMap geography, never separately authored content.
public sealed class StrategicManagementCanonicalGeographyReference
{
    public static StrategicManagementCanonicalGeographyReference Empty { get; } = new(
        new Dictionary<string, StrategicManagementProvinceReference>(StringComparer.Ordinal),
        new Dictionary<string, StrategicManagementCityReference>(StringComparer.Ordinal));

    public StrategicManagementCanonicalGeographyReference(
        IDictionary<string, StrategicManagementProvinceReference> provinces,
        IDictionary<string, StrategicManagementCityReference> cities)
    {
        Provinces = new ReadOnlyDictionary<string, StrategicManagementProvinceReference>(
            new Dictionary<string, StrategicManagementProvinceReference>(provinces, StringComparer.Ordinal));
        Cities = new ReadOnlyDictionary<string, StrategicManagementCityReference>(
            new Dictionary<string, StrategicManagementCityReference>(cities, StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, StrategicManagementProvinceReference> Provinces { get; }
    public IReadOnlyDictionary<string, StrategicManagementCityReference> Cities { get; }
}
