using System;
using System.Linq;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldGarrisonMutationService
{
    public int Add(WorldSiteState site, string unitTypeId, int count)
    {
        if (site == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return 0;
        }

        GarrisonState garrison = site.Garrison.FirstOrDefault(item => item.UnitTypeId == unitTypeId);
        if (garrison == null)
        {
            site.Garrison.Add(new GarrisonState { UnitTypeId = unitTypeId, Count = count });
        }
        else
        {
            garrison.Count += count;
        }

        return count;
    }

    public int Remove(WorldSiteState site, string unitTypeId, int count)
    {
        if (site == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return 0;
        }

        int remaining = count;
        int removedTotal = 0;
        foreach (GarrisonState garrison in site.Garrison.Where(item => item.UnitTypeId == unitTypeId).ToArray())
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
}
