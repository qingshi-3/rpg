using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Application.Config;

public sealed class StrategicManagementFacilityDefinitionConfig
{
    public List<StrategicFacilityDefinition> Facilities { get; set; } = new();
}

public static class StrategicManagementFacilityDefinitionConfigLoader
{
    public const string DefaultConfigPath = "res://config/strategic_management/first_slice_facilities.json";

    public static IReadOnlyList<StrategicFacilityDefinition> LoadDefaultFacilities() =>
        LoadFacilities(DefaultConfigPath);

    public static IReadOnlyList<StrategicFacilityDefinition> LoadFacilities(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        StrategicManagementFacilityDefinitionConfig config = JsonSerializer.Deserialize<StrategicManagementFacilityDefinitionConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid strategic management facility config path={path}");

        Validate(config, path);
        return config.Facilities;
    }

    private static void Validate(StrategicManagementFacilityDefinitionConfig config, string path)
    {
        if (config.Facilities == null || config.Facilities.Count == 0)
        {
            throw new InvalidOperationException($"Strategic management facility config has no facilities path={path}");
        }

        HashSet<string> facilityIds = new(StringComparer.Ordinal);
        foreach (StrategicFacilityDefinition facility in config.Facilities)
        {
            RequireNonEmpty(facility.FacilityDefinitionId, "facilityDefinitionId", path);
            RequireNonEmpty(facility.DisplayName, "displayName", path);

            if (!facilityIds.Add(facility.FacilityDefinitionId))
            {
                throw new InvalidOperationException($"Duplicate strategic management facility id={facility.FacilityDefinitionId} path={path}");
            }

            if (facility.SlotCost <= 0)
            {
                throw new InvalidOperationException($"Strategic management facility has non-positive slot cost id={facility.FacilityDefinitionId} path={path}");
            }

            if (facility.ProvidedTags == null || facility.ProvidedTags.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException($"Strategic management facility has empty provided tag id={facility.FacilityDefinitionId} path={path}");
            }

            foreach (StrategicResourceAmount cost in facility.BuildCost ?? Enumerable.Empty<StrategicResourceAmount>())
            {
                RequireNonEmpty(cost.ResourceId, "buildCost.resourceId", path);
                if (cost.Amount <= 0)
                {
                    throw new InvalidOperationException($"Strategic management facility has non-positive build cost id={facility.FacilityDefinitionId} resource={cost.ResourceId} path={path}");
                }
            }
        }
    }

    private static void RequireNonEmpty(string value, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Strategic management facility config field is empty field={field} path={path}");
        }
    }
}
