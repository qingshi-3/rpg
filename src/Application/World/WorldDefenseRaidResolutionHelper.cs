using Rpg.Domain.World;

namespace Rpg.Application.World;

internal static class WorldDefenseRaidResolutionHelper
{
    private static readonly WorldGarrisonMutationService GarrisonMutations = new();

    internal static void ResolveThreatArmy(WorldArmyState army)
    {
        if (army == null)
        {
            return;
        }

        army.Status = WorldArmyStatus.Defeated;
        army.ClearNavigationPath();
        army.ClearArrivalApproachOffset();
        army.ClearTargetApproachDirection();
    }

    internal static void ClearDefendingGarrison(WorldSiteState site)
    {
        if (site == null)
        {
            return;
        }

        site.Garrison.Clear();
    }

    internal static void TransferThreatArmyToCapturedSite(WorldArmyState army, WorldSiteState site)
    {
        if (army == null || site == null)
        {
            return;
        }

        foreach (GarrisonState unit in army.GarrisonUnits)
        {
            GarrisonMutations.Add(site, unit.UnitTypeId, unit.Count);
        }

        army.GarrisonUnits.Clear();
        army.Status = WorldArmyStatus.Garrisoned;
        army.SourceSiteId = site.SiteId;
        army.TargetSiteId = site.SiteId;
        army.ClearNavigationPath();
        army.ClearArrivalApproachOffset();
        army.ClearTargetApproachDirection();
    }

}
