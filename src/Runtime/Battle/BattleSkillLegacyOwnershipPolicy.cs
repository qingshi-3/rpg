using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle;

internal static class BattleSkillLegacyOwnershipPolicy
{
    internal static bool IsAllowedForCasterGroup(
        BattleRuntimeState state,
        BattleRuntimeActor caster,
        BattleSkillSnapshot skill)
    {
        HashSet<string> casterUnitIds = (skill?.CasterUnitIds ?? new List<string>())
            .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
            .Select(unitId => unitId.Trim())
            .ToHashSet(System.StringComparer.Ordinal);
        if (casterUnitIds.Count == 0)
        {
            return true;
        }

        if (state?.Actors == null || caster == null)
        {
            return false;
        }

        return state.Actors.Any(actor =>
            actor.HitPoints > 0 &&
            string.Equals(actor.BattleGroupId, caster.BattleGroupId ?? "", System.StringComparison.Ordinal) &&
            casterUnitIds.Contains(actor.UnitDefinitionId ?? ""));
    }
}
