using System.Linq;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteBattleUnitPoolService
{
    private readonly WorldGarrisonMutationService _garrisonMutations = new();

    public int ImportArmyForSiteBattle(WorldSiteState site, WorldArmyState army, string factionId)
    {
        if (site == null || army == null || string.IsNullOrWhiteSpace(army.ArmyId))
        {
            return 0;
        }

        int imported = 0;
        foreach (GarrisonState unit in army.GarrisonUnits.Where(item =>
                     item != null &&
                     item.Count > 0 &&
                     !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            int existing = site.Garrison
                .Where(garrison =>
                    garrison.UnitTypeId == unit.UnitTypeId &&
                    garrison.SourceKind == "PlayerArmy" &&
                    garrison.SourceId == army.ArmyId)
                .Sum(garrison => garrison.Count);
            int missing = System.Math.Max(0, unit.Count - existing);
            if (missing <= 0)
            {
                continue;
            }

            imported += _garrisonMutations.Add(
                site,
                unit.UnitTypeId,
                missing,
                factionId,
                "PlayerArmy",
                army.ArmyId,
                unit.Morale);
        }

        if (imported > 0)
        {
            GameLog.Info(
                nameof(WorldSiteBattleUnitPoolService),
                $"ArmyImportedIntoSiteBattlePool site={site.SiteId} army={army.ArmyId} units={imported}");
        }

        return imported;
    }

    public bool HasImportedArmy(WorldSiteState site, string armyId)
    {
        return site?.Garrison?.Any(garrison =>
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId &&
            garrison.Count > 0) == true;
    }
}
