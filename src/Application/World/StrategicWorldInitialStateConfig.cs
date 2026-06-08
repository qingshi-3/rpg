using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Rpg.Application.Config;

namespace Rpg.Application.World;

public sealed class StrategicWorldInitialStateConfig
{
    public List<StrategicWorldSiteInitialStateConfig> Sites { get; set; } = new();
}

public sealed class StrategicWorldSiteInitialStateConfig
{
    public string SiteId { get; set; } = "";
    public List<StrategicWorldInitialGarrisonConfig> InitialGarrison { get; set; } = new();
}

public sealed class StrategicWorldInitialGarrisonConfig
{
    public string UnitDefinitionId { get; set; } = "";
    public int Count { get; set; } = 1;
    public int Morale { get; set; } = 50;
}

public static class StrategicWorldInitialStateConfigLoader
{
    public const string DefaultConfigPath = "res://config/world/strategic_world_v1_initial_state.json";

    public static StrategicWorldInitialStateConfig LoadDefault() => Load(DefaultConfigPath);

    public static StrategicWorldInitialStateConfig Load(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        StrategicWorldInitialStateConfig config = JsonSerializer.Deserialize<StrategicWorldInitialStateConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid strategic world initial state config path={path}");

        Validate(config, path);
        return config;
    }

    private static void Validate(StrategicWorldInitialStateConfig config, string path)
    {
        if (config.Sites == null || config.Sites.Count == 0)
        {
            throw new InvalidOperationException($"Strategic world initial state config has no sites path={path}");
        }

        HashSet<string> siteIds = new(StringComparer.Ordinal);
        foreach (StrategicWorldSiteInitialStateConfig site in config.Sites)
        {
            string siteId = site.SiteId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(siteId))
            {
                throw new InvalidOperationException($"Strategic world initial state config has an empty site id path={path}");
            }

            if (!siteIds.Add(siteId))
            {
                throw new InvalidOperationException($"Strategic world initial state config has duplicate site id={siteId} path={path}");
            }

            foreach (StrategicWorldInitialGarrisonConfig entry in site.InitialGarrison ?? Enumerable.Empty<StrategicWorldInitialGarrisonConfig>())
            {
                string unitDefinitionId = entry.UnitDefinitionId?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(unitDefinitionId))
                {
                    throw new InvalidOperationException($"Strategic world initial state config has empty unit id site={siteId} path={path}");
                }

                if (entry.Count <= 0)
                {
                    throw new InvalidOperationException($"Strategic world initial state config has non-positive count site={siteId} unit={unitDefinitionId} count={entry.Count} path={path}");
                }
            }
        }
    }
}
