using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Application.Config;

public sealed class StrategicManagementContentConfig
{
    public List<StrategicResourceDefinition> Resources { get; set; } = new();
    public StrategicConscriptionDefinition Conscription { get; set; } = new();
    public List<StrategicBuildingDefinition> Buildings { get; set; } = new();
    public List<StrategicCorpsDefinition> Corps { get; set; } = new();
}

public sealed class StrategicManagementResourceDefinitionConfig
{
    public List<StrategicResourceDefinition> Resources { get; set; } = new();
}

public sealed class StrategicManagementConscriptionPolicyConfig
{
    public StrategicConscriptionDefinition Conscription { get; set; } = new();
}

public sealed class StrategicManagementCorpsDefinitionConfig
{
    public List<StrategicCorpsDefinition> Corps { get; set; } = new();
}

public static class StrategicManagementContentConfigLoader
{
    public const string ResourceConfigPath = "res://config/strategic_management/economy/resources.json";
    public const string ConscriptionConfigPath = "res://config/strategic_management/economy/conscription_policies.json";
    public const string CommonCorpsConfigPath = "res://config/strategic_management/military/corps_common.json";

    public static StrategicManagementContentConfig LoadDefaultContent()
    {
        StrategicManagementContentConfig content = new()
        {
            Resources = LoadResources(ResourceConfigPath).ToList(),
            Conscription = LoadConscription(ConscriptionConfigPath),
            Buildings = StrategicManagementBuildingDefinitionConfigLoader.LoadDefaultBuildings().ToList(),
            Corps = LoadCorps(CommonCorpsConfigPath).ToList()
        };

        ValidateReferences(content);
        return content;
    }

    public static IReadOnlyList<StrategicResourceDefinition> LoadResources(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        StrategicManagementResourceDefinitionConfig config = JsonSerializer.Deserialize<StrategicManagementResourceDefinitionConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid strategic management resources config path={path}");

        ValidateResources(config, path);
        return config.Resources;
    }

    public static StrategicConscriptionDefinition LoadConscription(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        StrategicManagementConscriptionPolicyConfig config = JsonSerializer.Deserialize<StrategicManagementConscriptionPolicyConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid strategic management conscription config path={path}");

        ValidateConscription(config.Conscription, path);
        return config.Conscription;
    }

    public static IReadOnlyList<StrategicCorpsDefinition> LoadCorps(string path)
    {
        string text = ProjectConfigFileReader.ReadAllText(path);
        StrategicManagementCorpsDefinitionConfig config = JsonSerializer.Deserialize<StrategicManagementCorpsDefinitionConfig>(
            text,
            ProjectJson.Options) ?? throw new InvalidOperationException($"Invalid strategic management corps config path={path}");

        ValidateCorps(config, path);
        return config.Corps;
    }

