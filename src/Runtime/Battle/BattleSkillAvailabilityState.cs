using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle;

public sealed class BattleSkillAvailabilityState
{
    private readonly Dictionary<string, BattleSkillAvailabilityEntry> _entries = new(System.StringComparer.Ordinal);

    internal void Initialize(IEnumerable<BattleSkillSnapshot> skills)
    {
        _entries.Clear();
        foreach (BattleSkillSnapshot skill in skills ?? Enumerable.Empty<BattleSkillSnapshot>())
        {
            string key = BuildKey("", skill);
            if (string.IsNullOrWhiteSpace(key) || _entries.ContainsKey(key))
            {
                continue;
            }

            _entries[key] = new BattleSkillAvailabilityEntry
            {
                RemainingUses = ResolveInitialUses(skill)
            };
        }
    }

    internal bool CanSubmit(string battleGroupId, BattleSkillSnapshot skill, out string reasonCode)
    {
        reasonCode = "";
        BattleSkillAvailabilityEntry entry = GetEntry(battleGroupId, skill);
        if (entry.RemainingUses == 0)
        {
            reasonCode = "skill_use_limit_exhausted";
            return false;
        }

        return true;
    }

    internal void MarkReleased(string battleGroupId, BattleSkillSnapshot skill)
    {
        BattleSkillAvailabilityEntry entry = GetEntry(battleGroupId, skill);
        if (entry.RemainingUses > 0)
        {
            entry.RemainingUses--;
        }
    }

    private BattleSkillAvailabilityEntry GetEntry(string battleGroupId, BattleSkillSnapshot skill)
    {
        string key = BuildKey(battleGroupId, skill);
        if (string.IsNullOrWhiteSpace(key))
        {
            return BattleSkillAvailabilityEntry.Unlimited;
        }

        if (!_entries.TryGetValue(key, out BattleSkillAvailabilityEntry entry))
        {
            entry = new BattleSkillAvailabilityEntry
            {
                RemainingUses = ResolveInitialUses(skill)
            };
            _entries[key] = entry;
        }

        return entry;
    }

    private static string BuildKey(string battleGroupId, BattleSkillSnapshot skill)
    {
        if (!string.IsNullOrWhiteSpace(skill?.GrantedSkillId))
        {
            return $"grant:{skill.GrantedSkillId.Trim()}";
        }

        string owner = !string.IsNullOrWhiteSpace(skill?.OwnerHeroId)
            ? $"hero:{skill.OwnerHeroId.Trim()}"
            : !string.IsNullOrWhiteSpace(skill?.OwnerBattleGroupId)
            ? skill.OwnerBattleGroupId.Trim()
            : battleGroupId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(owner) &&
            !string.IsNullOrWhiteSpace(skill?.LoadoutSlotId))
        {
            return $"loadout:{owner}:{skill.LoadoutSlotId.Trim()}";
        }

        string definitionId = ResolveSkillDefinitionId(skill);
        return string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(definitionId)
            ? ""
            : $"definition:{owner}:{definitionId}";
    }

    private static int ResolveInitialUses(BattleSkillSnapshot skill)
    {
        LimitedUseSkillCostSnapshot limitedUse = skill?.Costs?
            .OfType<LimitedUseSkillCostSnapshot>()
            .FirstOrDefault();
        if (limitedUse != null)
        {
            return System.Math.Max(0, limitedUse.MaxUses);
        }

        // Legacy in-memory fixtures still omit cost snapshots while representing
        // migrated first-slice one-use hero skills. Authored resources compile an
        // explicit LimitedUseSkillCostSnapshot and do not rely on this branch.
        return 1;
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }

    private sealed class BattleSkillAvailabilityEntry
    {
        internal static BattleSkillAvailabilityEntry Unlimited { get; } = new()
        {
            RemainingUses = -1
        };

        internal int RemainingUses { get; set; }
    }
}
