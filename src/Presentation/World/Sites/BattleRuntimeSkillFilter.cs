using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeSkillFilter
{
    internal static IReadOnlyList<BattleSkillSnapshot> FilterForGroup(
        IReadOnlyList<BattleSkillSnapshot> skills,
        BattleRuntimeCommandGroupView selected)
    {
        return (skills ?? System.Array.Empty<BattleSkillSnapshot>())
            .Where(skill => IsBattleRuntimeSkillAvailableForGroup(selected, skill))
            .ToArray();
    }

    internal static bool IsBattleRuntimeSkillAvailableForGroup(
        BattleRuntimeCommandGroupView selected,
        BattleSkillSnapshot skill) =>
        IsBattleRuntimeSkillAvailableForGroup(selected, skill, skill?.CasterUnitIds);

    internal static bool IsBattleRuntimeSkillAvailableForGroup(
        BattleRuntimeCommandGroupView selected,
        BattleSkillSnapshot skill,
        IEnumerable<string> casterUnitIds)
    {
        if (skill == null || string.IsNullOrWhiteSpace(ResolveSkillDefinitionId(skill)))
        {
            return false;
        }

        string groupKey = selected?.GroupKey ?? "";
        if (!string.IsNullOrWhiteSpace(skill.OwnerBattleGroupId) ||
            !string.IsNullOrWhiteSpace(skill.RuntimeCommanderGroupId))
        {
            return !string.IsNullOrWhiteSpace(groupKey) &&
                   (string.IsNullOrWhiteSpace(skill.OwnerBattleGroupId) ||
                    string.Equals(skill.OwnerBattleGroupId, groupKey, System.StringComparison.Ordinal)) &&
                   (string.IsNullOrWhiteSpace(skill.RuntimeCommanderGroupId) ||
                    string.Equals(skill.RuntimeCommanderGroupId, groupKey, System.StringComparison.Ordinal));
        }

        HashSet<string> casterUnitIdSet = (casterUnitIds ?? System.Array.Empty<string>())
            .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
            .Select(unitId => unitId.Trim())
            .ToHashSet(System.StringComparer.Ordinal);
        if (casterUnitIdSet.Count == 0)
        {
            return true;
        }

        return selected?.Forces != null &&
               selected.Forces.Any(force => casterUnitIdSet.Contains(force?.UnitDefinitionId ?? ""));
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }
}
