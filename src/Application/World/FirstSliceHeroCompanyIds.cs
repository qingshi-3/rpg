using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Rpg.Application.Config;

namespace Rpg.Application.World;

public sealed class FirstSliceHeroCompanyDefinition
{
    public string CompanyId { get; init; } = "";
    public string RoleId { get; init; } = "";
    public string HeroUnit { get; init; } = "";
    public string DefaultCorpsUnit { get; init; } = "";
    public int DefaultCorpsCount { get; init; } = 1;
    public string SkillId { get; init; } = "";
}

public sealed class FirstSliceBonefieldRosterDefinition
{
    public string RoleId { get; init; } = "";
    public string Unit { get; init; } = "";
    public int Count { get; init; } = 1;
}

public sealed class FirstSliceHeroCompanyConfig
{
    public List<FirstSliceHeroCompanyDefinition> Companies { get; set; } = new();
    public List<FirstSliceBonefieldRosterDefinition> BonefieldRoster { get; set; } = new();
}

public static class FirstSliceHeroCompanyConfigLoader
{
    public const string DefaultConfigPath = "res://config/battle/first_slice_hero_companies.json";

    public static FirstSliceHeroCompanyConfig LoadDefault() => Load(DefaultConfigPath);

    public static FirstSliceHeroCompanyConfig Load(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        FirstSliceHeroCompanyConfig config = JsonSerializer.Deserialize<FirstSliceHeroCompanyConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid first-slice hero company config path={path}");

        Validate(config, path);
        return config;
    }

    private static void Validate(FirstSliceHeroCompanyConfig config, string path)
    {
        if (config.Companies == null || config.Companies.Count == 0)
        {
            throw new InvalidOperationException($"First-slice hero company config has no companies path={path}");
        }

        HashSet<string> companyIds = new(StringComparer.Ordinal);
        HashSet<string> roleIds = new(StringComparer.Ordinal);
        HashSet<string> heroUnits = new(StringComparer.Ordinal);
        foreach (FirstSliceHeroCompanyDefinition company in config.Companies)
        {
            RequireNonEmpty(company.CompanyId, "companyId", path);
            RequireNonEmpty(company.RoleId, "roleId", path);
            RequireNonEmpty(company.HeroUnit, "heroUnit", path);
            RequireNonEmpty(company.DefaultCorpsUnit, "defaultCorpsUnit", path);
            RequireNonEmpty(company.SkillId, "skillId", path);
            if (company.DefaultCorpsCount <= 0)
            {
                throw new InvalidOperationException($"First-slice hero company config has non-positive default corps count company={company.CompanyId} path={path}");
            }

            if (!companyIds.Add(company.CompanyId))
            {
                throw new InvalidOperationException($"Duplicate first-slice company id={company.CompanyId} path={path}");
            }

            if (!roleIds.Add(company.RoleId))
            {
                throw new InvalidOperationException($"Duplicate first-slice company role={company.RoleId} path={path}");
            }

            if (!heroUnits.Add(company.HeroUnit))
            {
                throw new InvalidOperationException($"Duplicate first-slice hero unit={company.HeroUnit} path={path}");
            }
        }

        if (config.BonefieldRoster == null || config.BonefieldRoster.Count == 0)
        {
            throw new InvalidOperationException($"First-slice hero company config has no Bonefield roster path={path}");
        }

        HashSet<string> bonefieldRoles = new(StringComparer.Ordinal);
        foreach (FirstSliceBonefieldRosterDefinition roster in config.BonefieldRoster)
        {
            RequireNonEmpty(roster.RoleId, "bonefield.roleId", path);
            RequireNonEmpty(roster.Unit, "bonefield.unit", path);
            if (roster.Count <= 0)
            {
                throw new InvalidOperationException($"First-slice Bonefield roster has non-positive count role={roster.RoleId} path={path}");
            }

            if (!bonefieldRoles.Add(roster.RoleId))
            {
                throw new InvalidOperationException($"Duplicate first-slice Bonefield roster role={roster.RoleId} path={path}");
            }
        }
    }

    private static void RequireNonEmpty(string value, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"First-slice hero company config field is empty field={field} path={path}");
        }
    }
}

