using System;
using System.Linq;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldGarrisonMutationService
{
    public int Add(WorldSiteState site, string unitTypeId, int count)
    {
        return Add(site, unitTypeId, count, "", "", "", morale: 50);
    }

    public int Add(
        WorldSiteState site,
        string unitTypeId,
        int count,
        string factionId,
        string sourceKind,
        string sourceId,
        int morale = 50)
    {
        if (site == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return 0;
        }

        GarrisonState garrison = site.Garrison.FirstOrDefault(item =>
            item.UnitTypeId == unitTypeId &&
            SameOptionalValue(item.FactionId, factionId) &&
            SameOptionalValue(item.SourceKind, sourceKind) &&
            SameOptionalValue(item.SourceId, sourceId));
        if (garrison == null)
        {
            site.Garrison.Add(new GarrisonState
            {
                UnitTypeId = unitTypeId,
                Count = count,
                FactionId = factionId ?? "",
                SourceKind = sourceKind ?? "",
                SourceId = sourceId ?? "",
                Morale = morale
            });
        }
        else
        {
            garrison.Count += count;
            if (!string.IsNullOrWhiteSpace(factionId))
            {
                garrison.FactionId = factionId;
            }

            if (!string.IsNullOrWhiteSpace(sourceKind))
            {
                garrison.SourceKind = sourceKind;
            }

            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                garrison.SourceId = sourceId;
            }
        }

        return count;
    }

    public int Remove(WorldSiteState site, string unitTypeId, int count)
    {
        return Remove(site, unitTypeId, count, "", "", "");
    }

    public int Remove(
        WorldSiteState site,
        string unitTypeId,
        int count,
        string factionId,
        string sourceKind,
        string sourceId)
    {
        if (site == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return 0;
        }

        int remaining = count;
        int removedTotal = 0;
        foreach (GarrisonState garrison in site.Garrison.Where(item =>
                     item.UnitTypeId == unitTypeId &&
                     MatchesOptionalFilter(item.FactionId, factionId) &&
                     MatchesOptionalFilter(item.SourceKind, sourceKind) &&
                     MatchesOptionalFilter(item.SourceId, sourceId)).ToArray())
        {
            int removed = Math.Min(remaining, garrison.Count);
            garrison.Count -= removed;
            remaining -= removed;
            removedTotal += removed;
            if (garrison.Count <= 0)
            {
                site.Garrison.Remove(garrison);
            }

            if (remaining <= 0)
            {
                break;
            }
        }

        return removedTotal;
    }

    private static bool SameOptionalValue(string current, string expected)
    {
        return string.IsNullOrWhiteSpace(expected) ||
               string.Equals(current ?? "", expected, StringComparison.Ordinal);
    }

    private static bool MatchesOptionalFilter(string current, string expected)
    {
        return string.IsNullOrWhiteSpace(expected) ||
               string.Equals(current ?? "", expected, StringComparison.Ordinal);
    }
}