    private static void ValidateResources(StrategicManagementResourceDefinitionConfig config, string path)
    {
        if (config.Resources == null || config.Resources.Count == 0)
        {
            throw new InvalidOperationException($"Strategic management resource config has no resources path={path}");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (StrategicResourceDefinition resource in config.Resources)
        {
            RequireNonEmpty(resource.ResourceId, "resourceId", path);
            RequireNonEmpty(resource.DisplayName, "displayName", path);
            if (!ids.Add(resource.ResourceId))
            {
                throw new InvalidOperationException($"Duplicate strategic management resource id={resource.ResourceId} path={path}");
            }
        }
    }

    private static void ValidateConscription(StrategicConscriptionDefinition definition, string path)
    {
        if (definition?.Manual == null)
        {
            throw new InvalidOperationException($"Strategic management conscription config has no manual policy path={path}");
        }

        if (definition.Manual.ReserveGain <= 0)
        {
            throw new InvalidOperationException($"Strategic management manual conscription reserve gain must be positive path={path}");
        }

        ValidateAmounts(definition.Manual.Cost, "manual.cost", path);

        if (definition.AutoIntensities == null || definition.AutoIntensities.Count == 0)
        {
            throw new InvalidOperationException($"Strategic management conscription config has no auto intensities path={path}");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (StrategicConscriptionIntensityDefinition intensity in definition.AutoIntensities)
        {
            RequireNonEmpty(intensity.IntensityId, "autoIntensities.intensityId", path);
            RequireNonEmpty(intensity.DisplayName, "autoIntensities.displayName", path);
            if (!ids.Add(intensity.IntensityId))
            {
                throw new InvalidOperationException($"Duplicate strategic management conscription intensity id={intensity.IntensityId} path={path}");
            }

            if (intensity.ReserveGain < 0)
            {
                throw new InvalidOperationException($"Strategic management conscription intensity has negative reserve gain id={intensity.IntensityId} path={path}");
            }

            ValidateAmounts(intensity.Cost, $"autoIntensities[{intensity.IntensityId}].cost", path, allowEmpty: true);
        }

        if (!ids.Contains(StrategicManagementIds.ConscriptionOff))
        {
            throw new InvalidOperationException($"Strategic management conscription config must include {StrategicManagementIds.ConscriptionOff} path={path}");
        }
    }

    private static void ValidateCorps(StrategicManagementCorpsDefinitionConfig config, string path)
    {
        if (config.Corps == null || config.Corps.Count == 0)
        {
            throw new InvalidOperationException($"Strategic management corps config has no corps path={path}");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (StrategicCorpsDefinition corps in config.Corps)
        {
            RequireNonEmpty(corps.CorpsDefinitionId, "corpsDefinitionId", path);
            RequireNonEmpty(corps.DisplayName, "displayName", path);
            RequireNonEmpty(corps.BattleUnitId, "battleUnitId", path);
            if (!ids.Add(corps.CorpsDefinitionId))
            {
                throw new InvalidOperationException($"Duplicate strategic management corps id={corps.CorpsDefinitionId} path={path}");
            }

            if (corps.BattleUnitCount <= 0)
            {
                throw new InvalidOperationException($"Strategic management corps has non-positive battle unit count id={corps.CorpsDefinitionId} path={path}");
            }

            if (corps.SoldierCapacityCost <= 0)
            {
                throw new InvalidOperationException($"Strategic management corps has non-positive soldier capacity cost id={corps.CorpsDefinitionId} path={path}");
            }

            ValidateAmounts(corps.CreationCost, $"{corps.CorpsDefinitionId}.creationCost", path);
            ValidateAmounts(corps.ReplenishFullCost, $"{corps.CorpsDefinitionId}.replenishFullCost", path);
        }
    }

    private static void ValidateReferences(StrategicManagementContentConfig content)
    {
        HashSet<string> resourceIds = content.Resources
            .Select(resource => resource.ResourceId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (StrategicResourceAmount amount in EnumerateAmounts(content))
        {
            if (!resourceIds.Contains(amount.ResourceId))
            {
                throw new InvalidOperationException($"Strategic management content references unknown resource id={amount.ResourceId}");
            }
        }
    }

    private static IEnumerable<StrategicResourceAmount> EnumerateAmounts(StrategicManagementContentConfig content)
    {
        foreach (StrategicResourceAmount amount in content.Conscription.Manual.Cost)
        {
            yield return amount;
        }

        foreach (StrategicConscriptionIntensityDefinition intensity in content.Conscription.AutoIntensities)
        {
            foreach (StrategicResourceAmount amount in intensity.Cost)
            {
                yield return amount;
            }
        }

        foreach (StrategicBuildingDefinition building in content.Buildings)
        {
            foreach (StrategicResourceAmount amount in building.BuildCost)
            {
                yield return amount;
            }

            foreach (StrategicResourceAmount amount in building.ProvidedCapabilities?.ResourceProductionPerWorldTimePulse ??
                                                 Enumerable.Empty<StrategicResourceAmount>())
            {
                yield return amount;
            }
        }

        foreach (StrategicCorpsDefinition corps in content.Corps)
        {
            foreach (StrategicResourceAmount amount in corps.CreationCost)
            {
                yield return amount;
            }

            foreach (StrategicResourceAmount amount in corps.ReplenishFullCost)
            {
                yield return amount;
            }
        }
    }

    private static void ValidateAmounts(
        IReadOnlyCollection<StrategicResourceAmount> amounts,
        string field,
        string path,
        bool allowEmpty = false)
    {
        if (amounts == null || amounts.Count == 0)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new InvalidOperationException($"Strategic management config amount list is empty field={field} path={path}");
        }

        HashSet<string> resourceIds = new(StringComparer.Ordinal);
        foreach (StrategicResourceAmount amount in amounts)
        {
            RequireNonEmpty(amount.ResourceId, $"{field}.resourceId", path);
            if (!resourceIds.Add(amount.ResourceId))
            {
                throw new InvalidOperationException($"Strategic management config has duplicate resource amount field={field} resource={amount.ResourceId} path={path}");
            }

            if (amount.Amount <= 0)
            {
                throw new InvalidOperationException($"Strategic management config has non-positive amount field={field} resource={amount.ResourceId} path={path}");
            }
        }
    }

    private static void RequireNonEmpty(string value, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Strategic management content config field is empty field={field} path={path}");
        }
    }
}
