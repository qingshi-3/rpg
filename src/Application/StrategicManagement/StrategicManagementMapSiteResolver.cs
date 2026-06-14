using System.Linq;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementMapSiteResolver
{
    private readonly StrategicManagementDefinitionSet _definitions;

    public StrategicManagementMapSiteResolver(StrategicManagementDefinitionSet definitions)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
    }

    public bool TryResolveLocationId(string mapSiteId, out string locationId)
    {
        return TryResolveLocationIdForMapSite(mapSiteId, out locationId);
    }

    public bool TryResolveLocationIdForMapSite(string mapSiteId, out string locationId)
    {
        locationId = "";
        if (string.IsNullOrWhiteSpace(mapSiteId))
        {
            return false;
        }

        StrategicLocationDefinition location = _definitions.Locations.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.MapSiteId, mapSiteId, System.StringComparison.Ordinal));
        if (location == null)
        {
            return false;
        }

        locationId = location.LocationId;
        return true;
    }

    public bool TryResolveCityId(string mapSiteId, out string cityId)
    {
        return TryResolveCityIdForMapSite(mapSiteId, out cityId);
    }

    public bool TryResolveCityIdForMapSite(string mapSiteId, out string cityId)
    {
        cityId = "";
        if (!TryResolveLocationIdForMapSite(mapSiteId, out string locationId) ||
            !_definitions.Locations.TryGetValue(locationId, out StrategicLocationDefinition location) ||
            location.Kind != StrategicLocationKind.City)
        {
            return false;
        }

        cityId = location.LocationId;
        return true;
    }
}
