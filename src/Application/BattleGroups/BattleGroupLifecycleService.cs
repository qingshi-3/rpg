using System;
using Rpg.Domain.BattleGroups;

namespace Rpg.Application.BattleGroups;

public sealed class BattleGroupLifecycleService
{
    public BattleGroupState CreateAndStation(
        string battleGroupId,
        string heroId,
        string corpsId,
        string locationId)
    {
        RequireIdentity(battleGroupId, nameof(battleGroupId));
        RequireIdentity(heroId, nameof(heroId));
        RequireIdentity(corpsId, nameof(corpsId));
        RequireIdentity(locationId, nameof(locationId));

        return new BattleGroupState
        {
            BattleGroupId = battleGroupId,
            HeroId = heroId,
            CorpsId = corpsId,
            CurrentLocationId = locationId,
            Status = BattleGroupStatus.Stationed
        };
    }

    public bool TryLockForBattle(BattleGroupState group, string battleId)
    {
        if (group?.CanSortie != true || !HasRequiredIdentity(group) || string.IsNullOrWhiteSpace(battleId))
        {
            return false;
        }

        group.Status = BattleGroupStatus.InBattle;
        group.ActiveBattleId = battleId;
        return true;
    }

    public void ReleaseAfterBattle(BattleGroupState group)
    {
        if (group == null || group.Status != BattleGroupStatus.InBattle)
        {
            return;
        }

        group.Status = string.IsNullOrWhiteSpace(group.CurrentLocationId)
            ? BattleGroupStatus.Available
            : BattleGroupStatus.Stationed;
        group.ActiveBattleId = "";
    }

    private static void RequireIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Battle group lifecycle identities are required.", parameterName);
        }
    }

    private static bool HasRequiredIdentity(BattleGroupState group)
    {
        return !string.IsNullOrWhiteSpace(group.BattleGroupId) &&
            !string.IsNullOrWhiteSpace(group.HeroId) &&
            !string.IsNullOrWhiteSpace(group.CorpsId);
    }
}
