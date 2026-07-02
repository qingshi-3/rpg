using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Rpg.Application.Config;

public sealed class BattleSkillDefinitionIndexConfig
{
    public List<BattleSkillDefinitionIndexEntry> Skills { get; set; } = new();
}

public sealed class BattleSkillDefinitionIndexEntry
{
    public string SkillDefinitionId { get; set; } = "";
    public string ResourcePath { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
}

public sealed class BattleSkillDefinitionIndex
{
    public BattleSkillDefinitionIndex(
        IReadOnlyDictionary<string, string> resourcePathsBySkillDefinitionId,
        IReadOnlyDictionary<string, string> skillDefinitionIdsByAlias)
    {
        ResourcePathsBySkillDefinitionId = resourcePathsBySkillDefinitionId;
        SkillDefinitionIdsByAlias = skillDefinitionIdsByAlias;
    }

    public IReadOnlyDictionary<string, string> ResourcePathsBySkillDefinitionId { get; }
    public IReadOnlyDictionary<string, string> SkillDefinitionIdsByAlias { get; }

    public string NormalizeSkillDefinitionId(string skillDefinitionIdOrAlias)
    {
        string key = skillDefinitionIdOrAlias?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        return SkillDefinitionIdsByAlias.TryGetValue(key, out string canonicalId)
            ? canonicalId
            : key;
    }
}

public static class BattleSkillDefinitionIndexLoader
{
    public const string DefaultConfigPath = "res://config/battle/battle_skill_definitions.json";

    private static readonly string[] RequiredFirstSliceSkillDefinitionIds =
    {
        "skill_shield_barrier",
        "skill_sun_piercer",
        "skill_thunder_tag_throw",
        "skill_thunder_mark_fold",
        "skill_thunder_spiral_break"
    };

    public static BattleSkillDefinitionIndex LoadDefaultIndex() => LoadIndex(DefaultConfigPath);

    public static BattleSkillDefinitionIndex LoadIndex(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        BattleSkillDefinitionIndexConfig config = JsonSerializer.Deserialize<BattleSkillDefinitionIndexConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"battle_skill_definition_index_invalid path={path}");

        return BuildIndex(config, path);
    }

    private static BattleSkillDefinitionIndex BuildIndex(BattleSkillDefinitionIndexConfig config, string path)
    {
        Dictionary<string, string> resourcePaths = new(StringComparer.Ordinal);
        Dictionary<string, string> aliases = new(StringComparer.Ordinal);

        foreach (BattleSkillDefinitionIndexEntry entry in config.Skills ?? Enumerable.Empty<BattleSkillDefinitionIndexEntry>())
        {
            string skillDefinitionId = entry.SkillDefinitionId?.Trim() ?? "";
            string resourcePath = entry.ResourcePath?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(skillDefinitionId))
            {
                throw new InvalidOperationException($"battle_skill_definition_id_missing path={path}");
            }

            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new InvalidOperationException($"battle_skill_definition_resource_path_missing id={skillDefinitionId} path={path}");
            }

            if (!resourcePaths.TryAdd(skillDefinitionId, resourcePath))
            {
                throw new InvalidOperationException($"battle_skill_definition_duplicate id={skillDefinitionId} path={path}");
            }

            foreach (string alias in (entry.Aliases ?? new List<string>())
                .Select(item => item?.Trim() ?? "")
                .Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                if (string.Equals(alias, skillDefinitionId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"battle_skill_definition_alias_self_reference id={skillDefinitionId} alias={alias} path={path}");
                }

                if (aliases.ContainsKey(alias))
                {
                    throw new InvalidOperationException($"battle_skill_definition_alias_duplicate alias={alias} path={path}");
                }

                aliases[alias] = skillDefinitionId;
            }
        }

        if (resourcePaths.Count == 0)
        {
            throw new InvalidOperationException($"battle_skill_definition_index_empty path={path}");
        }

        foreach ((string alias, string canonicalId) in aliases)
        {
            if (resourcePaths.ContainsKey(alias))
            {
                throw new InvalidOperationException($"battle_skill_definition_alias_collides_with_id alias={alias} path={path}");
            }

            if (!resourcePaths.ContainsKey(canonicalId))
            {
                throw new InvalidOperationException($"battle_skill_definition_alias_target_missing alias={alias} target={canonicalId} path={path}");
            }
        }

        foreach (string requiredId in RequiredFirstSliceSkillDefinitionIds)
        {
            if (!resourcePaths.ContainsKey(requiredId))
            {
                throw new InvalidOperationException($"battle_skill_definition_required_missing id={requiredId} path={path}");
            }
        }

        return new BattleSkillDefinitionIndex(resourcePaths, aliases);
    }
}
