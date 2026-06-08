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
        if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
        {
            return false;
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
}
