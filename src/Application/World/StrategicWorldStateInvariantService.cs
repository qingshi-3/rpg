using System.Linq;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicWorldStateInvariantService
{
    public int RepairResolvedArmyPlacements(StrategicWorldState state)
    {
        if (state?.SiteStates == null)
        {
            return 0;
        }

        int removed = 0;
        foreach (WorldSiteState site in state.SiteStates.Values.Where(site => site?.UnitPlacements != null))
        {
            removed += site.UnitPlacements.RemoveAll(placement =>
                placement != null &&
                placement.SourceKind == "PlayerArmy" &&
                placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker &&
                IsResolvedOrInvalidArmy(state, placement.SourceId));
        }

        if (removed > 0)
        {
            GameLog.Warn(
                nameof(StrategicWorldStateInvariantService),
                $"StrategicWorldStateInvariantRepaired resolvedArmyPlacementsRemoved={removed}");
        }

        return removed;
    }

    public int RepairGarrisonMetadata(StrategicWorldState state)
    {
        if (state?.SiteStates == null)
        {
            return 0;
        }

        int repaired = 0;
        foreach (WorldSiteState site in state.SiteStates.Values.Where(site => site?.Garrison != null))
        {
            foreach (GarrisonState garrison in site.Garrison.Where(garrison => garrison != null))
            {
                if (string.IsNullOrWhiteSpace(garrison.FactionId))
                {
                    garrison.FactionId = site.OwnerFactionId ?? "";
                    repaired++;
                }

                if (string.IsNullOrWhiteSpace(garrison.SourceKind))
                {
                    garrison.SourceKind = "Garrison";
                    repaired++;
                }

                if (string.IsNullOrWhiteSpace(garrison.SourceId))
                {
                    garrison.SourceId = site.SiteId ?? "";
                    repaired++;
                }
            }
        }

        if (repaired > 0)
        {
            GameLog.Warn(
                nameof(StrategicWorldStateInvariantService),
                $"StrategicWorldStateInvariantRepaired garrisonMetadataFields={repaired}");
        }

        return repaired;
    }

    public int RepairAll(StrategicWorldState state)
    {
        return RepairGarrisonMetadata(state) + RepairResolvedArmyPlacements(state);
    }

    private static bool IsResolvedOrInvalidArmy(StrategicWorldState state, string armyId)
    {
        if (string.IsNullOrWhiteSpace(armyId) ||
            state?.ArmyStates == null ||
            !state.ArmyStates.TryGetValue(armyId, out WorldArmyState army) ||
            army == null)
        {
            return true;
        }

        if (army.Status is WorldArmyStatus.Garrisoned or WorldArmyStatus.Defeated)
        {
            return true;
        }

        return army.GarrisonUnits == null ||
               army.GarrisonUnits.Sum(unit => System.Math.Max(0, unit?.Count ?? 0)) <= 0;
    }
}
