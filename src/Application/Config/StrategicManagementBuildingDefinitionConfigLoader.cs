using System;
using System.Collections.Generic;
using System.Text.Json;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Application.Config;

public sealed class StrategicManagementBuildingDefinitionConfig
{
    public List<StrategicBuildingDefinition> Buildings { get; set; } = new();
}

public static class StrategicManagementBuildingDefinitionConfigLoader
{
    public const string DefaultConfigPath = "res://config/strategic_management/cities/buildings_foundation.json";

    public static IReadOnlyList<StrategicBuildingDefinition> LoadDefaultBuildings() =>
        LoadBuildings(DefaultConfigPath);

    public static IReadOnlyList<StrategicBuildingDefinition> LoadBuildings(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        StrategicManagementBuildingDefinitionConfig config = JsonSerializer.Deserialize<StrategicManagementBuildingDefinitionConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid strategic management building config path={path}");

        Validate(config, path);
        return config.Buildings;
    }

    private static void Validate(StrategicManagementBuildingDefinitionConfig config, string path)
    {
        if (config.Buildings == null || config.Buildings.Count == 0)
        {
            throw new InvalidOperationException($"Strategic management building config has no buildings path={path}");
        }

        HashSet<string> buildingIds = new(StringComparer.Ordinal);
        foreach (StrategicBuildingDefinition building in config.Buildings)
        {
            RequireNonEmpty(building.BuildingDefinitionId, "buildingDefinitionId", path);
            RequireNonEmpty(building.DisplayName, "displayName", path);
            RequireNonEmpty(building.IconPath, "iconPath", path);
            RequireNonEmpty(building.CategoryId, "categoryId", path);

            if (!buildingIds.Add(building.BuildingDefinitionId))
            {
                throw new InvalidOperationException($"Duplicate strategic management building id={building.BuildingDefinitionId} path={path}");
            }

            if (building.FootprintWidth <= 0 || building.FootprintHeight <= 0)
            {
                throw new InvalidOperationException($"Strategic management building has invalid footprint id={building.BuildingDefinitionId} size={building.FootprintWidth}x{building.FootprintHeight} path={path}");
            }

            ValidateAmounts(building.BuildCost, "buildCost", building.BuildingDefinitionId, path);
            ValidateAmounts(
                building.ProvidedCapabilities?.ResourceProductionPerWorldTimePulse,
                "providedCapabilities.resourceProductionPerWorldTimePulse",
                building.BuildingDefinitionId,
                path,
                allowEmpty: true);
        }
    }

    private static void ValidateAmounts(
        IReadOnlyCollection<StrategicResourceAmount> amounts,
        string field,
        string buildingId,
        string path,
        bool allowEmpty = false)
    {
        if (amounts == null || amounts.Count == 0)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new InvalidOperationException($"Strategic management building amount list is empty id={buildingId} field={field} path={path}");
        }

        HashSet<string> resourceIds = new(StringComparer.Ordinal);
        foreach (StrategicResourceAmount amount in amounts)
        {
            RequireNonEmpty(amount.ResourceId, $"{field}.resourceId", path);
            if (!resourceIds.Add(amount.ResourceId))
            {
                throw new InvalidOperationException($"Strategic management building has duplicate resource amount id={buildingId} field={field} resource={amount.ResourceId} path={path}");
            }

            if (amount.Amount <= 0)
            {
                throw new InvalidOperationException($"Strategic management building has non-positive amount id={buildingId} field={field} resource={amount.ResourceId} path={path}");
            }
        }
    }

    private static void RequireNonEmpty(string value, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Strategic management building config field is empty field={field} path={path}");
        }
    }
}
