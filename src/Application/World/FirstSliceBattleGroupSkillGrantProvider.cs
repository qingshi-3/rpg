using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Application.World;

public sealed class FirstSliceBattleGroupSkillGrantProvider
{
    public IReadOnlyList<BattleSkillGrantSnapshot> CreateGrants(IEnumerable<BattleGroupSnapshot> participatingGroups)
    {
        List<BattleSkillGrantSnapshot> grants = new();
        HashSet<string> emittedGrantIds = new(StringComparer.Ordinal);
        HashSet<string> emittedOwnerSlots = new(StringComparer.Ordinal);
        foreach (BattleGroupSnapshot group in participatingGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            if (group == null)
            {
                continue;
            }

            if (!IsPlayerFaction(group.FactionId))
            {
                continue;
            }

            if (!TryResolveCompany(group, out FirstSliceHeroCompanyDefinition company))
            {
                continue;
            }

            string runtimeCommanderGroupId = BattleCommanderGroupIdentity.Resolve(group);
            string ownerBattleGroupId = string.IsNullOrWhiteSpace(runtimeCommanderGroupId)
                ? group.BattleGroupId ?? ""
                : runtimeCommanderGroupId;
            int index = 0;
            foreach (string rawSkillDefinitionId in company.SkillDefinitionIds ?? Enumerable.Empty<string>())
            {
                string skillDefinitionId = rawSkillDefinitionId?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(skillDefinitionId))
                {
                    continue;
                }

                string slotId = ResolveSlotId(company, index);
                string ownerHeroId = group.HeroId ?? "";
                string ownerSlotKey = $"{ResolveOwnerSlotPrefix(ownerHeroId, ownerBattleGroupId)}:{slotId}";
                if (!emittedOwnerSlots.Add(ownerSlotKey))
                {
                    index++;
                    continue;
                }

                string grantedSkillId = ResolveGrantId(ownerHeroId, company, slotId);
                if (!emittedGrantIds.Add(grantedSkillId))
                {
                    index++;
                    continue;
                }

                grants.Add(new BattleSkillGrantSnapshot
                {
                    // Default grants are authored first-slice assignment facts. The
                    // current config is group-shaped, but skill ownership follows
                    // the stable hero id; battle-group ids remain combat context.
                    GrantedSkillId = grantedSkillId,
                    LoadoutSlotId = slotId,
                    OwnerHeroId = ownerHeroId,
                    OwnerBattleGroupId = ownerBattleGroupId,
                    RuntimeCommanderGroupId = runtimeCommanderGroupId,
                    SkillDefinitionId = skillDefinitionId,
                    SourceKind = "first_slice_default_hero_skill",
                    SourceId = company.CompanyId,
                    SkillLevel = 1
                });
                index++;
            }
        }

        return grants;
    }

    private static bool TryResolveCompany(
        BattleGroupSnapshot group,
        out FirstSliceHeroCompanyDefinition company)
    {
        foreach (string unitId in EnumerateGroupUnitIds(group))
        {
            if (FirstSliceHeroCompanyIds.TryGetCompanyByAnyUnit(unitId, out company))
            {
                return true;
            }
        }

        company = null;
        return false;
    }

    private static bool IsPlayerFaction(string factionId)
    {
        string normalized = factionId ?? "";
        // This first-slice adapter is fed by both legacy World snapshots and
        // Strategic Management bridge snapshots; both ids mean the same player side here.
        return string.Equals(normalized, StrategicWorldIds.FactionPlayer, StringComparison.Ordinal) ||
               string.Equals(normalized, StrategicManagementIds.FactionPlayer, StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateGroupUnitIds(BattleGroupSnapshot group)
    {
        yield return group?.HeroBattleUnitId ?? "";
        yield return group?.HeroDefinitionId ?? "";
        yield return group?.HeroId ?? "";
        yield return group?.CorpsBattleUnitId ?? "";
        yield return group?.CorpsDefinitionId ?? "";
        yield return group?.CorpsId ?? "";
    }

    private static string ResolveSlotId(FirstSliceHeroCompanyDefinition company, int index)
    {
        if (index <= 0)
        {
            return "primary";
        }

        string role = string.IsNullOrWhiteSpace(company?.RoleId)
            ? "skill"
            : company.RoleId.Trim();
        return $"{role}_{index + 1:00}";
    }

    private static string ResolveOwnerSlotPrefix(string ownerHeroId, string ownerBattleGroupId)
    {
        return !string.IsNullOrWhiteSpace(ownerHeroId)
            ? $"hero:{ownerHeroId.Trim()}"
            : $"group:{ownerBattleGroupId?.Trim() ?? ""}";
    }

    private static string ResolveGrantId(
        string ownerHeroId,
        FirstSliceHeroCompanyDefinition company,
        string slotId)
    {
        string owner = !string.IsNullOrWhiteSpace(ownerHeroId)
            ? $"hero:{ownerHeroId.Trim()}"
            : $"source:{company?.CompanyId?.Trim() ?? "unknown"}";
        return $"default_{owner}:grant:{slotId?.Trim() ?? ""}";
    }
}
