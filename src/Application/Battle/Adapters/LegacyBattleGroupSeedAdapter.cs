using System.Collections.Generic;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleGroupSeedAdapter
{
    public IReadOnlyList<BattleGroupState> SeedFromGarrison(WorldSiteState site, string heroIdPrefix)
    {
        List<BattleGroupState> groups = new();
        if (site == null)
        {
            return groups;
        }

        int index = 0;
        foreach (GarrisonState garrison in site.Garrison)
        {
            for (int count = 0; count < garrison.Count; count++)
            {
                groups.Add(new BattleGroupState
                {
                    BattleGroupId = $"{site.SiteId}:{garrison.UnitTypeId}:{index}",
                    HeroId = $"{heroIdPrefix}_{index}",
                    CorpsId = $"{garrison.UnitTypeId}_corps_{index}",
                    CurrentLocationId = site.SiteId,
                    Status = BattleGroupStatus.Stationed
                });
                index++;
            }
        }

        GameLog.Info(nameof(LegacyBattleGroupSeedAdapter), $"Seeded battle groups from legacy garrison site={site.SiteId} count={groups.Count}");
        return groups;
    }
}
