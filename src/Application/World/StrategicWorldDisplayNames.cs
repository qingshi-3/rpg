using Rpg.Definitions.World;

namespace Rpg.Application.World;

public static class StrategicWorldDisplayNames
{
    public static string GetResourceLabel(StrategicWorldDefinitionQueries queries, string resourceId, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "无" : fallback;
        }

        string displayName = queries?.GetResource(resourceId)?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName)
            ? string.IsNullOrWhiteSpace(fallback) ? resourceId : fallback
            : displayName;
    }

    public static string GetFactionLabel(StrategicWorldDefinitionQueries queries, string factionId, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "无" : fallback;
        }

        string displayName = queries?.GetFaction(factionId)?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName)
            ? string.IsNullOrWhiteSpace(fallback) ? factionId : fallback
            : displayName;
    }

    public static string GetSiteLabel(StrategicWorldDefinitionQueries queries, string siteId, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "无" : fallback;
        }

        string displayName = queries?.GetSite(siteId)?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName)
            ? string.IsNullOrWhiteSpace(fallback) ? siteId : fallback
            : displayName;
    }

    public static string GetFacilityLabel(StrategicWorldDefinitionQueries queries, string facilityId, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "无" : fallback;
        }

        string displayName = queries?.GetFacility(facilityId)?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName)
            ? string.IsNullOrWhiteSpace(fallback) ? facilityId : fallback
            : displayName;
    }
}
