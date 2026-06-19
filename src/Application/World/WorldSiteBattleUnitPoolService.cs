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
                    garrison.SourceId == army.ArmyId &&
                    string.Equals(garrison.StrategicParticipantId ?? "", unit.StrategicParticipantId ?? "", System.StringComparison.Ordinal))
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
                unit.Morale,
                unit.StrategicParticipantId);
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

    public int RemoveImportedArmyForSiteBattle(WorldSiteState site, string armyId)
    {
        if (site == null || string.IsNullOrWhiteSpace(armyId))
        {
            return 0;
        }

        int removedUnits = site.Garrison
            .Where(garrison =>
                garrison != null &&
                string.Equals(garrison.SourceKind, "PlayerArmy", System.StringComparison.Ordinal) &&
                string.Equals(garrison.SourceId, armyId, System.StringComparison.Ordinal))
            .Sum(garrison => System.Math.Max(0, garrison.Count));
        if (removedUnits <= 0)
        {
            return 0;
        }

        site.Garrison.RemoveAll(garrison =>
            garrison != null &&
            string.Equals(garrison.SourceKind, "PlayerArmy", System.StringComparison.Ordinal) &&
            string.Equals(garrison.SourceId, armyId, System.StringComparison.Ordinal));
        GameLog.Info(
            nameof(WorldSiteBattleUnitPoolService),
            $"ImportedArmyRemovedFromSiteBattlePool site={site.SiteId} army={armyId} units={removedUnits}");
        return removedUnits;
    }
}