public static class FirstSliceHeroCompanyIds
{
    private static readonly FirstSliceHeroCompanyConfig Config = FirstSliceHeroCompanyConfigLoader.LoadDefault();

    public static int DefaultCorpsCount => Companies.FirstOrDefault()?.DefaultCorpsCount ?? 0;

    public static string ShieldCompanyId => GetRequiredCompanyByRole("shield").CompanyId;
    public static string ArcherCompanyId => GetRequiredCompanyByRole("archer").CompanyId;
    public static string AssaultCompanyId => GetRequiredCompanyByRole("assault").CompanyId;

    public static string ShieldHeroUnit => GetRequiredCompanyByRole("shield").HeroUnit;
    public static string ShieldCorpsUnit => GetRequiredCompanyByRole("shield").DefaultCorpsUnit;
    public static int ShieldCorpsCount => GetRequiredCompanyByRole("shield").DefaultCorpsCount;
    public static string ArcherHeroUnit => GetRequiredCompanyByRole("archer").HeroUnit;
    public static string ArcherCorpsUnit => GetRequiredCompanyByRole("archer").DefaultCorpsUnit;
    public static int ArcherCorpsCount => GetRequiredCompanyByRole("archer").DefaultCorpsCount;
    public static string AssaultHeroUnit => GetRequiredCompanyByRole("assault").HeroUnit;
    public static string AssaultCorpsUnit => GetRequiredCompanyByRole("assault").DefaultCorpsUnit;
    public static int AssaultCorpsCount => GetRequiredCompanyByRole("assault").DefaultCorpsCount;

    public static string BonefieldLeaderUnit => GetRequiredBonefieldRosterByRole("leader").Unit;
    public static string BonefieldHarassmentUnit => GetRequiredBonefieldRosterByRole("harassment").Unit;
    public static string BonefieldRangedUnit => GetRequiredBonefieldRosterByRole("ranged").Unit;
    public static int BonefieldLeaderCount => GetRequiredBonefieldRosterByRole("leader").Count;
    public static int BonefieldHarassmentCount => GetRequiredBonefieldRosterByRole("harassment").Count;
    public static int BonefieldRangedCount => GetRequiredBonefieldRosterByRole("ranged").Count;

    public static IReadOnlyList<FirstSliceHeroCompanyDefinition> Companies => Config.Companies;

    public static IReadOnlyList<FirstSliceBonefieldRosterDefinition> BonefieldRoster => Config.BonefieldRoster;

    public static IReadOnlyList<string> HeroUnitIds => Companies
        .Select(company => company.HeroUnit)
        .ToArray();

    public static bool IsHeroUnit(string unitTypeId) =>
        Companies.Any(company => string.Equals(company.HeroUnit, unitTypeId, StringComparison.Ordinal));

    public static bool TryGetCompanyByHeroUnit(string heroUnitId, out FirstSliceHeroCompanyDefinition company)
    {
        company = Companies.FirstOrDefault(item =>
            string.Equals(item.HeroUnit, heroUnitId ?? "", StringComparison.Ordinal));
        return company != null;
    }

    public static bool TryGetCompanyByAnyUnit(string unitTypeId, out FirstSliceHeroCompanyDefinition company)
    {
        company = Companies.FirstOrDefault(item =>
            string.Equals(item.HeroUnit, unitTypeId ?? "", StringComparison.Ordinal) ||
            string.Equals(item.DefaultCorpsUnit, unitTypeId ?? "", StringComparison.Ordinal));
        return company != null;
    }

    private static FirstSliceHeroCompanyDefinition GetRequiredCompanyByRole(string roleId)
    {
        FirstSliceHeroCompanyDefinition company = Companies.FirstOrDefault(item =>
            string.Equals(item.RoleId, roleId ?? "", StringComparison.Ordinal));
        return company ?? throw new InvalidOperationException($"Missing first-slice company role={roleId}");
    }

    private static FirstSliceBonefieldRosterDefinition GetRequiredBonefieldRosterByRole(string roleId)
    {
        FirstSliceBonefieldRosterDefinition roster = BonefieldRoster.FirstOrDefault(item =>
            string.Equals(item.RoleId, roleId ?? "", StringComparison.Ordinal));
        return roster ?? throw new InvalidOperationException($"Missing first-slice Bonefield roster role={roleId}");
    }
}
