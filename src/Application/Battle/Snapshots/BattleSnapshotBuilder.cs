using System.Collections.Generic;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSnapshotBuilder
{
    public BattleStartSnapshot Build(
        string snapshotId,
        string battleId,
        string targetLocationId,
        IEnumerable<BattleGroupState> battleGroups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            TargetLocationId = targetLocationId ?? "",
            LocationContext = new LocationBattleContext { LocationId = targetLocationId ?? "" }
        };

        if (battleGroups == null)
        {
            return snapshot;
        }

        foreach (BattleGroupState group in battleGroups)
        {
            if (group == null ||
                !heroes.TryGetValue(group.HeroId, out HeroState hero) ||
                !corps.TryGetValue(group.CorpsId, out CorpsState corpsState))
            {
                continue;
            }

            snapshot.BattleGroups.Add(new BattleGroupSnapshot
            {
                BattleGroupId = group.BattleGroupId,
                HeroId = hero.HeroId,
                HeroDefinitionId = hero.HeroDefinitionId,
                HeroLevel = hero.Level,
                CorpsId = corpsState.CorpsId,
                CorpsDefinitionId = corpsState.CorpsDefinitionId,
                CorpsLevel = corpsState.Level,
                CorpsEquipmentLevel = corpsState.EquipmentLevel,
                CorpsStrength = CorpsStrengthPolicy.Clamp(corpsState.CorpsStrength),
                SourceLocationId = group.CurrentLocationId
            });
        }

        return snapshot;
    }
}
