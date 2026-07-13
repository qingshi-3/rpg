namespace Rpg.Application.World;

using Rpg.Definitions.StrategicManagement;

// Migration-only boundary for the retained legacy main scene. Delete with that scene in Stage 6.
public static class TemporaryLegacyStrategicSiteIdentityAdapter
{
    public const string LegacyPlayerCampSiteId = "player_camp";
    public const string LegacyBonefieldSiteId = "bonefield";

    public static bool TryResolveLocationId(string legacySiteId, out string locationId)
    {
        locationId = legacySiteId switch
        {
            LegacyPlayerCampSiteId => StrategicManagementIds.LocationQingheCore,
            LegacyBonefieldSiteId => StrategicManagementIds.LocationChiyanHighBasin,
            _ => ""
        };
        return !string.IsNullOrWhiteSpace(locationId);
    }

    public static bool TryResolveCityId(string legacySiteId, out string cityId) =>
        TryResolveLocationId(legacySiteId, out cityId);
}
