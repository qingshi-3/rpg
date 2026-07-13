using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle;

public sealed partial class BattleRuntimeSession
{
    private static List<BattleSkillSnapshot> CloneSkillDefinitions(
        IEnumerable<BattleSkillSnapshot> skillDefinitions)
    {
        // Skill snapshots have already passed the production launch gate. Runtime keeps
        // its existing identity normalization while the compiler-owned utility performs
        // the structure-preserving deep copy.
        return (skillDefinitions ?? Enumerable.Empty<BattleSkillSnapshot>())
            .Select(skill =>
            {
                BattleSkillSnapshot clone = BattleSkillSnapshotCopy.DeepClone(skill);
                clone.SkillDefinitionId = ResolveSkillDefinitionId(skill);
                clone.Tags = (skill.Tags ?? new List<string>())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(System.StringComparer.Ordinal)
                    .ToList();
                clone.CasterUnitIds = (skill.CasterUnitIds ?? new List<string>())
                    .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
                    .Select(unitId => unitId.Trim())
                    .Distinct(System.StringComparer.Ordinal)
                    .ToList();
                return clone;
            })
            .ToList();
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }
}
