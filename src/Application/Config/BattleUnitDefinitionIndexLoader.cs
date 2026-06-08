using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Rpg.Application.Config;

public sealed class BattleUnitDefinitionIndexConfig
{
    public List<BattleUnitDefinitionIndexEntry> UnitDefinitions { get; set; } = new();
}

public sealed class BattleUnitDefinitionIndexEntry
{
    public string UnitDefinitionId { get; set; } = "";
    public string ResourcePath { get; set; } = "";
}

public static class BattleUnitDefinitionIndexLoader
{
    public const string DefaultConfigPath = "res://config/battle/unit_definition_index.json";

    public static IReadOnlyDictionary<string, string> LoadDefaultPathIndex() =>
        LoadPathIndex(DefaultConfigPath);

    public static IReadOnlyDictionary<string, string> LoadPathIndex(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        BattleUnitDefinitionIndexConfig config = JsonSerializer.Deserialize<BattleUnitDefinitionIndexConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid unit definition index config path={path}");

        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (BattleUnitDefinitionIndexEntry entry in config.UnitDefinitions ?? Enumerable.Empty<BattleUnitDefinitionIndexEntry>())
        {
            string id = entry.UnitDefinitionId?.Trim() ?? "";
            string resourcePath = entry.ResourcePath?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new InvalidOperationException($"Invalid unit definition index entry path={path} id={id} resource={resourcePath}");
            }

            if (result.ContainsKey(id))
            {
                throw new InvalidOperationException($"Duplicate unit definition index id={id} path={path}");
            }

            result[id] = resourcePath;
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException($"Unit definition index config is empty path={path}");
        }

        return result;
    }
}

internal static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
